namespace MailHub.Infrastructure.Protocols;

// Dependency and boundary marker only. Stage four never connects to a provider.
public static class ProtocolAssembly
{
    public static string MailKitVersion => typeof(MailKit.Net.Imap.ImapClient).Assembly.GetName().Version!.ToString();
    public static string MimeKitVersion => typeof(MimeKit.MimeMessage).Assembly.GetName().Version!.ToString();
}
