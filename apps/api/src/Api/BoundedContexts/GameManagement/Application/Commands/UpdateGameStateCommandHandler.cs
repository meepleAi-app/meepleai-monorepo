using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Mappers;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles update of game session state.
/// Issue #2403: GameSessionState Entity
/// </summary>
internal class UpdateGameStateCommandHandler : ICommandHandler<UpdateGameStateCommand, GameSessionStateDto>
{
    private readonly IGameSessionStateRepository _stateRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateGameStateCommandHandler(
        IGameSessionStateRepository stateRepository,
        IUnitOfWork unitOfWork)
    {
        _stateRepository = stateRepository ?? throw new ArgumentNullException(nameof(stateRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GameSessionStateDto> Handle(UpdateGameStateCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Fetch state
        var state = await _stateRepository.GetByIdAsync(command.SessionStateId, cancellationToken)
            .ConfigureAwait(false);

        if (state == null)
            throw new NotFoundException("GameSessionState", command.SessionStateId.ToString());

        // Update state (domain method handles versioning)
        state.UpdateState(command.NewState, "system");

        // Persist
        await _stateRepository.UpdateAsync(state, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return state.ToDto();
    }
}
