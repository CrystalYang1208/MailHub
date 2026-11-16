using System.Text.Json;
using MailHub.Application.Engineering;
using MailHub.Application.Workspace;

namespace MailHub.Host;

public static class ProductEndpoints
{
    private static UserContext User(HttpContext c) => (UserContext)c.Items["user"]!;
    private static IResult Ok(object? data, int status = 200) => Results.Json(new ApiEnvelope<object?>(true, data, null, Guid.NewGuid()), statusCode: status);
    private static async Task<JsonElement> Body(HttpContext context) => (await context.Request.ReadFromJsonAsync<JsonElement>()).Clone();
    private static string Text(JsonElement value, string name, bool required = true)
    {
        if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var field) && field.ValueKind == JsonValueKind.String) return field.GetString()!;
        if (!required) return "";
        throw MailHubException.Invalid("缺少有效字段：" + name);
    }
    private static T Decode<T>(JsonElement body)
    {
        try { return body.Deserialize<T>(JsonDefaults.Options) ?? throw MailHubException.Invalid("请求内容不完整"); }
        catch (JsonException) { throw MailHubException.Invalid("请求字段格式不正确"); }
    }
    private static long Version(HttpContext c)
    {
        if (long.TryParse(c.Request.Headers.IfMatch.ToString().Trim('"'), out var value) && value >= 1) return value;
        throw MailHubException.Invalid("草稿请求需要If-Match版本号");
    }
    private static Dictionary<string, string?> Query(HttpContext c) => c.Request.Query.ToDictionary(x => x.Key, x => (string?)x.Value.ToString());
    private static DraftInput DraftFields(JsonElement body) => new(
        body.TryGetProperty("to", out var to) ? Decode<MailAddress[]>(to) : [], body.TryGetProperty("cc", out var cc) ? Decode<MailAddress[]>(cc) : [],
        body.TryGetProperty("bcc", out var bcc) ? Decode<MailAddress[]>(bcc) : [], Text(body, "subject", false), Text(body, "bodyText", false),
        body.TryGetProperty("bodyHtml", out var html) && html.ValueKind == JsonValueKind.String ? html.GetString() : null);
    public static void Map(WebApplication app)
    {
        var api = app.MapGroup("/api/v1");
        api.MapPost("/bridge/bootstrap", async (HttpContext c, EngineeringAccess access) =>
        {
            var body = await Body(c);
            if (!access.Exchange(Text(body, "code"))) throw new MailHubException(403, "BOOTSTRAP_EXPIRED", "启动链接已过期或已使用", "请重新运行启动器打开工作台");
            return Ok(new { bridgeToken = access.Token, expiresAt = access.TokenExpiresAt.UtcDateTime.ToString("O") });
        });
        api.MapGet("/users", (VaultService vault) => Ok(vault.ListUsers()));
        api.MapPost("/users", async (HttpContext c, VaultService vault) => { var b = await Body(c); return Ok(await vault.RegisterAsync(Text(b, "displayName"), Text(b, "password")), 201); });
        async Task<IResult> Login(HttpContext c, WorkspaceService workspace)
        {
            var b = await Body(c); var result = await workspace.Vault.LoginAsync(Text(b, "userId"), Text(b, "password"));
            var user = workspace.Vault.Current!; workspace.Recover(user); await workspace.SeedDemoAsync(user); user.Check(); return Ok(result);
        }
        api.MapPost("/sessions", Login); api.MapPost("/sessions/switch", Login);
        api.MapGet("/sessions/current", (HttpContext c, VaultService vault) => Ok(vault.ListUsers().Single(u => u.UserId == User(c).UserId)));
        api.MapDelete("/sessions/current", (VaultService vault) => { vault.Logout(); return Ok(new { done = true }); });
        api.MapPut("/users/current/password", async (HttpContext c, VaultService vault) => { var b = await Body(c); vault.ChangePassword(User(c), Text(b, "currentPassword"), Text(b, "newPassword")); return Ok(new { done = true }); });
        api.MapDelete("/users/current", async (HttpContext c, WorkspaceService w) => { var b = await Body(c); return Ok(await w.DeleteUserAsync(User(c), Text(b, "password")), 202); });
        api.MapGet("/providers", () => Ok(WorkspaceService.Providers()));
        api.MapGet("/accounts", (HttpContext c, WorkspaceService w) => Ok(w.Accounts(User(c))));
        api.MapPost("/accounts/validations", async (HttpContext c, WorkspaceService w) => Ok(await w.ValidateAccountAsync(User(c), Decode<AccountInput>(await Body(c)))));
        api.MapPost("/accounts", async (HttpContext c, WorkspaceService w) => Ok(await w.AddAccountAsync(User(c), Text(await Body(c), "validationId")), 201));
        api.MapGet("/accounts/{accountId}", (HttpContext c, WorkspaceService w, string accountId) => Ok(w.Account(User(c), accountId)));
        api.MapPatch("/accounts/{accountId}", async (HttpContext c, WorkspaceService w, string accountId) => { var b = await Body(c); if (!b.TryGetProperty("enabled", out var enabled) || enabled.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw MailHubException.Invalid("请指定启用或停用"); return Ok(w.SetEnabled(User(c), accountId, enabled.GetBoolean())); });
        api.MapDelete("/accounts/{accountId}", (HttpContext c, WorkspaceService w, string accountId) => Ok(w.RemoveAccount(User(c), accountId), 202));
        api.MapPost("/accounts/{accountId}/reauthorize", async (HttpContext c, WorkspaceService w, string accountId) => Ok(await w.ReauthorizeAsync(User(c), accountId, Decode<CredentialInput>(await Body(c)))));
        api.MapGet("/accounts/{accountId}/folders", (HttpContext c, WorkspaceService w, string accountId) => Ok(w.Folders(User(c), accountId)));
        api.MapPut("/settings/oauth-client", async (HttpContext c, WorkspaceService w) => Ok(w.ImportOAuthClient(User(c), await Body(c))));
        api.MapPost("/oauth/flows", async (HttpContext c, WorkspaceService w) => { var b = await Body(c); return Ok(await w.StartOAuth(User(c), Text(b, "address"), b.TryGetProperty("accountId", out var a) ? a.GetString() : null), 201); });
        api.MapGet("/oauth/flows/{flowId}", (HttpContext c, WorkspaceService w, string flowId) => Ok(w.OAuthStatus(User(c), flowId)));
        api.MapGet("/messages", (HttpContext c, WorkspaceService w) => Ok(w.Messages(User(c), Query(c))));
        api.MapGet("/search", (HttpContext c, WorkspaceService w) => Ok(w.Messages(User(c), Query(c), true)));
        api.MapGet("/messages/{messageId}", (HttpContext c, WorkspaceService w, string messageId) => Ok(w.Message(User(c), messageId)));
        api.MapGet("/messages/{messageId}/body", async (HttpContext c, WorkspaceService w, string messageId) => Ok(await w.BodyAsync(User(c), messageId)));
        api.MapPatch("/messages/{messageId}/read", async (HttpContext c, WorkspaceService w, string messageId) => { var b = await Body(c); if (!b.TryGetProperty("isRead", out var flag) || flag.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw MailHubException.Invalid("请指定已读状态"); return Ok(w.Mutation(User(c), messageId, flag.GetBoolean()), 202); });
        api.MapPost("/messages/{messageId}/trash", (HttpContext c, WorkspaceService w, string messageId) => Ok(w.Mutation(User(c), messageId, null), 202));
        api.MapPost("/messages/{messageId}/reply", async (HttpContext c, WorkspaceService w, string messageId) => { var b = await Body(c); return Ok(await w.DeriveDraftAsync(User(c), messageId, Text(b, "accountId"), Text(b, "comment", false), false), 201); });
        api.MapPost("/messages/{messageId}/forward", async (HttpContext c, WorkspaceService w, string messageId) => { var b = await Body(c); return Ok(await w.DeriveDraftAsync(User(c), messageId, Text(b, "accountId"), Text(b, "comment", false), true), 201); });
        api.MapGet("/drafts", (HttpContext c, WorkspaceService w) => Ok(w.Drafts(User(c), Query(c))));
        api.MapPost("/drafts", async (HttpContext c, WorkspaceService w) => { var b = await Body(c); return Ok(w.CreateDraft(User(c), Text(b, "accountId"), DraftFields(b)), 201); });
        api.MapGet("/drafts/{draftId}", (HttpContext c, WorkspaceService w, string draftId) => Ok(w.Draft(User(c), draftId)));
        api.MapPut("/drafts/{draftId}", async (HttpContext c, WorkspaceService w, string draftId) => Ok(w.SaveDraft(User(c), draftId, Version(c), DraftFields(await Body(c)))));
        api.MapDelete("/drafts/{draftId}", (HttpContext c, WorkspaceService w, string draftId) => Ok(w.DeleteDraft(User(c), draftId, Version(c))));
        api.MapPost("/drafts/{draftId}/attachments", async (HttpContext c, WorkspaceService w, string draftId) =>
        {
            var form = await c.Request.ReadFormAsync(User(c).Token); if (form.Files.Count != 1) throw MailHubException.Invalid("请每次上传一个附件");
            var file = form.Files[0]; await using var input = file.OpenReadStream();
            return Ok(await w.UploadDraftFileAsync(User(c), draftId, Version(c), file.FileName, file.ContentType, input), 201);
        }).DisableAntiforgery();
        api.MapDelete("/drafts/{draftId}/attachments/{fileId}", (HttpContext c, WorkspaceService w, string draftId, string fileId) => Ok(w.RemoveDraftFile(User(c), draftId, fileId, Version(c))));
        api.MapPost("/drafts/{draftId}/send", async (HttpContext c, WorkspaceService w, string draftId) => { var b = await Body(c); if (!b.TryGetProperty("draftVersion", out var version) || !version.TryGetInt64(out var number)) throw MailHubException.Invalid("缺少草稿版本号"); return Ok(w.SendDraft(User(c), draftId, number, c.Request.Headers["Idempotency-Key"].ToString()), 202); });
        api.MapGet("/submissions/{submissionId}", (HttpContext c, WorkspaceService w, string submissionId) => Ok(w.Submission(User(c), submissionId)));
        api.MapPost("/submissions/{submissionId}/resolve", async (HttpContext c, WorkspaceService w, string submissionId) => { var b = await Body(c); return Ok(w.ResolveSubmission(User(c), submissionId, Text(b, "decision"), b.TryGetProperty("confirmPossibleDuplicate", out var confirmed) && confirmed.ValueKind == JsonValueKind.True)); });
        api.MapPost("/downloads", async (HttpContext c, WorkspaceService w) => { var b = await Body(c); if (!b.TryGetProperty("attachmentIds", out var ids)) throw MailHubException.Invalid("请指定附件"); return Ok(await w.DownloadAsync(User(c), Decode<string[]>(ids)), 202); });
        api.MapGet("/downloads/{batchId}", (HttpContext c, WorkspaceService w, string batchId) => Ok(w.DownloadBatch(User(c), batchId)));
        api.MapGet("/attachments/{attachmentId}/content", async (HttpContext c, WorkspaceService w, string attachmentId) =>
        {
            var user = User(c); var (stream, name) = w.AttachmentContent(user, attachmentId); await using var file = stream;
            c.Response.ContentType = "application/octet-stream";
            c.Response.Headers.ContentDisposition = "attachment; filename*=UTF-8''" + Uri.EscapeDataString(name);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(user.Token, c.RequestAborted); await stream.CopyToAsync(c.Response.Body, linked.Token);
        });
        api.MapPost("/sync", async (HttpContext c, WorkspaceService w) => { var b = await Body(c); return Ok(await w.RequestSyncAsync(User(c), b.TryGetProperty("accountId", out var account) ? account.GetString() : null, b.TryGetProperty("folderId", out var folder) ? folder.GetString() : null), 202); });
        api.MapGet("/sync/jobs", (HttpContext c, WorkspaceService w) => Ok(w.Jobs(User(c), Query(c))));
        api.MapGet("/sync/jobs/{jobId}", (HttpContext c, WorkspaceService w, string jobId) => Ok(w.Job(User(c), jobId)));
        api.MapPost("/sync/jobs/{jobId}/retry", (HttpContext c, WorkspaceService w, string jobId) => Ok(w.RetryJob(User(c), jobId), 202));
        api.MapGet("/settings", (HttpContext c, WorkspaceService w) => Ok(w.Settings(User(c))));
        api.MapPatch("/settings", async (HttpContext c, WorkspaceService w) => { var b = await Body(c); return Ok(w.UpdateSettings(User(c), b.TryGetProperty("syncIntervalMinutes", out var interval) ? interval.GetInt32() : null, b.TryGetProperty("cacheLimitBytes", out var limit) ? limit.GetInt64() : null)); });
        api.MapDelete("/settings/body-cache", (HttpContext c, WorkspaceService w) => Ok(w.ClearBodyCache(User(c))));
        api.MapPost("/settings/mail-data/clear", (HttpContext c, WorkspaceService w) => Ok(w.ClearMailData(User(c)), 202));
        api.MapGet("/operations/{operationId}", (HttpContext c, WorkspaceService w, string operationId) => Ok(w.Operation(User(c), operationId)));
    }
}
