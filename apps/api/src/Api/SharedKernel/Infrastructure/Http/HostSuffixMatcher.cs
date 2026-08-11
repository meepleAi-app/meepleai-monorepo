namespace Api.SharedKernel.Infrastructure.Http;

/// <summary>
/// Registrable-domain suffix matching for host allow/deny lists.
/// <para>
/// A host matches a suffix when it IS the suffix or is a sub-domain of it — never by substring.
/// That distinction is the whole point: substring matching would treat the attacker-controlled
/// <c>evilwikimedia.org</c> or <c>wikimedia.org.attacker.example</c> as a match, which is a bypass
/// on an allow-list and a false positive on a deny-list.
/// </para>
/// <para>
/// Extracted in #3495 Slice F so the ADR-059 deny-list (<see cref="BggHostDenyList"/>) and the
/// per-sink egress allow-lists (<see cref="SsrfPinnedConnect"/>, finding M3) share one implementation
/// instead of drifting apart.
/// </para>
/// </summary>
internal static class HostSuffixMatcher
{
    /// <summary>
    /// True when <paramref name="host"/> equals one of <paramref name="suffixes"/> or is a
    /// sub-domain of it. Comparison is case-insensitive; a trailing root dot is tolerated.
    /// </summary>
    public static bool Matches(string? host, IReadOnlyCollection<string> suffixes)
    {
        ArgumentNullException.ThrowIfNull(suffixes);

        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        var normalized = host.Trim().TrimEnd('.').ToLowerInvariant();
        foreach (var suffix in suffixes)
        {
            if (string.Equals(normalized, suffix, StringComparison.Ordinal)
                || normalized.EndsWith("." + suffix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
