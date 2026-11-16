using System.Security.Cryptography;
using System.Text;
using MailHub.Application.Workspace;
using System.Security.AccessControl;
using System.Security.Principal;

namespace MailHub.Infrastructure.Security;

public static class PrivateDataDirectory
{
    public static void Prepare(string root)
    {
        Directory.CreateDirectory(root);
        if (OperatingSystem.IsWindows())
        {
            var security = new DirectorySecurity(); security.SetAccessRuleProtection(true, false);
            var inheritance = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
            foreach (var sid in new[] { WindowsIdentity.GetCurrent().User!, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null) })
                security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, inheritance, PropagationFlags.None, AccessControlType.Allow));
            new DirectoryInfo(root).SetAccessControl(security);
        }
        else File.SetUnixFileMode(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
}

public sealed class WindowsSecretProtector : ISecretProtector
{
    public byte[] Protect(byte[] value, string purpose)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows DPAPI required");
        return ProtectedData.Protect(value, SHA256.HashData(Encoding.UTF8.GetBytes(purpose)), DataProtectionScope.CurrentUser);
    }
    public byte[] Unprotect(byte[] value, string purpose)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows DPAPI required");
        return ProtectedData.Unprotect(value, SHA256.HashData(Encoding.UTF8.GetBytes(purpose)), DataProtectionScope.CurrentUser);
    }
}

// This protector is exclusively for synthetic data. Production never selects it.
public sealed class SyntheticSecretProtector : ISecretProtector
{
    private readonly byte[] key;
    public SyntheticSecretProtector(string root)
    {
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, "synthetic-only.key");
        if (!File.Exists(file)) File.WriteAllBytes(file, RandomNumberGenerator.GetBytes(32));
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        key = File.ReadAllBytes(file);
    }
    public byte[] Protect(byte[] value, string purpose) => EnvelopeCrypto.Seal(key, value, purpose);
    public byte[] Unprotect(byte[] value, string purpose) => EnvelopeCrypto.Open(key, value, purpose);
}
