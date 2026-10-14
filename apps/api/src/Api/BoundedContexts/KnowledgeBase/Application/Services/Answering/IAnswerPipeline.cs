using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Models;

#pragma warning disable MA0048 // File name must match type name - the pipeline contract and its request
namespace Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;

/// <summary>
/// #4168 (ADR-095 D3): la pipeline di risposta unica, l'unico posto che sceglie modello, prompt e
/// parametri di una risposta sulle regole. Ogni punto d'ingresso che risponde a una domanda passa
/// da qui.
/// </summary>
internal interface IAnswerPipeline
{
    /// <summary>
    /// Risponde in streaming, con il contratto SSE della chat: stato, citazioni, token, completamento,
    /// citazioni in linea e continuazione; un evento <c>Error</c> con il suo codice quando non si risponde.
    /// </summary>
    IAsyncEnumerable<RagStreamingEvent> StreamAsync(AnswerRequest query, CancellationToken cancellationToken);
}

/// <summary>
/// Una domanda alla pipeline (D3): il testo, l'ambito (un gioco, eventualmente ristretto ad alcuni
/// documenti), chi chiede e le sue preferenze di lingua, il contesto della chat.
/// </summary>
/// <param name="GameId">Il gioco della domanda; l'ambito resta un parametro, come stabilito da ADR-094.</param>
/// <param name="Query">La domanda, vuota solo nelle continuazioni.</param>
/// <param name="UserId">Chi chiede: il passo 1 controlla il suo accesso al gioco.</param>
/// <param name="UserRole">Il ruolo con cui si valuta l'accesso.</param>
/// <param name="AcceptLanguage">L'header della richiesta: decide la lingua quando la domanda è troppo corta per dirlo.</param>
/// <param name="ThreadId">La chat da cui prendere la cronologia.</param>
/// <param name="DocumentIds">I documenti a cui restringere il recupero.</param>
/// <param name="ResponseStyle">«concise», «detailed» o «continuation».</param>
/// <param name="ContinuationContext">La risposta parziale da continuare.</param>
/// <param name="Trial">#4169 (6c-3): la bozza da provare al posto della versione pubblicata; di solito <c>null</c>.</param>
internal sealed record AnswerRequest(
    string GameId,
    string Query,
    Guid UserId,
    string UserRole,
    string? AcceptLanguage = null,
    Guid? ThreadId = null,
    IReadOnlyList<Guid>? DocumentIds = null,
    string? ResponseStyle = null,
    string? ContinuationContext = null,
    AnswerTrial? Trial = null);

/// <summary>
/// #4169 (6c-3, ADR-095 D8): la prova di una versione non pubblicata dalla pagina «Agente». La pipeline
/// usa questo contenuto invece della versione pubblicata; non legge né scrive la cache, perché una bozza
/// cambia contenuto tenendo lo stesso id; non conta la quota del piano, perché la domanda è dell'admin
/// che prova; e la chiamata al gateway esce come <see cref="Api.Services.RequestSource.AdminOperation"/>.
/// </summary>
internal sealed record AnswerTrial(Guid VersionId, int VersionNumber, AgentProfileContent Content);
