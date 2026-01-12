using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for requesting deletion of a shared game.
/// Creates a delete request that requires admin approval.
/// </summary>
internal sealed class RequestDeleteSharedGameCommandHandler : ICommandHandler<RequestDeleteSharedGameCommand, Guid>
{
    private readonly ISharedGameRepository _gameRepository;
    private readonly ISharedGameDeleteRequestRepository _deleteRequestRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RequestDeleteSharedGameCommandHandler> _logger;

    public RequestDeleteSharedGameCommandHandler(
        ISharedGameRepository gameRepository,
        ISharedGameDeleteRequestRepository deleteRequestRepository,
        IUnitOfWork unitOfWork,
        ILogger<RequestDeleteSharedGameCommandHandler> logger)
    {
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _deleteRequestRepository = deleteRequestRepository ?? throw new ArgumentNullException(nameof(deleteRequestRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> Handle(RequestDeleteSharedGameCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Creating delete request for shared game: {GameId}, RequestedBy: {UserId}",
            command.GameId, command.RequestedBy);

        // Verify game exists
        _ = await _gameRepository.GetByIdAsync(command.GameId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Shared game with ID {command.GameId} not found");

        // Create delete request
        var deleteRequest = SharedGameDeleteRequest.Create(
            command.GameId,
            command.RequestedBy,
            command.Reason);

        await _deleteRequestRepository.AddAsync(deleteRequest, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Delete request created successfully: {RequestId} for game {GameId}",
            deleteRequest.Id, command.GameId);

        return deleteRequest.Id;
    }
}
