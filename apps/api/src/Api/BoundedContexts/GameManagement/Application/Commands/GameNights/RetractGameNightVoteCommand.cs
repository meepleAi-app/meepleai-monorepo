using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Command to retract a previously-cast approval vote for a candidate game — Issue #2700.
/// Idempotent: retracting an absent vote is a no-op.
/// </summary>
internal record RetractGameNightVoteCommand(
    Guid GameNightId,
    Guid VoterUserId,
    Guid CandidateGameId) : ICommand;
