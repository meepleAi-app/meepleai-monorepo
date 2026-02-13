using System.Runtime.CompilerServices;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Handlers;

/// <summary>
/// Handler for StreamPdfStatusQuery.
/// Implements Server-Sent Events for real-time PDF processing status updates.
/// Polls database every 2 seconds and streams state changes until terminal state reached.
/// Issue #4218: Real-Time Updates (SSE + Polling)
/// </summary>
internal sealed class StreamPdfStatusQueryHandler
    : IStreamingQueryHandler<StreamPdfStatusQuery, PdfStatusEventDto>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<StreamPdfStatusQueryHandler> _logger;

    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(2);

    public StreamPdfStatusQueryHandler(
        MeepleAiDbContext dbContext,
        ILogger<StreamPdfStatusQueryHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

#pragma warning disable S4456 // Standard MediatR streaming pattern
    public async IAsyncEnumerable<PdfStatusEventDto> Handle(
        StreamPdfStatusQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
#pragma warning restore S4456
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation("Starting SSE stream for PDF {PdfId}", query.PdfId);

        PdfProcessingState? previousState = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            // Poll database for current state
            var pdfEntity = await _dbContext.PdfDocuments
                .Where(p => p.Id == query.PdfId)
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (pdfEntity == null)
            {
                _logger.LogWarning("PDF {PdfId} not found in stream", query.PdfId);
                yield break;
            }

            // Authorization check: verify ownership or admin access
            if (pdfEntity.UploadedByUserId != query.UserId && !query.IsAdmin)
            {
                _logger.LogWarning(
                    "User {UserId} denied access to stream PDF {PdfId} status (owner: {OwnerId})",
                    query.UserId,
                    query.PdfId,
                    pdfEntity.UploadedByUserId);
                yield break;
            }

            // Parse processing state from string to enum
            if (!Enum.TryParse<PdfProcessingState>(pdfEntity.ProcessingState, out var currentState))
            {
                _logger.LogError("Invalid processing state value {State} for PDF {PdfId}", pdfEntity.ProcessingState, query.PdfId);
                yield break;
            }

            // Parse progress JSON
            var progress = string.IsNullOrEmpty(pdfEntity.ProcessingProgressJson)
                ? null
                : System.Text.Json.JsonSerializer.Deserialize<Api.Models.ProcessingProgress>(
                    pdfEntity.ProcessingProgressJson);

            // Create status event
            var statusEvent = new PdfStatusEventDto(
                DocumentId: pdfEntity.Id,
                State: currentState,
                Progress: progress?.PercentComplete ?? 0,
                Eta: progress?.EstimatedTimeRemaining?.ToString("c"),
                Timestamp: DateTime.UtcNow,
                ErrorMessage: pdfEntity.ProcessingError
            );

            // Emit event (always send on first poll, then only on state changes)
            bool stateChanged = previousState.HasValue && previousState.Value != currentState;
            bool isFirstPoll = !previousState.HasValue;

            if (isFirstPoll || stateChanged)
            {
                if (stateChanged)
                {
                    _logger.LogDebug(
                        "PDF {PdfId} state changed: {PreviousState} → {NewState}",
                        query.PdfId,
                        previousState,
                        currentState);
                }

                yield return statusEvent;
                previousState = currentState;
            }

            // Stop streaming if terminal state reached
            if (IsTerminalState(currentState))
            {
                _logger.LogInformation(
                    "PDF {PdfId} reached terminal state {State}, ending stream",
                    query.PdfId,
                    currentState);
                yield break;
            }

            // Wait before next poll
            try
            {
                await Task.Delay(PollingInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogInformation(ex, "SSE stream cancelled for PDF {PdfId}", query.PdfId);
                yield break;
            }
        }
    }

    private static bool IsTerminalState(PdfProcessingState state)
    {
        return state is PdfProcessingState.Ready or PdfProcessingState.Failed;
    }
}
