using ProteoSM.Domain.Common;
using ProteoSM.Domain.Tickets;

namespace ProteoSM.Domain.Tests;

public sealed class TicketRatingTests
{
    private static readonly DateTimeOffset Created = new(2026, 9, 26, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Completed = Created.AddHours(2);
    private static readonly Guid Requester = Guid.NewGuid();
    private static readonly Guid Team = Guid.NewGuid();
    private static readonly Guid Agent = Guid.NewGuid();
    private static readonly Guid RequestType = Guid.NewGuid();

    private static Ticket Incident() =>
        Ticket.CreateIncident("INC-2026-000001", "Yazıcı", "Kağıt sıkışıyor", null, null, Requester, Team, Created);

    private static Ticket ResolvedIncident()
    {
        var ticket = Incident();
        ticket.Complete("Sıkışma giderildi", TicketOutcome.Resolved, false, false, Completed);
        return ticket;
    }

    [Fact]
    public void Completed_resolved_ticket_is_rateable_until_fourteen_days()
    {
        var ticket = ResolvedIncident();

        Assert.True(TicketRating.IsRateable(ticket));
        Assert.Equal(Completed.AddDays(14), TicketRating.RateUntil(ticket));
    }

    [Fact]
    public void Completed_request_is_rateable()
    {
        var ticket = Ticket.CreateRequest("REQ-2026-000001", "Dizüstü", "Başlıyorum", null, null, Requester, Team, RequestType, false, Created);
        ticket.Complete("Teslim edildi", null, false, true, Completed);

        Assert.True(TicketRating.IsRateable(ticket));
    }

    [Fact]
    public void Cancelled_and_service_request_required_and_rejected_are_not_rateable()
    {
        var cancelled = Incident();
        cancelled.Cancel("Gerek kalmadı", Completed);

        var needsRequest = Incident();
        needsRequest.Complete("Talep açıldı", TicketOutcome.ServiceRequestRequired, true, false, Completed);

        var rejected = Ticket.CreateRequest("REQ-2026-000002", "Dizüstü", "Başlıyorum", null, null, Requester, Team, RequestType, true, Created);
        rejected.ApprovalRejected("Bütçe yok", Completed);

        foreach (var ticket in new[] { cancelled, needsRequest, rejected })
        {
            Assert.False(TicketRating.IsRateable(ticket));
            Assert.Null(TicketRating.RateUntil(ticket));
        }
    }

    [Fact]
    public void Open_ticket_is_not_rateable_and_create_throws_not_rateable()
    {
        var ticket = Incident();

        Assert.False(TicketRating.IsRateable(ticket));
        var ex = Assert.Throws<DomainException>(() => TicketRating.Create(ticket, Requester, 5, null, Completed));
        Assert.Equal(ErrorCodes.RatingNotRateable, ex.Code);
    }

    [Fact]
    public void Create_snapshots_team_and_assignee()
    {
        var ticket = Incident();
        ticket.Assign(Team, Agent, Created.AddMinutes(5), Agent);
        ticket.Complete("Bitti", TicketOutcome.Resolved, false, false, Completed);

        var rating = TicketRating.Create(ticket, Requester, 4, "İyi", Completed.AddHours(1));

        Assert.Equal(ticket.Id, rating.TicketId);
        Assert.Equal(ticket.TeamId, rating.TeamId);
        Assert.Equal(ticket.AssigneeId, rating.AssigneeId);
        Assert.Equal(Requester, rating.RatedBy);
        Assert.Equal(4, rating.Score);
        Assert.Equal("İyi", rating.Comment);
        Assert.Equal(Completed.AddHours(1), rating.RatedAt);
        Assert.Equal(rating.RatedAt, rating.UpdatedAt);
    }

    [Fact]
    public void Blank_comment_becomes_null()
    {
        var rating = TicketRating.Create(ResolvedIncident(), Requester, 3, "   ", Completed.AddHours(1));

        Assert.Null(rating.Comment);
    }

    [Fact]
    public void Create_at_window_end_throws_window_closed()
    {
        var ticket = ResolvedIncident();

        var ex = Assert.Throws<DomainException>(() => TicketRating.Create(ticket, Requester, 5, null, Completed + RatingLimits.Window));
        Assert.Equal(ErrorCodes.RatingWindowClosed, ex.Code);

        var rating = TicketRating.Create(ticket, Requester, 5, null, Completed + RatingLimits.Window - TimeSpan.FromSeconds(1));
        Assert.Equal(5, rating.Score);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Score_out_of_range_throws(int score)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TicketRating.Create(ResolvedIncident(), Requester, score, null, Completed.AddHours(1)));
    }

    [Fact]
    public void Update_changes_score_comment_and_updated_at_but_keeps_rated_at()
    {
        var ratedAt = Completed.AddHours(1);
        var rating = TicketRating.Create(ResolvedIncident(), Requester, 2, "Yavaş", ratedAt);

        rating.Update(5, "  Sonra düzeldi ", ratedAt.AddDays(1));

        Assert.Equal(5, rating.Score);
        Assert.Equal("Sonra düzeldi", rating.Comment);
        Assert.Equal(ratedAt, rating.RatedAt);
        Assert.Equal(ratedAt.AddDays(1), rating.UpdatedAt);
    }
}
