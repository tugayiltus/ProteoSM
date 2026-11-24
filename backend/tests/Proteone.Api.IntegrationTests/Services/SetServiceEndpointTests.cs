using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Proteone.Api.IntegrationTests.Changes;
using Proteone.Api.IntegrationTests.Infrastructure;
using Proteone.Api.IntegrationTests.Problems;
using Proteone.Api.IntegrationTests.Tickets;
using Proteone.Domain.Audit;
using Proteone.Domain.Changes;
using Proteone.Domain.Common;
using Proteone.Domain.Problems;
using Proteone.Domain.Tickets;

namespace Proteone.Api.IntegrationTests.Services;

[Collection(ApiCollection.Name)]
public sealed class SetServiceEndpointTests(ApiFactory factory) : IAsyncLifetime
{
    private const string ServiceSegment = "service";
    private const string ServiceProperty = "service";
    private const string InactiveCode = "services.inactive";
    private const string OpenApiDocumentName = "v1";

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<Guid> ServiceAsync(TicketWorld world, string name, bool active = true)
    {
        using var created = await world.Admin.PostAsync(ServiceWorld.AdminServicesPath, new { name, ownerTeamId = world.ServiceDesk.Id });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var service = await world.Admin.ReadJsonAsync<JsonElement>(created);
        if (!active)
        {
            using var deactivated = await world.Admin.PostAsync(
                $"{ServiceWorld.AdminServicesPath}/{ServiceWorld.Id(service)}/deactivate", new { version = ServiceWorld.Version(service) });
            Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        }

        return ServiceWorld.Id(service);
    }

    private static Task<HttpResponseMessage> PutServiceAsync(ApiClient client, string recordPath, Guid? serviceId, uint version) =>
        client.PutAsync($"{recordPath}/{ServiceSegment}", new { serviceId, version });

    private static async Task<JsonElement> SetAsync(ApiClient client, string recordPath, Guid? serviceId, uint version)
    {
        using var response = await PutServiceAsync(client, recordPath, serviceId, version);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await client.ReadJsonAsync<JsonElement>(response);
    }

    private static Guid? ServiceIdOf(JsonElement detail) =>
        detail.TryGetProperty(ServiceProperty, out var service) && service.ValueKind == JsonValueKind.Object
            ? service.GetProperty("id").GetGuid()
            : null;

    private static async Task<JsonElement> CompletedIncidentAsync(TicketWorld world, ApiClient requester)
    {
        var incident = await TicketWorld.CreateIncidentAsync(requester);
        using var completed = await world.Desk.PostAsync(
            TicketWorld.PathOf(incident, "complete"), new { version = TicketWorld.VersionOf(incident), resolution = "Çözüldü", outcome = "resolved" });
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        return await world.Desk.ReadJsonAsync<JsonElement>(completed);
    }

    [Fact]
    public async Task Staff_outside_the_team_tags_a_completed_incident_once_without_touching_its_state()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var serviceId = await ServiceAsync(world, "E-posta");
        var completed = await CompletedIncidentAsync(world, world.Field);

        var tagged = await SetAsync(world.Field, TicketWorld.PathOf(completed), serviceId, TicketWorld.VersionOf(completed));
        var again = await SetAsync(world.Field, TicketWorld.PathOf(completed), serviceId, TicketWorld.VersionOf(tagged));

        var service = tagged.GetProperty(ServiceProperty);
        Assert.Equal((serviceId, "E-posta", true), (service.GetProperty("id").GetGuid(), service.GetProperty("name").GetString(), service.GetProperty("isActive").GetBoolean()));
        Assert.False(tagged.GetProperty("canWork").GetBoolean());
        Assert.Equal("completed", tagged.GetProperty("status").GetString());
        Assert.Equal(completed.GetProperty("completedAt").GetDateTimeOffset(), tagged.GetProperty("completedAt").GetDateTimeOffset());
        Assert.Equal(TicketWorld.VersionOf(tagged), TicketWorld.VersionOf(again));
        var ticketId = completed.GetProperty("id").GetGuid();
        await using var db = factory.Database.CreateDbContext();
        var serviceEvent = await db.TicketEvents.SingleAsync(e => e.TicketId == ticketId && e.Kind == TicketEventKind.ServiceSet, Ct);
        Assert.Equal(Visibility.Internal, serviceEvent.Visibility);
        Assert.Contains("E-posta", serviceEvent.DataJson, StringComparison.Ordinal);
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Action == AuditActions.TicketServiceSet && a.EntityId == ticketId, Ct));
        Assert.Equal(TicketStatus.Completed, await db.Tickets.Where(t => t.Id == ticketId).Select(t => t.Status).SingleAsync(Ct));
    }

    [Fact]
    public async Task Inactive_service_is_refused_clearing_writes_a_cleared_event_and_a_stale_version_conflicts()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var active = await ServiceAsync(world, "Ağ");
        var inactive = await ServiceAsync(world, "Eski santral", active: false);
        var incident = await TicketWorld.CreateIncidentAsync(world.Employee);
        var tagged = await SetAsync(world.Desk, TicketWorld.PathOf(incident), active, TicketWorld.VersionOf(incident));

        using var refused = await PutServiceAsync(world.Desk, TicketWorld.PathOf(incident), inactive, TicketWorld.VersionOf(tagged));
        using var stale = await PutServiceAsync(world.Desk, TicketWorld.PathOf(incident), null, TicketWorld.VersionOf(incident));
        var cleared = await SetAsync(world.Desk, TicketWorld.PathOf(incident), null, TicketWorld.VersionOf(tagged));

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal(InactiveCode, await ApiClient.ReadCodeAsync(refused));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, await ApiClient.ReadCodeAsync(stale));
        Assert.Null(ServiceIdOf(cleared));
        var ticketId = incident.GetProperty("id").GetGuid();
        await using var db = factory.Database.CreateDbContext();
        var kinds = await db.TicketEvents.Where(e => e.TicketId == ticketId && (e.Kind == TicketEventKind.ServiceSet || e.Kind == TicketEventKind.ServiceCleared))
            .OrderBy(e => e.OccurredAt).ThenBy(e => e.Id).Select(e => e.Kind).ToListAsync(Ct);
        Assert.Equal([TicketEventKind.ServiceSet, TicketEventKind.ServiceCleared], kinds);
        Assert.Equal(2, await db.AuditEvents.CountAsync(a => a.Action == AuditActions.TicketServiceSet && a.EntityId == ticketId, Ct));
    }

    [Fact]
    public async Task Change_pending_approval_keeps_its_status_and_approval_rows()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var serviceId = await ServiceAsync(world, "ERP");
        var change = await ChangeWorld.CreateAsync(world.Desk, world);
        await ChangeWorld.CreateTaskAsync(world.Desk, change, "implementation", world.ServiceDesk.Id, world.DeskUser.Id);
        var submitted = await ChangeWorld.ActAsync(world.Desk, await ChangeWorld.DetailAsync(world.Desk, change), "submit");
        var changeId = change.GetProperty("id").GetGuid();
        List<(Guid, ChangeApprovalStatus, string)> approvalsBefore;
        await using (var before = factory.Database.CreateDbContext())
        {
            approvalsBefore = [.. (await before.ChangeApprovals.Where(a => a.ChangeId == changeId).OrderBy(a => a.Id).ToListAsync(Ct))
                .Select(a => (a.Id, a.Status, a.Fingerprint))];
        }

        var tagged = await SetAsync(world.Desk, ChangeWorld.PathOf(submitted), serviceId, ChangeWorld.VersionOf(submitted));

        Assert.Equal(serviceId, ServiceIdOf(tagged));
        Assert.Equal("pending_approval", tagged.GetProperty("status").GetString());
        Assert.Equal(
            submitted.GetProperty("approval").GetProperty("version").GetUInt32(),
            tagged.GetProperty("approval").GetProperty("version").GetUInt32());
        await using var db = factory.Database.CreateDbContext();
        var approvalsAfter = (await db.ChangeApprovals.Where(a => a.ChangeId == changeId).OrderBy(a => a.Id).ToListAsync(Ct))
            .Select(a => (a.Id, a.Status, a.Fingerprint)).ToList();
        Assert.Equal(approvalsBefore, approvalsAfter);
        Assert.Single(approvalsAfter);
        Assert.Equal(1, await db.ChangeEvents.CountAsync(e => e.ChangeId == changeId && e.Kind == ChangeEventKind.ServiceSet, Ct));
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Action == AuditActions.ChangeServiceSet && a.EntityId == changeId, Ct));
    }

    [Fact]
    public async Task Resolved_problem_takes_a_service_from_the_owner_team_but_not_from_other_staff()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var serviceId = await ServiceAsync(world, "Yazdırma");
        var problem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id);
        using var resolve = await world.Desk.PostAsync(
            ProblemWorld.PathOf(problem, "resolve"), new { version = ProblemWorld.VersionOf(problem), resolution = "Sürücü güncellendi" });
        var resolved = await world.Desk.ReadJsonAsync<JsonElement>(resolve);

        using var foreign = await PutServiceAsync(world.Field, ProblemWorld.PathOf(resolved), serviceId, ProblemWorld.VersionOf(resolved));
        var tagged = await SetAsync(world.Desk, ProblemWorld.PathOf(resolved), serviceId, ProblemWorld.VersionOf(resolved));

        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        Assert.Equal(serviceId, ServiceIdOf(tagged));
        Assert.Equal("resolved", tagged.GetProperty("status").GetString());
        var problemId = problem.GetProperty("id").GetGuid();
        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(1, await db.ProblemEvents.CountAsync(e => e.ProblemId == problemId && e.Kind == ProblemEventKind.ServiceSet, Ct));
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Action == AuditActions.ProblemServiceSet && a.EntityId == problemId, Ct));
        Assert.Equal(ProblemStatus.Resolved, await db.Problems.Where(p => p.Id == problemId).Select(p => p.Status).SingleAsync(Ct));
    }

    [Fact]
    public async Task Employee_never_sees_the_service_and_cannot_set_it()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var serviceId = await ServiceAsync(world, "VPN");
        var incident = await TicketWorld.CreateIncidentAsync(world.Employee);
        await SetAsync(world.Desk, TicketWorld.PathOf(incident), serviceId, TicketWorld.VersionOf(incident));

        var own = await TicketWorld.ReadAsync(world.Employee, TicketWorld.PathOf(incident));
        using var put = await PutServiceAsync(world.Employee, TicketWorld.PathOf(incident), null, TicketWorld.VersionOf(own));
        using var filtered = await world.Employee.GetAsync($"{TicketWorld.TicketsPath}?serviceId={serviceId}");

        Assert.False(own.TryGetProperty(ServiceProperty, out _));
        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, filtered.StatusCode);
    }

    [Fact]
    public async Task Contract_marks_the_ticket_service_optional_because_a_null_service_is_left_out()
    {
        var provider = factory.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>(OpenApiDocumentName);

        var document = await provider.GetOpenApiDocumentAsync(Ct);

        var schemas = document.Components!.Schemas!;
        Assert.Contains(ServiceProperty, schemas["TicketDetail"].Properties!.Keys);
        Assert.DoesNotContain(ServiceProperty, schemas["TicketDetail"].Required ?? new HashSet<string>());
        Assert.Contains(ServiceProperty, schemas["ChangeDetail"].Required!);
        Assert.Contains(ServiceProperty, schemas["ProblemDetail"].Required!);
    }

    [Fact]
    public async Task Lists_filter_by_service()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var serviceId = await ServiceAsync(world, "Dosya sunucusu");
        var tagged = await TicketWorld.CreateIncidentAsync(world.Employee, "Paylaşım açılmıyor");
        await TicketWorld.CreateIncidentAsync(world.Employee, "Fare bozuk");
        await SetAsync(world.Desk, TicketWorld.PathOf(tagged), serviceId, TicketWorld.VersionOf(tagged));
        var change = await ChangeWorld.CreateAsync(world.Desk, world);
        await ChangeWorld.CreateAsync(world.Desk, world, ChangeWorld.DraftBody("Yazıcı sürücüsü"));
        await SetAsync(world.Desk, ChangeWorld.PathOf(change), serviceId, ChangeWorld.VersionOf(change));
        var problem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id);
        await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id, "Ekranlar titriyor");
        await SetAsync(world.Desk, ProblemWorld.PathOf(problem), serviceId, ProblemWorld.VersionOf(problem));

        var tickets = await TicketWorld.ReadAsync(world.Desk, $"{TicketWorld.TicketsPath}?serviceId={serviceId}");
        var changes = await TicketWorld.ReadAsync(world.Desk, $"{ChangeWorld.ChangesPath}?serviceId={serviceId}");
        var problems = await TicketWorld.ReadAsync(world.Desk, $"{ProblemWorld.ProblemsPath}?serviceId={serviceId}");

        Assert.Equal(TicketWorld.NumberOf(tagged), Assert.Single(tickets.GetProperty("items").EnumerateArray()).GetProperty("number").GetString());
        Assert.Equal(ChangeWorld.NumberOf(change), Assert.Single(changes.GetProperty("items").EnumerateArray()).GetProperty("number").GetString());
        Assert.Equal(ProblemWorld.NumberOf(problem), Assert.Single(problems.GetProperty("items").EnumerateArray()).GetProperty("number").GetString());
    }
}
