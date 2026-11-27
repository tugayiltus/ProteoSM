using ProteoSM.Application.Features.Announcements;
using ProteoSM.Domain.Announcements;

namespace ProteoSM.Application.Features.Pilot;

/// <summary>One announcement without an end, so the home page shows it (ADR-0042).</summary>
internal sealed partial class PilotSeedSession
{
    private const string AnnouncementTitle = "VPN bakımı";
    private const string AnnouncementBody = "Cumartesi 20.00–22.00 arasında uzaktan bağlantı kesilebilir.";

    private Task SeedAnnouncementAsync()
    {
        var draft = new AnnouncementDraft(AnnouncementTitle, AnnouncementBody, AnnouncementLevel.Warning, _windowStart, EndsAt: null);
        return SetupAsync<CreateAnnouncementHandler, AnnouncementItem>(_adminId, (h, ct) => h.HandleAsync(new CreateAnnouncementCommand(draft), ct));
    }
}
