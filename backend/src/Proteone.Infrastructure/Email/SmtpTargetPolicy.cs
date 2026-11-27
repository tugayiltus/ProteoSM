using System.Net;
using System.Net.Sockets;

namespace Proteone.Infrastructure.Email;

/// <summary>
/// Which addresses a test send may connect to. Link-local addresses (169.254.0.0/16, fe80::/10, where cloud metadata
/// services live) and unspecified addresses (0.0.0.0/8, ::) are refused. Private and loopback addresses stay allowed,
/// because an internal mail relay is a normal target.
/// </summary>
internal static class SmtpTargetPolicy
{
    private const byte LinkLocalFirstOctet = 169;
    private const byte LinkLocalSecondOctet = 254;
    private const byte ThisNetworkFirstOctet = 0;

    public static bool IsAllowed(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] != ThisNetworkFirstOctet && !(bytes[0] == LinkLocalFirstOctet && bytes[1] == LinkLocalSecondOctet);
        }

        return !address.Equals(IPAddress.IPv6Any) && !address.IsIPv6LinkLocal;
    }
}

/// <summary>The host name or address of the SMTP server points to an address that <see cref="SmtpTargetPolicy"/> refuses.</summary>
internal sealed class SmtpHostNotAllowedException(IPAddress address)
    : Exception($"Connections to the address {address} are not allowed.");
