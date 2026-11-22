using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for approving share requests.
/// </summary>
internal sealed class ApproveShareRequestCommandHandler : ICommandHandler<ApproveShareRequestCommand, ApproveShareRequestResponse>
{
    private readonly IShareRequestRepository _shareRequestRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ApproveShareRequestCommandHandler> _logger;

    public ApproveShareRequestCommandHandler(
        IShareRequestRepository shareRequestRepository,
        IUnitOfWork unitOfWork,
        ILogger<ApproveShareRequestCommandHandler> logger)
    {
        _shareRequestRepository = shareRequestRepository ?? throw new ArgumentNullException(nameof(shareRequestRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ApproveShareRequestResponse> Handle(
        ApproveShareRequestCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Approving share request {ShareRequestId} by admin {AdminId}",
            command.ShareRequestId, command.AdminId);

        // 1. Get share request for update (with tracking)
        var shareRequest = await _shareRequestRepository.GetByIdForUpdateAsync(
            command.ShareRequestId,
            cancellationToken).ConfigureAwait(false);

        if (shareRequest == null)
        {
            throw new NotFoundException("ShareRequest", command.ShareRequestId.ToString());
        }

        // #5074: a NewGameProposal has no shared game yet; approving it here would mark it approved without
        // creating one. It goes through approve-game-proposal (ApproveAsNew, ApproveAsVariant, MergeKnowledgeBase).
        // #5076: so does a CoverChange (UpdateCover): approved here, its pending cover never reached the game.
        if (shareRequest.ContributionType is ContributionType.NewGameProposal or ContributionType.CoverChange)
        {
            throw new ConflictException(
                $"ShareRequest {shareRequest.Id} is a {shareRequest.ContributionType}: approve it with approve-game-proposal");
        }

        // #5076, decision of 2026-11-19: approval cannot copy attached documents yet. Nothing copies them for this
        // command: ApproveGameProposal copies a proposal's in its own command, and the event handler meant to copy
        // these (ShareRequestApprovedDocumentHandler) is gone, because it copied a proposal's a second time (#5078).
        // Approved here, the request would close with its documents left out, and nothing recovers them later: it
        // waits instead (#5109).
        if (shareRequest.AttachedDocuments.Count > 0)
        {
            throw new ConflictException(
                $"ShareRequest {shareRequest.Id} has {shareRequest.AttachedDocuments.Count} attached documents: approval cannot copy them to the catalog yet");
        }

        // 2. Approve the request (domain validates state and raises events)
        shareRequest.Approve(
            command.AdminId,
            command.TargetSharedGameId,
            command.AdminNotes);

        // 3. Update repository
        _shareRequestRepository.Update(shareRequest);

        // 4. Persist changes
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Share request {ShareRequestId} approved successfully. Target game: {TargetSharedGameId}",
            shareRequest.Id, shareRequest.TargetSharedGameId);

        // ShareRequestApprovedEvent: the repository collects it and SaveChangesAsync hands it to its handlers (#5078).

        return new ApproveShareRequestResponse(
            shareRequest.Id,
            shareRequest.Status,
            shareRequest.TargetSharedGameId,
            shareRequest.ResolvedAt!.Value);
    }
}
