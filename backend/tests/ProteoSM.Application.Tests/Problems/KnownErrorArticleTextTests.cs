using ProteoSM.Application.Features.Problems;
using ProteoSM.Domain.Knowledge;
using ProteoSM.Domain.Problems;
using ProteoSM.Domain.Tickets;

namespace ProteoSM.Application.Tests.Problems;

public sealed class KnownErrorArticleTextTests
{
    private const string Number = "PRB-2026-000007";
    private const string Symptom = "Kullanıcıların VPN bağlantısı kopuyor";
    private const string RootCause = "Güvenlik duvarı oturumu erken kapatıyor";
    private const string Workaround = "VPN istemcisini yeniden başlatın\nSonra tekrar bağlanın";
    private const string Resolution = "Güvenlik duvarı oturum süresi artırıldı";
    private const string KnownErrorTitle = "Bilinen hata: VPN kopuyor";
    private const string FixHeading = "## Kalıcı çözüm";
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

    private static Problem KnownError(string title = "VPN kopuyor")
    {
        var problem = Problem.Create(Number, title, Symptom, Level.Medium, Guid.NewGuid(), null, Guid.NewGuid(), Now);
        problem.Edit(title, Symptom, Level.Medium, RootCause, Workaround, Now);
        problem.MarkKnownError(Now);
        return problem;
    }

    [Fact]
    public void Build_gives_the_known_error_title_and_the_first_workaround_line_as_summary()
    {
        var (title, summary, _) = KnownErrorArticleText.Build(KnownError(), Number);

        Assert.Equal(KnownErrorTitle, title);
        Assert.Equal("VPN istemcisini yeniden başlatın", summary);
    }

    [Fact]
    public void Build_has_the_three_sections_and_the_problem_number_on_the_last_line_before_resolve()
    {
        var (_, _, body) = KnownErrorArticleText.Build(KnownError(), Number);

        Assert.Contains("## Belirti", body);
        Assert.Contains("## Kök neden", body);
        Assert.Contains("## Geçici çözüm", body);
        Assert.DoesNotContain(FixHeading, body);
        Assert.Contains(Symptom, body);
        Assert.Contains(RootCause, body);
        Assert.Equal($"Problem: {Number}", body.Split('\n')[^1]);
    }

    [Fact]
    public void Build_adds_the_permanent_fix_section_once_resolved()
    {
        var problem = KnownError();
        problem.Resolve(Resolution, Now);

        var (_, _, body) = KnownErrorArticleText.Build(problem, Number);

        Assert.Contains(FixHeading, body);
        Assert.Contains(Resolution, body);
        Assert.Equal($"Problem: {Number}", body.Split('\n')[^1]);
    }

    [Fact]
    public void Build_keeps_a_long_title_and_a_long_workaround_line_within_the_article_limits()
    {
        var longTitle = new string('a', Problem.TitleMaxLength);
        var problem = KnownError(longTitle);
        problem.Edit(longTitle, Symptom, Level.Medium, RootCause, new string('w', Problem.TextMaxLength), Now);

        var (title, summary, body) = KnownErrorArticleText.Build(problem, Number);

        Assert.Equal(ArticleLimits.TitleMaxLength, title.Length);
        Assert.StartsWith("Bilinen hata: aaa", title);
        Assert.Equal(ArticleLimits.SummaryMaxLength, summary.Length);
        Assert.True(body.Length <= ArticleLimits.BodyMaxLength);
    }
}
