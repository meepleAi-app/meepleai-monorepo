using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for rejecting a delete request for a shared game.
/// Rejecting will deny the deletion and keep the game active.
/// </summary>
internal sealed class RejectDeleteRequestCommandHandler : ICommandHandler<RejectDeleteRequestCommand, Unit>
{
    private readonly ISharedGameDeleteRequestRepository _deleteRequestRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RejectDeleteRequestCommandHandler> _logger;

    public RejectDeleteRequestCommandHandler(
        ISharedGameDeleteRequestRepository deleteRequestRepository,
        IUnitOfWork unitOfWork,
        ILogger<RejectDeleteRequestCommandHandler> logger)
    {
        _deleteRequestRepository = deleteRequestRepository ?? throw new ArgumentNullException(nameof(deleteRequestRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Unit> Handle(RejectDeleteRequestCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Rejecting delete request: {RequestId}, RejectedBy: {UserId}",
            command.RequestId, command.RejectedBy);

        var deleteRequest = await _deleteRequestRepository.GetByIdAsync(command.RequestId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Delete request with ID {command.RequestId} not found");

        // Call domain method to reject
        deleteRequest.Reject(command.RejectedBy, command.Reason);

        _deleteRequestRepository.Update(deleteRequest);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Delete request rejected successfully: {RequestId}",
            command.RequestId);

        return Unit.Value;
    }
}
