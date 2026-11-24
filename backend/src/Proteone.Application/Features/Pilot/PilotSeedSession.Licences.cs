using Proteone.Application.Features.Licences;
using Proteone.Domain.Licences;

namespace Proteone.Application.Features.Pilot;

/// <summary>One seeded licence; <paramref name="EndsInDays"/> null means no end date.</summary>
internal sealed record PilotLicence(string Vendor, string Software, string Name, LicenceKind Kind, int Quantity, int Seats, int? EndsInDays);

/// <summary>
/// Software and licences of the pilot seed (ADR-0039): per user and per device, full, ending soon and without an
/// end. Seats go to the first employees and devices in creation order, so no random draw of the other parts moves.
/// </summary>
internal sealed partial class PilotSeedSession
{
    // Fictional products, like every other name in the pilot data.
    private static readonly PilotLicence[] PilotLicences =
    [
        new("Örnek Yazılım", "Ofis Paketi", "Kurumsal abonelik", LicenceKind.PerUser, 100, 8, 200),
        new("Örnek Yazılım", "Çizim", "Tasarım ekibi", LicenceKind.PerUser, 3, 3, 25),
        new("Örnek Güvenlik", "Uç Nokta Koruması", "Yıllık paket", LicenceKind.PerDevice, 50, 6, 75),
        new("Örnek Güvenlik", "Yedekleme", "Sunucu lisansı", LicenceKind.PerDevice, 2, 2, null),
    ];

    private async Task SeedLicencesAsync()
    {
        var today = DateOnly.FromDateTime(realNow.UtcDateTime);
        var devices = await LinkableAssetsAsync();
        foreach (var plan in PilotLicences)
        {
            var software = await SetupAsync<CreateSoftwareHandler, SoftwareItem>(
                _adminId, (h, ct) => h.HandleAsync(new CreateSoftwareCommand(plan.Software, plan.Vendor), ct));
            var command = new CreateLicenceCommand(
                software.Id, plan.Name, plan.Kind, plan.Quantity, _intakeTeamId, plan.EndsInDays is { } days ? today.AddDays(days) : null, null);
            var licence = await SetupAsync<CreateLicenceHandler, LicenceDetail>(_adminId, (h, ct) => h.HandleAsync(command, ct));
            var perUser = plan.Kind == LicenceKind.PerUser;
            var targets = perUser ? _employees.Select(e => e.Id) : devices;
            foreach (var target in targets.Take(plan.Seats).ToList())
            {
                var assign = new AssignLicenceCommand(licence.Id, perUser ? target : null, perUser ? null : target);
                await SetupAsync<AssignLicenceHandler, LicenceDetail>(_adminId, (h, ct) => h.HandleAsync(assign, ct));
            }
        }
    }
}
