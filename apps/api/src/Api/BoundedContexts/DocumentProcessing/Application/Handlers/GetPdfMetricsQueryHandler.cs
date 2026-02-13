using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Handlers;

/// <summary>
/// Handler for GetPdfMetricsQuery.
/// Retrieves processing metrics for performance tracking and ETA calculation.
/// Issue #4219: Duration metrics and ETA calculation.
/// </summary>
internal class GetPdfMetricsQueryHandler : IQueryHandler<GetPdfMetricsQuery, PdfMetricsDto>
{
    private readonly IPdfDocumentRepository _repository;
    private readonly ILogger<GetPdfMetricsQueryHandler> _logger;

    public GetPdfMetricsQueryHandler(
        IPdfDocumentRepository repository,
        ILogger<GetPdfMetricsQueryHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PdfMetricsDto> Handle(
        GetPdfMetricsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation(
            "Retrieving PDF metrics: DocumentId={DocumentId}",
            query.DocumentId);

        // Retrieve document with metrics
        var document = await _repository.GetByIdAsync(query.DocumentId, cancellationToken)
            .ConfigureAwait(false);

        if (document == null)
        {
            _logger.LogWarning("PDF document {DocumentId} not found", query.DocumentId);
            throw new NotFoundException($"PDF document {query.DocumentId} not found");
        }

        // Calculate state durations
        var stateDurations = CalculateStateDurations(document);

        // Build metrics response
        var metrics = new PdfMetricsDto(
            DocumentId: document.Id,
            CurrentState: document.ProcessingState,
            ProgressPercentage: document.ProgressPercentage,
            TotalDuration: document.TotalDuration,
            EstimatedTimeRemaining: document.EstimatedTimeRemaining,
            StateDurations: stateDurations,
            RetryCount: document.RetryCount,
            PageCount: document.PageCount
        );

        _logger.LogInformation(
            "PDF metrics retrieved: DocumentId={DocumentId}, State={State}, Progress={Progress}%",
            document.Id, document.ProcessingState, document.ProgressPercentage);

        return metrics;
    }

    /// <summary>
    /// Calculates duration spent in each processing state.
    /// Issue #4219: Per-state timing for detailed metrics.
    /// </summary>
    private static Dictionary<string, TimeSpan> CalculateStateDurations(
        Api.BoundedContexts.DocumentProcessing.Domain.Entities.PdfDocument document)
    {
        var durations = new Dictionary<string, TimeSpan>(StringComparer.Ordinal);

        // Calculate duration for each state based on start times
        if (document.UploadingStartedAt.HasValue)
        {
            var end = document.ExtractingStartedAt ?? document.ProcessedAt ?? DateTime.UtcNow;
            durations["Uploading"] = end - document.UploadingStartedAt.Value;
        }

        if (document.ExtractingStartedAt.HasValue)
        {
            var end = document.ChunkingStartedAt ?? document.ProcessedAt ?? DateTime.UtcNow;
            durations["Extracting"] = end - document.ExtractingStartedAt.Value;
        }

        if (document.ChunkingStartedAt.HasValue)
        {
            var end = document.EmbeddingStartedAt ?? document.ProcessedAt ?? DateTime.UtcNow;
            durations["Chunking"] = end - document.ChunkingStartedAt.Value;
        }

        if (document.EmbeddingStartedAt.HasValue)
        {
            var end = document.IndexingStartedAt ?? document.ProcessedAt ?? DateTime.UtcNow;
            durations["Embedding"] = end - document.EmbeddingStartedAt.Value;
        }

        if (document.IndexingStartedAt.HasValue)
        {
            var end = document.ProcessedAt ?? DateTime.UtcNow;
            durations["Indexing"] = end - document.IndexingStartedAt.Value;
        }

        return durations;
    }
}
