using System.Net;
using Microsoft.AspNetCore.Hosting;
using ProteoSM.Api.IntegrationTests.Infrastructure;

namespace ProteoSM.Api.IntegrationTests.Hardening;

/// <summary>The build has two entry pages on one origin (ADR-0038); a deep link or a reload reaches the right one.</summary>
[Collection(ApiCollection.Name)]
public sealed class EntryPageTests(ApiFactory factory) : IDisposable
{
    private const string UserEntry = "user entry";
    private const string AdminEntry = "admin entry";

    private readonly string _webRoot = Directory.CreateTempSubdirectory("proteone-web-").FullName;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(_webRoot, recursive: true);

    [Theory]
    [InlineData("/", UserEntry)]
    [InlineData("/tickets/INC-2026-000001", UserEntry)]
    [InlineData("/administration", UserEntry)]
    [InlineData("/admin", AdminEntry)]
    [InlineData("/admin/", AdminEntry)]
    [InlineData("/admin/sla/policy", AdminEntry)]
    public async Task A_page_address_gets_its_entry(string path, string expected)
    {
        using var client = await ClientAsync();

        var response = await client.GetAsync(new Uri(path, UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expected, await response.Content.ReadAsStringAsync(Ct));
    }

    [Theory]
    // No endpoint serves a missing file, so the fallback policy answers an anonymous visitor.
    [InlineData("/admin/missing.js", HttpStatusCode.Unauthorized)]
    [InlineData("/api/v1/admin/users", HttpStatusCode.Unauthorized)]
    public async Task A_missing_file_and_the_admin_api_never_get_an_entry_page(string path, HttpStatusCode expected)
    {
        using var client = await ClientAsync();

        Assert.Equal(expected, (await client.GetAsync(new Uri(path, UriKind.Relative), Ct)).StatusCode);
    }

    private async Task<HttpClient> ClientAsync()
    {
        Directory.CreateDirectory(Path.Combine(_webRoot, "admin"));
        await File.WriteAllTextAsync(Path.Combine(_webRoot, "index.html"), UserEntry, Ct);
        await File.WriteAllTextAsync(Path.Combine(_webRoot, "admin", "index.html"), AdminEntry, Ct);
        return factory.WithWebHostBuilder(b => b.UseWebRoot(_webRoot)).CreateDefaultClient(new Uri("https://localhost"));
    }
}
