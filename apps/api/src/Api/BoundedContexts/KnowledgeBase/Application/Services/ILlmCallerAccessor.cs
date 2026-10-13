using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// #4164 (ADR-095 fetta 1b): l'utente per conto del quale parte una chiamata LLM, usato
/// <b>solo</b> per attribuirne il costo nel registro.
/// </summary>
/// <remarks>
/// <para>
/// La maggior parte dei chiamanti di <c>ILlmService</c> non passa un <c>User</c>: lo fa la
/// pipeline RAG, la classificazione della domanda, la traduzione. Il contesto che arriva al
/// servizio è quindi <c>LlmUserContext.Internal</c>, e il registro scriveva <c>user_id = null</c>
/// anche per una domanda fatta da un utente autenticato.
/// </para>
/// <para>
/// L'instradamento non cambia: continua a usare il contesto ricevuto. Passare l'utente vero al
/// selettore cambierebbe il tier, e quindi il modello che risponde.
/// </para>
/// </remarks>
internal interface ILlmCallerAccessor
{
    /// <summary>
    /// L'utente della richiesta in corso, o <c>null</c> se non c'è: un job, un hosted service,
    /// una chiamata di sistema, una richiesta anonima.
    /// </summary>
    LlmUserContext? Current { get; }
}
