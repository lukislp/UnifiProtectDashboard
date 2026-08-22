using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using UnifiCameraDashboard.Data;
using UnifiCameraDashboard.Services;

namespace UnifiCameraDashboard.Tests.Services;

public class CameraRepositoryTests
{
    [Fact]
    public async Task RemoveCameraAsync_SetsEnabledFalse_AndExcludesFromGetAllCameras()
    {
        await using var db = await TestDb.CreateAsync();
        var repository = new CameraRepository(db.Context, NullLogger<CameraRepository>.Instance);
        db.Context.Cameras.Add(new StoredCamera { UnifiId = "cam-1", Name = "Driveway" });
        await db.Context.SaveChangesAsync();

        await repository.RemoveCameraAsync("cam-1");

        var all = await repository.GetAllCamerasAsync();
        Assert.Empty(all);

        var stored = await db.Context.Cameras.SingleAsync(c => c.UnifiId == "cam-1");
        Assert.False(stored.Enabled);
    }

    [Fact]
    public async Task RemoveCameraAsync_DoesNotTouchEvents()
    {
        await using var db = await TestDb.CreateAsync();
        var repository = new CameraRepository(db.Context, NullLogger<CameraRepository>.Instance);
        db.Context.Cameras.Add(new StoredCamera { UnifiId = "cam-1", Name = "Driveway" });
        db.Context.Events.Add(new StoredEvent { UnifiEventId = "evt-1", CameraUnifiId = "cam-1", Type = "motion", Start = DateTime.UtcNow });
        await db.Context.SaveChangesAsync();

        await repository.RemoveCameraAsync("cam-1");

        var eventStillThere = await db.Context.Events.SingleAsync(e => e.UnifiEventId == "evt-1");
        Assert.Equal("cam-1", eventStillThere.CameraUnifiId);
    }

    [Fact]
    public async Task RestoreCameraAsync_SetsEnabledTrue_AndReappearsInGetAllCameras()
    {
        await using var db = await TestDb.CreateAsync();
        var repository = new CameraRepository(db.Context, NullLogger<CameraRepository>.Instance);
        db.Context.Cameras.Add(new StoredCamera { UnifiId = "cam-1", Name = "Driveway", Enabled = false });
        await db.Context.SaveChangesAsync();

        await repository.RestoreCameraAsync("cam-1");

        var all = await repository.GetAllCamerasAsync();
        var restored = Assert.Single(all);
        Assert.Equal("cam-1", restored.Id);
    }

    [Fact]
    public async Task GetRemovedCamerasAsync_ReturnsOnlyDisabledCameras()
    {
        await using var db = await TestDb.CreateAsync();
        var repository = new CameraRepository(db.Context, NullLogger<CameraRepository>.Instance);
        db.Context.Cameras.AddRange(
            new StoredCamera { UnifiId = "cam-active", Name = "Driveway", Enabled = true },
            new StoredCamera { UnifiId = "cam-removed", Name = "Old Test Camera", Enabled = false });
        await db.Context.SaveChangesAsync();

        var removed = await repository.GetRemovedCamerasAsync();

        var removedCamera = Assert.Single(removed);
        Assert.Equal("cam-removed", removedCamera.Id);
    }

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
