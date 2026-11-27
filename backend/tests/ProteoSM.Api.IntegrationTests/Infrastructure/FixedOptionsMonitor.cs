using Microsoft.Extensions.Options;

namespace ProteoSM.Api.IntegrationTests.Infrastructure;

/// <summary>An <see cref="IOptionsMonitor{TOptions}"/> that always returns one value, for code built by hand in a test.</summary>
public sealed class FixedOptionsMonitor<TOptions>(TOptions value) : IOptionsMonitor<TOptions>
{
    public TOptions CurrentValue => value;

    public TOptions Get(string? name) => value;

    public IDisposable? OnChange(Action<TOptions, string?> listener) => null;
}
