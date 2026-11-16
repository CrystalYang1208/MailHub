using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MailHub.Host;
using MailHub.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MailHub.Tests;

public sealed class FoundationTests
{
    private static string NewDirectory()
    {
        var root = Environment.GetEnvironmentVariable("MAILHUB_TEST_ROOT") ?? Path.Combine(Environment.CurrentDirectory, ".local/tests");
        return Path.Combine(root, Guid.NewGuid().ToString("N"));
    }
    [Fact]
    public async Task ApprovedMigrationCreatesVersionedDatabaseWithoutRewritingIt()
    {
        var directory = NewDirectory(); Directory.CreateDirectory(directory); var file = Path.Combine(directory, "profile.db");
        var version = await MigrationRunner.ApplyAsync(file, "profile"); Assert.True(MigrationRunner.HasWalFix(version));
        var evidenceRoot = Environment.GetEnvironmentVariable("MAILHUB_TEST_ROOT") ?? Path.Combine(Environment.CurrentDirectory, ".local/tests");
        Directory.CreateDirectory(evidenceRoot);
        await File.WriteAllTextAsync(Path.Combine(evidenceRoot, "sqlite-runtime.json"), JsonSerializer.Serialize(new { utc = DateTimeOffset.UtcNow, runtime = version, walFix = MigrationRunner.HasWalFix(version), status = "PASS" }));
        Assert.Equal(version, await MigrationRunner.ApplyAsync(file, "profile"));
        await using var connection = new SqliteConnection($"Data Source={file};Pooling=False"); await connection.OpenAsync(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM SchemaMigration"; Assert.Equal(1L, await command.ExecuteScalarAsync());
        command.CommandText = "SELECT count(*) FROM sqlite_master WHERE name IN ('Message','MessageSearch','CacheQuota','UX_Job_Active','UX_Outbox_ActiveDraft')"; Assert.Equal(5L, await command.ExecuteScalarAsync());
    }
    [Fact]
    public async Task LoopbackHostProtectsSyntheticDataAndConsumesBootstrapOnlyOnce()
    {
        var app = await HostFactory.CreateAsync(new(true, NewDirectory(), "missing-frontend-in-unit-test"));
        await using (app)
        {
            await app.StartAsync(); var access = app.Services.GetRequiredService<EngineeringAccess>();
            Assert.StartsWith("http://127.0.0.1:", access.Origin);
            using var client = new HttpClient { BaseAddress = new Uri(access.Origin) };
            var denied = await client.GetAsync("/_engineering/items");
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
            using var deniedBody = JsonDocument.Parse(await denied.Content.ReadAsStringAsync());
            var envelope = deniedBody.RootElement;
            Assert.False(envelope.GetProperty("ok").GetBoolean());
            Assert.Equal(JsonValueKind.Null, envelope.GetProperty("data").ValueKind);
            Assert.True(Guid.TryParse(envelope.GetProperty("requestId").GetString(), out _));
            Assert.Equal(new[] { "data", "error", "ok", "requestId" }, envelope.EnumerateObject().Select(x => x.Name).Order().ToArray());
            Assert.Equal(new[] { "action", "code", "message" }, envelope.GetProperty("error").EnumerateObject().Select(x => x.Name).Order().ToArray());
            using var exchange = new HttpRequestMessage(HttpMethod.Post, "/_engineering/bootstrap") { Content = JsonContent.Create(new { code = access.BootstrapCode }) }; exchange.Headers.Add("Origin", access.Origin);
            var response = await client.SendAsync(exchange); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); var token = body.RootElement.GetProperty("data").GetProperty("bridgeToken").GetString();
            using var replay = new HttpRequestMessage(HttpMethod.Post, "/_engineering/bootstrap") { Content = JsonContent.Create(new { code = access.BootstrapCode }) }; replay.Headers.Add("Origin", access.Origin); Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(replay)).StatusCode);
            client.DefaultRequestHeaders.Authorization = new("Bearer", token); var data = await client.GetAsync("/_engineering/items"); Assert.Equal(HttpStatusCode.OK, data.StatusCode); Assert.Contains("SQLite", await data.Content.ReadAsStringAsync());
            using var hostile = new HttpRequestMessage(HttpMethod.Get, "/_engineering/items"); hostile.Headers.Add("Origin", "https://untrusted.example.invalid"); Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(hostile)).StatusCode);
            using var badHost = new HttpRequestMessage(HttpMethod.Get, "/_engineering/items"); badHost.Headers.Host = "untrusted.example.invalid"; Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(badHost)).StatusCode);
            var unimplemented = await client.GetAsync("/api/v1/messages"); Assert.Equal(HttpStatusCode.ServiceUnavailable, unimplemented.StatusCode);
            await app.StopAsync();
        }
    }
}
