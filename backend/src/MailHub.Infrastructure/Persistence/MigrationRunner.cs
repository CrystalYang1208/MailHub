using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace MailHub.Infrastructure.Persistence;

public static class MigrationRunner
{
    public static bool HasWalFix(string version) => Version.TryParse(version, out var parsed)
        && (parsed >= new Version(3, 51, 3) || parsed == new Version(3, 50, 7) || parsed == new Version(3, 44, 6));

    public static async Task<string> ApplyAsync(string databasePath, string kind, CancellationToken ct = default)
    {
        if (kind is not ("catalog" or "profile")) throw new ArgumentException("Unknown migration kind", nameof(kind));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(databasePath))!);
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false,
            ForeignKeys = true,
        }.ToString());
        await connection.OpenAsync(ct);
        using var versionQuery = connection.CreateCommand();
        versionQuery.CommandText = "SELECT sqlite_version()";
        var runtime = (string)(await versionQuery.ExecuteScalarAsync(ct))!;
        if (!HasWalFix(runtime)) throw new InvalidOperationException("SQLITE_WAL_FIX_REQUIRED: " + runtime);
        using var resource = typeof(MigrationRunner).Assembly.GetManifestResourceStream($"MailHub.Migrations.{kind}.V0001.sql")
            ?? throw new InvalidOperationException("MIGRATION_RESOURCE_MISSING");
        using var reader = new StreamReader(resource, Encoding.UTF8);
        var sql = await reader.ReadToEndAsync(ct);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql)));
        await ExecuteAsync(connection, "PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; PRAGMA busy_timeout=5000; PRAGMA wal_autocheckpoint=0;", ct);
        using var schemaQuery = connection.CreateCommand();
        schemaQuery.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='SchemaMigration'";
        var trackingExists = Convert.ToInt32(await schemaQuery.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture) > 0;
        if (!trackingExists)
        {
            schemaQuery.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
            if (Convert.ToInt32(await schemaQuery.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture) > 0)
                throw new InvalidOperationException("UNMANAGED_DATABASE_REFUSED");
        }
        await ExecuteAsync(connection, "CREATE TABLE IF NOT EXISTS SchemaMigration(version INTEGER PRIMARY KEY,sha256 TEXT NOT NULL,applied_at_ms INTEGER NOT NULL)", ct);
        schemaQuery.CommandText = "SELECT sha256 FROM SchemaMigration WHERE version=1";
        var applied = await schemaQuery.ExecuteScalarAsync(ct);
        if (applied is string previous)
        {
            if (previous != hash) throw new InvalidOperationException("APPLIED_MIGRATION_HASH_CHANGED");
            return runtime;
        }
        using var transaction = connection.BeginTransaction();
        // WAL/connection PRAGMAs must run outside the schema transaction. Original DDL bytes stay unchanged.
        var statements = Regex.Replace(sql, @"^PRAGMA[^\r\n]*;\s*$", "", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        using var migrate = connection.CreateCommand(); migrate.Transaction = transaction; migrate.CommandText = statements;
        await migrate.ExecuteNonQueryAsync(ct);
        migrate.CommandText = "INSERT INTO SchemaMigration VALUES(1,$hash,$now); PRAGMA user_version=1;";
        migrate.Parameters.AddWithValue("$hash", hash); migrate.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        await migrate.ExecuteNonQueryAsync(ct);
        migrate.CommandText = "PRAGMA foreign_key_check"; migrate.Parameters.Clear();
        await using (var violations = await migrate.ExecuteReaderAsync(ct))
            if (await violations.ReadAsync(ct)) throw new InvalidOperationException("MIGRATION_FOREIGN_KEY_CHECK_FAILED");
        transaction.Commit();
        return runtime;
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken ct)
    {
        using var command = connection.CreateCommand(); command.CommandText = sql; await command.ExecuteNonQueryAsync(ct);
    }
}
