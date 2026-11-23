namespace Tgysm.Api.Contracts;

/// <summary>Body of <c>PUT …/{number}/service</c> on tickets, changes and problems; a null service clears it.</summary>
public sealed record SetServiceRequest(Guid? ServiceId, uint Version);

/// <summary>Route segment of the set-service action, shared by tickets, changes and problems.</summary>
internal static class ServiceRoutes
{
    public const string ServiceSegment = "/service";
}
