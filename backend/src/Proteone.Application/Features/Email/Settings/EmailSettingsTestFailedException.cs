namespace Proteone.Application.Features.Email.Settings;

/// <summary>
/// A save of active settings was refused because its test send failed. <see cref="Detail"/> is the server's message,
/// already sanitized and redacted by <see cref="EmailSettingsTester"/>. The admin needs it to see why the new values fail,
/// because the page cannot run a test of unsaved values.
/// </summary>
public sealed class EmailSettingsTestFailedException(string code, string? detail)
    : Exception("The new values of the active mail settings failed the test send.")
{
    public string Code { get; } = code;

    public string? Detail { get; } = detail;
}
