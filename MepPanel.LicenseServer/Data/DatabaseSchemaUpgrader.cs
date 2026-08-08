using Microsoft.EntityFrameworkCore;

namespace MepPanel.LicenseServer.Data;

/// <summary>
/// Small, idempotent upgrades for databases created before EF migrations
/// were introduced. These upgrades preserve existing users and devices.
/// </summary>
internal static class DatabaseSchemaUpgrader
{
    public static async Task UpgradeAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        await AddColumnIfMissingAsync(
            db,
            "Licenses",
            "EnabledFeatures",
            "TEXT NOT NULL DEFAULT 'MEPDB'");
        await AddColumnIfMissingAsync(db, "Licenses", "UpdatedAtUtc", "TEXT NULL");

        await AddColumnIfMissingAsync(db, "Users", "UpdatedAtUtc", "TEXT NULL");

        await AddColumnIfMissingAsync(db, "Devices", "PluginVersion", "TEXT NOT NULL DEFAULT ''");
        await AddColumnIfMissingAsync(db, "Devices", "RevokedAtUtc", "TEXT NULL");

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "AuditLogs" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_AuditLogs" PRIMARY KEY AUTOINCREMENT,
                "UserId" INTEGER NULL,
                "Action" TEXT NOT NULL,
                "Detail" TEXT NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL
            );
            """);
    }

    private static async Task AddColumnIfMissingAsync(
        AppDbContext db,
        string table,
        string column,
        string definition)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != System.Data.ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync();
        }

        try
        {
            using var columns = connection.CreateCommand();
            columns.CommandText = $"PRAGMA table_info(\"{table}\");";
            using var reader = await columns.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            using var alter = connection.CreateCommand();
            alter.CommandText =
                $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {definition};";
            await alter.ExecuteNonQueryAsync();
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }
}
