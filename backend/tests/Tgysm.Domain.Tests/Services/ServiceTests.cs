using Tgysm.Domain.Assets;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Common;
using Tgysm.Domain.Problems;
using Tgysm.Domain.Services;
using Tgysm.Domain.Tickets;

namespace Tgysm.Domain.Tests.Services;

public sealed class ServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Now.AddHours(3);
    private static readonly Guid Team = Guid.NewGuid();
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly Guid Store = Guid.NewGuid();
    private static readonly Guid ServiceId = Guid.NewGuid();
    private static readonly AssetCatalogEntry Laptop = new(Guid.NewGuid(), AssetType.Laptop, "Lenovo", "T14");

    private static Asset NewAsset() => Asset.Create("DT-0001", Laptop, null, Store, null, null, null, null, Now).Asset;

    [Fact]
    public void Create_trims_and_normalizes_the_name_and_starts_active()
    {
        var service = Service.Create("  SAP Portal  ", "  Finans  ", Team, Now);

        Assert.Equal("SAP Portal", service.Name);
        Assert.Equal("sap portal", service.NormalizedName);
        Assert.Equal("Finans", service.Description);
        Assert.Equal(Team, service.OwnerTeamId);
        Assert.True(service.IsActive);
        Assert.Contains("finans", service.SearchText);
        Assert.Contains("sap", service.SearchText);
        Assert.Equal((Now, Now), (service.CreatedAt, service.UpdatedAt));
    }

    [Fact]
    public void Names_that_differ_only_by_case_or_dotless_i_share_a_key() =>
        Assert.Equal(Service.Create("IT Portal", null, Team, Now).NormalizedName, Service.Create("ıt portal", null, Team, Now).NormalizedName);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_a_blank_name(string name) =>
        Assert.Throws<ArgumentException>(() => Service.Create(name, null, Team, Now));

    [Fact]
    public void Create_rejects_a_name_over_the_limit_and_accepts_the_limit()
    {
        Assert.Throws<ArgumentException>(() => Service.Create(new string('a', ServiceLimits.NameMaxLength + 1), null, Team, Now));
        Assert.Equal(ServiceLimits.NameMaxLength, Service.Create(new string('a', ServiceLimits.NameMaxLength), null, Team, Now).Name.Length);
    }

    [Fact]
    public void Create_rejects_a_description_over_the_limit() =>
        Assert.Throws<ArgumentException>(() => Service.Create("SAP", new string('a', ServiceLimits.DescriptionMaxLength + 1), Team, Now));

    [Fact]
    public void Blank_description_is_stored_as_null() =>
        Assert.Null(Service.Create("SAP", "  ", Team, Now).Description);

    [Fact]
    public void Update_changes_the_card_and_touches_updated_at()
    {
        var service = Service.Create("SAP", null, Team, Now);
        var otherTeam = Guid.NewGuid();

        service.Update(" SAP ERP ", "Muhasebe", otherTeam, Later);

        Assert.Equal(("SAP ERP", "Muhasebe", otherTeam, Later), (service.Name, service.Description, service.OwnerTeamId, service.UpdatedAt));
        Assert.Equal(Now, service.CreatedAt);
    }

    [Fact]
    public void SetActive_toggles_the_flag()
    {
        var service = Service.Create("SAP", null, Team, Now);

        service.SetActive(false, Later);
        Assert.False(service.IsActive);
        Assert.Equal(Later, service.UpdatedAt);

        service.SetActive(true, Later);
        Assert.True(service.IsActive);
    }

    [Fact]
    public void Linking_a_retired_asset_throws()
    {
        var asset = NewAsset();
        asset.Retire(null, Actor, "kırık", Now);

        Assert.Equal(ErrorCodes.ServiceAssetNotLinkable, Assert.Throws<DomainException>(() => ServiceAsset.Create(ServiceId, asset, Actor, Now)).Code);
    }

    [Fact]
    public void Linking_a_lost_asset_throws()
    {
        var asset = NewAsset();
        asset.MarkLost(null, Actor, "taksi", Now);

        Assert.Equal(ErrorCodes.ServiceAssetNotLinkable, Assert.Throws<DomainException>(() => ServiceAsset.Create(ServiceId, asset, Actor, Now)).Code);
    }

    [Fact]
    public void Linking_an_in_stock_asset_works()
    {
        var asset = NewAsset();

        var link = ServiceAsset.Create(ServiceId, asset, Actor, Now);

        Assert.Equal((ServiceId, asset.Id, Actor, Now), (link.ServiceId, link.AssetId, link.CreatedById, link.CreatedAt));
    }

    [Fact]
    public void Linking_an_assigned_asset_works()
    {
        var asset = NewAsset();
        asset.Assign(Guid.NewGuid(), Actor, null, null, Now);

        Assert.Equal(AssetStatus.Assigned, asset.Status);
        Assert.Equal(asset.Id, ServiceAsset.Create(ServiceId, asset, Actor, Now).AssetId);
    }

    [Fact]
    public void Linking_an_asset_in_maintenance_works()
    {
        var asset = NewAsset();
        asset.StartMaintenance(null, false, Actor, Now);

        Assert.Equal(AssetStatus.Maintenance, asset.Status);
        Assert.Equal(asset.Id, ServiceAsset.Create(ServiceId, asset, Actor, Now).AssetId);
    }

    [Fact]
    public void Ticket_SetService_on_a_completed_ticket_changes_only_the_service()
    {
        var ticket = Ticket.CreateIncident("INC-2026-000001", "Yazıcı", "Sıkışma", null, null, Actor, Team, Now);
        ticket.Assign(Team, Actor, Now);
        ticket.Start(Now);
        ticket.Complete("Toner", TicketOutcome.Resolved, hasRelatedRequest: false, isApproved: false, Now.AddHours(1));
        var completedAt = ticket.CompletedAt;

        Assert.True(ticket.SetService(ServiceId, Later));

        Assert.Equal(ServiceId, ticket.ServiceId);
        Assert.Equal(TicketStatus.Completed, ticket.Status);
        Assert.Equal(completedAt, ticket.CompletedAt);
        Assert.Equal(Later, ticket.UpdatedAt);
        Assert.False(ticket.SetService(ServiceId, Later.AddHours(1)));
        Assert.Equal(Later, ticket.UpdatedAt);
    }

    [Fact]
    public void Ticket_SetService_null_clears_and_reports_a_change()
    {
        var ticket = Ticket.CreateIncident("INC-2026-000001", "Yazıcı", "Sıkışma", null, null, Actor, Team, Now);
        ticket.SetService(ServiceId, Now);

        Assert.True(ticket.SetService(null, Later));
        Assert.Null(ticket.ServiceId);
        Assert.False(ticket.SetService(null, Later));
    }

    [Fact]
    public void Change_SetService_keeps_the_plan_fingerprint_on_a_pending_change()
    {
        var change = ChangeRequest.Create(
            "CHG-2026-000001",
            new ChangeDraft("Güncelle", "Gerekçe", null, Level.Medium, Level.Low, Now.AddDays(1), Now.AddDays(1).AddHours(2), "Uygula", "Doğrula", "Geri al"),
            Team, Actor, Actor, Now);
        change.Submit(1, Now);
        Assert.Equal(ChangeStatus.PendingApproval, change.Status);
        var assets = new[] { Guid.NewGuid() };
        var before = ChangePlanFingerprint.Compute(change, assets, []);

        Assert.True(change.SetService(ServiceId, Later));

        Assert.Equal(ServiceId, change.ServiceId);
        Assert.Equal(ChangeStatus.PendingApproval, change.Status);
        Assert.Equal(before, ChangePlanFingerprint.Compute(change, assets, []));
        Assert.False(change.SetService(ServiceId, Later));
    }

    [Fact]
    public void Problem_SetService_on_a_resolved_problem_works()
    {
        var problem = Problem.Create("PRB-2026-000001", "Wi-Fi", "Kopuyor", Level.Medium, Team, null, Actor, Now);
        problem.Resolve("Firmware", Now);
        Assert.True(problem.IsClosed);

        Assert.True(problem.SetService(ServiceId, Later));

        Assert.Equal(ServiceId, problem.ServiceId);
        Assert.Equal(ProblemStatus.Resolved, problem.Status);
        Assert.Equal(Later, problem.UpdatedAt);
        Assert.False(problem.SetService(ServiceId, Later.AddHours(1)));
    }
}
