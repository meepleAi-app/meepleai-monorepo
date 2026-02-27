using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Application.EventHandlers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;

/// <summary>
/// Handles VectorDocumentIndexedEvent to notify users when their PDF knowledge base is ready.
/// Issue #4942: Send multi-channel notification when RAG indexing completes.
/// Follows pattern from PdfNotificationEventHandler.
/// </summary>
internal sealed class VectorDocumentIndexedEventHandler : DomainEventHandlerBase<VectorDocumentIndexedEvent>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IVectorDocumentRepository _vectorDocRepo;
    private readonly IPdfDocumentRepository _pdfDocRepo;
    private readonly IUserRepository _userRepo;
    private readonly INotificationRepository _notificationRepo;
    private readonly INotificationPreferencesRepository _preferencesRepo;
    private readonly IMediator _mediator;
    private readonly IPushNotificationService _pushService;

    public VectorDocumentIndexedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<VectorDocumentIndexedEventHandler> logger,
        IVectorDocumentRepository vectorDocRepo,
        IPdfDocumentRepository pdfDocRepo,
        IUserRepository userRepo,
        INotificationRepository notificationRepo,
        INotificationPreferencesRepository preferencesRepo,
        IMediator mediator,
        IPushNotificationService pushService)
        : base(dbContext, logger)
    {
        _dbContext = dbContext;
        _vectorDocRepo = vectorDocRepo;
        _pdfDocRepo = pdfDocRepo;
        _userRepo = userRepo;
        _notificationRepo = notificationRepo;
        _preferencesRepo = preferencesRepo;
        _mediator = mediator;
        _pushService = pushService;
    }

    protected override async Task HandleEventAsync(
        VectorDocumentIndexedEvent domainEvent,
        CancellationToken cancellationToken)
    {
        // Retrieve the VectorDocument to get the PdfDocumentId
        var vectorDoc = await _vectorDocRepo.GetByIdAsync(domainEvent.DocumentId, cancellationToken).ConfigureAwait(false);
        if (vectorDoc == null)
        {
            Logger.LogWarning(
                "VectorDocumentIndexedEventHandler: VectorDocument {DocumentId} not found, skipping notification",
                domainEvent.DocumentId);
            return;
        }

        // Retrieve the PDF document to get UploadedByUserId and file name
        var pdfDoc = await _pdfDocRepo.GetByIdAsync(vectorDoc.PdfDocumentId, cancellationToken).ConfigureAwait(false);
        if (pdfDoc == null)
        {
            Logger.LogWarning(
                "VectorDocumentIndexedEventHandler: PdfDocument {PdfId} not found for VectorDocument {DocumentId}, skipping notification",
                vectorDoc.PdfDocumentId,
                domainEvent.DocumentId);
            return;
        }

        // Compensate: UploadPdfCommandHandler creates VectorDocument directly (bypassing Quartz pipeline)
        // and never sets ProcessingState=Ready. The domain state machine forbids Pending→Ready via
        // TransitionTo(), so we update the infrastructure entity directly — safe here because the
        // event fires only after indexing is confirmed complete.
        if (pdfDoc.ProcessingState != PdfProcessingState.Ready
            && pdfDoc.ProcessingState != PdfProcessingState.Failed)
        {
            var pdfEntity = await _dbContext.PdfDocuments
                .FirstOrDefaultAsync(p => p.Id == pdfDoc.Id, cancellationToken)
                .ConfigureAwait(false);

            if (pdfEntity != null)
            {
                pdfEntity.ProcessingState = "Ready";
                pdfEntity.ProcessingStatus = "completed";
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                Logger.LogInformation(
                    "Compensating update: set ProcessingState=Ready for PdfDocument {PdfId} (was {PrevState}) after vector indexing",
                    pdfDoc.Id,
                    pdfDoc.ProcessingState);
            }
        }

        var userId = pdfDoc.UploadedByUserId;

        // Fail-safe: if user has not configured notification preferences, skip silently
        var prefs = await _preferencesRepo.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (prefs == null)
        {
            Logger.LogWarning(
                "VectorDocumentIndexedEventHandler: No notification preferences for user {UserId}, skipping notification",
                userId);
            return;
        }

        var fileName = pdfDoc.FileName.Value;
        var agentLink = $"/library/games/{domainEvent.GameId}/agent";
        var chunkCount = domainEvent.ChunkCount;

        // In-App Notification
        if (prefs.InAppOnDocumentReady)
        {
            var notification = new Notification(
                id: Guid.NewGuid(),
                userId: userId,
                type: NotificationType.ProcessingJobCompleted,
                severity: NotificationSeverity.Success,
                title: "Knowledge Base pronta",
                message: $"'{fileName}' è stato indicizzato ({chunkCount} chunk). Puoi ora creare il tuo agente.",
                link: agentLink
            );
            await _notificationRepo.AddAsync(notification, cancellationToken).ConfigureAwait(false);
            Logger.LogInformation(
                "KB ready in-app notification created for user {UserId}, game {GameId}, chunks: {ChunkCount}",
                userId,
                domainEvent.GameId,
                chunkCount);
        }

        // Email Notification via Queue
        if (prefs.EmailOnDocumentReady)
        {
            var user = await _userRepo.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
            if (user != null)
            {
                try
                {
                    await _mediator.Send(new EnqueueEmailCommand(
                        UserId: userId,
                        To: user.Email,
                        Subject: $"La tua Knowledge Base è pronta: {fileName} - MeepleAI",
                        TemplateName: "kb_indexed",
                        UserName: user.DisplayName,
                        FileName: fileName,
                        DocumentUrl: agentLink
                    ), cancellationToken).ConfigureAwait(false);

                    Logger.LogInformation(
                        "KB ready email enqueued for user {UserId}",
                        userId);
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to enqueue KB ready email for user {UserId}", userId);
                }
#pragma warning restore CA1031
            }
        }

        // Push Notification
        if (prefs.PushOnDocumentReady && prefs.HasPushSubscription)
        {
            try
            {
                await _pushService.SendPushNotificationAsync(
                    prefs.PushEndpoint!,
                    prefs.PushP256dhKey!,
                    prefs.PushAuthKey!,
                    "Knowledge Base pronta",
                    $"'{fileName}' è stato indicizzato ({chunkCount} chunk). Crea il tuo agente!",
                    agentLink,
                    cancellationToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to send push notification for KB ready to user {UserId}", userId);
            }
#pragma warning restore CA1031
        }
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(VectorDocumentIndexedEvent domainEvent)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["Action"] = "VectorDocumentIndexed",
            ["DocumentId"] = domainEvent.DocumentId,
            ["GameId"] = domainEvent.GameId,
            ["ChunkCount"] = domainEvent.ChunkCount
        };
    }
}
