using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
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

        // Domain event ShareRequestApprovedEvent is dispatched automatically

        return new ApproveShareRequestResponse(
            shareRequest.Id,
            shareRequest.Status,
            shareRequest.TargetSharedGameId,
            shareRequest.ResolvedAt!.Value);
    }
}
