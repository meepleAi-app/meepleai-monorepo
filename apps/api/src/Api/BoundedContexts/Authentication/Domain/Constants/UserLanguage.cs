using System.Globalization;

namespace Api.BoundedContexts.Authentication.Domain.Constants;

/// <summary>
/// The language a new account starts with (#4783). It decides what the emails the user sends — a game night
/// invitation follows the ORGANIZER's <c>User.Language</c> (#4754) — are written in, and until the user
/// opens Settings → Preferences it is the only value there is: every account used to be born <c>en</c>,
/// so an Italian speaker invited their friends in English.
///
/// Only the languages the product can actually write in (an interface bundle and an email template) are
/// ever chosen here. Preferences may still store others (<c>fr</c> is in the selector); a new account does
/// not start with a language nothing is translated into.
/// </summary>
internal static class UserLanguage
{
    public const string Italian = "it";
    public const string English = "en";

    /// <summary>What an account starts with when nothing says otherwise.</summary>
    public const string Default = English;

    private static readonly string[] Supported = [Italian, English];

    // A header is untrusted input: a real one is a few dozen bytes. Past these bounds the rest is ignored.
    private const int MaxHeaderLength = 256;
    private const int MaxTags = 16;

    /// <summary>
    /// Picks the starting language. In order: <paramref name="explicitLocale"/> — what the client says the
    /// user is looking at, the most direct evidence — then the <c>Accept-Language</c> header
    /// (<paramref name="acceptLanguage"/>), then <see cref="Default"/>. A value that is not one of the
    /// supported languages is skipped, not mapped: <c>"de"</c> does not become English by being the first
    /// thing offered.
    /// </summary>
    public static string Resolve(string? explicitLocale, string? acceptLanguage = null) =>
        FromTag(explicitLocale) ?? FromAcceptLanguage(acceptLanguage) ?? Default;

    /// <summary>"it", "IT", "it-IT" and "it_IT" are Italian; anything else not supported is null.</summary>
    private static string? FromTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return null;

        // The primary subtag. Not `Split('-', '_')`: that reads `'_'` as the `count` of `Split(char, int)`.
        var trimmed = tag.Trim();
        var end = trimmed.IndexOfAny(['-', '_']);
        var primary = end < 0 ? trimmed : trimmed[..end];
        return Array.Find(Supported, s => string.Equals(s, primary, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// <c>it-IT,it;q=0.9,en;q=0.8</c>: tags ranked by their quality, in order of appearance on a tie;
    /// <c>q=0</c> ("not acceptable") and <c>*</c> never match; the first supported one wins.
    /// </summary>
    private static string? FromAcceptLanguage(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
            return null;

        if (header.Length > MaxHeaderLength)
        {
            // Cut where a tag ends, not in the middle of one: `it;q=0.1` cut to `it;q=` would read as `q=1`.
            var lastComma = header.LastIndexOf(',', MaxHeaderLength - 1);
            header = lastComma > 0 ? header[..lastComma] : header[..MaxHeaderLength];
        }

        var ranked = new List<(string Tag, double Quality, int Order)>();
        foreach (var part in header.Split(',').Take(MaxTags))
        {
            var pieces = part.Split(';');
            var quality = 1.0;
            var valid = true;
            foreach (var parameter in pieces.Skip(1).Select(p => p.Trim()))
            {
                if (!parameter.StartsWith("q=", StringComparison.OrdinalIgnoreCase))
                    continue;

                // A `q` that is not a number in [0, 1] makes the tag unusable: it is dropped, not read as 1.
                valid = double.TryParse(parameter[2..], NumberStyles.Float, CultureInfo.InvariantCulture, out quality)
                    && quality is >= 0 and <= 1;
            }

            // `q=0` is «not acceptable».
            if (valid && quality > 0)
                ranked.Add((pieces[0].Trim(), quality, ranked.Count));
        }

        return ranked
            .OrderByDescending(r => r.Quality)
            .ThenBy(r => r.Order)
            .Select(r => FromTag(r.Tag))
            .FirstOrDefault(language => language is not null);
    }
}
