using Api.BoundedContexts.KnowledgeBase.Application.Queries;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// #4512: reads the embedding service's Prometheus metrics — the one parser and the one timeout.
/// Used by <see cref="GetEmbeddingServiceMetricsQueryHandler"/> and, outside the MediatR pipeline,
/// by the pipeline health handler.
/// <para>
/// 🔴 Implementations must stay plain HTTP: the pipeline health handler starts this read while it
/// sends its queries over the request's <c>MeepleAiDbContext</c>, and EF Core forbids two operations
/// on one context at once (#4059). A dependency on the context, on <c>IMediator</c>, or on anything
/// that uses them, breaks that handler.
/// </para>
/// </summary>
internal interface IEmbeddingServiceMetricsReader
{
    /// <summary>
    /// The metrics, or <see cref="EmbeddingServiceMetricsDto.Unavailable"/> when the service does not
    /// give them. Throws <see cref="OperationCanceledException"/> only when
    /// <paramref name="cancellationToken"/> is cancelled: the caller gave up, the service did not fail.
    /// </summary>
    Task<EmbeddingServiceMetricsDto> ReadAsync(CancellationToken cancellationToken);
}
