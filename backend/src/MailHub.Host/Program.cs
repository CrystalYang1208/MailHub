using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MailHub.Host;

var demo = args.Contains("--demo", StringComparer.Ordinal);
var engineering = Environment.GetEnvironmentVariable("MAILHUB_ENGINEERING") == "1";
if (!demo && !engineering && !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) throw new PlatformNotSupportedException("真实邮箱模式要求Windows 11；合成演示可使用--demo");
var root = Environment.GetEnvironmentVariable("MAILHUB_ROOT");
if (string.IsNullOrEmpty(root))
{
    var candidate = new DirectoryInfo(AppContext.BaseDirectory);
    while (candidate.Parent is not null && !Directory.Exists(Path.Combine(candidate.FullName, "frontend")) && !Directory.Exists(Path.Combine(candidate.FullName, "wwwroot"))) candidate = candidate.Parent;
    root = Directory.Exists(Path.Combine(candidate.FullName, "frontend")) || Directory.Exists(Path.Combine(candidate.FullName, "wwwroot")) ? candidate.FullName : AppContext.BaseDirectory;
}
root = Path.GetFullPath(root);
var data = Environment.GetEnvironmentVariable(engineering ? "MAILHUB_ENGINEERING_DATA" : "MAILHUB_DATA") ?? Path.Combine(root, ".local", engineering ? "development" : demo ? "demo" : "mailhub");
Directory.CreateDirectory(data);
var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Environment.UserDomainName + "\\" + Environment.UserName)))[..24] + (demo ? "Demo" : engineering ? "Engineering" : "Product");
var pipeName = "MailHub.Workspace." + identity;
Mutex? singleton = null;
if (OperatingSystem.IsWindows())
{
    singleton = new Mutex(true, "Local\\" + pipeName, out var created);
    if (!created)
    {
        singleton.Dispose();
        try
        {
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous);
            await pipe.ConnectAsync(5000); using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true }; using var reader = new StreamReader(pipe, leaveOpen: true);
            await writer.WriteLineAsync("OpenWorkspace"); var url = await reader.ReadLineAsync();
            if (url is not null && url.StartsWith("http://127.0.0.1:", StringComparison.Ordinal)) Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return;
        }
        catch { throw new InvalidOperationException("另一个MailHub实例正在运行；请从原窗口打开或停止原实例"); }
    }
}
using var instance = singleton;
using var processLock = new FileStream(Path.Combine(data, "host.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
var web = Directory.Exists(Path.Combine(root, "wwwroot")) ? Path.Combine(root, "wwwroot") : Path.Combine(root, "frontend/dist");
var app = await HostFactory.CreateAsync(new(engineering, data, web, demo));
await app.StartAsync();
var access = app.Services.GetRequiredService<EngineeringAccess>();
var startupFile = Environment.GetEnvironmentVariable("MAILHUB_STARTUP_FILE") ?? Path.Combine(data, "startup.json");
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(startupFile))!);
var startupUrl = access.Origin + "/#code=" + access.BootstrapCode;
await File.WriteAllTextAsync(startupFile, JsonSerializer.Serialize(new { origin = access.Origin, url = startupUrl, pid = Environment.ProcessId, demo }));
if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(startupFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
Console.WriteLine(demo ? "MailHub合成演示已启动。所有示例均非真实邮件。" : "MailHub本地工作台已启动。关闭此程序可停止服务。");
Console.WriteLine("启动信息保存在本机私有文件：" + startupFile);
if (OperatingSystem.IsWindows() && !args.Contains("--no-browser", StringComparer.Ordinal)) Process.Start(new ProcessStartInfo(startupUrl) { UseShellExecute = true });
if (OperatingSystem.IsWindows())
{
    _ = Task.Run(async () =>
    {
        while (!app.Lifetime.ApplicationStopping.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous);
                await pipe.WaitForConnectionAsync(app.Lifetime.ApplicationStopping);
                using var reader = new StreamReader(pipe, leaveOpen: true); using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
                if (await reader.ReadLineAsync(app.Lifetime.ApplicationStopping) == "OpenWorkspace") await writer.WriteLineAsync(access.Origin + "/#code=" + access.CreateCode());
            }
            catch (OperationCanceledException) { break; }
            catch (IOException) { }
        }
    });
}
await app.WaitForShutdownAsync();
