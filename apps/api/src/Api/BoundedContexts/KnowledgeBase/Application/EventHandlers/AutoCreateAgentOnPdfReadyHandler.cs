using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;

/// <summary>
/// Automatically creates an AI agent when a PDF finishes processing (state == Ready).
/// Only triggers for admin-priority PDFs to avoid auto-creating agents for all user uploads.
/// Uses the first active system definition with "Balanced" strategy as defaults.
/// </summary>
internal sealed class AutoCreateAgentOnPdfReadyHandler : INotificationHandler<PdfStateChangedEvent>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IAgentDefinitionRepository _definitionRepo;
    private readonly IMediator _mediator;
    private readonly ILogger<AutoCreateAgentOnPdfReadyHandler> _logger;

    public AutoCreateAgentOnPdfReadyHandler(
        MeepleAiDbContext dbContext,
        IAgentDefinitionRepository definitionRepo,
        IMediator mediator,
        ILogger<AutoCreateAgentOnPdfReadyHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _definitionRepo = definitionRepo ?? throw new ArgumentNullException(nameof(definitionRepo));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
                .Select(p => new { p.SharedGameId, p.PrivateGameId, p.ProcessingPriority })
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

            // Resolve game id from PDF (private > shared > empty)
            var resolvedGameId = pdfEntity.PrivateGameId ?? pdfEntity.SharedGameId ?? Guid.Empty;

            // Get first active system definition
            var definitions = await _definitionRepo.GetAllAsync(cancellationToken).ConfigureAwait(false);
            var defaultDefinition = definitions.FirstOrDefault(d => d.IsSystemDefined && d.IsActive);

            if (defaultDefinition == null)
            {
                _logger.LogWarning(
                    "AutoCreateAgent: No active system definition found. Cannot auto-create agent for game {GameId}",
                    resolvedGameId);
                return;
            }

            // Look up the user's tier and role for quota enforcement (Issue #4944)
            var userInfo = await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.Id == notification.UploadedByUserId)
                .Select(u => new { u.Tier, u.Role })
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (userInfo == null)
            {
                _logger.LogWarning(
                    "AutoCreateAgent: User {UserId} not found in database. Cannot determine tier/role for quota enforcement. Skipping auto-agent creation for game {GameId}",
                    notification.UploadedByUserId,
                    resolvedGameId);
                return;
            }

            // Create agent with defaults
            var command = new CreateGameAgentCommand(
                GameId: resolvedGameId,
                AgentDefinitionId: defaultDefinition.Id,
                StrategyName: "Balanced",
                StrategyParameters: null,
                UserId: notification.UploadedByUserId,
                UserTier: userInfo.Tier,
                UserRole: userInfo.Role);

            var result = await _mediator.Send(command, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "AutoCreateAgent: Agent auto-created for game {GameId} (PDF {PdfId}), library entry {EntryId}",
                resolvedGameId,
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

            // Issue #902 SG1: emit explicit AGENT_CREATION_FAILED notification (no silent swallow).
            // Downstream consumers (UserNotifications BC) can surface this to the user.
            var errorCode = ex switch
            {
                Api.Middleware.Exceptions.TierQuotaExceededException => "AGENT_SLOT_QUOTA_EXCEEDED",
                Api.Middleware.Exceptions.TierFeatureLockedException => "TIER_FEATURE_LOCKED",
                _ => "AGENT_CREATION_FAILED"
            };

            try
            {
                // Resolve gameId again (defensive — the original lookup may have failed before reaching this point)
                var gameIdForEvent = await _dbContext.PdfDocuments
                    .AsNoTracking()
                    .Where(p => p.Id == notification.PdfDocumentId)
                    .Select(p => (Guid?)(p.PrivateGameId ?? p.SharedGameId ?? Guid.Empty))
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false) ?? Guid.Empty;

                await _mediator.Publish(
                    new AutoAgentCreationFailedEvent(
                        PdfDocumentId: notification.PdfDocumentId,
                        GameId: gameIdForEvent,
                        UserId: notification.UploadedByUserId,
                        ErrorCode: errorCode,
                        Reason: ex.Message),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception publishEx)
            {
                // Notification publish itself failed — log but never re-throw (pipeline stability).
                _logger.LogError(
                    publishEx,
                    "AutoCreateAgent: Failed to publish AutoAgentCreationFailedEvent for PDF {PdfId}",
                    notification.PdfDocumentId);
            }
        }
#pragma warning restore CA1031
    }
}
