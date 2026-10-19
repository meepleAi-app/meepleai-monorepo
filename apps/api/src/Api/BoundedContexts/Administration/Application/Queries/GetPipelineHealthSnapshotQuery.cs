using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Query with Result record
namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// #4512: everything <c>GET /api/v1/admin/kb/pipeline/health</c> reads, in one query, so the endpoint
/// sends this and nothing else. The endpoint still builds the answer's stages from it.
/// </summary>
internal sealed record GetPipelineHealthSnapshotQuery : IQuery<PipelineHealthSnapshot>;

/// <summary>
/// The four query results and the embedding metrics the pipeline health is built from.
/// <paramref name="EmbeddingMetrics"/> is <see cref="EmbeddingServiceMetricsDto.Unavailable"/> when
/// the embedding service gives no metrics.
/// </summary>
internal sealed record PipelineHealthSnapshot(
    IReadOnlyCollection<ServiceHealthStatus> ServiceHealth,
    Dictionary<string, StepDurationStats> StepDurations,
    PdfStorageHealthDto StorageHealth,
    PaginatedQueueResponse Queue,
    EmbeddingServiceMetricsDto EmbeddingMetrics);
