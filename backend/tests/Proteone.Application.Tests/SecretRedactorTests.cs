using System.Text;
using Proteone.Application.Common;

namespace Proteone.Application.Tests;

public sealed class SecretRedactorTests
{
    private const string User = "mailer@firma.com";
    private const string Password = "gizli-şifre-123";

    private static string Base64(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    [Theory]
    [InlineData("plain")]
    [InlineData("login")]
    [InlineData("plain-empty-authzid")]
    [InlineData("plain-user-authzid")]
    public void Each_form_of_the_password_becomes_redacted(string form)
    {
        var secret = form switch
        {
            "plain" => Password,
            "login" => Base64(Password),
            "plain-empty-authzid" => Base64($"\0{User}\0{Password}"),
            _ => Base64($"{User}\0{User}\0{Password}"),
        };

        var text = SecretRedactor.Redact($"535 rejected {secret} end", User, Password);

        Assert.Equal($"535 rejected {SecretRedactor.RedactedValue} end", text);
    }

    [Fact]
    public void Without_a_password_the_text_stays_the_same()
    {
        Assert.Equal("535 rejected", SecretRedactor.Redact("535 rejected", User, null));
    }
}
