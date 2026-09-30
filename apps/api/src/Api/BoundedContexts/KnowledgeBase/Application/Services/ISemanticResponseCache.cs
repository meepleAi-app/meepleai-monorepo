namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

internal interface ISemanticResponseCache
{
    /// <summary>
    /// Try to get a cached response for a semantically similar query.
    /// Returns null on cache miss or if similarity &lt; threshold.
    /// </summary>
    Task<CachedRagResponse?> TryGetAsync(
        Guid gameId,
        float[] queryVector,
        CancellationToken ct = default);

    /// <summary>
    /// Store a RAG response in the semantic cache.
    /// </summary>
    Task SetAsync(
        Guid gameId,
        float[] queryVector,
        CachedRagResponse response,
        CancellationToken ct = default);

    /// <summary>
    /// Invalidate all cached responses for a game (called on re-index).
    /// </summary>
    Task InvalidateGameAsync(Guid gameId, CancellationToken ct = default);
}

/// <summary>
/// Una citazione come viene conservata in cache.
///
/// Prima qui viaggiava il solo snippet, e su cache-hit la pagina veniva ricostruita dall'indice
/// nella lista: ogni risposta servita dalla cache attribuiva le citazioni a pagine che non le
/// contenevano — il sintomo del titolo di #3855, su un percorso diverso dalle house rule.
///
/// `PageNumber` e `DocumentId` sono nullable perche' un'assenza dichiarata e' onesta, mentre un
/// numero inventato e' una fonte falsa che il frontend tratta come coordinata navigabile.
/// </summary>
internal sealed record CachedCitation(
    string Snippet,
    int? PageNumber,
    string? DocumentId);

internal sealed record CachedRagResponse(
    string Answer,
    IReadOnlyList<CachedCitation> Citations,
    string ModelUsed,
    DateTimeOffset CachedAt);
