using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Returns the candidate-vote tally for a game night — Issue #2700.
/// Participant-scoped (organizer or invited) per the IDOR guard shared with the detail read
/// (#2698). The <c>VotedByMe</c> flag reflects the caller's own approvals.
/// </summary>
internal sealed class GetGameNightVoteTallyQueryHandler
    : IQueryHandler<GetGameNightVoteTallyQuery, GameNightVoteTallyDto>
{
    private readonly IGameNightEventRepository _repository;
    private readonly TimeProvider _timeProvider;

    public GetGameNightVoteTallyQueryHandler(
        IGameNightEventRepository repository,
        TimeProvider timeProvider)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<GameNightVoteTallyDto> Handle(
        GetGameNightVoteTallyQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var gameNight = await _repository.GetByIdAsync(query.GameNightId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameNightEvent", query.GameNightId.ToString());

        // #2698 IDOR: participant-scoped read (organizer or invited).
        var isParticipant = gameNight.OrganizerId == query.CallerUserId
            || gameNight.Rsvps.Any(r => r.UserId == query.CallerUserId);
        if (!isParticipant)
            throw new ForbiddenException("Only the organizer or an invited player can view the vote tally.");

        var tally = gameNight.TallyVotes();

        var myApprovals = gameNight.Votes
            .Where(v => v.VoterUserId == query.CallerUserId)
            .Select(v => v.CandidateGameId)
            .ToHashSet();

        var candidates = tally.CountsByCandidate
            .Select(kv => new GameNightCandidateTallyDto(kv.Key, kv.Value, myApprovals.Contains(kv.Key)))
            .ToList();

        return new GameNightVoteTallyDto(
            IsVotingClosed: gameNight.IsVotingClosed(_timeProvider.GetUtcNow()),
            IsTie: tally.IsTie,
            WinnerGameId: tally.WinnerGameId,
            LeadingCandidateGameIds: tally.LeadingCandidateGameIds,
            Candidates: candidates);
    }
}
