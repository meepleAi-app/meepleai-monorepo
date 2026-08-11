using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Command to cast (approve) a vote for a candidate game on a game night — Issue #2700.
/// Idempotent: re-approving an already-approved candidate is a no-op.
/// </summary>
internal record CastGameNightVoteCommand(
    Guid GameNightId,
    Guid VoterUserId,
    Guid CandidateGameId) : ICommand;
