#pragma warning disable MA0002 // Dictionary without StringComparer
#pragma warning disable MA0026 // TODO comments
#pragma warning disable S1135 // Sonar TODO comments
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.AgentModes;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.Middleware.Exceptions;
using Api.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handler for SuggestMoveCommand - generates move suggestions using Player Mode agent.
/// Issue #2404 - Player Mode move suggestions (MVP - simplified without full AgentConfiguration)
/// </summary>
internal sealed class SuggestMoveCommandHandler : IRequestHandler<SuggestMoveCommand, MoveSuggestionsDto>
{
    private readonly IGameSessionStateRepository _sessionStateRepository;
    private readonly IAgentRepository _agentRepository;
    private readonly IEmbeddingRepository _embeddingRepository;
    private readonly IEmbeddingService _embeddingService;
    private readonly IEnumerable<IAgentModeHandler> _modeHandlers;
    private readonly ILogger<SuggestMoveCommandHandler> _logger;

    public SuggestMoveCommandHandler(
        IGameSessionStateRepository sessionStateRepository,
        IAgentRepository agentRepository,
        IEmbeddingRepository embeddingRepository,
        IEmbeddingService embeddingService,
        IEnumerable<IAgentModeHandler> modeHandlers,
        ILogger<SuggestMoveCommandHandler> logger)
    {
        _sessionStateRepository = sessionStateRepository;
        _agentRepository = agentRepository;
        _embeddingRepository = embeddingRepository;
        _embeddingService = embeddingService;
        _modeHandlers = modeHandlers;
        _logger = logger;
    }

    public async Task<MoveSuggestionsDto> Handle(
        SuggestMoveCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing move suggestion request for Session {SessionId} with Agent {AgentId}",
            request.SessionId,
            request.AgentId);

        // 1. Validate session exists
        var sessionState = await _sessionStateRepository
            .GetBySessionIdAsync(request.SessionId, cancellationToken)
            .ConfigureAwait(false);

        if (sessionState == null)
        {
            throw new NotFoundException($"Game session {request.SessionId} not found");
        }

        // 2. Validate agent exists and is active
        var agent = await _agentRepository
            .GetByIdAsync(request.AgentId, cancellationToken)
            .ConfigureAwait(false);

        if (agent == null)
        {
            throw new NotFoundException($"Agent {request.AgentId} not found");
        }

        if (!agent.IsActive)
        {
            throw new InvalidOperationException($"Agent {agent.Name} is not active");
        }

        // 3. Perform vector search for relevant rules
        var query = request.Query ?? "What moves are available in this game state?";

        // Generate query embedding
        var embeddingResult = await _embeddingService.GenerateEmbeddingAsync(
            query,
            cancellationToken).ConfigureAwait(false);

        if (embeddingResult == null || embeddingResult.Embeddings.Count == 0)
        {
            throw new InvalidOperationException("Failed to generate query embedding");
        }

        var queryVector = new Vector(embeddingResult.Embeddings[0]);

        // Perform vector search
        var embeddingResults = await _embeddingRepository.SearchByVectorAsync(
            gameId: request.SessionId,
            queryVector: queryVector,
            topK: 5,
            minScore: 0.5,
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);

        // Convert embeddings to SearchResult entities
        var searchResults = embeddingResults.Select((embedding, index) =>
        {
            var similarity = queryVector.CosineSimilarity(embedding.Vector);
            var clampedSimilarity = Math.Clamp(similarity, 0.0, 1.0);
            var relevanceScore = new Confidence(clampedSimilarity);

            return new Api.BoundedContexts.KnowledgeBase.Domain.Entities.SearchResult(
                id: Guid.NewGuid(),
                vectorDocumentId: embedding.VectorDocumentId,
                textContent: embedding.TextContent,
                pageNumber: embedding.PageNumber,
                relevanceScore: relevanceScore,
                rank: index + 1,
                searchMethod: "vector"
            );
        }).ToList();

        _logger.LogDebug(
            "Vector search returned {ResultCount} results for query: {Query}",
            searchResults.Count,
            query);

        // 4. Get Player mode handler
        var playerHandler = _modeHandlers
            .FirstOrDefault(h => h.SupportedMode == AgentMode.Player);

        if (playerHandler == null)
        {
            throw new InvalidOperationException("Player mode handler not registered");
        }

        // 5. Build context (MVP: use minimal configuration without full AgentConfiguration entity)
        // TODO Issue #2404: Integrate with AgentConfiguration when ready
        var mockConfig = CreateMockConfiguration(agent);

        var context = new AgentModeContext(
            Agent: agent,
            Configuration: mockConfig,
            Query: query,
            GameId: request.SessionId,
            ChatThreadId: null,
            SearchResults: searchResults,
            UserId: request.UserId);

        var result = await playerHandler
            .HandleAsync(context, cancellationToken)
            .ConfigureAwait(false);

        // 6. Parse suggestions from result
        var suggestions = ParseSuggestionsFromResult(result);

        _logger.LogInformation(
            "Generated {SuggestionCount} move suggestions for Session {SessionId}",
            suggestions.Count,
            request.SessionId);

        return new MoveSuggestionsDto
        {
            Suggestions = suggestions,
            Confidence = result.Confidence,
            SessionId = request.SessionId,
            AgentId = request.AgentId,
            GeneratedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a mock AgentConfiguration for MVP (will be replaced with real configuration)
    /// TODO Issue #2404: Remove this when AgentConfiguration repository is available
    /// </summary>
    private static AgentConfiguration CreateMockConfiguration(Agent agent)
    {
        // Use internal constructor to create minimal configuration
        // For MVP, assume Player mode with default settings
        return AgentConfiguration.Create(
            agentId: agent.Id,
            llmProvider: LlmProvider.OpenRouter,
            llmModel: "anthropic/claude-3.5-sonnet",
            agentMode: AgentMode.Player,
            selectedDocumentIds: new List<Guid>(), // Empty for MVP
            temperature: 0.7m,
            maxTokens: 2000,
            systemPromptOverride: null,
            createdBy: Guid.Empty);
    }

    /// <summary>
    /// Parses suggestions from agent mode result
    /// MVP: Returns formatted content as single suggestion
    /// TODO Issue #2404: Extract structured MoveSuggestion list from PlayerModeHandler metadata
    /// </summary>
    private static List<MoveSuggestionItemDto> ParseSuggestionsFromResult(AgentModeResult result)
    {
        return new List<MoveSuggestionItemDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Action = "Analizza il contenuto della risposta",
                Reasoning = result.Content,
                Risk = "medium",
                ConfidenceScore = (float)result.Confidence,
                StateChange = new Dictionary<string, object>()
            }
        };
    }
}
