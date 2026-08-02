using System.Net;
using System.Net.Sockets;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

/// <summary>
/// SSRF IP classifier (issue #3495, finding H3). Pure, fail-closed classification of an
/// IP as blocked (private / reserved / special-purpose) per the IANA IPv4 and IPv6
/// Special-Purpose Address Registries. Default-DENY for the unknown high ranges (multicast
/// 224/4, reserved/class-E 240/4) that a naive private-only check leaves open. IPv4-mapped
/// and the well-known IPv4-in-IPv6 tunneling prefixes are unwrapped/blocked so a private
/// v4 cannot be smuggled through an IPv6 literal.
/// </summary>
internal static class SsrfPolicy
{
    /// <summary>True when the address must NOT be dialed (private/reserved/special-purpose).</summary>
    public static bool IsBlocked(IPAddress ip)
    {
        ArgumentNullException.ThrowIfNull(ip);

        // Unwrap IPv4-mapped IPv6 (::ffff:a.b.c.d) and classify as IPv4.
        if (ip.IsIPv4MappedToIPv6)
        {
            return IsBlocked(ip.MapToIPv4());
        }

        return ip.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsBlockedV4(ip.GetAddressBytes()),
            AddressFamily.InterNetworkV6 => IsBlockedV6(ip),
            _ => true, // unknown address family: fail closed
        };
    }

    private static bool IsBlockedV4(byte[] b)
    {
        byte a = b[0], c = b[1], d = b[2];
        return a switch
        {
            0 => true,                                     // 0.0.0.0/8 this-network
            10 => true,                                    // 10/8 private
            127 => true,                                   // 127/8 loopback
            100 => c is >= 64 and <= 127,                  // 100.64/10 CGNAT
            169 => c == 254,                               // 169.254/16 link-local + cloud metadata
            172 => c is >= 16 and <= 31,                   // 172.16/12 private
            192 => (c == 0 && d == 0)                      // 192.0.0/24 IETF protocol assignments
                   || (c == 0 && d == 2)                   // 192.0.2/24 TEST-NET-1
                   || (c == 88 && d == 99)                 // 192.88.99/24 6to4 anycast
                   || c == 168,                            // 192.168/16 private
            198 => c is 18 or 19                           // 198.18/15 benchmarking
                   || (c == 51 && d == 100),               // 198.51.100/24 TEST-NET-2
            203 => c == 0 && d == 113,                     // 203.0.113/24 TEST-NET-3
            >= 224 => true,                                // 224/4 multicast + 240/4 reserved/class-E + 255.255.255.255
            _ => false,
        };
    }

    private static bool IsBlockedV6(IPAddress ip)
    {
        if (ip.Equals(IPAddress.IPv6Loopback)) return true;  // ::1
        if (ip.IsIPv6Multicast) return true;                 // ff00::/8
        if (ip.IsIPv6LinkLocal) return true;                 // fe80::/10
        if (ip.IsIPv6SiteLocal) return true;                 // fec0::/10 (deprecated)

        byte[] b = ip.GetAddressBytes(); // 16 bytes, network order

        // Unique-Local Addresses fc00::/7 (fc00::/8 + fd00::/8).
        if ((b[0] & 0xFE) == 0xFC) return true;

        // ::/96 low block: unspecified (::), loopback, IPv4-compatible ::a.b.c.d (first 96 bits zero).
        if (AllZero(b, 0, 12)) return true;

        // NAT64 well-known prefix 64:ff9b::/96 (embeds a v4 destination).
        if (b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xFF && b[3] == 0x9B && AllZero(b, 4, 8)) return true;

        // 2001:0000::/32 Teredo and 2001:db8::/32 documentation (leave the rest of 2001::/16 public).
        if (b[0] == 0x20 && b[1] == 0x01)
        {
            if (b[2] == 0x00 && b[3] == 0x00) return true; // Teredo
            if (b[2] == 0x0D && b[3] == 0xB8) return true; // documentation
        }

        // 2002::/16 6to4 (deprecated; embeds a v4).
        if (b[0] == 0x20 && b[1] == 0x02) return true;

        return false;
    }

    private static bool AllZero(byte[] b, int start, int count)
    {
        for (int i = start; i < start + count; i++)
        {
            if (b[i] != 0) return false;
        }
        return true;
    }
}
