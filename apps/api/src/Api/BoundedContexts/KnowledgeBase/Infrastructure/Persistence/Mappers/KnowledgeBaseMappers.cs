using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Services;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.Mappers;

/// <summary>
/// Mapping extensions between domain entities and persistence entities.
/// Handles impedance mismatch between domain (Guid) and persistence (Guid after Phase 2 migration).
/// </summary>
internal static class KnowledgeBaseMappers
{
    /// <summary>
    /// Maps domain VectorDocument to persistence VectorDocumentEntity.
    /// </summary>
    public static VectorDocumentEntity ToEntity(this VectorDocument domain)
    {
        ArgumentNullException.ThrowIfNull(domain);
        return new VectorDocumentEntity
        {
            Id = domain.Id,
            GameId = domain.GameId,
            PdfDocumentId = domain.PdfDocumentId,
            ChunkCount = domain.TotalChunks,
            TotalCharacters = 0, // Not tracked in domain
            IndexingStatus = "completed", // Simplified status mapping
            IndexedAt = domain.IndexedAt,
            IndexingError = null,
            EmbeddingModel = "nomic-embed-text", // Default model
            EmbeddingDimensions = 768, // Default dimensions
            Metadata = domain.Metadata, // Map metadata field
            SharedGameId = domain.SharedGameId // Issue #5185: cross-BC reference
        };
    }

    /// <summary>
    /// Maps persistence VectorDocumentEntity to domain VectorDocument.
    /// </summary>
    public static VectorDocument ToDomain(this VectorDocumentEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var domain = new VectorDocument(
            id: entity.Id,
            gameId: entity.GameId ?? Guid.Empty,
            pdfDocumentId: entity.PdfDocumentId,
            language: "en", // Default language (not stored in entity)
            totalChunks: entity.ChunkCount,
            sharedGameId: entity.SharedGameId // Issue #5185
        );

        // Restore metadata using internal method
        if (!string.IsNullOrEmpty(entity.Metadata))
        {
            domain.SetMetadata(entity.Metadata);
        }

        return domain;
    }

    /// <summary>
    /// Maps HybridSearchResult to domain SearchResult.
    /// </summary>
    public static Domain.Entities.SearchResult ToDomainSearchResult(
        this HybridSearchResult result,
        int rank)
    {
        // Parse PdfDocumentId as the VectorDocumentId (they are the same in this context)
        var vectorDocId = Guid.Parse(result.PdfDocumentId);
        var pageNum = result.PageNumber ?? 1; // Default to page 1 if null
        var score = (double)result.HybridScore; // Use HybridScore as relevance
        var searchMethod = result.Mode.ToString().ToLowerInvariant(); // Convert enum to string

        return new Domain.Entities.SearchResult(
            id: Guid.NewGuid(),
            vectorDocumentId: vectorDocId,
            textContent: result.Content,
            pageNumber: pageNum,
            relevanceScore: new Confidence(score),
            rank: rank,
            searchMethod: searchMethod
        );
    }

    /// <summary>
    /// Extracts float[] from EmbeddingResult.
    /// EmbeddingResult contains List&lt;float[]&gt;, we take the first one for single text queries.
    /// </summary>
    public static float[] ToFloatArray(this EmbeddingResult embeddingResult)
    {
        if (!embeddingResult.Success || embeddingResult.Embeddings.Count == 0)
            throw new InvalidOperationException(
                $"Cannot extract embedding: {embeddingResult.ErrorMessage ?? "No embeddings generated"}");

        return embeddingResult.Embeddings[0];
    }

    /// <summary>
    /// Creates domain Embedding from Qdrant search result data.
    /// Note: This is a simplified mapping - real Qdrant results would need proper deserialization.
    /// </summary>
    public static Embedding CreateEmbeddingFromQdrant(
        Guid embeddingId,
        Guid vectorDocumentId,
        string textContent,
        int pageNumber,
        float[] vectorArray,
        string model,
        int chunkIndex)
    {
        var vector = new Vector(vectorArray);
        return new Embedding(
            id: embeddingId,
            vectorDocumentId: vectorDocumentId,
            textContent: textContent,
            vector: vector,
            model: model,
            chunkIndex: chunkIndex,
            pageNumber: Math.Max(1, pageNumber)
        );
    }

    /// <summary>
    /// Converts domain Embedding to Qdrant point format.
    /// Returns tuple with data needed for Qdrant indexing.
    /// </summary>
    public static (Guid id, float[] vector, Dictionary<string, object> payload) ToQdrantPoint(
        this Embedding embedding)
    {
        var payload = new Dictionary<string, object>
(StringComparer.Ordinal)
        {
            ["vector_document_id"] = embedding.VectorDocumentId.ToString(),
            ["text_content"] = embedding.TextContent,
            ["page_number"] = embedding.PageNumber,
            ["chunk_index"] = embedding.ChunkIndex
        };

        return (embedding.Id, embedding.Vector.Values.ToArray(), payload);
    }

    /// <summary>
    /// Maps persistence AgentSessionEntity to domain AgentSession.
    /// Issue #3184 (AGT-010): Session-Based Agent Lifecycle.
    /// </summary>
    public static AgentSession ToDomain(AgentSessionEntity entity)
    {
        var gameState = GameState.FromJson(entity.CurrentGameStateJson);

        var session = new AgentSession(
            id: entity.Id,
            agentDefinitionId: entity.AgentDefinitionId,
            gameSessionId: entity.GameSessionId,
            userId: entity.UserId,
            gameId: entity.GameId,
            initialState: gameState
        );

        // Use reflection to restore read-only properties (StartedAt, EndedAt, IsActive)
        var startedAtProp = typeof(AgentSession).GetProperty(nameof(AgentSession.StartedAt));
        startedAtProp?.SetValue(session, entity.StartedAt);

        if (entity.EndedAt.HasValue)
        {
            var endedAtProp = typeof(AgentSession).GetProperty(nameof(AgentSession.EndedAt));
            endedAtProp?.SetValue(session, entity.EndedAt);
        }

        var isActiveProp = typeof(AgentSession).GetProperty(nameof(AgentSession.IsActive));
        isActiveProp?.SetValue(session, entity.IsActive);

        return session;
    }

    /// <summary>
    /// Maps domain AgentSession to persistence AgentSessionEntity.
    /// Issue #3184 (AGT-010): Session-Based Agent Lifecycle.
    /// </summary>
    public static AgentSessionEntity ToEntity(AgentSession session)
    {
        return new AgentSessionEntity
        {
            Id = session.Id,
            AgentDefinitionId = session.AgentDefinitionId,
            GameSessionId = session.GameSessionId,
            UserId = session.UserId,
            GameId = session.GameId,
            CurrentGameStateJson = session.CurrentGameState.ToJson(),
            StartedAt = session.StartedAt,
            EndedAt = session.EndedAt,
            IsActive = session.IsActive
        };
    }
}
