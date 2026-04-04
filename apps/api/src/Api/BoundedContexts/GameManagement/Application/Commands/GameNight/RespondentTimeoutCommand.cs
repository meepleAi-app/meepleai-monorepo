using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNight;

/// <summary>
/// Handles the timeout scenario when a respondent does not reply to a dispute.
/// The dispute proceeds with the initiator's claim only.
/// </summary>
internal record RespondentTimeoutCommand(
    Guid DisputeId
) : ICommand;
