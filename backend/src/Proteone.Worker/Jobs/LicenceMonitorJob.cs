using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Proteone.Application.Features.Licences;

namespace Proteone.Worker.Jobs;

/// <summary>Runs <see cref="NotifyExpiringLicencesHandler"/> on an interval; a licence is announced once per end date (ADR-0039).</summary>
public sealed class LicenceMonitorJob(
    IServiceScopeFactory scopes,
    IOptionsMonitor<WorkerOptions> options,
    TimeProvider clock,
    ILogger<LicenceMonitorJob> logger)
    : PeriodicJob(scopes, options, clock, logger)
{
    public const string JobName = "LicenceMonitor";

    protected override string Name => JobName;

    protected override Task RunOnceAsync(IServiceProvider scoped, CancellationToken cancellationToken) =>
        scoped.GetRequiredService<NotifyExpiringLicencesHandler>().HandleAsync(cancellationToken);
}
