namespace Api.BoundedContexts.KbQuality.Application.Ports;

/// <summary>
/// Port to KnowledgeBase BC: executes a single retrieval query and returns chunk IDs.
/// Implemented by KbSearchProviderAdapter (Task 18) calling the KB SearchQuery.
/// </summary>
public interface IKbSearchProvider
{
    /// <param name="docId">Restrict search to chunks belonging to this PDF.</param>
    /// <param name="question">Natural-language query from the goldset.</param>
    /// <param name="topK">Max chunks to retrieve (typically 10 for P@1/3/5).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<SearchResult> SearchAsync(Guid docId, string question, int topK, CancellationToken ct);
}

/// <param name="RetrievedChunkIds">
/// L'id del <c>text_chunks</c> di ogni risultato, nell'ordine del recupero (#4288). Un risultato che
/// non porta l'id (un embedding scritto senza <c>source_chunk_id</c>, vedi #4384) vale <c>null</c>:
/// tiene il suo posto e non è mai un colpo, altrimenti i risultati dopo di lui salirebbero di rango.
/// </param>
/// <param name="Elapsed">La durata della ricerca.</param>
public sealed record SearchResult(IReadOnlyList<Guid?> RetrievedChunkIds, TimeSpan Elapsed);
