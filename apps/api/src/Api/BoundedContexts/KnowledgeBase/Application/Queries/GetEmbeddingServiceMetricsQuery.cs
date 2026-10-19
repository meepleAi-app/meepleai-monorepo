using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Query with Result record
namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// #4512: the embedding service's Prometheus counters, for <c>GET /admin/embedding/metrics</c>.
/// That endpoint and <c>/admin/kb/pipeline/health</c> used to read them each on its own, with an
/// <c>IHttpClientFactory</c> injected in the endpoint, two copies of the parser and two timeouts.
/// Both now go through <see cref="Services.IEmbeddingServiceMetricsReader"/>.
/// </summary>
internal sealed record GetEmbeddingServiceMetricsQuery : IQuery<EmbeddingServiceMetricsDto>;

/// <summary>
/// The counters the embedding service exposes on <c>/metrics</c> (<c>apps/embedding-service/main.py</c>),
/// and two values derived from them. All zeros — <see cref="Unavailable"/> — when the service does
/// not answer, answers with an error, or has not served a request yet: the web validates exactly
/// these six numbers (<c>EmbeddingServiceMetricsSchema</c>), so "unavailable" has no field of its
/// own.
/// </summary>
/// <param name="RequestsTotal"><c>embed_requests_total</c>.</param>
/// <param name="FailuresTotal"><c>embed_failures_total</c>.</param>
/// <param name="DurationMsSum"><c>embed_duration_ms_sum</c>, rounded to two decimals.</param>
/// <param name="TotalCharsSum"><c>embed_total_chars_sum</c>, rounded to a whole number.</param>
/// <param name="AvgDurationMs">Duration sum over requests, rounded to two decimals; 0 with no requests.</param>
/// <param name="FailureRate">Failures over requests, as a percentage rounded to two decimals; 0 with no requests.</param>
internal sealed record EmbeddingServiceMetricsDto(
    long RequestsTotal,
    long FailuresTotal,
    double DurationMsSum,
    double TotalCharsSum,
    double AvgDurationMs,
    double FailureRate)
{
    /// <summary>What the admin pages show when the service gives no metrics.</summary>
    internal static EmbeddingServiceMetricsDto Unavailable { get; } = new(0, 0, 0, 0, 0, 0);
}
