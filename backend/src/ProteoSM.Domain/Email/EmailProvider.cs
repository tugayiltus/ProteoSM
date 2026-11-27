namespace ProteoSM.Domain.Email;

/// <summary>Where outgoing mail goes. Google is a preset of the SMTP fields; Smtp lets the admin enter every field.</summary>
public enum EmailProvider
{
    Google,
    Smtp,
}
