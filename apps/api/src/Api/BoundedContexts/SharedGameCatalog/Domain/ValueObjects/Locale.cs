using System.Diagnostics.CodeAnalysis;
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

    /// <summary>
    /// Throwing constructor. Use <see cref="TryCreate"/> when the caller wants
    /// to avoid exception-as-control-flow (FluentValidation predicates, batch
    /// imports). Issue #2399.
    /// </summary>
    /// <exception cref="InvalidLocaleException">
    /// Thrown when <paramref name="raw"/> is null, empty, whitespace, or does
    /// not match the canonical ISO 639-1 (+ optional 3166-1 region) shape.
    /// </exception>
    public static Locale Create(string raw)
    {
        if (TryCreate(raw, out var locale))
        {
            return locale;
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidLocaleException("Locale cannot be empty");
        }

        throw new InvalidLocaleException($"Invalid ISO 639-1 locale: {raw}");
    }

    /// <summary>
    /// Non-throwing parse. Returns <c>true</c> with the normalised value when
    /// <paramref name="raw"/> matches the canonical ISO 639-1 (+ optional
    /// 3166-1 region) shape, otherwise <c>false</c> with <paramref name="value"/>
    /// set to <c>null</c>. Issue #2399 — replaces the
    /// <c>try { Create(...); } catch { … }</c> pattern in
    /// <see cref="Api.BoundedContexts.SharedGameCatalog.Application.Services.SharedTranslationValidationRules.BeValidLocale"/>
    /// and other parser callers so the happy path costs zero exception
    /// allocations.
    /// </summary>
    public static bool TryCreate(string? raw, [NotNullWhen(true)] out Locale? value)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            value = null;
            return false;
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
            value = null;
            return false;
        }

        value = new Locale(normalized);
        return true;
    }

    /// <summary>
    /// Canonical English locale. SharedGame.Title/Description always hold the EN copy;
    /// a translation row with this locale would be semantically redundant and is rejected
    /// by the SharedGameTranslation aggregate factory.
    /// </summary>
    public static readonly Locale CanonicalEn = new("en");

    public override string ToString() => Value;
}
