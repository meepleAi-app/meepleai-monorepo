using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNight;

/// <summary>
/// Opens a new structured rule dispute during a live game session.
/// Returns the dispute ID.
/// </summary>
internal record OpenStructuredDisputeCommand(
    Guid SessionId,
    Guid InitiatorPlayerId,
    string InitiatorClaim
) : ICommand<Guid>;
