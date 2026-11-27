using System.Net;
using Proteone.Infrastructure.Email;

namespace Proteone.Api.IntegrationTests.Notifications;

/// <summary>
/// The addresses an admin-chosen SMTP host may connect to. IPv6 forms that carry an IPv4 address are checked as that
/// IPv4 address, and the known cloud metadata addresses are refused. Private and loopback relays stay allowed.
/// </summary>
public sealed class SmtpTargetPolicyTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("10.0.0.25")]
    [InlineData("172.16.4.2")]
    [InlineData("192.168.1.10")]
    [InlineData("100.64.0.1")]
    [InlineData("8.8.8.8")]
    [InlineData("2001:db8::25")]
    [InlineData("fd00::25")]
    [InlineData("::ffff:10.0.0.25")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("64:ff9b::808:808")]
    [InlineData("::10.0.0.25")]
    public void Ordinary_private_loopback_and_public_addresses_are_allowed(string address) =>
        Assert.True(SmtpTargetPolicy.IsAllowed(IPAddress.Parse(address)));

    [Theory]
    [InlineData("169.254.169.254")]
    [InlineData("0.0.0.0")]
    [InlineData("0.1.2.3")]
    [InlineData("::")]
    [InlineData("fe80::1")]
    public void Link_local_and_unspecified_addresses_are_refused(string address) =>
        Assert.False(SmtpTargetPolicy.IsAllowed(IPAddress.Parse(address)));

    [Theory]
    [InlineData("::ffff:169.254.169.254")]
    [InlineData("::ffff:0.0.0.0")]
    [InlineData("::ffff:100.100.100.200")]
    public void Ipv4_mapped_addresses_are_checked_as_ipv4(string address) =>
        Assert.False(SmtpTargetPolicy.IsAllowed(IPAddress.Parse(address)));

    [Theory]
    [InlineData("::169.254.169.254")]
    [InlineData("::168.63.129.16")]
    public void Ipv4_compatible_addresses_are_checked_as_ipv4(string address) =>
        Assert.False(SmtpTargetPolicy.IsAllowed(IPAddress.Parse(address)));

    [Theory]
    [InlineData("64:ff9b::169.254.169.254")]
    [InlineData("64:ff9b::a9fe:a9fe")]
    [InlineData("64:ff9b::0.0.0.0")]
    [InlineData("64:ff9b::100.100.100.200")]
    public void Nat64_addresses_are_checked_as_ipv4(string address) =>
        Assert.False(SmtpTargetPolicy.IsAllowed(IPAddress.Parse(address)));

    [Theory]
    [InlineData("fd00:ec2::254")]
    [InlineData("100.100.100.200")]
    [InlineData("168.63.129.16")]
    public void Known_cloud_metadata_addresses_are_refused(string address) =>
        Assert.False(SmtpTargetPolicy.IsAllowed(IPAddress.Parse(address)));
}
