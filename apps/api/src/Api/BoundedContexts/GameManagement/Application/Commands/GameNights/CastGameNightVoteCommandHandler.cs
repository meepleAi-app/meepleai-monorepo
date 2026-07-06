using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Casts an approval vote for a candidate game — Issue #2700.
/// The aggregate enforces the guards (Published, voting open, valid candidate, confirmed voter)
/// and returns the new vote (or null for an idempotent re-approval). The vote is inserted via
/// the tracked <see cref="IGameNightEventRepository.AddVoteAsync"/> path.
/// </summary>
internal sealed class CastGameNightVoteCommandHandler : ICommandHandler<CastGameNightVoteCommand>
{
    private readonly IGameNightEventRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public CastGameNightVoteCommandHandler(
        IGameNightEventRepository repository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task Handle(CastGameNightVoteCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var gameNight = await _repository.GetByIdAsync(command.GameNightId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameNightEvent", command.GameNightId.ToString());

        var vote = gameNight.CastVote(
            command.VoterUserId, command.CandidateGameId, _timeProvider.GetUtcNow());

        if (vote is null)
            return; // approval already recorded — idempotent

        await _repository.AddVoteAsync(vote, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
