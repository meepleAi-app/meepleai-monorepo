using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handler for <see cref="GetEmbeddingServiceMetricsQuery"/>. The read itself — client, timeout,
/// parser — is <see cref="IEmbeddingServiceMetricsReader"/>'s, because the pipeline health reads the
/// same metrics alongside its own sends and must not go through the MediatR pipeline to do it (see
/// <c>GetPipelineHealthSnapshotQueryHandler</c>).
/// </summary>
internal sealed class GetEmbeddingServiceMetricsQueryHandler
    : IQueryHandler<GetEmbeddingServiceMetricsQuery, EmbeddingServiceMetricsDto>
{
    private readonly IEmbeddingServiceMetricsReader _reader;

    public GetEmbeddingServiceMetricsQueryHandler(IEmbeddingServiceMetricsReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    public Task<EmbeddingServiceMetricsDto> Handle(
        GetEmbeddingServiceMetricsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _reader.ReadAsync(cancellationToken);
    }
}
