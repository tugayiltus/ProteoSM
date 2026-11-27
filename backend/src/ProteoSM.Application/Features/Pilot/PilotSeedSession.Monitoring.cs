using ProteoSM.Application.Features.Monitoring;
using ProteoSM.Domain.Monitoring;
using ProteoSM.Domain.Tickets;

namespace ProteoSM.Application.Features.Pilot;

/// <summary>One monitoring source (ADR-0041). Its secret is dropped: replace it in /admin to send events to a pilot.</summary>
internal sealed partial class PilotSeedSession
{
    private const string MonitoringSourceName = "Sunucu izleme";

    private Task SeedMonitoringAsync()
    {
        var draft = new MonitoringSourceDraft(MonitoringSourceName, _intakeTeamId, _intakeMembers[0], null, null, Level.High, Level.Medium, IsActive: true);
        return SetupAsync<CreateMonitoringSourceHandler, MonitoringSourceSecret>(_adminId, (h, ct) => h.HandleAsync(new CreateMonitoringSourceCommand(draft), ct));
    }
}
