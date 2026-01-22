using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;

/// <summary>
/// Event handler for copying documents when a share request is approved.
/// Issue #2732: Document copying on approval.
/// </summary>
internal sealed class ShareRequestApprovedDocumentHandler : DomainEventHandlerBase<ShareRequestApprovedEvent>
{
    private readonly IShareRequestDocumentService _documentService;
    private readonly IShareRequestRepository _shareRequestRepo;

    public ShareRequestApprovedDocumentHandler(
        MeepleAiDbContext dbContext,
        IShareRequestDocumentService documentService,
        IShareRequestRepository shareRequestRepo,
        ILogger<ShareRequestApprovedDocumentHandler> logger)
        : base(dbContext, logger)
    {
        _documentService = documentService;
        _shareRequestRepo = shareRequestRepo;
    }

    protected override async Task HandleEventAsync(
        ShareRequestApprovedEvent domainEvent,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get share request with attached documents
            var shareRequest = await _shareRequestRepo.GetByIdAsync(
                domainEvent.ShareRequestId,
                cancellationToken).ConfigureAwait(false);

            if (shareRequest == null)
            {
                Logger.LogWarning(
                    "Share request {ShareRequestId} not found for document copying",
                    domainEvent.ShareRequestId);
                return;
            }

            // Check if there are documents to copy
            if (shareRequest.AttachedDocuments.Count == 0)
            {
                Logger.LogInformation(
                    "No documents attached to share request {ShareRequestId}, skipping copy",
                    domainEvent.ShareRequestId);
                return;
            }

            // Target shared game must exist for document copying
            if (!domainEvent.TargetSharedGameId.HasValue)
            {
                Logger.LogWarning(
                    "No target shared game ID for share request {ShareRequestId}, cannot copy documents",
                    domainEvent.ShareRequestId);
                return;
            }

            var documentIds = shareRequest.AttachedDocuments
                .Select(d => d.DocumentId)
                .ToList();

            // Copy documents to shared game storage
            var copiedDocumentIds = await _documentService.CopyDocumentsToSharedGame(
                documentIds,
                domainEvent.TargetSharedGameId.Value,
                shareRequest.UserId, // Contributor
                cancellationToken).ConfigureAwait(false);

            Logger.LogInformation(
                "Copied {Count} documents from share request {ShareRequestId} to shared game {SharedGameId}",
                copiedDocumentIds.Count,
                domainEvent.ShareRequestId,
                domainEvent.TargetSharedGameId.Value);
        }
        catch (Exception ex)
        {
            // Document copying failure should NOT fail the approval flow
            Logger.LogError(
                ex,
                "Failed to copy documents for share request {ShareRequestId}. Document assignment skipped.",
                domainEvent.ShareRequestId);
        }
    }

    protected override Guid? GetUserId(ShareRequestApprovedEvent domainEvent)
        => domainEvent.AdminId;

    protected override Dictionary<string, object?>? GetAuditMetadata(ShareRequestApprovedEvent domainEvent)
        => new(StringComparer.Ordinal)
        {
            ["ShareRequestId"] = domainEvent.ShareRequestId,
            ["TargetSharedGameId"] = domainEvent.TargetSharedGameId
        };
}
