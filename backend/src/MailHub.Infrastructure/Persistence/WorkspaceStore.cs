using MailHub.Application.Workspace;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;

namespace MailHub.Infrastructure.Persistence;

public sealed class WorkspaceStore(string root) : IWorkspaceStore
{
    private readonly object writer = new();
    public static async Task<WorkspaceStore> CreateAsync(string root)
    {
        Directory.CreateDirectory(root);
        await MigrationRunner.ApplyAsync(Path.Combine(root, "catalog.db"), "catalog");
        var store = new WorkspaceStore(root);
        using var resource = typeof(WorkspaceStore).Assembly.GetManifestResourceStream("MailHub.Migrations.catalog.V0002.sql")!;
        using var reader = new StreamReader(resource, Encoding.UTF8);
        var sql = await reader.ReadToEndAsync(); var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql)));
        store.Write<object?>(null, db =>
        {
            var previous = db.One("SELECT sha256 FROM SchemaMigration WHERE version=2");
            if (previous is not null) { if (previous.Text("sha256") != hash) throw new InvalidOperationException("APPLIED_MIGRATION_HASH_CHANGED"); return null; }
            db.Execute(sql);
            db.Execute("INSERT INTO SchemaMigration VALUES(2,$hash,$now)", ("hash", hash), ("now", JsonDefaults.Now));
            db.Execute("PRAGMA user_version=2");
            return null;
        });
        foreach (var user in store.Read(null, db => db.Query("SELECT user_id,state FROM LocalUser WHERE state<>'active'")))
        {
            var id = user.Text("user_id");
            if (user.Text("state") == "provisioning" && File.Exists(store.Database(id)))
            {
                await store.CreateProfileAsync(id);
                store.Write<object?>(null, db => { db.Execute("UPDATE LocalUser SET state='active' WHERE user_id=$u", ("u", id)); return null; });
            }
            else
            {
                store.RemoveProfile(id);
                store.Write<object?>(null, db => { db.Execute("DELETE FROM LocalUser WHERE user_id=$u", ("u", id)); return null; });
            }
        }
        return store;
    }
    public string UserDirectory(string userId)
    {
        if (!Guid.TryParse(userId, out var id)) throw MailHubException.Missing();
        return Path.Combine(root, "users", id.ToString());
    }
    private string Database(string? userId) => userId is null ? Path.Combine(root, "catalog.db") : Path.Combine(UserDirectory(userId), "profile.db");
    public async Task CreateProfileAsync(string userId, CancellationToken ct = default)
    {
        await MigrationRunner.ApplyAsync(Database(userId), "profile", ct);
        Write<object?>(userId, db =>
        {
            db.Execute("INSERT OR IGNORE INTO Profile(user_id,created_at_ms) VALUES($u,$now)", ("u", userId), ("now", JsonDefaults.Now));
            db.Execute("INSERT OR IGNORE INTO ProfileSettings(singleton,updated_at_ms) VALUES(1,$now)", ("now", JsonDefaults.Now));
            db.Execute("INSERT OR IGNORE INTO CacheQuota(singleton) VALUES(1)");
            return null;
        }, ct);
        Directory.CreateDirectory(Path.Combine(UserDirectory(userId), "files"));
    }
    public T Read<T>(string? userId, Func<IDataSession, T> query, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        using var connection = Open(userId);
        var result = query(new DataSession(connection, null));
        ct.ThrowIfCancellationRequested();
        return result;
    }
    public T Write<T>(string? userId, Func<IDataSession, T> operation, CancellationToken ct = default)
    {
        lock (writer)
        {
            ct.ThrowIfCancellationRequested();
            using var connection = Open(userId);
            using var transaction = connection.BeginTransaction();
            var result = operation(new DataSession(connection, transaction));
            ct.ThrowIfCancellationRequested();
            transaction.Commit();
            return result;
        }
    }
    private SqliteConnection Open(string? userId)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Database(userId), Mode = SqliteOpenMode.ReadWrite, ForeignKeys = true, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA busy_timeout=5000; PRAGMA synchronous=FULL; PRAGMA secure_delete=ON";
        command.ExecuteNonQuery();
        return connection;
    }
    public void RemoveProfile(string userId)
    {
        lock (writer) if (Directory.Exists(UserDirectory(userId))) Directory.Delete(UserDirectory(userId), true);
    }
    private sealed class DataSession(SqliteConnection connection, SqliteTransaction? transaction) : IDataSession
    {
        private SqliteCommand Command(string sql, (string Name, object? Value)[] parameters)
        {
            var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = sql;
            foreach (var (name, value) in parameters) command.Parameters.AddWithValue("$" + name.TrimStart('$'), value ?? DBNull.Value);
            return command;
        }
        public int Execute(string sql, params (string Name, object? Value)[] parameters)
        {
            using var command = Command(sql, parameters); return command.ExecuteNonQuery();
        }
        public List<DataRow> Query(string sql, params (string Name, object? Value)[] parameters)
        {
            using var command = Command(sql, parameters); using var reader = command.ExecuteReader();
            List<DataRow> rows = [];
            while (reader.Read())
            {
                var row = new DataRow();
                for (var i = 0; i < reader.FieldCount; i++) row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                rows.Add(row);
            }
            return rows;
        }
    }
}
