using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for InvokeAgentCommand - executes agent invocation using domain services.
/// </summary>
/// <remarks>
/// Issue #867: Game Master Agent Integration.
/// Architecture (ADR-004): Single-context transaction using KnowledgeBase domain services.
/// Pattern: Agent orchestrates VectorSearchDomainService + QualityTrackingDomainService.
/// </remarks>
public sealed class InvokeAgentCommandHandler
    : IRequestHandler<InvokeAgentCommand, AgentResponseDto>
{
    private readonly IAgentRepository _agentRepository;
    private readonly IEmbeddingRepository _embeddingRepository;
    private readonly IEmbeddingService _embeddingService;
    private readonly QualityTrackingDomainService _qualityTrackingService;
    private readonly ILogger<InvokeAgentCommandHandler> _logger;

    public InvokeAgentCommandHandler(
        IAgentRepository agentRepository,
        IEmbeddingRepository embeddingRepository,
        IEmbeddingService embeddingService,
        QualityTrackingDomainService qualityTrackingService,
        ILogger<InvokeAgentCommandHandler> logger)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _embeddingRepository = embeddingRepository ?? throw new ArgumentNullException(nameof(embeddingRepository));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _qualityTrackingService = qualityTrackingService ?? throw new ArgumentNullException(nameof(qualityTrackingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgentResponseDto> Handle(
        InvokeAgentCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Invoking agent {AgentId} with query: {Query}",
            request.AgentId,
            request.Query);

        try
        {
            // 1. Retrieve agent from repository
            var agent = await _agentRepository.GetByIdAsync(request.AgentId, cancellationToken).ConfigureAwait(false);
            if (agent == null)
            {
                throw new InvalidOperationException($"Agent not found: {request.AgentId}");
            }

            // 2. Validate agent is active
            if (!agent.IsActive)
            {
                throw new InvalidOperationException($"Agent is not active: {agent.Name}");
            }

            _logger.LogDebug(
                "Retrieved agent: {AgentName} (Type: {AgentType}, Strategy: {Strategy})",
                agent.Name,
                agent.Type.Value,
                agent.Strategy.Name);

            // 3. Generate query embedding
            var embeddingResult = await _embeddingService.GenerateEmbeddingAsync(
                request.Query,
                cancellationToken).ConfigureAwait(false);

            if (embeddingResult == null || embeddingResult.Embeddings.Count == 0)
            {
                throw new InvalidOperationException("Failed to generate query embedding");
            }

            var queryVector = new Vector(embeddingResult.Embeddings[0]);

            _logger.LogDebug(
                "Generated query embedding: {Dimensions} dimensions",
                queryVector.Dimensions);

            // 4. Validate GameId is provided (required for vector search)
            if (!request.GameId.HasValue)
            {
                _logger.LogWarning("No GameId provided - cannot perform vector search");
                return CreateEmptyResponse(agent, request.Query);
            }

            // 5. Perform vector search using agent strategy parameters
            // Direct search via repository (delegates to Qdrant)
            var topK = agent.Strategy.GetParameter("TopK", 10);
            var minScore = agent.Strategy.GetParameter("MinScore", 0.55);

            _logger.LogDebug(
                "Performing vector search: GameId={GameId}, TopK={TopK}, MinScore={MinScore}",
                request.GameId.Value, topK, minScore);

            var searchResults = await _embeddingRepository.SearchByVectorAsync(
                gameId: request.GameId.Value,
                queryVector: queryVector,
                topK: topK,
                minScore: minScore,
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);

            if (searchResults.Count == 0)
            {
                _logger.LogWarning(
                    "Vector search returned no results for GameId: {GameId}",
                    request.GameId);

                return CreateEmptyResponse(agent, request.Query);
            }

            _logger.LogDebug(
                "Vector search returned {Count} embeddings",
                searchResults.Count);

            // 6. Convert embeddings to SearchResult entities for quality tracking
            var domainSearchResults = searchResults.Select((embedding, index) =>
            {
                var similarity = queryVector.CosineSimilarity(embedding.Vector);
                var clampedSimilarity = Math.Clamp(similarity, 0.0, 1.0);
                var relevanceScore = new Confidence(clampedSimilarity);

                return new Domain.Entities.SearchResult(
                    id: Guid.NewGuid(),
                    vectorDocumentId: embedding.VectorDocumentId,
                    textContent: embedding.TextContent,
                    pageNumber: embedding.PageNumber,
                    relevanceScore: relevanceScore,
                    rank: index + 1,
                    searchMethod: "vector"
                );
            }).ToList();

            // 7. Calculate overall confidence using quality tracking service
            var overallConfidence = _qualityTrackingService.CalculateSearchConfidence(domainSearchResults);

            // 8. Record invocation on agent
            // Issue #1694: This handler performs vector search only (no LLM call), so token usage is empty.
            // LLM calls happen in AskQuestionQueryHandler/StreamQaQueryHandler which track tokens there.
            agent.RecordInvocation(request.Query, TokenUsage.Empty);
            await _agentRepository.UpdateAsync(agent, cancellationToken).ConfigureAwait(false);

            // 9. Build and return result
            var result = new AgentInvocationResult(
                invocationId: Guid.NewGuid(),
                agentId: agent.Id,
                agentName: agent.Name,
                agentType: agent.Type,
                searchResults: domainSearchResults,
                confidence: overallConfidence,
                query: request.Query,
                executedAt: DateTime.UtcNow
            );

            _logger.LogInformation(
                "Agent invocation completed: InvocationId={InvocationId}, Results={ResultCount}, Confidence={Confidence:F3}",
                result.InvocationId,
                result.ResultCount,
                result.Confidence.Value);

            // 10. Convert to DTO and return
            return AgentResponseDto.FromDomain(result);
        }
#pragma warning disable S2139 // Exceptions should be either logged or rethrown but not both
        // HANDLER PATTERN: Log agent invocation failures before propagating.
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to invoke agent {AgentId}: {ErrorMessage}",
                request.AgentId,
                ex.Message);

            throw;
        }
#pragma warning restore S2139
    }

    /// <summary>
    /// Creates an empty response when no candidates found.
    /// </summary>
    private static AgentResponseDto CreateEmptyResponse(
        Domain.Entities.Agent agent,
        string query)
    {
        return new AgentResponseDto
        {
            InvocationId = Guid.NewGuid(),
            AgentId = agent.Id,
            AgentName = agent.Name,
            AgentType = agent.Type.Value,
            Query = query,
            Results = new List<SearchResultDto>(),
            Confidence = 0.0,
            QualityLevel = "Low",
            ExecutedAt = DateTime.UtcNow,
            ResultCount = 0
        };
    }
}
