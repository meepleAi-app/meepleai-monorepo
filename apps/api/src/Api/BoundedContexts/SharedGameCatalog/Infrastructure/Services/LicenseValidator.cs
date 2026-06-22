using System.Text.RegularExpressions;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

/// <summary>
/// Validates Wikimedia Commons license short names against the whitelist locked
/// in ADR <c>adr-2026-06-09-wikidata-enrichment-architecture.md</c> DEC-3c
/// (#1823). The whitelist covers Public Domain / CC0 / CC-BY (any version) /
/// CC-BY-SA (any version); every other license — including CC-BY-NC, CC-BY-ND,
/// "All Rights Reserved", "Fair use" — is rejected so that catalog enrichment
/// never persists an image whose redistribution is encumbered.
/// </summary>
/// <remarks>
/// <para>
/// Input is the raw <c>extmetadata.LicenseShortName</c> value returned by the
/// Commons API (e.g. "CC BY 2.0", "CC-BY-SA-4.0", "Public domain"). Validation
/// is case-insensitive and tolerant of spaces vs hyphens (the same license is
/// reported in both forms across Commons).
/// </para>
/// <para>
/// Spike sess.46h validated the whitelist against 13 real boardgame samples:
/// all 13 machine-readable licenses matched the whitelist (100%). No rejected
/// licenses observed in the sample, but the spec-panel Crispin C-002 concern
/// requires adversarial test fixtures to guard against future drift.
/// </para>
/// </remarks>
internal static class LicenseValidator
{
    /// <summary>
    /// Whitelist regex per DEC-3c. Matches Public Domain / PD / CC0 plus any
    /// CC-BY or CC-BY-SA version. Hyphens and spaces are interchangeable
    /// between tokens to accept both Commons forms ("CC BY 2.0" and "CC-BY-2.0").
    /// </summary>
    private static readonly Regex WhitelistPattern = new(
        @"^(?:public domain|PD|CC0|CC[ -]BY(?:[ -][0-9.]+)?|CC[ -]BY[ -]SA(?:[ -][0-9.]+)?)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.ExplicitCapture,
        TimeSpan.FromMilliseconds(50));

    /// <summary>
    /// Returns <c>true</c> when the supplied license short name matches the
    /// whitelist. Null / empty / whitespace-only input returns <c>false</c>.
    /// </summary>
    public static bool IsWhitelisted(string? licenseShortName)
    {
        if (string.IsNullOrWhiteSpace(licenseShortName))
        {
            return false;
        }

        return WhitelistPattern.IsMatch(licenseShortName.Trim());
    }

    /// <summary>
    /// Returns the canonical hyphenated form of a whitelisted license for storage
    /// in <c>shared_games.cover_license</c>. Throws when the license is rejected —
    /// callers must guard with <see cref="IsWhitelisted(string)"/> first.
    /// </summary>
    /// <exception cref="ArgumentException">License is not in the whitelist.</exception>
    public static string Normalize(string licenseShortName)
    {
        if (!IsWhitelisted(licenseShortName))
        {
            throw new ArgumentException(
                $"License '{licenseShortName}' is not in the DEC-3c whitelist.",
                nameof(licenseShortName));
        }

        // Trim + replace spaces with hyphens for consistent storage. The regex
        // above already enforces the structural shape, so a simple replace is
        // sufficient — no need to re-parse the version suffix.
        return licenseShortName
            .Trim()
            .Replace(' ', '-')
            .ToUpperInvariant();
    }
}
