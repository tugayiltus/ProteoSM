using System.Text;
using Tgysm.Domain.Knowledge;
using Tgysm.Domain.Problems;

namespace Tgysm.Application.Features.Problems;

/// <summary>
/// The generated known-error article. The Turkish headings and title prefix are product content (like the email
/// template defaults), so they live here and not in the locale files.
/// </summary>
public static class KnownErrorArticleText
{
    public const string TitlePrefix = "Bilinen hata: ";
    public const string SymptomHeading = "## Belirti";
    public const string RootCauseHeading = "## Kök neden";
    public const string WorkaroundHeading = "## Geçici çözüm";
    public const string ResolutionHeading = "## Kalıcı çözüm";
    public const string ProblemLinePrefix = "Problem: ";

    private const char LineBreak = '\n';

    public static (string Title, string Summary, string Body) Build(Problem problem, string number)
    {
        var title = Truncate(TitlePrefix + problem.Title, ArticleLimits.TitleMaxLength);
        var summary = Truncate(FirstLine(problem.Workaround), ArticleLimits.SummaryMaxLength);

        var body = new StringBuilder();
        Section(body, SymptomHeading, problem.Description);
        Section(body, RootCauseHeading, problem.RootCause);
        Section(body, WorkaroundHeading, problem.Workaround);
        if (problem.Status == ProblemStatus.Resolved)
        {
            Section(body, ResolutionHeading, problem.Resolution);
        }

        body.Append(ProblemLinePrefix).Append(number);
        return (title, summary, body.ToString());
    }

    private static void Section(StringBuilder body, string heading, string? text) =>
        body.Append(heading).Append(LineBreak).Append(LineBreak).Append(text?.Trim()).Append(LineBreak).Append(LineBreak);

    private static string FirstLine(string? text) =>
        (text ?? string.Empty).Split(LineBreak, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? string.Empty;

    private static string Truncate(string text, int maxLength) => text.Length <= maxLength ? text : text[..maxLength];
}
