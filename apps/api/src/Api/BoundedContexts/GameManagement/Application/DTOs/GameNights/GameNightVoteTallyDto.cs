namespace Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;

/// <summary>
/// Approval-vote tally for a game night's candidate games — Issue #2700.
/// </summary>
/// <param name="IsVotingClosed">True once voting has closed (ScheduledAt - 1h).</param>
/// <param name="IsTie">True when more than one candidate shares the lead.</param>
/// <param name="WinnerGameId">Resolved winner (organiser tie-break, else single leader, else null).</param>
/// <param name="LeadingCandidateGameIds">Candidate(s) with the maximum vote count.</param>
/// <param name="Candidates">Per-candidate counts with the caller's own approval flag.</param>
public sealed record GameNightVoteTallyDto(
    bool IsVotingClosed,
    bool IsTie,
    Guid? WinnerGameId,
    IReadOnlyList<Guid> LeadingCandidateGameIds,
    IReadOnlyList<GameNightCandidateTallyDto> Candidates);

/// <summary>Per-candidate approval tally row.</summary>
/// <param name="GameId">The candidate game.</param>
/// <param name="VoteCount">Number of approval votes for this candidate.</param>
/// <param name="VotedByMe">Whether the requesting user has approved this candidate.</param>
public sealed record GameNightCandidateTallyDto(Guid GameId, int VoteCount, bool VotedByMe);
