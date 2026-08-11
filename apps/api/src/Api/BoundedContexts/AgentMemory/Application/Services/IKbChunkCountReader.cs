namespace Api.BoundedContexts.AgentMemory.Application.Services;

/// <summary>
/// Cross-BC read-only service for counting indexed KB chunks per game.
/// Defined here (AgentMemory.Application) so the BC owns the consumer contract;
/// the implementation lives in Infrastructure and queries the shared DbContext
/// (KnowledgeBase BC owns the VectorDocuments table).
/// </summary>
internal interface IKbChunkCountReader
{
    /// <summary>
    /// Returns the number of indexed VectorDocuments associated with the given game.
    /// Used by <c>GetHouseRulesMetadataQueryHandler</c> to populate the
    /// <c>kbCount</c> pip in the SP6 §F drawer ConnectionBar.
    /// </summary>
    Task<int> CountByGameAsync(Guid gameId, CancellationToken ct = default);
}
