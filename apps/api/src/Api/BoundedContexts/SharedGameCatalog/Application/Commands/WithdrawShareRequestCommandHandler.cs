using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for withdrawing a share request.
/// Issue #2733: API Endpoints Utente per Share Requests
/// </summary>
internal sealed class WithdrawShareRequestCommandHandler : ICommandHandler<WithdrawShareRequestCommand>
{
    private readonly IShareRequestRepository _shareRequestRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<WithdrawShareRequestCommandHandler> _logger;

    public WithdrawShareRequestCommandHandler(
        IShareRequestRepository shareRequestRepository,
        IUnitOfWork unitOfWork,
        ILogger<WithdrawShareRequestCommandHandler> logger)
    {
        _shareRequestRepository = shareRequestRepository ?? throw new ArgumentNullException(nameof(shareRequestRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(
        WithdrawShareRequestCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Withdrawing share request {ShareRequestId} by user {UserId}",
            command.ShareRequestId, command.UserId);

        // 1. Get the share request with tracking for updates
        var shareRequest = await _shareRequestRepository.GetByIdForUpdateAsync(
            command.ShareRequestId,
            cancellationToken).ConfigureAwait(false);

        if (shareRequest == null)
        {
            throw new InvalidOperationException($"Share request {command.ShareRequestId} not found");
        }

        // 2. Verify user owns the request
        if (shareRequest.UserId != command.UserId)
        {
            _logger.LogWarning(
                "User {UserId} attempted to withdraw share request {ShareRequestId} owned by {OwnerId}",
                command.UserId, command.ShareRequestId, shareRequest.UserId);
            throw new InvalidOperationException("User does not own this share request");
        }

        // 3. Withdraw the request (domain will validate state transition)
        shareRequest.Withdraw();

        // 4. Save changes
        _shareRequestRepository.Update(shareRequest);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Successfully withdrew share request {ShareRequestId}",
            command.ShareRequestId);
    }
}
