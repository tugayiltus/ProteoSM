using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProteoSM.Application.Abstractions;
using ProteoSM.Application.Features.Releases;
using ProteoSM.Domain.Changes;
using ProteoSM.Domain.Releases;

namespace ProteoSM.Application.Features.Pilot;

/// <summary>One planned and one running release over the first open changes of the pilot seed (ADR-0040); no random draw.</summary>
internal sealed partial class PilotSeedSession
{
    private const int ChangesPerRelease = 2;
    private const int PlannedReleaseDays = 21;
    private const string RunningReleaseTitle = "Altyapı sürümü";
    private const string PlannedReleaseTitle = "Uygulama sürümü";
    private const string ReleaseDescription = "Birlikte yayına alınan değişiklikler.";

    private async Task SeedReleasesAsync()
    {
        List<string> open;
        await using (var scope = scopes.CreateAsyncScope())
        {
            open = await scope.ServiceProvider.GetRequiredService<IAppDbContext>().ChangeRequests
                .Where(c => c.Status != ChangeStatus.Closed && c.Status != ChangeStatus.Cancelled)
                .OrderBy(c => c.Number).Select(c => c.Number).Take(ChangesPerRelease * 2).ToListAsync(cancellationToken);
        }

        var running = await SeedReleaseAsync(RunningReleaseTitle, realNow, open.Take(ChangesPerRelease));
        await ActAsync<StartReleaseHandler, ReleaseDetail>(
            _adminId, Cap, (h, ct) => h.HandleAsync(new StartReleaseCommand(running.Number, running.Version), ct));
        await SeedReleaseAsync(PlannedReleaseTitle, realNow.AddDays(PlannedReleaseDays), open.Skip(ChangesPerRelease));
    }

    private async Task<ReleaseDetail> SeedReleaseAsync(string title, DateTimeOffset start, IEnumerable<string> changes)
    {
        var draft = new ReleaseDraft(title, ReleaseDescription, _intakeTeamId, null, start, start.AddDays(1), null);
        var release = await ActAsync<CreateReleaseHandler, ReleaseDetail>(_adminId, Cap, (h, ct) => h.HandleAsync(new CreateReleaseCommand(draft), ct));
        foreach (var change in changes)
        {
            release = await ActAsync<AddReleaseChangeHandler, ReleaseDetail>(
                _adminId, Cap, (h, ct) => h.HandleAsync(new AddReleaseChangeCommand(release.Number, change), ct));
        }

        return release;
    }
}
