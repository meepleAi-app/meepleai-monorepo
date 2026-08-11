namespace Api.SharedKernel.Infrastructure.Http;

/// <summary>
/// Issue #3495 finding M3 (Slice F) — per-sink host allow-lists, applied by the connect-pin to every
/// connection (initial request and each redirect hop).
/// <para>
/// This is defence in depth, not a replacement for the IP deny-list: the deny-list answers "is this
/// address internal?", the allow-list answers "is this a host we talk to at all?". Only the second
/// one stops a compromised or hijacked upstream from redirecting us to an arbitrary PUBLIC host —
/// exfiltration or abuse that the IP policy has no way to recognise.
/// </para>
/// <para>
/// Entries are registrable domains matched exactly or as a parent of the host
/// (<see cref="HostSuffixMatcher"/>), so a look-alike such as <c>evilwikimedia.org</c> does NOT match.
/// </para>
/// <para>
/// Deliberately absent:
/// </para>
/// <list type="bullet">
///   <item>the <b>manual</b> arbitrary-URL sink — fetching an admin-supplied host is its purpose;
///   there the ADR-059 deny-list, the scheme/port gate and the pin are the boundary (M3 says so
///   explicitly);</item>
///   <item><b>Slack</b> — the target comes from an operator-configured webhook URL, which teams
///   legitimately point at a Slack-compatible receiver; an allow-list here would break that
///   configuration rather than protect it.</item>
/// </list>
/// </summary>
internal static class EgressAllowLists
{
    /// <summary>boardgamegeek.com XML API (typed + named clients, catalog provider).</summary>
    public static readonly string[] Bgg = { "boardgamegeek.com" };

    /// <summary>BGG cover images: the API returns asset URLs on the geekdo image CDNs.</summary>
    public static readonly string[] BggCover = { "boardgamegeek.com", "geekdo-images.com", "geekdo.com" };

    /// <summary>query.wikidata.org SPARQL — served from the Wikimedia estate.</summary>
    public static readonly string[] Wikidata = { "wikidata.org", "wikimedia.org" };

    /// <summary>commons.wikimedia.org + the upload.wikimedia.org CDN the FilePath 302 lands on.</summary>
    public static readonly string[] Wikimedia = { "wikimedia.org" };
}
