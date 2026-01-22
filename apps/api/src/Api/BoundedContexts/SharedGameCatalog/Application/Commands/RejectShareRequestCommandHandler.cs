using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for rejecting share requests.
/// </summary>
internal sealed class RejectShareRequestCommandHandler : ICommandHandler<RejectShareRequestCommand, RejectShareRequestResponse>
{
    private readonly IShareRequestRepository _shareRequestRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RejectShareRequestCommandHandler> _logger;

    public RejectShareRequestCommandHandler(
        IShareRequestRepository shareRequestRepository,
        IUnitOfWork unitOfWork,
        ILogger<RejectShareRequestCommandHandler> logger)
    {
        _shareRequestRepository = shareRequestRepository ?? throw new ArgumentNullException(nameof(shareRequestRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RejectShareRequestResponse> Handle(
        RejectShareRequestCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Rejecting share request {ShareRequestId} by admin {AdminId}",
            command.ShareRequestId, command.AdminId);

        // 1. Get share request for update (with tracking)
        var shareRequest = await _shareRequestRepository.GetByIdForUpdateAsync(
            command.ShareRequestId,
            cancellationToken).ConfigureAwait(false);

        if (shareRequest == null)
        {
            throw new NotFoundException("ShareRequest", command.ShareRequestId.ToString());
        }

        // 2. Reject the request (domain validates state and raises events)
        shareRequest.Reject(command.AdminId, command.Reason);

        // 3. Update repository
        _shareRequestRepository.Update(shareRequest);

        // 4. Persist changes
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Share request {ShareRequestId} rejected. Reason: {Reason}",
            shareRequest.Id, command.Reason);

        // Domain event ShareRequestRejectedEvent is dispatched automatically

        return new RejectShareRequestResponse(
            shareRequest.Id,
            shareRequest.Status,
            shareRequest.ResolvedAt!.Value);
    }
}
