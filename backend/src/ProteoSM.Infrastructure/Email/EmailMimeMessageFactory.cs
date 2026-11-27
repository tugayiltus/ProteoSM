using MimeKit;
using ProteoSM.Application.Abstractions;

namespace ProteoSM.Infrastructure.Email;

/// <summary>Builds the MIME message both <see cref="SmtpEmailSender"/> and <see cref="PickupFolderEmailSender"/> send (ADR-0016: plain text only, no HTML).</summary>
internal static class EmailMimeMessageFactory
{
    public static MimeMessage Build(string from, EmailMessage message) => Build(MailboxAddress.Parse(from), message);

    public static MimeMessage Build(MailboxAddress from, EmailMessage message)
    {
        var mime = new MimeMessage();
        mime.From.Add(from);
        mime.To.Add(new MailboxAddress(message.ToName, message.To));
        mime.Subject = message.Subject;
        mime.Body = new TextPart(MimeKit.Text.TextFormat.Plain) { Text = message.Body };
        return mime;
    }
}
