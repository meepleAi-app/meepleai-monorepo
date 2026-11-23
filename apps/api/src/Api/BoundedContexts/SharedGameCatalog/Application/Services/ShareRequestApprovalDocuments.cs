using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Middleware.Exceptions;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// #5109, decision of 2026-11-23: approve-share-request and bulk-approve copy the attached documents of a request
/// into the shared game it contributes to (<see cref="ShareRequest.ApprovalDestinationId"/>). The destination is the
/// request's, never the HTTP call's: a <c>TargetSharedGameId</c> that names another game is refused, as
/// approve-game-proposal refuses one (its I3 guard). Bulk-approve carries one <c>TargetSharedGameId</c> for the
/// whole batch, so honouring it would pour the documents of different games into one.
/// </summary>
internal static class ShareRequestApprovalDocuments
{
    /// <summary>The shared game the approval contributes to.</summary>
    /// <exception cref="ConflictException">
    /// The request has no destination here, or <paramref name="requestedTargetId"/> names another game.
    /// </exception>
    public static Guid DestinationOf(ShareRequest shareRequest, Guid? requestedTargetId)
    {
        ArgumentNullException.ThrowIfNull(shareRequest);

        var destination = shareRequest.ApprovalDestinationId
            ?? throw new ConflictException(
                $"ShareRequest {shareRequest.Id} is a {shareRequest.ContributionType} with no shared game to contribute to");

        if (requestedTargetId is { } requested && requested != destination)
        {
            throw new ConflictException(
                $"ShareRequest {shareRequest.Id} contributes to SharedGame {destination}: TargetSharedGameId {requested} does not match");
        }

        return destination;
    }

    /// <summary>
    /// Copies the attached documents into <paramref name="destinationId"/>, once. The copies join the caller's unit of
    /// work: they are saved with the approval or not at all. Call it after <see cref="ShareRequest.Approve"/>, which
    /// can still refuse, so that a refused approval leaves no copied file in storage.
    /// </summary>
    /// <returns>The number of attached documents handed to the copy.</returns>
    public static async Task<int> CopyAttachedAsync(
        IShareRequestDocumentService documentService,
        ShareRequest shareRequest,
        Guid destinationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documentService);
        ArgumentNullException.ThrowIfNull(shareRequest);

        var documentIds = shareRequest.AttachedDocuments.Select(d => d.DocumentId).ToList();
        if (documentIds.Count == 0)
            return 0;

        await documentService
            .CopyDocumentsToSharedGame(documentIds, destinationId, shareRequest.UserId, cancellationToken)
            .ConfigureAwait(false);
        return documentIds.Count;
    }
}
