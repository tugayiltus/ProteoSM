using Proteone.Domain.Assets;

namespace Proteone.Application.Features.Services;

public sealed record ServiceListItem(Guid Id, string Name, Guid OwnerTeamId, string OwnerTeamName, bool IsActive, int AssetCount, int OpenTicketCount);

public sealed record ServiceDetail(Guid Id, string Name, string? Description, Guid OwnerTeamId, string OwnerTeamName, bool IsActive, uint Version);

public sealed record ServiceRef(Guid Id, string Name, bool IsActive);

public sealed record ServiceAssetItem(Guid AssetId, string AssetTag, AssetStatus Status, DateTimeOffset LinkedAt);
