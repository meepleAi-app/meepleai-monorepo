using System.Runtime.CompilerServices;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Handler for StreamPdfProgressQuery.
/// Implements Server-Sent Events for real-time PDF processing progress updates.
/// Subscribes to PdfProgressStreamService for push-based updates with heartbeat.
/// Issue #4209: SSE Progress Stream for Public PDFs
/// </summary>
internal sealed class StreamPdfProgressQueryHandler
    : IStreamingQueryHandler<StreamPdfProgressQuery, ProcessingProgressJson>
{
    private readonly IPdfDocumentRepository _pdfRepository;
    private readonly IPdfProgressStreamService _progressService;
    private readonly ILogger<StreamPdfProgressQueryHandler> _logger;

    public StreamPdfProgressQueryHandler(
        IPdfDocumentRepository pdfRepository,
        IPdfProgressStreamService progressService,
        ILogger<StreamPdfProgressQueryHandler> logger)
    {
        _pdfRepository = pdfRepository ?? throw new ArgumentNullException(nameof(pdfRepository));
        _progressService = progressService ?? throw new ArgumentNullException(nameof(progressService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

#pragma warning disable S4456 // Standard MediatR streaming pattern
    public async IAsyncEnumerable<ProcessingProgressJson> Handle(
        StreamPdfProgressQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
#pragma warning restore S4456
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation("Starting SSE progress stream for PDF {PdfId}", query.PdfId);

        // Authorization check: verify ownership or admin access
        var pdf = await _pdfRepository.GetByIdAsync(query.PdfId, cancellationToken).ConfigureAwait(false);
        if (pdf == null)
        {
            _logger.LogWarning("PDF {PdfId} not found in progress stream", query.PdfId);
            yield break;
        }

        if (pdf.UploadedByUserId != query.UserId && !query.IsAdmin)
        {
            _logger.LogWarning(
                "User {UserId} denied access to stream PDF {PdfId} progress (owner: {OwnerId})",
                query.UserId,
                query.PdfId,
                pdf.UploadedByUserId);
            yield break;
        }

        // Subscribe to progress stream
        await foreach (var progress in _progressService.SubscribeToProgress(query.PdfId, cancellationToken).ConfigureAwait(false))
        {
            yield return progress;
        }

        _logger.LogInformation("SSE progress stream ended for PDF {PdfId}", query.PdfId);
    }
}
