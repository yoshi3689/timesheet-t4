using System.Net;
using TimesheetApp.Middleware;

namespace TimesheetApp.Tests;

public class IpAllowlistTests
{
    [Fact]
    public void Allows_ExactSlash32Match()
    {
        var network = IPNetwork.Parse("203.0.113.42/32");
        var ip = IPAddress.Parse("203.0.113.42");
        Assert.True(IpAllowlist.IsAllowed(ip, new[] { network }));
    }

    [Fact]
    public void Rejects_AddressOutsideSlash32()
    {
        var network = IPNetwork.Parse("203.0.113.42/32");
        var ip = IPAddress.Parse("203.0.113.43");
        Assert.False(IpAllowlist.IsAllowed(ip, new[] { network }));
    }

    [Theory]
    [InlineData("203.0.113.0")]
    [InlineData("203.0.113.255")]
    public void Allows_AddressesWithinSlash24Boundary(string address)
    {
        var network = IPNetwork.Parse("203.0.113.0/24");
        var ip = IPAddress.Parse(address);
        Assert.True(IpAllowlist.IsAllowed(ip, new[] { network }));
    }

    [Fact]
    public void Rejects_AddressJustOutsideSlash24Boundary()
    {
        var network = IPNetwork.Parse("203.0.113.0/24");
        var ip = IPAddress.Parse("203.0.114.0");
        Assert.False(IpAllowlist.IsAllowed(ip, new[] { network }));
    }

    [Fact]
    public void Rejects_IPv6AddressAgainstIPv4Cidr()
    {
        var network = IPNetwork.Parse("203.0.113.0/24");
        var ip = IPAddress.Parse("::1");
        Assert.False(IpAllowlist.IsAllowed(ip, new[] { network }));
    }

    [Fact]
    public void Allows_ExactIPv6Match()
    {
        var network = IPNetwork.Parse("2001:db8::/32");
        var ip = IPAddress.Parse("2001:db8::1");
        Assert.True(IpAllowlist.IsAllowed(ip, new[] { network }));
    }

    [Fact]
    public void Rejects_WhenNoRemoteAddressResolved()
    {
        var network = IPNetwork.Parse("203.0.113.0/24");
        Assert.False(IpAllowlist.IsAllowed(null, new[] { network }));
    }

    // Program.cs calls IPNetwork.TryParse on every IP_ALLOWED_CIDRS entry at
    // startup and throws InvalidOperationException on the first failure — it
    // never silently drops a malformed entry (which could widen the allowlist)
    // or silently disables the feature. This confirms the detection these
    // entries rely on.
    [Theory]
    [InlineData("not-an-ip/24")]
    [InlineData("203.0.113.0/99")]
    [InlineData("203.0.113.0")]
    public void MalformedCidrEntry_FailsToParse(string entry)
    {
        Assert.False(IPNetwork.TryParse(entry, out _));
    }
}
