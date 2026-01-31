using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;

/// <summary>
/// Event handler for cleaning up document associations when a share request is rejected.
/// Issue #2732: Cleanup orphaned documents on rejection.
/// </summary>
internal sealed class ShareRequestRejectedDocumentHandler : DomainEventHandlerBase<ShareRequestRejectedEvent>
{
    private readonly IShareRequestDocumentService _documentService;

    public ShareRequestRejectedDocumentHandler(
        MeepleAiDbContext dbContext,
        IShareRequestDocumentService documentService,
        ILogger<ShareRequestRejectedDocumentHandler> logger)
        : base(dbContext, logger)
    {
        _documentService = documentService;
    }

    protected override async Task HandleEventAsync(
        ShareRequestRejectedEvent domainEvent,
        CancellationToken cancellationToken)
    {
        try
        {
            // Cleanup document associations (documents stay in user library)
            await _documentService.CleanupOrphanedDocuments(
                domainEvent.ShareRequestId,
                cancellationToken).ConfigureAwait(false);

            Logger.LogInformation(
                "Cleaned up document associations for rejected share request {ShareRequestId}",
                domainEvent.ShareRequestId);
        }
        catch (Exception ex)
        {
            // Cleanup failure should NOT fail the rejection flow
            Logger.LogError(
                ex,
                "Failed to cleanup documents for share request {ShareRequestId}. Manual cleanup may be required.",
                domainEvent.ShareRequestId);
        }
    }

    protected override Guid? GetUserId(ShareRequestRejectedEvent domainEvent)
        => domainEvent.AdminId;

    protected override Dictionary<string, object?>? GetAuditMetadata(ShareRequestRejectedEvent domainEvent)
        => new(StringComparer.Ordinal)
        {
            ["ShareRequestId"] = domainEvent.ShareRequestId,
            ["Reason"] = domainEvent.Reason
        };
}
