using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Mappers;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Handles creation of game state snapshot.
/// Issue #2403: GameSessionState Entity (snapshots for undo/history)
/// </summary>
internal class CreateStateSnapshotCommandHandler : ICommandHandler<CreateStateSnapshotCommand, GameStateSnapshotDto>
{
    private readonly IGameSessionStateRepository _stateRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateStateSnapshotCommandHandler(
        IGameSessionStateRepository stateRepository,
        IUnitOfWork unitOfWork)
    {
        _stateRepository = stateRepository ?? throw new ArgumentNullException(nameof(stateRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GameStateSnapshotDto> Handle(CreateStateSnapshotCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Fetch state
        var state = await _stateRepository.GetByIdAsync(command.SessionStateId, cancellationToken)
            .ConfigureAwait(false);

        if (state == null)
            throw new NotFoundException("GameSessionState", command.SessionStateId.ToString());

        // Create snapshot (domain method handles validation)
        var snapshot = state.CreateSnapshot(
            turnNumber: command.TurnNumber,
            description: command.Description,
            createdBy: "system"
        );

        // Persist
        await _stateRepository.UpdateAsync(state, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return snapshot.ToDto();
    }
}
