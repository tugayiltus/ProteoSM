using ProteoSM.Domain.Problems;
using ProteoSM.Domain.Tickets;

namespace ProteoSM.Domain.Notifications;

/// <summary>Turkish labels of a problem's status and impact, for the server-rendered <c>status</c> and <c>impact</c> email variables (same texts as the problem screens).</summary>
public static class ProblemLabels
{
    private static readonly IReadOnlyDictionary<ProblemStatus, string> Statuses = new Dictionary<ProblemStatus, string>
    {
        [ProblemStatus.Open] = "Açık",
        [ProblemStatus.Investigating] = "İnceleniyor",
        [ProblemStatus.KnownError] = "Bilinen hata",
        [ProblemStatus.Resolved] = "Çözüldü",
        [ProblemStatus.Cancelled] = "İptal edildi",
    };

    public static string Of(ProblemStatus status) => Statuses.TryGetValue(status, out var label) ? label : status.ToString();

    // Impact uses the same three levels and texts as a change's risk.
    public static string Of(Level impact) => ChangeLabels.Of(impact);
}
