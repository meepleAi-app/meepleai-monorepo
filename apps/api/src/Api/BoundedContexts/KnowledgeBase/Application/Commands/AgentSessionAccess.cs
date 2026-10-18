using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.Middleware.Exceptions;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// #4282: una sessione agente la usa solo chi l'ha avviata, e solo dalla sessione di gioco a cui
/// appartiene. Le rotte di <c>AgentSessionEndpoints</c> leggono l'id della sessione agente dal corpo
/// della richiesta, quindi senza questo controllo bastava conoscerlo.
/// </summary>
/// <remarks>
/// La regola è quella dell'assistente in partita (#4276): chi non è il proprietario riceve 403, su
/// tutte le rotte. Una sessione agente che non esiste, o che appartiene a un'altra sessione di gioco,
/// risponde 404.
/// </remarks>
internal static class AgentSessionAccess
{
    /// <summary>
    /// Restituisce la sessione agente se <paramref name="userId"/> la possiede e appartiene alla
    /// sessione di gioco <paramref name="gameSessionId"/>; altrimenti lancia.
    /// </summary>
    /// <exception cref="NotFoundException">La sessione agente non esiste, o è di un'altra sessione di gioco.</exception>
    /// <exception cref="ForbiddenException">La sessione agente è di un altro utente.</exception>
    internal static AgentSession EnsureOwnedBy(
        AgentSession? agentSession,
        Guid agentSessionId,
        Guid gameSessionId,
        Guid userId)
    {
        if (agentSession is null)
            throw new NotFoundException("AgentSession", agentSessionId.ToString());

        if (agentSession.UserId != userId)
            throw new ForbiddenException("Only the user who launched this agent session can use it.");

        if (agentSession.GameSessionId != gameSessionId)
            throw new NotFoundException("AgentSession", agentSessionId.ToString());

        return agentSession;
    }
}
