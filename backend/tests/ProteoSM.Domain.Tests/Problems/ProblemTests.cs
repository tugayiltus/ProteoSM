using ProteoSM.Domain.Common;
using ProteoSM.Domain.Problems;
using ProteoSM.Domain.Tickets;

namespace ProteoSM.Domain.Tests.Problems;

public sealed class ProblemTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Now.AddHours(2);
    private const string RootCause = "Faulty switch firmware";
    private const string Workaround = "Reboot the switch";

    private static Problem NewProblem(Guid? ownerId = null) =>
        Problem.Create("PRB-2026-000001", "  Wi-Fi drops  ", "Users lose the connection", Level.Medium, Guid.NewGuid(), ownerId, Guid.NewGuid(), Now);

    private static Problem Investigating()
    {
        var problem = NewProblem();
        problem.Start(Now);
        return problem;
    }

    private static Problem KnownError()
    {
        var problem = Investigating();
        problem.Edit(problem.Title, problem.Description, problem.Impact, RootCause, Workaround, Now);
        problem.MarkKnownError(Later);
        return problem;
    }

    private static void AssertCode(string code, Action action) =>
        Assert.Equal(code, Assert.Throws<DomainException>(action).Code);

    [Fact]
    public void Create_starts_open_with_trimmed_title_and_search_key()
    {
        var problem = NewProblem();

        Assert.Equal(ProblemStatus.Open, problem.Status);
        Assert.Equal("Wi-Fi drops", problem.Title);
        Assert.False(string.IsNullOrEmpty(problem.SearchText));
        Assert.Equal(Now, problem.CreatedAt);
        Assert.Equal(Now, problem.UpdatedAt);
        Assert.False(problem.IsClosed);
        Assert.Null(problem.ClosedAt);
    }

    [Fact]
    public void Start_moves_open_to_investigating()
    {
        var problem = NewProblem();

        problem.Start(Later);

        Assert.Equal(ProblemStatus.Investigating, problem.Status);
        Assert.Equal(Later, problem.UpdatedAt);
    }

    [Fact]
    public void Start_from_investigating_is_invalid() =>
        AssertCode(ErrorCodes.ProblemInvalidTransition, () => Investigating().Start(Later));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MarkKnownError_sets_status_and_timestamp_from_open_or_investigating(bool start)
    {
        var problem = NewProblem();
        if (start)
        {
            problem.Start(Now);
        }

        problem.Edit(problem.Title, problem.Description, problem.Impact, RootCause, Workaround, Now);
        problem.MarkKnownError(Later);

        Assert.Equal(ProblemStatus.KnownError, problem.Status);
        Assert.Equal(Later, problem.KnownErrorAt);
        Assert.False(problem.IsClosed);
    }

    [Theory]
    [InlineData(null, Workaround)]
    [InlineData("  ", Workaround)]
    [InlineData(RootCause, null)]
    [InlineData(RootCause, "")]
    [InlineData(null, null)]
    public void MarkKnownError_needs_root_cause_and_workaround(string? rootCause, string? workaround)
    {
        var problem = Investigating();
        problem.Edit(problem.Title, problem.Description, problem.Impact, rootCause, workaround, Now);

        AssertCode(ErrorCodes.ProblemKnownErrorIncomplete, () => problem.MarkKnownError(Later));
        Assert.Equal(ProblemStatus.Investigating, problem.Status);
        Assert.Null(problem.KnownErrorAt);
    }

    [Fact]
    public void MarkKnownError_from_known_error_is_invalid() =>
        AssertCode(ErrorCodes.ProblemInvalidTransition, () => KnownError().MarkKnownError(Later));

    [Fact]
    public void Edit_cannot_clear_root_cause_of_a_known_error() =>
        AssertCode(ErrorCodes.ProblemKnownErrorIncomplete, () =>
        {
            var problem = KnownError();
            problem.Edit(problem.Title, problem.Description, problem.Impact, null, problem.Workaround, Later);
        });

    [Theory]
    [InlineData(ProblemStatus.Open)]
    [InlineData(ProblemStatus.Investigating)]
    [InlineData(ProblemStatus.KnownError)]
    public void Resolve_closes_from_any_open_state(ProblemStatus from)
    {
        var problem = from switch
        {
            ProblemStatus.Open => NewProblem(),
            ProblemStatus.Investigating => Investigating(),
            _ => KnownError(),
        };

        problem.Resolve("  Firmware updated  ", Later);

        Assert.Equal(ProblemStatus.Resolved, problem.Status);
        Assert.Equal("Firmware updated", problem.Resolution);
        Assert.Equal(Later, problem.ResolvedAt);
        Assert.Equal(Later, problem.ClosedAt);
        Assert.True(problem.IsClosed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_needs_a_resolution(string resolution) =>
        AssertCode(ErrorCodes.ProblemResolutionRequired, () => NewProblem().Resolve(resolution, Later));

    [Theory]
    [InlineData(ProblemStatus.Open)]
    [InlineData(ProblemStatus.Investigating)]
    [InlineData(ProblemStatus.KnownError)]
    public void Cancel_closes_from_any_open_state(ProblemStatus from)
    {
        var problem = from switch
        {
            ProblemStatus.Open => NewProblem(),
            ProblemStatus.Investigating => Investigating(),
            _ => KnownError(),
        };

        problem.Cancel(" duplicate ", Later);

        Assert.Equal(ProblemStatus.Cancelled, problem.Status);
        Assert.Equal("duplicate", problem.CancelReason);
        Assert.Null(problem.ResolvedAt);
        Assert.Equal(Later, problem.ClosedAt);
        Assert.True(problem.IsClosed);
    }

    [Fact]
    public void Cancel_needs_a_reason() =>
        AssertCode(ErrorCodes.ProblemCancelReasonRequired, () => NewProblem().Cancel(" ", Later));

    public static TheoryData<Action<Problem>> MovesFromClosed() => new()
    {
        p => p.Start(Later),
        p => p.MarkKnownError(Later),
        p => p.Resolve("again", Later),
        p => p.Cancel("again", Later),
    };

    [Theory]
    [MemberData(nameof(MovesFromClosed))]
    public void Closed_problems_cannot_move_again(Action<Problem> move)
    {
        var resolved = KnownError();
        resolved.Resolve("done", Later);
        var cancelled = NewProblem();
        cancelled.Cancel("no", Later);

        AssertCode(ErrorCodes.ProblemInvalidTransition, () => move(resolved));
        AssertCode(ErrorCodes.ProblemInvalidTransition, () => move(cancelled));
    }

    public static TheoryData<Action<Problem>> EditsOfClosed() => new()
    {
        p => p.Edit("t", "d", Level.Low, null, null, Later),
        p => p.Transfer(Guid.NewGuid(), Later),
        p => p.SetOwner(Guid.NewGuid(), Later),
    };

    [Theory]
    [MemberData(nameof(EditsOfClosed))]
    public void Edits_of_a_closed_problem_are_rejected(Action<Problem> edit)
    {
        var problem = NewProblem();
        problem.Cancel("no", Now);

        AssertCode(ErrorCodes.ProblemClosed, () => edit(problem));
    }

    [Fact]
    public void Edit_changes_fields_and_search_key()
    {
        var problem = NewProblem();
        var before = problem.SearchText;

        problem.Edit(" New title ", " New text ", Level.High, " cause ", " fix ", Later);

        Assert.Equal("New title", problem.Title);
        Assert.Equal("New text", problem.Description);
        Assert.Equal(Level.High, problem.Impact);
        Assert.Equal("cause", problem.RootCause);
        Assert.Equal("fix", problem.Workaround);
        Assert.NotEqual(before, problem.SearchText);
        Assert.Equal(Later, problem.UpdatedAt);
    }

    [Fact]
    public void Transfer_clears_the_owner()
    {
        var problem = NewProblem(Guid.NewGuid());
        var team = Guid.NewGuid();

        problem.Transfer(team, Later);

        Assert.Equal(team, problem.OwnerTeamId);
        Assert.Null(problem.OwnerId);
    }

    [Fact]
    public void SetOwner_sets_and_clears_the_owner()
    {
        var problem = NewProblem();
        var owner = Guid.NewGuid();

        problem.SetOwner(owner, Later);
        Assert.Equal(owner, problem.OwnerId);

        problem.SetOwner(null, Later);
        Assert.Null(problem.OwnerId);
    }

    [Fact]
    public void AttachArticle_sets_the_article_once()
    {
        var problem = KnownError();
        var article = Guid.NewGuid();

        problem.AttachArticle(article, Later);

        Assert.Equal(article, problem.ArticleId);
        AssertCode(ErrorCodes.ProblemArticleNotAllowed, () => problem.AttachArticle(Guid.NewGuid(), Later));
    }

    [Fact]
    public void AttachArticle_is_allowed_on_a_resolved_problem()
    {
        var problem = KnownError();
        problem.Resolve("done", Later);

        problem.AttachArticle(Guid.NewGuid(), Later);

        Assert.NotNull(problem.ArticleId);
    }

    [Fact]
    public void AttachArticle_is_rejected_on_a_cancelled_problem()
    {
        var problem = NewProblem();
        problem.Cancel("no", Later);

        AssertCode(ErrorCodes.ProblemArticleNotAllowed, () => problem.AttachArticle(Guid.NewGuid(), Later));
    }

    [Fact]
    public void AttachArticle_needs_a_known_error() =>
        AssertCode(ErrorCodes.ProblemArticleNotAllowed, () => Investigating().AttachArticle(Guid.NewGuid(), Later));

    [Fact]
    public void Ticket_link_for_an_incident_keeps_its_fields()
    {
        var problemId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var by = Guid.NewGuid();

        var link = ProblemTicketLink.Create(problemId, ticketId, TicketKind.Incident, by, Now);

        Assert.Equal(problemId, link.ProblemId);
        Assert.Equal(ticketId, link.TicketId);
        Assert.Equal(by, link.CreatedById);
        Assert.Equal(Now, link.CreatedAt);
    }

    [Fact]
    public void Ticket_link_for_a_request_is_rejected() =>
        AssertCode(ErrorCodes.ProblemTicketNotIncident, () => ProblemTicketLink.Create(Guid.NewGuid(), Guid.NewGuid(), TicketKind.Request, Guid.NewGuid(), Now));

    [Fact]
    public void Change_link_keeps_its_fields()
    {
        var problemId = Guid.NewGuid();
        var changeId = Guid.NewGuid();
        var by = Guid.NewGuid();

        var link = ProblemChangeLink.Create(problemId, changeId, by, Now);

        Assert.Equal(problemId, link.ProblemId);
        Assert.Equal(changeId, link.ChangeId);
        Assert.Equal(by, link.CreatedById);
        Assert.Equal(Now, link.CreatedAt);
    }

    [Fact]
    public void Number_formats_and_validates()
    {
        Assert.Equal("PRB-2026-000042", ProblemNumber.Format(2026, 42));
        Assert.True(ProblemNumber.IsValid("PRB-2026-000042"));
        Assert.False(ProblemNumber.IsValid("TSK-2026-000042"));
        Assert.Equal("PRB-2026-000042", ProblemNumber.Normalize(" prb-2026-000042 "));
    }
}
