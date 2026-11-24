using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Proteone.Application.Abstractions;
using Proteone.Application.Features.Changes;
using Proteone.Application.Features.Problems;
using Proteone.Application.Features.Services;
using Proteone.Application.Features.Tickets;
using Proteone.Domain.Assets;

namespace Proteone.Application.Features.Pilot;

/// <summary>
/// The service catalogue of the pilot seed: eight services with an owner team and one to four assets, and a service on
/// about a third of the tickets, every third change and every second problem. Its draws come from a random of its own,
/// so the rest of the seed (counts, states and outcomes the other tests pin) stays exactly as it was.
/// </summary>
internal sealed partial class PilotSeedSession
{
    private const int ServiceRandomOffset = 12;
    private const int MinAssetsPerService = 1;
    private const int MaxAssetsPerService = 4;
    private const int ServicedTicketPercent = 30;
    private const int ServicedChangeEvery = 3;
    private const int ServicedProblemEvery = 2;

    private readonly Random _serviceRandom = new(PilotSeed.RandomSeed + ServiceRandomOffset);
    private readonly List<Guid> _serviceIds = [];

    private async Task SeedServicesAsync()
    {
        var linkable = await LinkableAssetsAsync();
        foreach (var service in PilotSeedServiceData.Services)
        {
            var owner = service.FieldTeamOwns ? _fieldTeamId : _intakeTeamId;
            var created = await SetupAsync<CreateServiceHandler, ServiceDetail>(
                _adminId, (h, ct) => h.HandleAsync(new CreateServiceCommand(service.Name, service.Description, owner), ct));
            _serviceIds.Add(created.Id);

            var assets = linkable.ToArray();
            _serviceRandom.Shuffle(assets);
            foreach (var assetId in assets.Take(_serviceRandom.Next(MinAssetsPerService, MaxAssetsPerService + 1)))
            {
                await SetupAsync<LinkServiceAssetHandler, ServiceAssetItem>(_adminId, (h, ct) => h.HandleAsync(new LinkServiceAssetCommand(created.Id, assetId), ct));
            }
        }
    }

    /// <summary>A retired or lost device cannot back a service; the rest keep their creation order, so the draw is stable.</summary>
    private async Task<List<Guid>> LinkableAssetsAsync()
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var unlinkable = await db.Assets
            .Where(a => a.Status == AssetStatus.Retired || a.Status == AssetStatus.Lost)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);
        return [.. _assetIds.Except(unlinkable)];
    }

    /// <summary>About a third of the new tickets get a service, set by a member of the team that holds the ticket.</summary>
    private async Task<TicketDetail> MaybeSetTicketServiceAsync(TicketDetail ticket, DateTimeOffset at)
    {
        if (_serviceRandom.Next(Hundred) >= ServicedTicketPercent)
        {
            return ticket;
        }

        var members = ticket.TeamId == _fieldTeamId ? _fieldMembers : _intakeMembers;
        var actor = members[_serviceRandom.Next(members.Count)];
        var command = new SetTicketServiceCommand(ticket.Number, NextService(), ticket.Version);
        return await ActAsync<SetTicketServiceHandler, TicketDetail>(actor, at, (h, ct) => h.HandleAsync(command, ct));
    }

    private async Task MaybeSetChangeServiceAsync(int index, ChangeDetail change, Guid owner, DateTimeOffset at)
    {
        if (index % ServicedChangeEvery != 0)
        {
            return;
        }

        var command = new SetChangeServiceCommand(change.Number, NextService(), change.Version);
        await ActAsync<SetChangeServiceHandler, ChangeDetail>(owner, at, (h, ct) => h.HandleAsync(command, ct));
    }

    private async Task<ProblemDetail> MaybeSetProblemServiceAsync(int index, ProblemDetail problem, Guid owner, DateTimeOffset at)
    {
        if (index % ServicedProblemEvery != 0)
        {
            return problem;
        }

        var command = new SetProblemServiceCommand(problem.Number, NextService(), problem.Version);
        return await ActAsync<SetProblemServiceHandler, ProblemDetail>(owner, at, (h, ct) => h.HandleAsync(command, ct));
    }

    private Guid NextService() => _serviceIds[_serviceRandom.Next(_serviceIds.Count)];
}
