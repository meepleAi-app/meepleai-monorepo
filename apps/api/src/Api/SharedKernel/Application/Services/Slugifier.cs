using System.Text.RegularExpressions;

namespace Api.SharedKernel.Application.Services;

/// <summary>
/// Cross-context utility for converting names to URL-safe slugs.
/// Examples:
///   "Ark Nova"              -> "ark-nova"
///   "7 Wonders Duel"        -> "7-wonders-duel"
///   "King's Dilemma"        -> "kings-dilemma"
///   "Ticket to Ride: Europe"-> "ticket-to-ride-europe"
/// </summary>
internal static class Slugifier
{
    public static string Slugify(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "unknown";

        var normalized = name.Normalize(System.Text.NormalizationForm.FormD);

        var asciiOnly = Regex.Replace(normalized, @"\P{IsBasicLatin}", string.Empty,
            RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));

        var lower = asciiOnly.ToLowerInvariant();

        var noSpecial = Regex.Replace(lower, @"[^a-z0-9\s]", string.Empty,
            RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));

        var hyphenated = Regex.Replace(noSpecial.Trim(), @"\s+", "-",
            RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));

        var slug = hyphenated.Trim('-');

        return string.IsNullOrEmpty(slug) ? "unknown" : slug;
    }
}
