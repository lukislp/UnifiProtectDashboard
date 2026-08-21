using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using UnifiCameraDashboard.Data;
using UnifiCameraDashboard.Models;
using UnifiCameraDashboard.Services;

namespace UnifiCameraDashboard.Tests.Services;

public class EventRepositoryTests
{
    [Fact]
    public async Task UpsertFromWebSocketAsync_NewEventFullData_CreatesRow()
    {
        await using var db = await TestDb.CreateAsync();
        var repository = new EventRepository(db.Context, NullLogger<EventRepository>.Instance);

        var data = ParseJson("""
            {"type":"motion","camera":"cam-1","score":87,"start":1700000000000,"smartDetectTypes":["person","vehicle"]}
            """);

        var (id, isNew) = await repository.UpsertFromWebSocketAsync("evt-1", data);

        Assert.True(isNew);
        var stored = await db.Context.Events.SingleAsync(e => e.Id == id);
        Assert.Equal("motion", stored.Type);
        Assert.Equal("cam-1", stored.CameraUnifiId);
        Assert.Equal(87, stored.Score);
        Assert.Equal("person,vehicle", stored.SmartDetectTypes);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1700000000000).UtcDateTime, stored.Start);
        Assert.Null(stored.End);
    }

    [Fact]
    public async Task UpsertFromWebSocketAsync_PartialUpdate_OnlyTouchesPresentFields()
    {
        await using var db = await TestDb.CreateAsync();
        var repository = new EventRepository(db.Context, NullLogger<EventRepository>.Instance);

        await repository.UpsertFromWebSocketAsync("evt-2", ParseJson("""
            {"type":"motion","camera":"cam-1","score":50,"start":1700000000000,"smartDetectTypes":["person"]}
            """));

        // A real "update" action's data frame is a partial patch - only the changed field(s)
        // are present. This must NOT clobber Type/Camera/Score/Start with defaults.
        var (id, isNew) = await repository.UpsertFromWebSocketAsync("evt-2", ParseJson("""
            {"end":1700000005000}
            """));

        Assert.False(isNew);
        var stored = await db.Context.Events.SingleAsync(e => e.Id == id);
        Assert.Equal("motion", stored.Type);
        Assert.Equal("cam-1", stored.CameraUnifiId);
        Assert.Equal(50, stored.Score);
        Assert.Equal("person", stored.SmartDetectTypes);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1700000000000).UtcDateTime, stored.Start);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1700000005000).UtcDateTime, stored.End);
    }

    [Fact]
    public async Task UpsertFromWebSocketAsync_UpdateForUnknownEvent_CreatesBestEffortRowWithoutThrowing()
    {
        // Simulates a gap: we missed the "add" action (e.g. reconnect) and only saw a later
        // "update". Should not throw - creates what it can; REST backfill fills the rest later.
        await using var db = await TestDb.CreateAsync();
        var repository = new EventRepository(db.Context, NullLogger<EventRepository>.Instance);

        var (id, isNew) = await repository.UpsertFromWebSocketAsync("evt-3", ParseJson("""{"score":42}"""));

        Assert.True(isNew);
        var stored = await db.Context.Events.SingleAsync(e => e.Id == id);
        Assert.Equal(42, stored.Score);
        Assert.Equal(string.Empty, stored.Type);
    }

    [Fact]
    public async Task UpsertFromRestAsync_ExistingEvent_OverwritesAllFields()
    {
        // Unlike the websocket path, a REST list item is always a complete object - this must
        // be a full overwrite, not a merge.
        await using var db = await TestDb.CreateAsync();
        var repository = new EventRepository(db.Context, NullLogger<EventRepository>.Instance);

        await repository.UpsertFromRestAsync(new ProtectEventPayload
        {
            Id = "evt-4",
            Type = "motion",
            Camera = "cam-1",
            Score = 10,
            Start = 1700000000000,
            SmartDetectTypes = ["person"],
        });

        var (id, needsThumbnail) = await repository.UpsertFromRestAsync(new ProtectEventPayload
        {
            Id = "evt-4",
            Type = "smartDetectZone",
            Camera = "cam-2",
            Score = 95,
            Start = 1700000000000,
            End = 1700000010000,
            SmartDetectTypes = ["vehicle", "package"],
        });

        Assert.True(needsThumbnail);

        var stored = await db.Context.Events.SingleAsync(e => e.Id == id);
        Assert.Equal("smartDetectZone", stored.Type);
        Assert.Equal("cam-2", stored.CameraUnifiId);
        Assert.Equal(95, stored.Score);
        Assert.Equal("vehicle,package", stored.SmartDetectTypes);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1700000010000).UtcDateTime, stored.End);
    }

    [Fact]
    public async Task CloseStaleOpenEventsAsync_ClosesOnlyEventsOlderThanThreshold()
    {
        await using var db = await TestDb.CreateAsync();
        var repository = new EventRepository(db.Context, NullLogger<EventRepository>.Instance);

        var old = new StoredEvent { UnifiEventId = "old", Type = "motion", Start = DateTime.UtcNow.AddHours(-2) };
        var recent = new StoredEvent { UnifiEventId = "recent", Type = "motion", Start = DateTime.UtcNow.AddMinutes(-5) };
        db.Context.Events.AddRange(old, recent);
        await db.Context.SaveChangesAsync();

        var closedCount = await repository.CloseStaleOpenEventsAsync(TimeSpan.FromHours(1));

        Assert.Equal(1, closedCount);
        Assert.NotNull((await db.Context.Events.SingleAsync(e => e.UnifiEventId == "old")).End);
        Assert.Null((await db.Context.Events.SingleAsync(e => e.UnifiEventId == "recent")).End);
    }

    [Fact]
    public async Task GetRecentEventsAsync_OrdersNewestFirstAndFiltersByCameraAndType()
    {
        await using var db = await TestDb.CreateAsync();
        var repository = new EventRepository(db.Context, NullLogger<EventRepository>.Instance);

        db.Context.Events.AddRange(
            new StoredEvent { UnifiEventId = "e1", CameraUnifiId = "cam-1", Type = "motion", Start = DateTime.UtcNow.AddMinutes(-10) },
            new StoredEvent { UnifiEventId = "e2", CameraUnifiId = "cam-1", Type = "motion", Start = DateTime.UtcNow.AddMinutes(-1) },
            new StoredEvent { UnifiEventId = "e3", CameraUnifiId = "cam-2", Type = "ring", Start = DateTime.UtcNow.AddMinutes(-5) });
        await db.Context.SaveChangesAsync();

        var results = await repository.GetRecentEventsAsync(skip: 0, take: 10, cameraId: "cam-1");

        Assert.Equal(["e2", "e1"], results.Select(e => e.UnifiEventId));
    }

    [Fact]
    public async Task GetRecentEventsAsync_FiltersByYoloLabel()
    {
        await using var db = await TestDb.CreateAsync();
        var repository = new EventRepository(db.Context, NullLogger<EventRepository>.Instance);

        db.Context.Events.AddRange(
            new StoredEvent { UnifiEventId = "e1", Type = "motion", YoloLabels = "car,truck", Start = DateTime.UtcNow.AddMinutes(-2) },
            new StoredEvent { UnifiEventId = "e2", Type = "motion", YoloLabels = "person", Start = DateTime.UtcNow.AddMinutes(-1) });
        await db.Context.SaveChangesAsync();

        var results = await repository.GetRecentEventsAsync(skip: 0, take: 10, yoloLabel: "car");

        var only = Assert.Single(results);
        Assert.Equal("e1", only.UnifiEventId);
    }

    [Fact]
    public async Task SetYoloLabelsAsync_StoresLabelsAndTimestamp_EmptyListIsAMeaningfulResult()
    {
        await using var db = await TestDb.CreateAsync();
        var repository = new EventRepository(db.Context, NullLogger<EventRepository>.Instance);
        db.Context.Events.Add(new StoredEvent { UnifiEventId = "evt-5", Type = "motion", Start = DateTime.UtcNow });
        await db.Context.SaveChangesAsync();

        await repository.SetYoloLabelsAsync("evt-5", ["person", "car"]);
        var withLabels = await db.Context.Events.SingleAsync(e => e.UnifiEventId == "evt-5");
        Assert.Equal("person,car", withLabels.YoloLabels);
        Assert.NotNull(withLabels.YoloClassifiedAt);

        // Classifying again with nothing detected must NOT look like "not yet classified".
        await repository.SetYoloLabelsAsync("evt-5", []);
        var reclassified = await db.Context.Events.SingleAsync(e => e.UnifiEventId == "evt-5");
        Assert.Equal(string.Empty, reclassified.YoloLabels);
        Assert.NotNull(reclassified.YoloClassifiedAt);
    }

    [Fact]
    public async Task UpsertFromRestAsync_EventAlreadyHasThumbnail_NeedsThumbnailIsFalse()
    {
        await using var db = await TestDb.CreateAsync();
        var repository = new EventRepository(db.Context, NullLogger<EventRepository>.Instance);
        db.Context.Events.Add(new StoredEvent
        {
            UnifiEventId = "evt-6",
            Type = "motion",
            CameraUnifiId = "cam-1",
            ThumbnailPath = "/data/thumbnails/evt-6.jpg",
            Start = DateTime.UtcNow,
        });
        await db.Context.SaveChangesAsync();

        var (_, needsThumbnail) = await repository.UpsertFromRestAsync(new ProtectEventPayload
        {
            Id = "evt-6",
            Type = "motion",
            Camera = "cam-1",
            Start = 1700000000000,
        });

        Assert.False(needsThumbnail);
    }

    private static JsonElement ParseJson(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private sealed class TestDb : IAsyncDisposable
    {
        public required DashboardDbContext Context { get; init; }
        public required SqliteConnection Connection { get; init; }

        public static async Task<TestDb> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<DashboardDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new DashboardDbContext(options);
            await context.Database.EnsureCreatedAsync();

            return new TestDb { Context = context, Connection = connection };
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
