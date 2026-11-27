using System.Net;
using System.Net.Sockets;

namespace Proteone.Infrastructure.Email;

/// <summary>
/// Which addresses an admin-chosen SMTP host may connect to. Link-local addresses (169.254.0.0/16, fe80::/10, where cloud
/// metadata services live), unspecified addresses (0.0.0.0/8, ::) and the known cloud metadata addresses are refused.
/// IPv6 forms that carry an IPv4 address (IPv4-mapped, IPv4-compatible, NAT64) are checked as that IPv4 address, so they
/// cannot go around the IPv4 rules. Private and loopback addresses stay allowed, because an internal mail relay is a
/// normal target.
/// </summary>
internal static class SmtpTargetPolicy
{
    private const byte LinkLocalFirstOctet = 169;
    private const byte LinkLocalSecondOctet = 254;
    private const byte ThisNetworkFirstOctet = 0;
    private const int Ipv6PrefixLength = 12;
    private const int Ipv4Length = 4;

    // The NAT64 well-known prefix 64:ff9b::/96 (RFC 6052).
    private static readonly byte[] Nat64Prefix = [0x00, 0x64, 0xff, 0x9b, 0, 0, 0, 0, 0, 0, 0, 0];

    // Cloud metadata services outside the link-local ranges: AWS over IPv6 (fd00:ec2::254), Alibaba Cloud
    // (100.100.100.200) and the Azure wire server (168.63.129.16). They give out credentials, so a mail host must never
    // point at them.
    private static readonly IPAddress[] MetadataAddresses =
    [
        IPAddress.Parse("fd00:ec2::254"),
        IPAddress.Parse("100.100.100.200"),
        IPAddress.Parse("168.63.129.16"),
    ];

    public static bool IsAllowed(IPAddress address)
    {
        address = EmbeddedIpv4(address) ?? address;
        if (Array.Exists(MetadataAddresses, address.Equals))
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] != ThisNetworkFirstOctet && !(bytes[0] == LinkLocalFirstOctet && bytes[1] == LinkLocalSecondOctet);
        }

        return !address.Equals(IPAddress.IPv6Any) && !address.IsIPv6LinkLocal;
    }

    /// <summary>The IPv4 address an IPv6 address carries in its last 32 bits, or null when it carries none.</summary>
    private static IPAddress? EmbeddedIpv4(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return null;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            return address.MapToIPv4();
        }

        // :: and ::1 are in ::/96 too, but they are the IPv6 unspecified and loopback addresses, not IPv4-compatible ones.
        if (address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.IPv6Loopback))
        {
            return null;
        }

        var bytes = address.GetAddressBytes();
        var prefix = bytes.AsSpan(0, Ipv6PrefixLength);
        var isCompatible = !prefix.ContainsAnyExcept((byte)0);
        return isCompatible || prefix.SequenceEqual(Nat64Prefix) ? new IPAddress(bytes.AsSpan(Ipv6PrefixLength, Ipv4Length)) : null;
    }
}

/// <summary>The host name or address of the SMTP server points to an address that <see cref="SmtpTargetPolicy"/> refuses.</summary>
internal sealed class SmtpHostNotAllowedException(IPAddress address)
    : Exception($"Connections to the address {address} are not allowed.");
