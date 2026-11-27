using System.Net;
using System.Net.Sockets;

namespace ProteoSM.Infrastructure.Email;

/// <summary>
/// Which addresses an admin-chosen SMTP host may connect to. Private and loopback addresses stay allowed, because an
/// internal mail relay is a normal target; the rules refuse only what can reach a cloud metadata service or go around
/// the checks.
/// </summary>
internal static class SmtpTargetPolicy
{
    // Refused IPv4 ranges: "this network" (0.0.0.0/8, includes unspecified), link-local (169.254.0.0/16, AWS, GCP and
    // Azure metadata), Alibaba Cloud metadata (100.100.100.200) and the Azure wire server (168.63.129.16).
    private static readonly IPNetwork[] RefusedIpv4 =
    [
        IPNetwork.Parse("0.0.0.0/8"),
        IPNetwork.Parse("169.254.0.0/16"),
        IPNetwork.Parse("100.100.100.200/32"),
        IPNetwork.Parse("168.63.129.16/32"),
    ];

    // Refused IPv6 ranges: unspecified (::), link-local (fe80::/10), AWS metadata over IPv6 (fd00:ec2::/32), and the
    // translation prefixes NAT64 (64:ff9b::/96), NAT64 local use (64:ff9b:1::/48), 6to4 (2002::/16) and Teredo
    // (2001::/32). A translation prefix carries an IPv4 address that a gateway reaches for us, so it is refused outright
    // instead of decoded: an internal relay is never only reachable that way.
    private static readonly IPNetwork[] RefusedIpv6 =
    [
        IPNetwork.Parse("::/128"),
        IPNetwork.Parse("fe80::/10"),
        IPNetwork.Parse("fd00:ec2::/32"),
        IPNetwork.Parse("64:ff9b::/96"),
        IPNetwork.Parse("64:ff9b:1::/48"),
        IPNetwork.Parse("2002::/16"),
        IPNetwork.Parse("2001::/32"),
    ];

    // IPv4-compatible addresses (::/96) other than :: and ::1 carry an IPv4 address in their last 32 bits.
    private static readonly IPNetwork Ipv4Compatible = IPNetwork.Parse("::/96");
    private const int Ipv4Offset = 12;
    private const int Ipv4Length = 4;

    public static bool IsAllowed(IPAddress address)
    {
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            // A zone index only makes sense on a link-local address (refused anyway), so any scoped address is refused.
            // Every later check compares the bare bytes.
            if (address.ScopeId != 0)
            {
                return false;
            }

            address = new IPAddress(address.GetAddressBytes());
            if (address.IsIPv4MappedToIPv6)
            {
                address = address.MapToIPv4();
            }
            else if (IsIpv4Compatible(address))
            {
                address = new IPAddress(address.GetAddressBytes().AsSpan(Ipv4Offset, Ipv4Length));
            }
        }

        var refused = address.AddressFamily == AddressFamily.InterNetwork ? RefusedIpv4 : RefusedIpv6;
        return !Array.Exists(refused, range => range.Contains(address));
    }

    private static bool IsIpv4Compatible(IPAddress address) =>
        Ipv4Compatible.Contains(address) && !address.Equals(IPAddress.IPv6Any) && !address.Equals(IPAddress.IPv6Loopback);
}

/// <summary>The host name or address of the SMTP server points to an address that <see cref="SmtpTargetPolicy"/> refuses.</summary>
internal sealed class SmtpHostNotAllowedException(IPAddress address)
    : Exception($"Connections to the address {address} are not allowed.");
