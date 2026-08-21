using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace UnifiCameraDashboard.Data;

/// <summary>
/// Bridges every dashboard database created before EF Core migrations were introduced
/// (via <c>Database.EnsureCreated()</c>) onto the migrations model, without dropping or
/// recreating existing data.
/// </summary>
public static class DatabaseMigrationExtensions
{
    // Must match the ProductVersion recorded in Migrations/DashboardDbContextModelSnapshot.cs.
    private const string EfCoreProductVersion = "10.0.10";

    public static async Task MigrateSafelyAsync(this DashboardDbContext context)
    {
        var appliedMigrations = (await context.Database.GetAppliedMigrationsAsync()).ToList();
        if (appliedMigrations.Count == 0 && await LegacySchemaExistsAsync(context))
        {
            // Pre-migrations EnsureCreated() database: the InitialCreate migration's tables
            // already exist, so stamp it as applied instead of letting Migrate() run its Up()
            // (which would try to CREATE TABLE over tables that are already there and fail).
            var baseline = (await context.Database.GetPendingMigrationsAsync()).FirstOrDefault();
            if (baseline != null)
            {
                var historyRepository = context.GetService<IHistoryRepository>();
                await context.Database.ExecuteSqlRawAsync(historyRepository.GetCreateIfNotExistsScript());
                await context.Database.ExecuteSqlRawAsync(
                    historyRepository.GetInsertScript(new HistoryRow(baseline, EfCoreProductVersion)));
            }
        }

        // Fresh database, already-migrated database, and now-stamped legacy database all take
        // the same path from here: apply whatever migrations haven't run yet (none, for a
        // legacy database on the day this ships - InitialCreate is exactly its current schema).
        await context.Database.MigrateAsync();
    }

    private static async Task<bool> LegacySchemaExistsAsync(DashboardDbContext context)
    {
        var connection = context.Database.GetDbConnection();
        var wasClosed = connection.State != ConnectionState.Open;
        if (wasClosed)
        {
            await connection.OpenAsync();
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Cameras';";
            var count = (long)(await command.ExecuteScalarAsync() ?? 0L);
            return count > 0;
        }
        finally
        {
            if (wasClosed)
            {
                await connection.CloseAsync();
            }
        }
    }
}
