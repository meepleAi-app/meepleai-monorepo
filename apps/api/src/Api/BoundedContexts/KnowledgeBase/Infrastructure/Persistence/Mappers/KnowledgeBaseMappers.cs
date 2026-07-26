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
            // #2284 issue 2: thread TotalCharacters through the domain (was hardcoded 0,
            // silently zeroing the audit field on every new ingestion).
            TotalCharacters = domain.TotalCharacters,
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
        // #2284 issue 1: use Rehydrate (not the public ctor) so reading from DB does NOT
        // enqueue a fresh VectorDocumentIndexedEvent into the outbox — that was a latent
        // read-side bug introduced by PR #2278 (every mapper ToDomain raised an event).
        return VectorDocument.Rehydrate(
            id: entity.Id,
            gameId: entity.GameId ?? Guid.Empty,
            pdfDocumentId: entity.PdfDocumentId,
            language: "en", // Default language (not stored on entity)
            totalChunks: entity.ChunkCount,
            indexedAt: entity.IndexedAt ?? DateTime.UtcNow,
            sharedGameId: entity.SharedGameId, // Issue #5185
            metadata: entity.Metadata,
            totalCharacters: entity.TotalCharacters);
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
    /// Maps a RAW keyword-search result (issue #3270 §6) to a domain SearchResult, carrying the
    /// {PdfDocumentId}_{ChunkIndex} fusion identity + RoleTags. RelevanceScore is the raw ts_rank_cd
    /// (clamped to Confidence's [0,1]); HybridFusionCore applies role-boost/legend downstream.
    /// </summary>
    public static Domain.Entities.SearchResult ToDomainSearchResult(this KeywordSearchResult result, int rank)
    {
        var pdfDocId = Guid.Parse(result.PdfDocumentId);
        var score = Math.Clamp((double)result.RelevanceScore, 0.0, 1.0);
        return new Domain.Entities.SearchResult(
            id: Guid.NewGuid(),
            vectorDocumentId: pdfDocId,          // same convention the HybridSearchResult mapper uses
            textContent: result.Content,
            pageNumber: result.PageNumber ?? 1,
            relevanceScore: new Confidence(score),
            rank: rank,
            searchMethod: "keyword",
            pdfDocumentId: pdfDocId,
            chunkIndex: result.ChunkIndex,
            roleTags: result.RoleTags);
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
