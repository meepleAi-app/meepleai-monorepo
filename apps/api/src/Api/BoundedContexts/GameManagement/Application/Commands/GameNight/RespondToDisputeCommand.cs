using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNight;

/// <summary>
/// Adds a respondent's counter-claim to an existing dispute.
/// </summary>
internal record RespondToDisputeCommand(
    Guid DisputeId,
    Guid RespondentPlayerId,
    string RespondentClaim
) : ICommand;
