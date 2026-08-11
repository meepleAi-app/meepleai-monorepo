// #3495 M2: SSRF egress observability — blocked/allowed counters by sink + reason.
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    #region Egress SSRF Metrics (#3495 M2)

    /// <summary>
    /// Bounded set of egress SINKS. A compile-time constant per outbound client — NEVER a host/IP
    /// (that would be unbounded cardinality). Used as the <c>sink</c> tag on the counters below.
    /// </summary>
    public static class EgressSinks
    {
        public const string BggCover = "bgg_cover";
        public const string Manual = "manual";
        public const string Wikidata = "wikidata";
        public const string Wikimedia = "wikimedia";
        public const string Bgg = "bgg";
        public const string Slack = "slack";
    }

    /// <summary>
    /// Bounded set of egress BLOCK reasons. Used as the <c>reason</c> tag; never carries host/IP.
    /// </summary>
    public static class EgressBlockReasons
    {
        public const string PrivateIp = "private_ip";
        public const string DenylistHit = "denylist_hit";
        /// <summary>A hop targeted a non-default port — port-probing an internal service (#3495 H2).</summary>
        public const string Port = "port";
        /// <summary>The host is outside the fixed-host sink's allow-list (#3495 M3).</summary>
        public const string HostNotAllowed = "host_not_allowed";
        /// <summary>The response was content-encoded, so the byte ceiling could not bound the decoded size (#3495 C5).</summary>
        public const string ContentEncoding = "content_encoding";
        public const string RedirectExhausted = "redirect_exhausted";
        public const string SizeCap = "size_cap";
        public const string DecodeFail = "decode_fail";
        /// <summary>La risoluzione DNS ha lanciato (NXDOMAIN, timeout del resolver, socket error) — #3583.</summary>
        public const string DnsFailure = "dns_failure";
        public const string Timeout = "timeout";
        public const string BreakerOpen = "breaker_open";
        public const string Scheme = "scheme";
    }

    /// <summary>
    /// Egress requests BLOCKED before/at dial, by sink and reason. Incremented immediately BEFORE the
    /// throw at each guard site. Tags are ONLY the two bounded constants — host/IP is never a tag.
    /// </summary>
    public static readonly Counter<long> EgressBlocked = Meter.CreateCounter<long>(
        name: "meepleai.egress.blocked.total",
        unit: "blocks",
        description: "Egress requests blocked by the SSRF guard, by sink and reason (#3495 M2)");

    /// <summary>
    /// Egress connections VALIDATED/allowed, by sink. The denominator for a block-ratio alert:
    /// a ratio that collapses to 0 while traffic flows means the guard was silently bypassed.
    /// </summary>
    public static readonly Counter<long> EgressAllowed = Meter.CreateCounter<long>(
        name: "meepleai.egress.allowed.total",
        unit: "requests",
        description: "Egress connections validated and allowed, by sink (block-ratio denominator, #3495 M2)");

    /// <summary>Records an egress block. <paramref name="sink"/>/<paramref name="reason"/> MUST be
    /// <see cref="EgressSinks"/>/<see cref="EgressBlockReasons"/> constants — never a host or IP.</summary>
    public static void RecordEgressBlocked(string sink, string reason) =>
        EgressBlocked.Add(1, new TagList { { "sink", sink }, { "reason", reason } });

    /// <summary>Records an allowed egress connection for the block-ratio denominator.</summary>
    public static void RecordEgressAllowed(string sink) =>
        EgressAllowed.Add(1, new TagList { { "sink", sink } });

    #endregion
}
