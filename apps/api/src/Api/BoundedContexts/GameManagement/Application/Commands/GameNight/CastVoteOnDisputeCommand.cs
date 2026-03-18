using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNight;

/// <summary>
/// Records a player's vote on an active dispute verdict.
/// Part of the democratic override voting system.
/// </summary>
internal record CastVoteOnDisputeCommand(
    Guid DisputeId,
    Guid PlayerId,
    bool AcceptsVerdict
) : ICommand;
