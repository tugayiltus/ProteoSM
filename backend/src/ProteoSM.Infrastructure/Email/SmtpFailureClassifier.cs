using System.Net.Sockets;
using MailKit.Net.Smtp;
using MailKit.Security;
using ProteoSM.Domain.Common;
using TlsAuthenticationException = System.Security.Authentication.AuthenticationException;

namespace ProteoSM.Infrastructure.Email;

/// <summary>Turns a failed SMTP step into the <c>email_settings.*</c> error code the admin page shows.</summary>
internal static class SmtpFailureClassifier
{
    public static string Classify(SmtpStageException failure)
    {
        var inner = failure.Inner;
        if (inner is TimeoutException)
        {
            return ErrorCodes.EmailSettingsTimeout;
        }

        return failure.Stage switch
        {
            SmtpStage.Connect => ClassifyConnect(inner),

            // Any failure here is about the login: a rejected login (MailKit's own AuthenticationException) or a
            // server that offers no AUTH at all (NotSupportedException).
            SmtpStage.Authenticate => ErrorCodes.EmailSettingsAuthFailed,
            SmtpStage.Send => ClassifySend(inner),
            _ => ErrorCodes.EmailSettingsSendFailed,
        };
    }

    private static string ClassifyConnect(Exception inner) => inner switch
    {
        SmtpHostNotAllowedException => ErrorCodes.EmailSettingsHostNotAllowed,
        SocketException or { InnerException: SocketException } => ErrorCodes.EmailSettingsConnectFailed,

        // The TLS handshake failed (MailKit wraps it, .NET may throw its own AuthenticationException), or the server
        // does not offer STARTTLS (NotSupportedException).
        SslHandshakeException or TlsAuthenticationException or NotSupportedException => ErrorCodes.EmailSettingsTlsFailed,
        _ => ErrorCodes.EmailSettingsSendFailed,
    };

    private static string ClassifySend(Exception inner) => inner switch
    {
        SmtpCommandException { ErrorCode: SmtpErrorCode.SenderNotAccepted } => ErrorCodes.EmailSettingsSenderRejected,
        SmtpCommandException { ErrorCode: SmtpErrorCode.RecipientNotAccepted } => ErrorCodes.EmailSettingsRecipientRejected,
        _ => ErrorCodes.EmailSettingsSendFailed,
    };
}
