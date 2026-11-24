using System.Text.RegularExpressions;

namespace Proteone.Application.Tests;

/// <summary>The former product name stays only where stored data, upgrades or applied migrations need it.</summary>
public sealed partial class FormerProductNameTests
{
    private const string Migrations = "backend/src/Proteone.Infrastructure/Persistence/Migrations/";

    private static readonly string[] SearchedPaths = ["backend/src", "backend/tests", "deploy", "frontend/src", "frontend/e2e", "frontend/guide", "frontend/index.html", "tools", ".github", "Dockerfile"];

    private static readonly string[] SkippedFolders = ["bin", "obj", "node_modules", "certs"];

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
        "backend/tests/Proteone.Api.IntegrationTests/Persistence/ProteoneBrandMigrationTests.cs",
        // Upgrade of an existing installation and of browser settings.
        "deploy/backup/backup.sh",
        "deploy/compose.yaml",
        "frontend/index.html",
        "frontend/src/shared/layout/useSidebar.ts",
        "frontend/src/shared/ui/colorMode.ts",
        "tools/deploy.sh",
        "tools/seed-pilot.sh",
        "tools/tests/backup-test.sh",
        "backend/tests/Proteone.Application.Tests/FormerProductNameTests.cs",
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
    }

    private static IEnumerable<string> Files() =>
        from searched in SearchedPaths
        let path = Path.Combine(RepositoryPaths.Root, searched)
        from file in File.Exists(path) ? [path] : Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
        let relative = Path.GetRelativePath(RepositoryPaths.Root, file).Replace(Path.DirectorySeparatorChar, '/')
        where !relative.Split('/').Any(SkippedFolders.Contains)
        select relative;
}
