using System.Text.RegularExpressions;
using Api.BoundedContexts.SharedGameCatalog.Application.Exceptions;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing an ISO 639-1 language code, optionally
/// with an ISO 3166-1 regional suffix (e.g. <c>"it"</c>, <c>"en-GB"</c>).
/// Normalized to lower-case language + upper-case region.
/// Issue #2339 — sub-PR 1/3 Wave 1.
/// </summary>
public sealed record Locale
{
    private static readonly Regex IsoFormat = new(
        @"^[a-z]{2}(?:-[A-Z]{2})?$",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    public string Value { get; }

    private Locale(string value)
    {
        Value = value;
    }

    public static Locale Create(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidLocaleException("Locale cannot be empty");
        }

        var trimmed = raw.Trim();
        var normalized = trimmed.ToLowerInvariant();

        // Uppercase regional suffix when present: "it-it" -> "it-IT"
        if (normalized.Length == 5 && normalized[2] == '-')
        {
            normalized = string.Concat(
                normalized.AsSpan(0, 3),
                normalized.AsSpan(3).ToString().ToUpperInvariant());
        }

        if (!IsoFormat.IsMatch(normalized))
        {
            throw new InvalidLocaleException($"Invalid ISO 639-1 locale: {raw}");
        }

        return new Locale(normalized);
    }

    /// <summary>
    /// Canonical English locale. SharedGame.Title/Description always hold the EN copy;
    /// a translation row with this locale would be semantically redundant and is rejected
    /// by the SharedGameTranslation aggregate factory.
    /// </summary>
    public static readonly Locale CanonicalEn = new("en");

    public override string ToString() => Value;
}
