namespace Api.SharedKernel.Infrastructure.Http;

/// <summary>
/// #3495 C6 — server-side mirror of the ADR-059 §5 / #2123 BGG asset freeze. The canonical source
/// is the front-end ESLint rule <c>local/no-bgg-host</c> (regex in
/// <c>apps/web/eslint-rules/no-bgg-host.js</c>); this is its back-end counterpart so an admin cannot
/// launder around the ban via the "manual cover from URL" arbitrary-URL path.
/// <para>
/// Matched by registrable-domain suffix (exact host or a sub-domain of it), NOT by substring — so a
/// look-alike attacker host such as <c>evilgeekdo.com</c> or <c>geekdo.com.attacker.example</c> is
/// NOT banned (it is not a BGG asset), while <c>cf.geekdo-images.com</c>, <c>images.geekdo.com</c>
/// and <c>www.boardgamegeek.com</c> are.
/// </para>
/// </summary>
internal static class BggHostDenyList
{
    private static readonly string[] BannedSuffixes =
    {
        "geekdo-images.com",  // cf.geekdo-images.com, geekdo-images.com
        "geekdo.com",         // images.geekdo.com
        "boardgamegeek.com",  // *.boardgamegeek.com
    };

    /// <summary>True when <paramref name="url"/> is an absolute URL whose host is BGG/geekdo.</summary>
    public static bool IsBanned(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && IsBannedHost(uri.Host);

    /// <summary>True when <paramref name="host"/> equals or is a sub-domain of a banned suffix.</summary>
    public static bool IsBannedHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        var normalized = host.Trim().TrimEnd('.').ToLowerInvariant();
        foreach (var suffix in BannedSuffixes)
        {
            if (string.Equals(normalized, suffix, StringComparison.Ordinal) ||
                normalized.EndsWith("." + suffix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
