namespace Api.BoundedContexts.Authentication.Domain.Constants;

/// <summary>
/// Single server-side source of truth for the current Terms of Service version
/// (#2954 F1). The ToS text lives in the frontend locales (it.json/en.json) and its
/// display date in apps/web/src/app/(public)/terms/page.tsx (lastUpdated). This
/// constant MUST be bumped in the same change whenever that text materially changes.
/// </summary>
public static class TermsVersion
{
    /// <summary>Current ToS version identifier (date-based; matches terms/page.tsx lastUpdated).</summary>
    public const string Current = "2026-07-15";
}
