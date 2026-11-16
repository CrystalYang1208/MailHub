using System.Globalization;
using System.Text;

namespace MailHub.Domain;

public static class MailRules
{
    public const long AttachmentLimit = 25_000_000;
    public const long BatchLimit = 200_000_000;
    public const long DefaultCacheLimit = 500_000_000;
    public static string SearchText(string value) => value.Normalize(NormalizationForm.FormC).ToLowerInvariant();
    public static string AddressKey(string value, string provider)
    {
        var parts = value.Trim().Split('@');
        if (parts.Length != 2 || parts.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("INVALID_ADDRESS");
        var domain = new IdnMapping().GetAscii(parts[1]).ToLowerInvariant();
        var local = parts[0].Normalize(NormalizationForm.FormC);
        if (provider != "generic") local = local.ToLowerInvariant();
        return local + "@" + domain;
    }
    public static TimeSpan RetryDelay(int attempt) => TimeSpan.FromMinutes(Math.Pow(2, Math.Clamp(attempt - 1, 0, 3)));
    public static string LikeLiteral(string value) => "%" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal) + "%";
    public static string FtsLiteral(string value) => "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
}
