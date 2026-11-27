using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProteoSM.Application.Abstractions;
using ProteoSM.Application.Common;

namespace ProteoSM.Application.Features.Auth.Settings;

public sealed class GetAuthSettingsHandler(IAppDbContext db, IOptionsMonitor<AppOptions> options)
{
    public async Task<AuthSettingsDto> HandleAsync(CancellationToken cancellationToken) =>
        AuthSettingsMapping.ToDto(await db.AuthSettings.AsNoTracking().SingleAsync(cancellationToken), options.CurrentValue.PublicBaseUrl);
}
