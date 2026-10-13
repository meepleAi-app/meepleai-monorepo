using System.Diagnostics.CodeAnalysis;

namespace Api.Middleware.Exceptions;

/// <summary>
/// #4166 (ADR-095 D6): il gateway LLM non ha fatto partire la chiamata (tetto di spesa, quota, modello
/// fuori catalogo). Mappata su 503: il servizio AI è in pausa, la richiesta in sé è valida.
/// </summary>
/// <remarks>
/// La usano i consumatori che segnalano gli errori con eccezioni invece che con eventi di stream:
/// senza, una chiamata rifiutata diventava una risposta vuota, e da lì un errore di validazione.
/// </remarks>
public sealed class LlmCallRefusedException : HttpException
{
    [SetsRequiredMembers]
    public LlmCallRefusedException(string errorCode, string message)
        : base(StatusCodes.Status503ServiceUnavailable, errorCode, message)
    {
    }
}
