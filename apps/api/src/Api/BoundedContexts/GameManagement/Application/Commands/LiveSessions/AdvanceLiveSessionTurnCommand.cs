using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Command to advance to the next turn in a live game session.
/// Issue #4749: CQRS commands for live sessions.
/// </summary>
/// <remarks>
/// <c>ExpectedTurnIndex</c> (#4277) è l'indice di turno su cui chi chiede ha deciso. Se è dato e il turno
/// nel frattempo è già passato, il comando non avanza e risponde con un conflitto
/// <see cref="TurnChangedCode"/>: due richieste nate sullo stesso turno lo fanno passare una volta sola.
/// Senza, il comando avanza come ha sempre fatto.
/// </remarks>
internal record AdvanceLiveSessionTurnCommand(Guid SessionId, int? ExpectedTurnIndex = null) : ICommand
{
    /// <summary>Il codice del conflitto quando l'indice di turno non è più quello atteso.</summary>
    public const string TurnChangedCode = "turn_changed";
}
