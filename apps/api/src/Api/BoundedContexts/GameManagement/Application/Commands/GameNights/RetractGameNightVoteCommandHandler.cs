using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Retracts a previously-cast approval vote — Issue #2700. The aggregate enforces the
/// voting-open guard and returns the removed vote (or null when absent). The vote is deleted
/// via the tracked <see cref="IGameNightEventRepository.RemoveVoteAsync"/> path.
/// </summary>
internal sealed class RetractGameNightVoteCommandHandler : ICommandHandler<RetractGameNightVoteCommand>
{
    private readonly IGameNightEventRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public RetractGameNightVoteCommandHandler(
        IGameNightEventRepository repository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task Handle(RetractGameNightVoteCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var gameNight = await _repository.GetByIdAsync(command.GameNightId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameNightEvent", command.GameNightId.ToString());

        var vote = gameNight.RetractVote(
            command.VoterUserId, command.CandidateGameId, _timeProvider.GetUtcNow());

        if (vote is null)
            return; // nothing to retract — idempotent

        await _repository.RemoveVoteAsync(vote.Id, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
