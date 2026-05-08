using System.Text.RegularExpressions;

namespace Api.Infrastructure.Services;

/// <summary>
/// Server-side markdown subset enforcement per PR #732 §6.3.3 spec verbatim.
/// Wave 3 Phase 3 (Issue #805).
/// </summary>
/// <remarks>
/// <para>
/// <b>Subset rules</b>:
/// <list type="bullet">
///   <item>Headings: H1, H2, H3 only — H4-H6 demoted to <c>**bold**</c>.</item>
///   <item>Lists: ul/ol up to 2 nested levels — passthrough (deep nesting handled FE-side).</item>
///   <item>Code: fenced blocks and inline — passthrough.</item>
///   <item>Tables: pipe syntax (GitHub-flavored) — passthrough.</item>
///   <item>Blockquote: <c>&gt;</c> prefix — passthrough.</item>
///   <item>Emphasis: <c>*bold*</c>, <c>_italic_</c> — passthrough.</item>
///   <item>NO HTML — raw tags stripped.</item>
///   <item>NO images — replaced with <c>[Image: alt]</c> placeholder text.</item>
///   <item>NO embeds — same path as HTML strip.</item>
///   <item>NO footnotes — <c>[^N]</c> markers and <c>[^N]: ...</c> definitions stripped.</item>
/// </list>
/// </para>
///
/// <para>
/// <b>Implementation</b>: regex-based (no Markdig dependency in repo). The
/// constraints above are deliberately conservative — anything we cannot easily
/// reason about with regex is stripped, not sanitized leniently. This trades
/// fidelity for safety, which is the correct default for user-rendered content.
/// </para>
///
/// <para>
/// <b>Order matters</b>:
/// <list type="number">
///   <item>Strip footnote definitions (multi-line) first.</item>
///   <item>Strip footnote markers second (so order-of-encounter is irrelevant).</item>
///   <item>Replace images third (before HTML strip — images use <c>![]()</c>, not raw HTML).</item>
///   <item>Strip raw HTML tags fourth (covers <c>&lt;script&gt;</c>, <c>&lt;iframe&gt;</c>, etc.).</item>
///   <item>Demote H4-H6 last (line-anchored, idempotent).</item>
/// </list>
/// </para>
/// </remarks>
public static class MarkdownSubsetSanitizer
{
    // Bounded timeout (200ms) on each regex pass mitigates ReDoS exposure for
    // adversarial input; analyzer rule MA0009 enforces this default.
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(200);
    private const RegexOptions BaseRegexOptions =
        RegexOptions.Compiled | RegexOptions.ExplicitCapture;

    // H4-H6 demotion: lines starting with 4..6 '#' chars + space + heading text.
    // ExplicitCapture means the (?<text>.+) group below is the only capture.
    // Multiline so '^'/'$' anchor to line boundaries.
    private static readonly Regex HeadingDemoteRegex = new(
        @"^#{4,6}[ \t]+(?<text>.+)$",
        BaseRegexOptions | RegexOptions.Multiline,
        RegexTimeout);

    // Image: ![alt text](url) → "[Image: alt text]". The alt text may contain
    // spaces and basic punctuation; URL is captured but discarded. We do NOT
    // attempt to handle nested brackets — markdown parsers don't either.
    private static readonly Regex ImageRegex = new(
        @"!\[(?<alt>[^\]]*)\]\([^\)]*\)",
        BaseRegexOptions,
        RegexTimeout);

    // Raw HTML: simple tag matcher. Strips opening, closing, and self-closing
    // tags. Does NOT attempt to parse HTML — anything tag-shaped goes.
    private static readonly Regex HtmlTagRegex = new(
        @"<\/?[a-zA-Z][^>]*>",
        BaseRegexOptions,
        RegexTimeout);

    // Footnote definition: a line starting with '[^N]:' followed by content
    // (single-line; multi-line continuation rare in extracted KB content).
    private static readonly Regex FootnoteDefinitionRegex = new(
        @"^\[\^[^\]]+\]:[ \t]+.*$",
        BaseRegexOptions | RegexOptions.Multiline,
        RegexTimeout);

    // Footnote marker: inline reference like '[^N]' that appears within the body
    // text. Stripped after definitions are removed so the body reads clean.
    private static readonly Regex FootnoteMarkerRegex = new(
        @"\[\^[^\]]+\]",
        BaseRegexOptions,
        RegexTimeout);

    /// <summary>
    /// Sanitize markdown content to the spec-compliant subset.
    /// Empty / whitespace input is returned as-is.
    /// </summary>
    public static string Sanitize(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return content ?? string.Empty;
        }

        var step1 = FootnoteDefinitionRegex.Replace(content, string.Empty);
        var step2 = FootnoteMarkerRegex.Replace(step1, string.Empty);
        var step3 = ImageRegex.Replace(step2, m =>
        {
            var alt = m.Groups["alt"].Value;
            return string.IsNullOrWhiteSpace(alt)
                ? "[Image]"
                : $"[Image: {alt}]";
        });
        var step4 = HtmlTagRegex.Replace(step3, string.Empty);
        var step5 = HeadingDemoteRegex.Replace(step4, m => $"**{m.Groups["text"].Value}**");

        return step5;
    }
}
