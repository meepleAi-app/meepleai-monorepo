using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Command for the organiser to resolve a voting tie after voting closes, choosing one of
/// the tied leaders as the winner — Issue #2700.
/// </summary>
internal record ResolveGameNightVotingTieCommand(
    Guid GameNightId,
    Guid HostUserId,
    Guid WinningCandidateGameId) : ICommand;
