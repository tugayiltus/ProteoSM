namespace Proteone.Application.Abstractions;

/// <summary>
/// The mail transport from the environment (<c>Email:*</c>), shown on the admin page. It holds no user name or password.
/// <see cref="IsPickup"/> is true when <c>Email:Mode</c> is Pickup: then every mail goes to <c>.eml</c> files.
/// </summary>
public sealed record EmailEnvironmentInfo(bool IsPickup, string From, string Host, int Port);

/// <summary>Reads <see cref="EmailEnvironmentInfo"/> from the current configuration.</summary>
public interface IEmailEnvironment
{
    EmailEnvironmentInfo Current { get; }
}
