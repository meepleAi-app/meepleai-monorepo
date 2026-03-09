using Api.Infrastructure;
using Api.SharedKernel.Application.IntegrationEvents;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Application.EventHandlers;

/// <summary>
/// Handles VectorDocumentReadyIntegrationEvent to update PDF document processing state.
/// Compensating update: sets ProcessingState=Ready when vector indexing completes
/// but the PDF state machine wasn't properly transitioned.
/// Issue #5237: Decoupled from KnowledgeBase — state update now within DocumentProcessing context.
/// </summary>
internal sealed class VectorDocumentReadyStateHandler
    : INotificationHandler<VectorDocumentReadyIntegrationEvent>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<VectorDocumentReadyStateHandler> _logger;

    public VectorDocumentReadyStateHandler(
        MeepleAiDbContext dbContext,
        ILogger<VectorDocumentReadyStateHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Handle(
        VectorDocumentReadyIntegrationEvent evt,
        CancellationToken cancellationToken)
    {
        // Only update if state is not already Ready or Failed
        if (string.Equals(evt.CurrentProcessingState, "Ready", StringComparison.Ordinal)
            || string.Equals(evt.CurrentProcessingState, "Failed", StringComparison.Ordinal))
        {
            return;
        }

        var pdfEntity = await _dbContext.PdfDocuments
            .FirstOrDefaultAsync(p => p.Id == evt.PdfDocumentId, cancellationToken)
            .ConfigureAwait(false);

        if (pdfEntity != null)
        {
            pdfEntity.ProcessingState = "Ready";
            pdfEntity.ProcessingStatus = "completed";
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Compensating update: set ProcessingState=Ready for PdfDocument {PdfId} (was {PrevState}) after vector indexing",
                evt.PdfDocumentId,
                evt.CurrentProcessingState);
        }
    }
}
