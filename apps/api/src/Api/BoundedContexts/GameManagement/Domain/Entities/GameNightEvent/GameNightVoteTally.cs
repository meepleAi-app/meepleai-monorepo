namespace Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;

/// <summary>
/// Read-model result of tallying candidate-game approval votes on a game night — Issue #2700.
///
/// <para><see cref="CountsByCandidate"/> maps every candidate game id to its approval count
/// (candidates with zero votes are present with a count of 0).</para>
/// <para><see cref="LeadingCandidateGameIds"/> holds the candidate(s) with the maximum count
/// (empty when no votes were cast). <see cref="IsTie"/> is true when more than one candidate
/// shares the lead.</para>
/// <para><see cref="WinnerGameId"/> is the resolved winner: an organiser tie-break decision if
/// one was made, otherwise the single leader, otherwise null (no votes or unresolved tie).</para>
/// </summary>
public sealed record GameNightVoteTally(
    IReadOnlyDictionary<Guid, int> CountsByCandidate,
    IReadOnlyList<Guid> LeadingCandidateGameIds,
    bool IsTie,
    Guid? WinnerGameId);
