using System.Text.RegularExpressions;

namespace ProteoSM.Application.Tests;

/// <summary>
/// The former product names stay only where stored data, applied migrations, compatibility or history need them: TGYSM
/// (ADR-0037) and the bare name Proteone, the product name before ProteoSM (case-sensitive word, so neither the umbrella
/// brand ProteONE nor ProteoSM matches), together with the PROTEONE_ prefix of environment variables.
/// </summary>
public sealed partial class FormerProductNameTests
{
    private const string Migrations = "backend/src/ProteoSM.Infrastructure/Persistence/Migrations/";

    private static readonly string[] SearchedPaths = ["backend/src", "backend/tests", "deploy", "frontend/src", "frontend/e2e", "frontend/guide", "frontend/index.html", "frontend/admin", "frontend/public", "docs", "tools", ".github", "Dockerfile", "CLAUDE.md", "README.md", "SECURITY.md", "capabilities.md", "proteosm.md", "frontend/CLAUDE.md"];

    private static readonly string[] SkippedFolders = ["bin", "obj", "node_modules", "certs"];

    /// <summary>Settings of this machine, not part of the repository (git-ignored).</summary>
    private const string LocalSettings = "deploy/.env";

    private static readonly string[] BinaryExtensions = [".png", ".pdf"];

    private static readonly string[] AllowedFiles =
    [
        // Data Protection name and purposes: stored secrets were protected with them.
        "backend/src/ProteoSM.Domain/Notifications/OutboxEvent.cs",
        "backend/src/ProteoSM.Infrastructure/Security/AuthSecretProtector.cs",
        "backend/src/ProteoSM.Infrastructure/Security/KeyStorage.cs",
        "backend/tests/ProteoSM.Api.IntegrationTests/Security/DataProtectionKeysTests.cs",
        // Applied migration, and the one that replaces the name in stored templates.
        Migrations + "20260928065946_EmailTemplates.cs",
        Migrations + "20261124115906_ProteoneBrand.cs",
        Migrations + "20261124135355_ProteoneHistory.cs",
        "backend/tests/ProteoSM.Api.IntegrationTests/Persistence/ProteoneBrandMigrationTests.cs",
        "backend/tests/ProteoSM.Application.Tests/FormerProductNameTests.cs",
        // Documents that explain the stored names.
        "SECURITY.md",
        "capabilities.md",
        "docs/adr/0004-local-identity-cookie-csrf.md",
        "docs/adr/0017-password-set-links.md",
        "docs/adr/0030-login-method-chosen-in-the-admin-panel.md",
        "docs/adr/0037-proteone-name-and-compatibility.md",
        "docs/adr/0043-no-upgrade-path-from-tgysm.md",
        "docs/adr/0047-proteosm-name-and-move.md",
        "docs/adr/README.md",
        "docs/superpowers/specs/2026-11-27-proteosm-brand-and-ux-scope.md",
    ];

    [GeneratedRegex("tgysm", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FormerName();

    /// <summary>Folders whose files may name Proteone: history (accepted decisions, plans, release evidence, applied migrations).</summary>
    private static readonly string[] ProteoneAllowedFolders =
    [
        "docs/adr/",
        "docs/superpowers/",
        "docs/release/",
        Migrations,
    ];

    private static readonly string[] ProteoneAllowedFiles =
    [
        // Kept literals: Data Protection purposes of stored secrets, and the monitoring webhook header contract.
        "backend/src/ProteoSM.Infrastructure/Security/EmailSecretProtector.cs",
        "backend/src/ProteoSM.Infrastructure/Security/LicenceKeyProtector.cs",
        "backend/src/ProteoSM.Infrastructure/Security/MonitoringSecretProtector.cs",
        "backend/tests/ProteoSM.Api.IntegrationTests/Security/DataProtectionKeysTests.cs",
        "backend/src/ProteoSM.Api/Endpoints/MonitoringEndpoints.cs",
        "backend/tests/ProteoSM.Api.IntegrationTests/Monitoring/MonitoringWebhookTests.cs",
        "frontend/e2e/modules.spec.ts",
        // The migrations that replaced TGYSM with Proteone in stored rows, and this guard.
        "backend/tests/ProteoSM.Api.IntegrationTests/Persistence/ProteoneBrandMigrationTests.cs",
        "backend/tests/ProteoSM.Api.IntegrationTests/Persistence/ProteoSMEmailDefaultsMigrationTests.cs",
        "backend/tests/ProteoSM.Application.Tests/FormerProductNameTests.cs",
        // Documents that explain the kept literals, and the memory path of this machine (`~/claude/Proteone`).
        "SECURITY.md",
        "capabilities.md",
        "CLAUDE.md",
        // Fallbacks that read a PROTEONE_ variable or key while the PROTEOSM_ one is missing.
        "tools/lib/product-env.sh",
        "tools/lib/env-file.sh",
        "tools/tests/env-file-test.sh",
        "tools/seed-pilot.sh",
        "deploy/compose.yaml",
        "backend/src/ProteoSM.Infrastructure/Persistence/AppDbContextFactory.cs",
        "backend/tests/ProteoSM.Api.IntegrationTests/Infrastructure/TestEnvironment.cs",
        // Restore of proteone- archives and the move of a Proteone stack: deploy guard, procedure, drill.
        "tools/deploy.sh",
        "tools/tests/deploy-guard-test.sh",
        "tools/restore.sh",
        "tools/proteosm-move-drill.sh",
        "tools/lib/proteosm_move_drill.py",
        "docs/operations.md",
    ];

    [GeneratedRegex(@"\bProteone\b|PROTEONE_", RegexOptions.CultureInvariant)]
    private static partial Regex ProteoneName();

    [Theory]
    [InlineData("Proteone", true)]
    [InlineData("a Proteone stack", true)]
    [InlineData("PROTEONE_SUBNET", true)]
    [InlineData("ProteONE", false)]
    [InlineData("ProteoSM", false)]
    [InlineData("ProteoneBrand", false)]
    [InlineData("proteone-api", false)]
    public void The_Proteone_pattern_matches_only_the_bare_former_name(string text, bool matches) =>
        Assert.Equal(matches, ProteoneName().IsMatch(text));

    [Fact]
    public void The_name_before_ProteoSM_appears_only_in_the_allowed_files()
    {
        var found = Files()
            .Where(file => ProteoneName().IsMatch(file) || ProteoneName().IsMatch(File.ReadAllText(Path.Combine(RepositoryPaths.Root, file))))
            .Order(StringComparer.Ordinal)
            .ToList();

        var unexpected = found.Where(file => !ProteoneAllowedFiles.Contains(file) && !ProteoneAllowedFolders.Any(folder => file.StartsWith(folder, StringComparison.Ordinal))).ToList();

        Assert.Empty(unexpected);
        // A listed file that no longer names Proteone leaves the list, so the list only shrinks.
        Assert.Empty(ProteoneAllowedFiles.Except(found));
    }

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
