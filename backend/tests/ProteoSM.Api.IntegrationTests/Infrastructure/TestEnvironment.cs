namespace ProteoSM.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Test settings from environment variables: <c>PROTEOSM_&lt;name&gt;</c>, or <c>PROTEONE_&lt;name&gt;</c> (the name before the
/// ProteoSM rename) while the new variable is unset.
/// </summary>
public static class TestEnvironment
{
    public const string Prefix = "PROTEOSM_";
    public const string FormerPrefix = "PROTEONE_";

    public static string? Get(string name) =>
        Environment.GetEnvironmentVariable(Prefix + name) ?? Environment.GetEnvironmentVariable(FormerPrefix + name);
}
