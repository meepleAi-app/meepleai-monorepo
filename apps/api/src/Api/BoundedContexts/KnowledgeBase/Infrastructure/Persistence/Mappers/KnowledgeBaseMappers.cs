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
    /// Maps persistence AgentEntity to domain Agent.
    /// </summary>
    public static Agent ToDomain(AgentEntity entity)
    {
        var agentType = AgentType.Parse(entity.Type);
        var strategy = DeserializeStrategy(entity.StrategyName, entity.StrategyParametersJson);

        var agent = new Agent(
            id: entity.Id,
            name: entity.Name,
            type: agentType,
            strategy: strategy,
            isActive: entity.IsActive,
            gameId: entity.GameId,
            createdByUserId: entity.CreatedByUserId
        );

        // Use reflection to set read-only properties (CreatedAt, LastInvokedAt, InvocationCount)
        var createdAtProp = typeof(Agent).GetProperty(nameof(Agent.CreatedAt));
        createdAtProp?.SetValue(agent, entity.CreatedAt);

        var lastInvokedAtProp = typeof(Agent).GetProperty(nameof(Agent.LastInvokedAt));
        lastInvokedAtProp?.SetValue(agent, entity.LastInvokedAt);

        var invocationCountProp = typeof(Agent).GetProperty(nameof(Agent.InvocationCount));
        invocationCountProp?.SetValue(agent, entity.InvocationCount);

        return agent;
    }

    /// <summary>
    /// Maps domain Agent to persistence AgentEntity.
    /// </summary>
    public static AgentEntity ToEntity(Agent agent)
    {
        var (strategyName, parametersJson) = SerializeStrategy(agent.Strategy);

        return new AgentEntity
        {
            Id = agent.Id,
            Name = agent.Name,
            Type = agent.Type.Value,
            StrategyName = strategyName,
            StrategyParametersJson = parametersJson,
            IsActive = agent.IsActive,
            CreatedAt = agent.CreatedAt,
            LastInvokedAt = agent.LastInvokedAt,
            InvocationCount = agent.InvocationCount,
            GameId = agent.GameId,
            CreatedByUserId = agent.CreatedByUserId
        };
    }

    /// <summary>
    /// Deserializes AgentStrategy from name and JSON parameters.
    /// </summary>
    private static AgentStrategy DeserializeStrategy(string name, string parametersJson)
    {
        var parameters = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(parametersJson)
            ?? new Dictionary<string, object>(StringComparer.Ordinal);

        // Convert JsonElement to appropriate types
        var typedParams = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var (key, value) in parameters)
        {
            if (value is System.Text.Json.JsonElement jsonElement)
            {
                typedParams[key] = jsonElement.ValueKind switch
                {
                    System.Text.Json.JsonValueKind.Number => jsonElement.GetDouble(),
                    System.Text.Json.JsonValueKind.String => jsonElement.GetString() ?? "",
                    System.Text.Json.JsonValueKind.True => true,
                    System.Text.Json.JsonValueKind.False => false,
                    System.Text.Json.JsonValueKind.Array => jsonElement.EnumerateArray()
                        .Select(e => e.GetString() ?? "").ToArray(),
                    _ => value
                };
            }
            else
            {
                typedParams[key] = value;
            }
        }

        return AgentStrategy.Custom(name, typedParams);
    }

    /// <summary>
    /// Serializes AgentStrategy to name and JSON parameters.
    /// </summary>
    private static (string name, string parametersJson) SerializeStrategy(AgentStrategy strategy)
    {
        var parametersJson = System.Text.Json.JsonSerializer.Serialize(strategy.Parameters);
        return (strategy.Name, parametersJson);
    }

    /// <summary>
    /// Maps persistence AgentTypologyEntity to domain AgentTypology.
    /// Issue #3176: AGT-002, #3177: AGT-003
    /// </summary>
    public static AgentTypology ToDomain(AgentTypologyEntity entity)
    {
        var status = (TypologyStatus)entity.Status;
        var strategy = DeserializeStrategy(entity.DefaultStrategyJson);

        var typology = new AgentTypology(
            id: entity.Id,
            name: entity.Name,
            description: entity.Description,
            basePrompt: entity.BasePrompt,
            defaultStrategy: strategy,
            createdBy: entity.CreatedBy,
            status: status
        );

        // Use reflection to set read-only properties (ApprovedBy, ApprovedAt, CreatedAt, IsDeleted)
        if (entity.ApprovedBy.HasValue)
        {
            var approvedByProp = typeof(AgentTypology).GetProperty(nameof(AgentTypology.ApprovedBy));
            approvedByProp?.SetValue(typology, entity.ApprovedBy);
        }

        if (entity.ApprovedAt.HasValue)
        {
            var approvedAtProp = typeof(AgentTypology).GetProperty(nameof(AgentTypology.ApprovedAt));
            approvedAtProp?.SetValue(typology, entity.ApprovedAt);
        }

        var createdAtProp = typeof(AgentTypology).GetProperty(nameof(AgentTypology.CreatedAt));
        createdAtProp?.SetValue(typology, entity.CreatedAt);

        var isDeletedProp = typeof(AgentTypology).GetProperty(nameof(AgentTypology.IsDeleted));
        isDeletedProp?.SetValue(typology, entity.IsDeleted);

        return typology;
    }

    /// <summary>
    /// Maps domain AgentTypology to persistence AgentTypologyEntity.
    /// Issue #3176: AGT-002, #3177: AGT-003
    /// </summary>
    public static AgentTypologyEntity ToEntity(AgentTypology typology)
    {
        var strategyJson = SerializeStrategyToJson(typology.DefaultStrategy);

        return new AgentTypologyEntity
        {
            Id = typology.Id,
            Name = typology.Name,
            Description = typology.Description,
            BasePrompt = typology.BasePrompt,
            DefaultStrategyJson = strategyJson,
            Status = (int)typology.Status,
            CreatedBy = typology.CreatedBy,
            ApprovedBy = typology.ApprovedBy,
            CreatedAt = typology.CreatedAt,
            ApprovedAt = typology.ApprovedAt,
            IsDeleted = typology.IsDeleted
        };
    }

    /// <summary>
    /// Deserializes AgentStrategy from JSON string.
    /// </summary>
    private static AgentStrategy DeserializeStrategy(string strategyJson)
    {
        var strategyData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(strategyJson)
            ?? throw new InvalidOperationException("Failed to deserialize strategy JSON");

        if (!strategyData.TryGetValue("Name", out var nameElement))
            throw new InvalidOperationException("Strategy JSON missing 'Name' property");

        var name = nameElement.GetString() ?? throw new InvalidOperationException("Strategy name is null");

        var parameters = new Dictionary<string, object>(StringComparer.Ordinal);
        if (strategyData.TryGetValue("Parameters", out var paramsElement))
        {
            foreach (var prop in paramsElement.EnumerateObject())
            {
                parameters[prop.Name] = prop.Value.ValueKind switch
                {
                    System.Text.Json.JsonValueKind.Number => prop.Value.GetDouble(),
                    System.Text.Json.JsonValueKind.String => prop.Value.GetString() ?? "",
                    System.Text.Json.JsonValueKind.True => true,
                    System.Text.Json.JsonValueKind.False => false,
                    System.Text.Json.JsonValueKind.Array => prop.Value.EnumerateArray()
                        .Select(e => e.GetString() ?? "").ToArray(),
                    _ => prop.Value.ToString()
                };
            }
        }

        return AgentStrategy.Custom(name, parameters);
    }

    /// <summary>
    /// Serializes AgentStrategy to JSON string.
    /// </summary>
    private static string SerializeStrategyToJson(AgentStrategy strategy)
    {
        var data = new
        {
            Name = strategy.Name,
            Parameters = strategy.Parameters
        };
        return System.Text.Json.JsonSerializer.Serialize(data);
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
            agentId: entity.AgentId,
            gameSessionId: entity.GameSessionId,
            userId: entity.UserId,
            gameId: entity.GameId,
            typologyId: entity.TypologyId,
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
            AgentId = session.AgentId,
            GameSessionId = session.GameSessionId,
            UserId = session.UserId,
            GameId = session.GameId,
            TypologyId = session.TypologyId,
            CurrentGameStateJson = session.CurrentGameState.ToJson(),
            StartedAt = session.StartedAt,
            EndedAt = session.EndedAt,
            IsActive = session.IsActive
        };
    }
}
