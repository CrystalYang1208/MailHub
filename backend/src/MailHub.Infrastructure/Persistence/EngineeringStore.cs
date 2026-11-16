using MailHub.Application.Engineering;
using Microsoft.Data.Sqlite;

namespace MailHub.Infrastructure.Persistence;

public sealed class EngineeringStore : IEngineeringStore
{
    private readonly string databasePath;
    public EngineeringStore(string databasePath) => this.databasePath = databasePath;

    public static async Task<EngineeringStore> CreateAsync(string directory, CancellationToken ct = default)
    {
        Directory.CreateDirectory(directory);
        var marker = Path.Combine(directory, "SYNTHETIC_ENGINEERING_ONLY");
        var path = Path.Combine(directory, "profile.db");
        if (File.Exists(path) && !File.Exists(marker)) throw new InvalidOperationException("REAL_DATABASE_REFUSED");
        await File.WriteAllTextAsync(marker, "Synthetic stage-four data only", ct);
        await MigrationRunner.ApplyAsync(Path.Combine(directory, "catalog.db"), "catalog", ct);
        await MigrationRunner.ApplyAsync(path, "profile", ct);
        await using var connection = new SqliteConnection($"Data Source={path};Pooling=False;Foreign Keys=True"); await connection.OpenAsync(ct);
        using var command = connection.CreateCommand();
        command.CommandText = """
            BEGIN IMMEDIATE;
            INSERT OR IGNORE INTO Profile VALUES('00000000-0000-4000-8000-000000000001',1,0);
            INSERT OR IGNORE INTO ProfileSettings(singleton,updated_at_ms) VALUES(1,0);
            INSERT OR IGNORE INTO CacheQuota VALUES(1,500000000,0);
            INSERT OR IGNORE INTO MailAccount(account_id,owner_id,address,provider_key,auth_type,imap_host,imap_port,imap_security,smtp_host,smtp_port,smtp_security,enabled,status,created_at_ms,updated_at_ms)
            VALUES('00000000-0000-4000-8000-000000000002','00000000-0000-4000-8000-000000000001','fixture@example.invalid','generic','password','imap.example.invalid',993,'tls','smtp.example.invalid',465,'tls',0,'disabled',0,0);
            INSERT OR IGNORE INTO Folder(folder_id,account_id,remote_name,role,uid_validity,state) VALUES('00000000-0000-4000-8000-000000000003','00000000-0000-4000-8000-000000000002','INBOX','inbox',1,'active');
            INSERT OR IGNORE INTO Message(message_id,account_id,folder_id,uid_validity,uid,subject,sender_json,recipients_json,internal_date_ms,is_read,flags_json,size_bytes,has_attachments,body_index_state,server_state)
            VALUES('00000000-0000-4000-8000-000000000004','00000000-0000-4000-8000-000000000002','00000000-0000-4000-8000-000000000003',1,1,'SQLite 合成数据读取成功','[]','{}',0,0,'[]',0,0,'pending','present');
            INSERT OR IGNORE INTO SearchDocument SELECT message_row_id,'sqlite 合成数据读取成功','','',0 FROM Message;
            COMMIT;
            """;
        await command.ExecuteNonQueryAsync(ct);
        return new EngineeringStore(path);
    }

    public async Task<EngineeringSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly;Pooling=False"); await connection.OpenAsync(cancellationToken);
        using var command = connection.CreateCommand(); command.CommandText = "SELECT subject FROM Message ORDER BY message_row_id LIMIT 5";
        var items = new List<EngineeringItem>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken)) items.Add(new EngineeringItem(reader.GetString(0)));
        return new EngineeringSnapshot(items, connection.ServerVersion);
    }
}
