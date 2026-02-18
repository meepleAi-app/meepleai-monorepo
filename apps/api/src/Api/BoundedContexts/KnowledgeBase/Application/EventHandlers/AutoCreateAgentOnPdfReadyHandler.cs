using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;

/// <summary>
/// Automatically creates an AI agent when a PDF finishes processing (state == Ready).
/// Only triggers for admin-priority PDFs to avoid auto-creating agents for all user uploads.
/// Uses the first approved typology with "Balanced" strategy as defaults.
/// </summary>
internal sealed class AutoCreateAgentOnPdfReadyHandler : INotificationHandler<PdfStateChangedEvent>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IAgentTypologyRepository _typologyRepo;
    private readonly IMediator _mediator;
    private readonly ILogger<AutoCreateAgentOnPdfReadyHandler> _logger;

    public AutoCreateAgentOnPdfReadyHandler(
        MeepleAiDbContext dbContext,
        IAgentTypologyRepository typologyRepo,
        IMediator mediator,
        ILogger<AutoCreateAgentOnPdfReadyHandler> logger)
    {
        _dbContext = dbContext;
        _typologyRepo = typologyRepo;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Handle(PdfStateChangedEvent notification, CancellationToken cancellationToken)
    {
        // Only trigger on Ready state
        if (notification.NewState != PdfProcessingState.Ready)
            return;

        try
        {
            // Get the PDF entity to check priority (ProcessingPriority is on EF entity, not domain)
            var pdfEntity = await _dbContext.PdfDocuments
                .Where(p => p.Id == notification.PdfDocumentId)
                .Select(p => new { p.GameId, p.ProcessingPriority })
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (pdfEntity == null)
            {
                _logger.LogWarning(
                    "AutoCreateAgent: PDF {PdfId} not found, skipping auto-agent creation",
                    notification.PdfDocumentId);
                return;
            }

            // Only auto-create for admin-priority PDFs (wizard flow)
            if (!string.Equals(pdfEntity.ProcessingPriority, "Admin", StringComparison.Ordinal))
            {
                _logger.LogDebug(
                    "AutoCreateAgent: PDF {PdfId} is not admin priority, skipping",
                    notification.PdfDocumentId);
                return;
            }

            // Get first approved typology
            var typologies = await _typologyRepo.GetAllAsync(cancellationToken).ConfigureAwait(false);
            var defaultTypology = typologies.FirstOrDefault(t => t.Status == KnowledgeBase.Domain.ValueObjects.TypologyStatus.Approved);

            if (defaultTypology == null)
            {
                _logger.LogWarning(
                    "AutoCreateAgent: No approved typology found. Cannot auto-create agent for game {GameId}",
                    pdfEntity.GameId);
                return;
            }

            // Create agent with defaults
            var command = new CreateGameAgentCommand(
                GameId: pdfEntity.GameId,
                TypologyId: defaultTypology.Id,
                StrategyName: "Balanced",
                StrategyParameters: null,
                UserId: notification.UploadedByUserId);

            var result = await _mediator.Send(command, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "AutoCreateAgent: Agent auto-created for game {GameId} (PDF {PdfId}), library entry {EntryId}",
                pdfEntity.GameId,
                notification.PdfDocumentId,
                result.LibraryEntryId);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            // Log but don't throw - auto-creation failure shouldn't break the pipeline
            _logger.LogError(
                ex,
                "AutoCreateAgent: Failed to auto-create agent for PDF {PdfId}. Error: {Message}",
                notification.PdfDocumentId,
                ex.Message);
        }
#pragma warning restore CA1031
    }
}
