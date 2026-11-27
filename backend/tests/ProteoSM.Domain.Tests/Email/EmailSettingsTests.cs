using ProteoSM.Domain.Common;
using ProteoSM.Domain.Email;

namespace ProteoSM.Domain.Tests.Email;

public sealed class EmailSettingsTests
{
    private const string Address = "servis@sirket.com";
    private const string Host = "mail.sirket.local";
    private const string UserName = "servis";
    private const string Protected = "protected-1";
    private const string ErrorCode = ErrorCodes.EmailSettingsAuthFailed;
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Now.AddMinutes(5);

    private static EmailSettings WithSmtp()
    {
        var settings = EmailSettings.CreateDefault(Now);
        settings.Update(EmailProvider.Smtp, "Servis Masası", Address, Host, 587, EmailSecurity.StartTls, UserName, Protected, Now);
        return settings;
    }

    private static EmailSettings Active()
    {
        var settings = WithSmtp();
        settings.RecordTest(true, null, Now);
        settings.Activate(Now);
        return settings;
    }

    [Fact]
    public void Default_is_an_inactive_google_preset()
    {
        var settings = EmailSettings.CreateDefault(Now);

        Assert.Equal(EmailSettings.SingletonId, settings.Id);
        Assert.Equal((EmailProvider.Google, EmailSettings.GoogleHost, EmailSettings.GooglePort, EmailSettings.GoogleSecurity), (settings.Provider, settings.Host, settings.Port, settings.Security));
        Assert.False(settings.IsActive);
        Assert.False(settings.IsConfigured);
        Assert.False(settings.IsTested);
    }

    [Fact]
    public void Google_update_forces_preset_host_port_and_security()
    {
        var settings = EmailSettings.CreateDefault(Now);

        settings.Update(EmailProvider.Google, null, "hesap@gmail.com", "x.local", 25, EmailSecurity.None, "hesap@gmail.com", Protected, Now);

        Assert.Equal(("smtp.gmail.com", 587, EmailSecurity.StartTls), (settings.Host, settings.Port, settings.Security));
        Assert.True(settings.IsConfigured);
    }

    [Fact]
    public void Update_trims_and_turns_blank_names_into_null()
    {
        var settings = EmailSettings.CreateDefault(Now);

        settings.Update(EmailProvider.Smtp, "   ", $"  {Address} ", $" {Host} ", 25, EmailSecurity.None, "  ", null, Later);

        Assert.Null(settings.SenderName);
        Assert.Null(settings.UserName);
        Assert.Equal((Address, Host, Later), (settings.SenderAddress, settings.Host, settings.UpdatedAt));
    }

    [Fact]
    public void Null_password_keeps_the_stored_one()
    {
        var settings = WithSmtp();

        settings.Update(EmailProvider.Smtp, "Servis Masası", Address, Host, 587, EmailSecurity.StartTls, UserName, null, Now);

        Assert.Equal(Protected, settings.PasswordProtected);
    }

    [Fact]
    public void Clearing_the_user_name_drops_the_stored_password()
    {
        var settings = WithSmtp();

        settings.Update(EmailProvider.Smtp, "Servis Masası", Address, Host, 25, EmailSecurity.None, null, null, Now);

        Assert.Null(settings.UserName);
        Assert.Null(settings.PasswordProtected);
    }

    [Fact]
    public void None_security_with_a_user_name_throws_plain_text_login()
    {
        var settings = WithSmtp();

        var error = Assert.Throws<DomainException>(
            () => settings.Update(EmailProvider.Smtp, null, Address, Host, 25, EmailSecurity.None, UserName, null, Later));

        Assert.Equal(ErrorCodes.EmailSettingsPlainTextLogin, error.Code);
        Assert.Equal((EmailSecurity.StartTls, Now), (settings.Security, settings.UpdatedAt));
    }

    public static TheoryData<string> FieldChanges => ["provider", "name", "address", "host", "port", "security", "user name", "password"];

    [Theory]
    [MemberData(nameof(FieldChanges))]
    public void Each_field_change_breaks_the_tested_fingerprint(string field)
    {
        var settings = WithSmtp();
        settings.RecordTest(true, null, Now);
        Assert.True(settings.IsTested);
        var before = settings.Fingerprint();

        switch (field)
        {
            case "provider":
                settings.Update(EmailProvider.Google, "Servis Masası", Address, EmailSettings.GoogleHost, EmailSettings.GooglePort, EmailSettings.GoogleSecurity, UserName, Protected, Later);
                break;
            case "name":
                settings.Update(EmailProvider.Smtp, "Destek", Address, Host, 587, EmailSecurity.StartTls, UserName, null, Later);
                break;
            case "address":
                settings.Update(EmailProvider.Smtp, "Servis Masası", "destek@sirket.com", Host, 587, EmailSecurity.StartTls, UserName, null, Later);
                break;
            case "host":
                settings.Update(EmailProvider.Smtp, "Servis Masası", Address, "mail2.sirket.local", 587, EmailSecurity.StartTls, UserName, null, Later);
                break;
            case "port":
                settings.Update(EmailProvider.Smtp, "Servis Masası", Address, Host, 2525, EmailSecurity.StartTls, UserName, null, Later);
                break;
            case "security":
                settings.Update(EmailProvider.Smtp, "Servis Masası", Address, Host, 587, EmailSecurity.SslTls, UserName, null, Later);
                break;
            case "user name":
                settings.Update(EmailProvider.Smtp, "Servis Masası", Address, Host, 587, EmailSecurity.StartTls, "destek", null, Later);
                break;
            default:
                settings.Update(EmailProvider.Smtp, "Servis Masası", Address, Host, 587, EmailSecurity.StartTls, UserName, "protected-2", Later);
                break;
        }

        Assert.NotEqual(before, settings.Fingerprint());
        Assert.False(settings.IsTested);
    }

    [Fact]
    public void Same_values_keep_the_fingerprint()
    {
        var settings = WithSmtp();
        settings.RecordTest(true, null, Now);

        settings.Update(EmailProvider.Smtp, "Servis Masası", Address, Host, 587, EmailSecurity.StartTls, UserName, null, Later);

        Assert.True(settings.IsTested);
    }

    [Fact]
    public void Activate_untested_throws_not_tested()
    {
        var settings = WithSmtp();

        var error = Assert.Throws<DomainException>(() => settings.Activate(Now));

        Assert.Equal(ErrorCodes.EmailSettingsNotTested, error.Code);
        Assert.False(settings.IsActive);
    }

    [Fact]
    public void Passing_test_then_activate_sets_active()
    {
        var settings = WithSmtp();

        settings.RecordTest(true, null, Later);
        settings.Activate(Later);

        Assert.True(settings.IsActive);
        Assert.Equal((Later, true, (string?)null), (settings.LastTestedAt, settings.LastTestSucceeded, settings.LastTestErrorCode));
        Assert.Equal(Later, settings.UpdatedAt);
    }

    [Fact]
    public void Failed_test_on_active_settings_keeps_them_active()
    {
        var settings = Active();
        var fingerprint = settings.TestedFingerprint;

        settings.RecordTest(false, ErrorCode, Later);

        Assert.True(settings.IsActive);
        Assert.True(settings.IsTested);
        Assert.Equal(fingerprint, settings.TestedFingerprint);
        Assert.Equal((Later, false, ErrorCode), (settings.LastTestedAt, settings.LastTestSucceeded, settings.LastTestErrorCode));
    }

    [Fact]
    public void Failed_test_on_inactive_settings_clears_the_fingerprint()
    {
        var settings = WithSmtp();
        settings.RecordTest(true, null, Now);

        settings.RecordTest(false, ErrorCode, Later);

        Assert.Null(settings.TestedFingerprint);
        Assert.False(settings.IsTested);
        Assert.Equal((false, ErrorCode), (settings.LastTestSucceeded, settings.LastTestErrorCode));
        Assert.Throws<DomainException>(() => settings.Activate(Later));
    }

    [Fact]
    public void Passing_test_clears_the_last_error_code()
    {
        var settings = WithSmtp();
        settings.RecordTest(false, ErrorCode, Now);

        settings.RecordTest(true, ErrorCode, Later);

        Assert.Null(settings.LastTestErrorCode);
        Assert.True(settings.LastTestSucceeded);
    }

    [Fact]
    public void Too_long_error_code_is_rejected()
    {
        var settings = WithSmtp();

        Assert.Throws<ArgumentException>(
            () => settings.RecordTest(false, new string('x', EmailSettings.LastTestErrorCodeMaxLength + 1), Now));
    }

    [Fact]
    public void Use_environment_keeps_every_value()
    {
        var settings = Active();
        var fingerprint = settings.Fingerprint();

        settings.UseEnvironment(Later);

        Assert.False(settings.IsActive);
        Assert.Equal(fingerprint, settings.Fingerprint());
        Assert.True(settings.IsTested);
        Assert.Equal((Protected, UserName, Later), (settings.PasswordProtected, settings.UserName, settings.UpdatedAt));
    }

    [Fact]
    public void Use_environment_after_a_failed_test_needs_a_new_test()
    {
        var settings = Active();
        settings.RecordTest(false, ErrorCode, Later);

        settings.UseEnvironment(Later);

        Assert.False(settings.IsTested);
        Assert.Equal(Address, settings.SenderAddress);
        var error = Assert.Throws<DomainException>(() => settings.Activate(Later));
        Assert.Equal(ErrorCodes.EmailSettingsNotTested, error.Code);
    }

    [Theory]
    [InlineData(EmailSecurity.StartTls, 587)]
    [InlineData(EmailSecurity.SslTls, 465)]
    [InlineData(EmailSecurity.None, 25)]
    public void Default_port_follows_the_security(EmailSecurity security, int port) =>
        Assert.Equal(port, EmailSettings.DefaultPort(security));

    [Fact]
    public void Google_password_loses_its_spaces()
    {
        Assert.Equal("abcdefghijklmnop", EmailSettings.NormalizePassword(EmailProvider.Google, "abcd efgh ijkl mnop"));
        Assert.Equal("abcdefghijklmnop", EmailSettings.NormalizePassword(EmailProvider.Google, " abcd\tefgh ijkl mnop "));
        Assert.Equal("abcd efgh ijkl mnop", EmailSettings.NormalizePassword(EmailProvider.Smtp, "abcd efgh ijkl mnop"));
    }

    [Fact]
    public void Fingerprint_helper_hashes_parts_joined_by_new_lines() =>
        Assert.Equal(
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData("a\nb"u8)),
            SettingsFingerprint.Compute("a", "b"));
}
