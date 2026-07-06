using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Resolves a voting tie by persisting the organiser's chosen winner — Issue #2700.
/// The aggregate enforces the guards (organiser-only, voting closed, an actual tie, valid
/// choice); the resolved winner is written directly to the event row via
/// <see cref="IGameNightEventRepository.SetVotingWinnerAsync"/>.
/// </summary>
internal sealed class ResolveGameNightVotingTieCommandHandler : ICommandHandler<ResolveGameNightVotingTieCommand>
{
    private readonly IGameNightEventRepository _repository;
    private readonly TimeProvider _timeProvider;

    public ResolveGameNightVotingTieCommandHandler(
        IGameNightEventRepository repository,
        TimeProvider timeProvider)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task Handle(ResolveGameNightVotingTieCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var gameNight = await _repository.GetByIdAsync(command.GameNightId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameNightEvent", command.GameNightId.ToString());

        gameNight.ResolveVotingTie(
            command.HostUserId, command.WinningCandidateGameId, _timeProvider.GetUtcNow());

        await _repository.SetVotingWinnerAsync(
            command.GameNightId, gameNight.VotingWinnerGameId, cancellationToken).ConfigureAwait(false);
    }
}
