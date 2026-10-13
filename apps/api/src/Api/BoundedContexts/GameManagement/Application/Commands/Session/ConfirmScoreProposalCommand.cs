using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.Commands.Session;

/// <summary>
/// Host confirms a score proposal. Score is recorded in the LiveGameSession and broadcast to all participants.
/// E3-3: Score Proposal Flow.
/// </summary>
/// <remarks>
/// <c>ExpectedCurrentValue</c> (#4277) è il valore della cella su cui chi conferma ha deciso; una cella
/// che non esiste vale zero. Se è dato e la cella nel frattempo è cambiata, il comando non scrive e
/// risponde con un conflitto <see cref="ScoreChangedCode"/>. Il controllo avviene sull'aggregato appena
/// caricato, e il salvataggio con <c>xmin</c> lo rende atomico con la scrittura. Senza, il comando
/// sostituisce la cella come ha sempre fatto.
/// </remarks>
public sealed record ConfirmScoreProposalCommand(
    Guid SessionId,
    Guid ConfirmingUserId,
    Guid TargetPlayerId,
    int Round,
    string Dimension,
    int Value,
    int? ExpectedCurrentValue = null) : IRequest
{
    /// <summary>Il codice del conflitto quando la cella non ha più il valore atteso.</summary>
    public const string ScoreChangedCode = "score_changed";
}
