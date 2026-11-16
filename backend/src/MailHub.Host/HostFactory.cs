using System.Net;
using System.Security.Cryptography;
using System.Text;
using MailHub.Application.Engineering;
using MailHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.FileProviders;
using MailHub.Application.Workspace;
using MailHub.Infrastructure.Security;
using MailHub.Infrastructure.Protocols;

namespace MailHub.Host;

public sealed record HostOptions(bool Engineering, string DataDirectory, string WebRoot, bool Demo = false);
public sealed class EngineeringAccess
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, long> codes = new();
    public string Origin { get; set; } = "";
    public string BootstrapCode { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public string Token { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public DateTimeOffset TokenExpiresAt { get; } = DateTimeOffset.UtcNow.AddHours(24);
    public EngineeringAccess() { codes[BootstrapCode] = JsonDefaults.Now + 180_000; }
    public string CreateCode()
    {
        foreach (var item in codes.Where(x => x.Value < JsonDefaults.Now)) codes.TryRemove(item.Key, out _);
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); codes[code] = JsonDefaults.Now + 180_000; return code;
    }
    public bool Exchange(string? code) => code is not null && codes.TryRemove(code, out var until) && until > JsonDefaults.Now;
    public bool Allows(string header) => DateTimeOffset.UtcNow < TokenExpiresAt && header.StartsWith("Bearer ", StringComparison.Ordinal)
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(header[7..]), Encoding.UTF8.GetBytes(Token));
}

public static class HostFactory
{
    public static async Task<WebApplication> CreateAsync(HostOptions options)
    {
        var builder = WebApplication.CreateBuilder(Array.Empty<string>());
        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(format => { format.SingleLine = true; format.UseUtcTimestamp = true; format.TimestampFormat = "yyyy-MM-ddTHH:mm:ssZ "; });
        builder.Logging.AddFilter((category, level) => category is not null && category.StartsWith("MailHub.", StringComparison.Ordinal) && level >= LogLevel.Information);
        builder.WebHost.ConfigureKestrel(server => { server.Listen(IPAddress.Loopback, 0); server.Limits.MaxRequestBodySize = 28_000_000; });
        builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(form => { form.MultipartBodyLengthLimit = 25_100_000; form.MemoryBufferThreshold = 64 * 1024; });
        builder.Services.AddSingleton(new EngineeringAccess());
        if (options.Engineering) builder.Services.AddSingleton<IEngineeringStore>(await EngineeringStore.CreateAsync(options.DataDirectory));
        else
        {
            if (!options.Demo && !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) throw new PlatformNotSupportedException("Windows 11 required for real mailboxes");
            PrivateDataDirectory.Prepare(options.DataDirectory);
            var marker = Path.Combine(options.DataDirectory, "workspace-mode");
            var mode = options.Demo ? "synthetic-only" : "windows-production";
            if (File.Exists(marker) && File.ReadAllText(marker) != mode) throw new InvalidOperationException("DATA_MODE_MISMATCH");
            File.WriteAllText(marker, mode);
            var store = await WorkspaceStore.CreateAsync(options.DataDirectory);
            ISecretProtector protector = options.Demo ? new SyntheticSecretProtector(options.DataDirectory) : new WindowsSecretProtector();
            store.Write<object?>(null, db => { db.Execute("INSERT OR IGNORE INTO CatalogSecret VALUES(1,$key)", ("key", protector.Protect(RandomNumberGenerator.GetBytes(32), "catalog-hmac"))); return null; });
            builder.Services.AddSingleton<IWorkspaceStore>(store); builder.Services.AddSingleton(protector);
            builder.Services.AddSingleton<VaultService>(); builder.Services.AddSingleton<IOAuthProvider, GoogleOAuthProvider>();
            builder.Services.AddSingleton<IMailGateway>(new LimitedMailGateway(options.Demo ? new SyntheticMailGateway(options.DataDirectory) : new MailKitGateway()));
            builder.Services.AddSingleton(s => new WorkspaceService(store, s.GetRequiredService<VaultService>(), s.GetRequiredService<IMailGateway>(), s.GetRequiredService<IOAuthProvider>(), protector, options.Demo));
            builder.Services.AddHostedService<WorkspaceWorker>();
        }
        var app = builder.Build(); var access = app.Services.GetRequiredService<EngineeringAccess>();
        app.Lifetime.ApplicationStarted.Register(() => access.Origin = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
        app.Use(async (context, next) =>
        {
            try
            {
                var origin = access.Origin;
                if (origin.Length == 0 || context.Request.Host.Value != new Uri(origin).Authority) { await Error(context, 403, "HOST_REJECTED", "本地访问地址不正确", "请从启动脚本打开"); return; }
                var source = context.Request.Headers.Origin.ToString();
                if ((source.Length > 0 && source != origin) || context.Request.Headers["Sec-Fetch-Site"] == "cross-site") { await Error(context, 403, "ORIGIN_REJECTED", "拒绝跨站访问", "请使用同源工作台"); return; }
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.Headers["Referrer-Policy"] = "no-referrer";
                context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; connect-src 'self'; img-src 'self' data:; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";
                if (context.Request.Path.StartsWithSegments("/_engineering") && !options.Engineering) { await Error(context, 404, "NOT_FOUND", "工程模式未开启", "请使用工程启动脚本"); return; }
                var bootstrap = context.Request.Path == "/_engineering/bootstrap" || (!options.Engineering && context.Request.Path == "/api/v1/bridge/bootstrap");
                if ((context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/_engineering")) && !bootstrap && !access.Allows(context.Request.Headers.Authorization.ToString())) { await Error(context, 401, "BRIDGE_REQUIRED", "缺少有效本地访问令牌", "请重新打开启动链接"); return; }
                var multipart = context.Request.Path.StartsWithSegments("/api/v1/drafts") && context.Request.Path.Value!.EndsWith("/attachments", StringComparison.Ordinal) && context.Request.HasFormContentType;
                if (context.Request.Method is not ("GET" or "HEAD") && (source != origin || (!context.Request.HasJsonContentType() && !multipart))) { await Error(context, 403, "REQUEST_REJECTED", "请求来源或格式不正确", "请使用本地工作台"); return; }
                if (!options.Engineering && context.Request.Path.StartsWithSegments("/api/v1") && !bootstrap)
                {
                    var bridgeOnly = context.Request.Path == "/api/v1/users" || (context.Request.Path == "/api/v1/sessions" && context.Request.Method == "POST");
                    if (!bridgeOnly) context.Items["user"] = app.Services.GetRequiredService<VaultService>().Require(context.Request.Headers["X-MailHub-Session"].ToString());
                }
                await next();
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
            catch (BadHttpRequestException) { if (!context.Response.HasStarted) await Error(context, 400, "BAD_REQUEST", "请求格式不正确", "请重新打开工作台"); }
            catch (System.Text.Json.JsonException) { if (!context.Response.HasStarted) await Error(context, 400, "BAD_REQUEST", "请求格式不正确", "请重新打开工作台"); }
            catch (MailHubException error) { if (!context.Response.HasStarted) await Error(context, error.Status, error.Code, error.Message, error.Action); }
            catch (OperationCanceledException) { if (!context.Response.HasStarted) await Error(context, 401, "SESSION_REQUIRED", "本地用户已锁定或操作已取消", "请重新登录后重试"); }
            catch (Exception error)
            {
                app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("MailHub.Engineering").LogError("Request failed: {ErrorType}", error.GetType().Name);
                if (!context.Response.HasStarted) await Error(context, 500, "INTERNAL_ERROR", "本地程序暂时无法完成请求", "请查看脱敏诊断记录");
            }
        });
        app.MapGet("/_health", () => Results.Json(Ok(new { status = "ready", engineering = options.Engineering, demo = options.Demo })));
        app.MapPost("/_engineering/bootstrap", async (HttpContext context) =>
        {
            var request = await context.Request.ReadFromJsonAsync<BootstrapRequest>();
            if (!access.Exchange(request?.Code)) { await Error(context, 403, "BOOTSTRAP_INVALID", "启动链接已失效", "请重新运行启动脚本"); return; }
            await context.Response.WriteAsJsonAsync(Ok(new { bridgeToken = access.Token, expiresAt = access.TokenExpiresAt }));
        });
        app.MapGet("/_engineering/items", async (IServiceProvider services, CancellationToken ct) => Results.Json(Ok(await services.GetRequiredService<IEngineeringStore>().ReadAsync(ct))));
        if (options.Engineering) app.Map("/api/{**path}", async (HttpContext context) => await Error(context, 503, "FEATURE_NOT_IMPLEMENTED", "该业务功能尚未开发", "本阶段仅验证工程联通"));
        else ProductEndpoints.Map(app);
        if (Directory.Exists(options.WebRoot))
        {
            app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = new PhysicalFileProvider(options.WebRoot) });
            app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(options.WebRoot) });
        }
        // Route after static middleware so the JSON fallback cannot shadow index.html.
        app.UseRouting();
        app.MapFallback(async context => await Error(context, 404, "NOT_FOUND", "资源不存在", "请检查本地链接"));
        return app;
    }
    private static ApiEnvelope<T> Ok<T>(T data) => new(true, data, null, Guid.NewGuid());
    private static async Task Error(HttpContext context, int status, string code, string message, string action)
    {
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ApiEnvelope<object>(false, null, new(code, message, action), Guid.NewGuid()));
    }
    private sealed record BootstrapRequest(string Code);
}
