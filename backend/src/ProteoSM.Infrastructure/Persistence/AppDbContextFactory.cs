using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ProteoSM.Infrastructure.Persistence;

/// <summary>Used only by dotnet-ef (migrations add / database update).</summary>
internal sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public const string ConnectionEnvironmentVariable = "PROTEOSM_CONNECTION_STRING";

    /// <summary>The name before the ProteoSM rename; read while <see cref="ConnectionEnvironmentVariable"/> is unset.</summary>
    public const string FormerConnectionEnvironmentVariable = "PROTEONE_CONNECTION_STRING";

    // "migrations add" never connects; this value only satisfies the provider.
    private const string DesignTimeFallbackConnection = "Host=localhost;Database=proteosm_design;Username=proteosm";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable)
            ?? Environment.GetEnvironmentVariable(FormerConnectionEnvironmentVariable)
            ?? DesignTimeFallbackConnection;
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(options);
    }
}
