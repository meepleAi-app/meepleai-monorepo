using System.Diagnostics.CodeAnalysis;

namespace Api.Infrastructure.Persistence;

/// <summary>
/// Cuts a text to the length of the <c>varchar</c> column it is written to (#5156). Postgres refuses a longer value
/// (22001) rather than cutting it, and for an audit row that refusal loses the row. The cut keeps the start and ends
/// with an ellipsis, so it is visibly a cut, as #4566 did for the details of a domain event. A null stays null.
/// </summary>
internal static class ColumnText
{
    private const string Ellipsis = "...";

    [return: NotNullIfNotNull(nameof(value))]
    public static string? Fit(string? value, int maxLength)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxLength, Ellipsis.Length);

        if (value is null || value.Length <= maxLength)
        {
            return value;
        }

        var kept = maxLength - Ellipsis.Length;

        // Never half of a character outside the BMP: a lone surrogate is not valid UTF-8.
        if (char.IsHighSurrogate(value[kept - 1]))
        {
            kept--;
        }

        return string.Concat(value.AsSpan(0, kept), Ellipsis);
    }
}
