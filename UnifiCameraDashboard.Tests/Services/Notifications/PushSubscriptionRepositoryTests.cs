using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using UnifiCameraDashboard.Data;
using UnifiCameraDashboard.Services.Notifications;

namespace UnifiCameraDashboard.Tests.Services.Notifications;

public class PushSubscriptionRepositoryTests
{
    [Fact]
    public async Task UpsertAsync_NewEndpoint_AddsSubscription()
    {
        await using var db = await TestDb.CreateAsync();
        var repository = new PushSubscriptionRepository(db.Context, NullLogger<PushSubscriptionRepository>.Instance);

        await repository.UpsertAsync("https://push.example/1", "p256dh-1", "auth-1");

        var all = await repository.GetAllAsync();
        var subscription = Assert.Single(all);
        Assert.Equal("https://push.example/1", subscription.Endpoint);
        Assert.Equal("p256dh-1", subscription.P256dh);
        Assert.Equal("auth-1", subscription.Auth);
    }

    [Fact]
    public async Task UpsertAsync_ExistingEndpoint_UpdatesKeysInsteadOfDuplicating()
    {
        await using var db = await TestDb.CreateAsync();
        var repository = new PushSubscriptionRepository(db.Context, NullLogger<PushSubscriptionRepository>.Instance);

        await repository.UpsertAsync("https://push.example/1", "old-p256dh", "old-auth");
        await repository.UpsertAsync("https://push.example/1", "new-p256dh", "new-auth");

        var all = await repository.GetAllAsync();
        var subscription = Assert.Single(all);
        Assert.Equal("new-p256dh", subscription.P256dh);
        Assert.Equal("new-auth", subscription.Auth);
    }

    [Fact]
    public async Task RemoveAsync_RemovesOnlyTheMatchingEndpoint()
    {
        await using var db = await TestDb.CreateAsync();
        var repository = new PushSubscriptionRepository(db.Context, NullLogger<PushSubscriptionRepository>.Instance);
        await repository.UpsertAsync("https://push.example/1", "p256dh-1", "auth-1");
        await repository.UpsertAsync("https://push.example/2", "p256dh-2", "auth-2");

        await repository.RemoveAsync("https://push.example/1");

        var all = await repository.GetAllAsync();
        var remaining = Assert.Single(all);
        Assert.Equal("https://push.example/2", remaining.Endpoint);
    }

    [Fact]
    public async Task RemoveAsync_UnknownEndpoint_DoesNothing()
    {
        await using var db = await TestDb.CreateAsync();
        var repository = new PushSubscriptionRepository(db.Context, NullLogger<PushSubscriptionRepository>.Instance);
        await repository.UpsertAsync("https://push.example/1", "p256dh-1", "auth-1");

        await repository.RemoveAsync("https://push.example/does-not-exist");

        var all = await repository.GetAllAsync();
        Assert.Single(all);
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
