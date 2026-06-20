using System.Net;
using System.Text.RegularExpressions;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

/// <summary>
/// Strips HTML markup from Wikimedia Commons attribution strings so the FE
/// renders them safely as plain text (DEC-G6-1 LOCKED 2026-06-20).
/// </summary>
/// <remarks>
/// Wikimedia Commons <c>extmetadata.Artist</c> + <c>Credit</c> fields are HTML by
/// convention (e.g. <c>&lt;a href="..."&gt;Klaus Teuber&lt;/a&gt;</c>). This helper
/// strips tags AND decodes HTML entities so the persisted DB value (and the FE)
/// see plain text only. Issue #2055 Phase G AC-G6.
///
/// <para>Pattern matches everything between &lt; and &gt; greedily, replacing it
/// with empty string. Then <see cref="WebUtility.HtmlDecode(string?)"/> converts
/// entities like <c>&amp;amp;</c> back to <c>&amp;</c>. Final string is trimmed +
/// whitespace-collapsed to one space.</para>
///
/// <para>Both regexes carry a 1-second timeout (MA0009 ReDoS guard) because the
/// upstream HTML is attacker-supplied via Wikimedia Commons extmetadata.</para>
/// </remarks>
internal static class AttributionTextExtractor
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);
    private static readonly Regex TagRegex = new("<[^>]*>", RegexOptions.Compiled, RegexTimeout);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled, RegexTimeout);

    public static string? Strip(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var stripped = TagRegex.Replace(html, " ");
        var decoded = WebUtility.HtmlDecode(stripped);
        var collapsed = WhitespaceRegex.Replace(decoded, " ").Trim();

        return string.IsNullOrEmpty(collapsed) ? null : collapsed;
    }
}
