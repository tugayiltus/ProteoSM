using Microsoft.EntityFrameworkCore;
using ProteoSM.Application.Abstractions;

namespace ProteoSM.Application.Features.Email.Settings;

public sealed class GetEmailSettingsHandler(IAppDbContext db, IEmailEnvironment environment)
{
    public async Task<EmailSettingsDto> HandleAsync(CancellationToken cancellationToken) =>
        EmailSettingsMapping.ToDto(await db.EmailSettings.AsNoTracking().SingleAsync(cancellationToken), environment.Current);
}
