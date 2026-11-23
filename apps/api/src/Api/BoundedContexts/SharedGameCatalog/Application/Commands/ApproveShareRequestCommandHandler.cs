using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
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
    private readonly IShareRequestDocumentService _documentService;
    private readonly IContributorRepository _contributorRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ApproveShareRequestCommandHandler> _logger;

    public ApproveShareRequestCommandHandler(
        IShareRequestRepository shareRequestRepository,
        IShareRequestDocumentService documentService,
        IContributorRepository contributorRepository,
        IUnitOfWork unitOfWork,
        ILogger<ApproveShareRequestCommandHandler> logger)
    {
        _shareRequestRepository = shareRequestRepository ?? throw new ArgumentNullException(nameof(shareRequestRepository));
        _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
        _contributorRepository = contributorRepository ?? throw new ArgumentNullException(nameof(contributorRepository));
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

        // #5109, decision of 2026-11-23: the attached documents go to the game the request contributes to, and a
        // TargetSharedGameId naming another game is refused (409). This command copies them itself, as
        // ApproveGameProposal does for a proposal: no event handler does (#5078 removed the one that copied twice).
        var destination = ShareRequestApprovalDocuments.DestinationOf(shareRequest, command.TargetSharedGameId);

        // 2. Approve the request (domain validates state and raises events)
        shareRequest.Approve(
            command.AdminId,
            command.TargetSharedGameId,
            command.AdminNotes);

        // After Approve, which can still refuse: a refused approval leaves no copied file behind. The rows of the
        // copies are saved together with the approval; a copy that fails refuses it (409).
        var copied = await ShareRequestApprovalDocuments
            .CopyAttachedAsync(_documentService, shareRequest, destination, cancellationToken)
            .ConfigureAwait(false);
        if (copied.Count > 0)
        {
            _logger.LogInformation(
                "Copied {Count} documents from ShareRequest {ShareRequestId} to SharedGame {SharedGameId}",
                copied.Count, shareRequest.Id, destination);
        }

        // #5139: the author becomes a contributor of the game, saved with the approval.
        var contributions = new ShareRequestApprovalContributions(_contributorRepository);
        await contributions.RecordAsync(
                shareRequest,
                destination,
                ShareRequestApprovalContributions.KindOf(shareRequest),
                copied,
                cancellationToken)
            .ConfigureAwait(false);
        await contributions.AddToUnitOfWorkAsync(cancellationToken).ConfigureAwait(false);

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
