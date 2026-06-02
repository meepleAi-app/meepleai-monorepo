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

public sealed record SearchResult(IReadOnlyList<Guid> RetrievedChunkIds, TimeSpan Elapsed);
