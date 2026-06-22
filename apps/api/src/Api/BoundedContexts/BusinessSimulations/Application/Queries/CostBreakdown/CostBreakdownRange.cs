namespace Api.BoundedContexts.BusinessSimulations.Application.Queries.CostBreakdown;

/// <summary>
/// Supported time windows for the admin cost breakdown queries
/// (Issue #1838 SP5 F4-C5). Tied to the FE range select (7d / 30d / 90d / 1y).
/// </summary>
internal enum CostBreakdownRange
{
    /// <summary>Last 7 days.</summary>
    SevenDays = 7,

    /// <summary>Last 30 days (default).</summary>
    ThirtyDays = 30,

    /// <summary>Last 90 days.</summary>
    NinetyDays = 90,

    /// <summary>Last 365 days.</summary>
    OneYear = 365,
}

internal static class CostBreakdownRangeExtensions
{
    /// <summary>
    /// Parses the wire value sent by the FE (<c>7d</c> / <c>30d</c> / <c>90d</c> / <c>1y</c>)
    /// into a <see cref="CostBreakdownRange"/>. Unknown / empty values fall back
    /// to <see cref="CostBreakdownRange.ThirtyDays"/> (the spec default).
    /// </summary>
    public static CostBreakdownRange FromWireValue(string? wire)
    {
        if (string.IsNullOrWhiteSpace(wire)) return CostBreakdownRange.ThirtyDays;

        return wire.ToLowerInvariant() switch
        {
            "7d" => CostBreakdownRange.SevenDays,
            "30d" => CostBreakdownRange.ThirtyDays,
            "90d" => CostBreakdownRange.NinetyDays,
            "1y" or "365d" => CostBreakdownRange.OneYear,
            _ => CostBreakdownRange.ThirtyDays,
        };
    }

    public static string ToWireValue(this CostBreakdownRange range) => range switch
    {
        CostBreakdownRange.SevenDays => "7d",
        CostBreakdownRange.ThirtyDays => "30d",
        CostBreakdownRange.NinetyDays => "90d",
        CostBreakdownRange.OneYear => "1y",
        _ => "30d",
    };

    /// <summary>Number of days in the window — used to compute the <c>fromDate</c>.</summary>
    public static int Days(this CostBreakdownRange range) => (int)range;
}
