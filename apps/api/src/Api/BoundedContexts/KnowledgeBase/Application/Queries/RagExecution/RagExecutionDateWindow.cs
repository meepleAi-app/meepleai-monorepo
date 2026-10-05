namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.RagExecution;

/// <summary>
/// Normalizes the optional <c>dateFrom</c>/<c>dateTo</c> window that the two
/// <c>/api/v1/admin/rag-executions</c> endpoints accept as bare <c>YYYY-MM-DD</c>
/// query-string values.
/// </summary>
/// <remarks>
/// <para>
/// Two independent defects made that window unusable, and a single request showed both.
/// Measured 2026-10-04 against the local stack, as superadmin:
/// <c>?skip=0&amp;take=20</c> answered 200, while <c>?skip=0&amp;take=20&amp;dateFrom=2026-10-04&amp;dateTo=2026-10-04</c>
/// answered 400 — and so did <c>dateFrom=2026-10-03&amp;dateTo=2026-10-04</c>, so the failure was
/// never about the two dates being equal.
/// </para>
/// <para>
/// (1) Minimal-API binding of <c>DateTime?</c> yields <see cref="DateTimeKind.Unspecified"/>, which
/// Npgsql refuses to write to a <c>timestamptz</c> column. The thrown
/// <see cref="ArgumentException"/> (top frame
/// <c>Npgsql.Internal.Converters.DateTimeConverterResolver`1.Get</c>) is mapped by
/// <c>ApiExceptionHandlerMiddleware</c> to 400 "Invalid request parameters", which reads like a
/// rejected parameter but is a failed query. The identical class was already fixed once, in
/// <c>GetAgentMetricsQueryHandler</c> by a83f2b41f (#255, "add DateTimeKind.Utc to ToDateTime()
/// calls to fix Npgsql timestamptz conversion error"); it never reached these two handlers.
/// </para>
/// <para>
/// (2) A date-only upper bound binds to midnight, so the inclusive <c>CreatedAt &lt;= dateTo</c>
/// excludes the whole day it names. Fixing the Kind alone would turn the 400 into a 200 carrying
/// zero rows, leaving <c>/admin/agents/inspector</c> — whose default filter is
/// <c>dateFrom = dateTo = today</c> — exactly as empty. Reading a date-only end as the end of that
/// day is what <c>RagExecutionRepository.GetAggregatedMetricsAsync</c> already does for its
/// <see cref="DateOnly"/> bounds (<c>ToDateTime(TimeOnly.MaxValue)</c>).
/// </para>
/// </remarks>
internal static class RagExecutionDateWindow
{
    /// <summary>
    /// Inclusive lower bound, as a UTC instant. A date-only value already means that day's
    /// midnight, so only the kind needs fixing.
    /// </summary>
    internal static DateTime? StartInclusive(DateTime? dateFrom) =>
        dateFrom.HasValue ? AsUtc(dateFrom.Value) : null;

    /// <summary>
    /// Inclusive upper bound, as a UTC instant. A value with no time component names a day and
    /// therefore covers all of it; a caller that spells out a time keeps that instant.
    /// </summary>
    internal static DateTime? EndInclusive(DateTime? dateTo)
    {
        if (!dateTo.HasValue)
        {
            return null;
        }

        var utc = AsUtc(dateTo.Value);
        if (utc.TimeOfDay != TimeSpan.Zero)
        {
            return utc;
        }

        // Clamp on the last representable day: AddDays(1) there throws, and a 500 would be a worse
        // answer to dateTo=9999-12-31 than a bound Postgres accepts.
        return utc.Date >= DateTime.MaxValue.Date
            ? DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc)
            : utc.AddDays(1).AddTicks(-1);
    }

    /// <summary>
    /// Whether the normalized window is non-empty. Two equal dates are ordered: they name one whole
    /// day. A strictly inverted window (<c>dateFrom</c> after <c>dateTo</c>) is not, and callers use
    /// this to reject it instead of silently answering with no rows.
    /// </summary>
    internal static bool IsOrdered(DateTime? dateFrom, DateTime? dateTo)
    {
        var start = StartInclusive(dateFrom);
        var end = EndInclusive(dateTo);

        return !start.HasValue || !end.HasValue || end.Value >= start.Value;
    }

    private static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();
}
