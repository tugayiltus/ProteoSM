using System.Text.RegularExpressions;

namespace Proteone.Application.Tests;

/// <summary>The former product name stays only where stored data or applied migrations need it.</summary>
public sealed partial class FormerProductNameTests
{
    private const string Migrations = "backend/src/Proteone.Infrastructure/Persistence/Migrations/";

    private static readonly string[] SearchedPaths = ["backend/src", "backend/tests", "deploy", "frontend/src", "frontend/e2e", "frontend/guide", "frontend/index.html", "frontend/admin", "frontend/public", "docs", "tools", ".github", "Dockerfile", "CLAUDE.md", "README.md", "SECURITY.md", "capabilities.md", "proteone.md", "frontend/CLAUDE.md"];

    private static readonly string[] SkippedFolders = ["bin", "obj", "node_modules", "certs"];

    /// <summary>Settings of this machine, not part of the repository (git-ignored).</summary>
    private const string LocalSettings = "deploy/.env";

    private static readonly string[] BinaryExtensions = [".png", ".pdf"];

    private static readonly string[] AllowedFiles =
    [
        // Data Protection name and purposes: stored secrets were protected with them.
        "backend/src/Proteone.Domain/Notifications/OutboxEvent.cs",
        "backend/src/Proteone.Infrastructure/Security/AuthSecretProtector.cs",
        "backend/src/Proteone.Infrastructure/Security/KeyStorage.cs",
        "backend/tests/Proteone.Api.IntegrationTests/Security/DataProtectionKeysTests.cs",
        // Applied migration, and the one that replaces the name in stored templates.
        Migrations + "20260928065946_EmailTemplates.cs",
        Migrations + "20261124115906_ProteoneBrand.cs",
        Migrations + "20261124135355_ProteoneHistory.cs",
        "backend/tests/Proteone.Api.IntegrationTests/Persistence/ProteoneBrandMigrationTests.cs",
        "backend/tests/Proteone.Application.Tests/FormerProductNameTests.cs",
        // Documents that explain the stored names.
        "SECURITY.md",
        "capabilities.md",
        "docs/adr/0004-local-identity-cookie-csrf.md",
        "docs/adr/0017-password-set-links.md",
        "docs/adr/0030-login-method-chosen-in-the-admin-panel.md",
        "docs/adr/0037-proteone-name-and-compatibility.md",
        "docs/adr/0043-no-upgrade-path-from-tgysm.md",
        "docs/adr/README.md",
    ];

    [GeneratedRegex("tgysm", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FormerName();

    [Fact]
    public void The_former_name_appears_only_in_the_allowed_files()
    {
        var found = Files()
            .Where(file => FormerName().IsMatch(file) || FormerName().IsMatch(File.ReadAllText(Path.Combine(RepositoryPaths.Root, file))))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(found);
        Assert.Empty(found.Except(AllowedFiles));
        Assert.Empty(AllowedFiles.Except(found));
    }

    private static IEnumerable<string> Files() =>
        from searched in SearchedPaths
        let path = Path.Combine(RepositoryPaths.Root, searched)
        from file in File.Exists(path) ? [path] : Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
        let relative = Path.GetRelativePath(RepositoryPaths.Root, file).Replace(Path.DirectorySeparatorChar, '/')
        where relative != LocalSettings && !BinaryExtensions.Contains(Path.GetExtension(relative)) && !relative.Split('/').Any(SkippedFolders.Contains)
        select relative;
}
