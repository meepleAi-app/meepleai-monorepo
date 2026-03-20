using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Mappers;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Handles restoration of game state from snapshot.
/// Issue #2403: GameSessionState Entity (restore for undo)
/// </summary>
internal class RestoreStateSnapshotCommandHandler : ICommandHandler<RestoreStateSnapshotCommand, GameSessionStateDto>
{
    private readonly IGameSessionStateRepository _stateRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RestoreStateSnapshotCommandHandler(
        IGameSessionStateRepository stateRepository,
        IUnitOfWork unitOfWork)
    {
        _stateRepository = stateRepository ?? throw new ArgumentNullException(nameof(stateRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GameSessionStateDto> Handle(RestoreStateSnapshotCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Fetch state with snapshots
        var state = await _stateRepository.GetByIdAsync(command.SessionStateId, cancellationToken)
            .ConfigureAwait(false);

        if (state == null)
            throw new NotFoundException("GameSessionState", command.SessionStateId.ToString());

        // Restore from snapshot (domain method handles backup creation)
        state.RestoreFromSnapshot(command.SnapshotId, "system");

        // Persist
        await _stateRepository.UpdateAsync(state, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return state.ToDto();
    }
}
