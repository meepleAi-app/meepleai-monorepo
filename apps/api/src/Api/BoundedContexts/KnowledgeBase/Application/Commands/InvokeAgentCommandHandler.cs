using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
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
internal sealed class InvokeAgentCommandHandler
    : IRequestHandler<InvokeAgentCommand, AgentResponseDto>
{
    private readonly IAgentRepository _agentRepository;
    private readonly IEmbeddingRepository _embeddingRepository;
    private readonly IEmbeddingService _embeddingService;
    private readonly QualityTrackingDomainService _qualityTrackingService;
    private readonly IRagAccessService _ragAccessService;
    private readonly ILogger<InvokeAgentCommandHandler> _logger;

    public InvokeAgentCommandHandler(
        IAgentRepository agentRepository,
        IEmbeddingRepository embeddingRepository,
        IEmbeddingService embeddingService,
        QualityTrackingDomainService qualityTrackingService,
        IRagAccessService ragAccessService,
        ILogger<InvokeAgentCommandHandler> logger)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _embeddingRepository = embeddingRepository ?? throw new ArgumentNullException(nameof(embeddingRepository));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _qualityTrackingService = qualityTrackingService ?? throw new ArgumentNullException(nameof(qualityTrackingService));
        _ragAccessService = ragAccessService ?? throw new ArgumentNullException(nameof(ragAccessService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgentResponseDto> Handle(
        InvokeAgentCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // RAG access enforcement
        if (request.GameId.HasValue && request.UserId.HasValue)
        {
            var userRole = Enum.TryParse<UserRole>(request.UserRole, ignoreCase: true, out var parsedRole)
                ? parsedRole : UserRole.User;
            var canAccess = await _ragAccessService.CanAccessRagAsync(
                request.UserId.Value, request.GameId.Value, userRole, cancellationToken).ConfigureAwait(false);
            if (!canAccess)
                throw new ForbiddenException("Accesso RAG non autorizzato");
        }

        _logger.LogInformation(
            "Invoking agent {AgentId} with query: {Query}",
            request.AgentId,
            request.Query);

        try
        {
            // Step 1: Retrieve and validate agent
            var agent = await RetrieveAndValidateAgentAsync(request.AgentId, cancellationToken).ConfigureAwait(false);

            _logger.LogDebug(
                "Retrieved agent: {AgentName} (Type: {AgentType}, Strategy: {Strategy})",
                agent.Name,
                agent.Type.Value,
                agent.Strategy.Name);

            // Step 2: Perform vector search with agent strategy
            var (domainSearchResults, overallConfidence) = await PerformVectorSearchWithAgentStrategyAsync(
                request.Query, request.GameId, agent, cancellationToken).ConfigureAwait(false);

            if (domainSearchResults == null)
            {
                return CreateEmptyResponse(agent, request.Query);
            }

            // Step 3: Build and record agent invocation
            var result = await BuildAndRecordAgentInvocationAsync(
                agent, request.Query, domainSearchResults, overallConfidence!, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Agent invocation completed: InvocationId={InvocationId}, Results={ResultCount}, Confidence={Confidence:F3}",
                result.InvocationId,
                result.ResultCount,
                result.Confidence.Value);

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
    /// Retrieves agent from repository and validates it is active.
    /// </summary>
    private async Task<Agent> RetrieveAndValidateAgentAsync(
        Guid agentId,
        CancellationToken cancellationToken)
    {
        var agent = await _agentRepository.GetByIdAsync(agentId, cancellationToken).ConfigureAwait(false);
        if (agent == null)
        {
            throw new InvalidOperationException($"Agent not found: {agentId}");
        }

        if (!agent.IsActive)
        {
            throw new InvalidOperationException($"Agent is not active: {agent.Name}");
        }

        return agent;
    }

    /// <summary>
    /// Performs vector search using agent strategy parameters.
    /// Returns (searchResults, confidence) or (null, null) if no results or no GameId.
    /// </summary>
    private async Task<(List<Domain.Entities.SearchResult>? searchResults, Confidence? confidence)> PerformVectorSearchWithAgentStrategyAsync(
        string query,
        Guid? gameId,
        Agent agent,
        CancellationToken cancellationToken)
    {
        // Generate query embedding
        var embeddingResult = await _embeddingService.GenerateEmbeddingAsync(
            query,
            cancellationToken).ConfigureAwait(false);

        if (embeddingResult == null || embeddingResult.Embeddings.Count == 0)
        {
            throw new InvalidOperationException("Failed to generate query embedding");
        }

        var queryVector = new Vector(embeddingResult.Embeddings[0]);

        _logger.LogDebug(
            "Generated query embedding: {Dimensions} dimensions",
            queryVector.Dimensions);

        // Validate GameId is provided (required for vector search)
        if (!gameId.HasValue)
        {
            _logger.LogWarning("No GameId provided - cannot perform vector search");
            return (null, null);
        }

        // Perform vector search using agent strategy parameters
        var topK = agent.Strategy.GetParameter("TopK", 10);
        var minScore = agent.Strategy.GetParameter("MinScore", 0.55);

        _logger.LogDebug(
            "Performing vector search: GameId={GameId}, TopK={TopK}, MinScore={MinScore}",
            gameId.Value, topK, minScore);

        var searchResults = await _embeddingRepository.SearchByVectorAsync(
            gameId: gameId.Value,
            queryVector: queryVector,
            topK: topK,
            minScore: minScore,
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);

        if (searchResults.Count == 0)
        {
            _logger.LogWarning(
                "Vector search returned no results for GameId: {GameId}",
                gameId);

            return (null, null);
        }

        _logger.LogDebug(
            "Vector search returned {Count} embeddings",
            searchResults.Count);

        // Convert embeddings to SearchResult entities for quality tracking
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

        // Calculate overall confidence
        var overallConfidence = _qualityTrackingService.CalculateSearchConfidence(domainSearchResults);

        return (domainSearchResults, overallConfidence);
    }

    /// <summary>
    /// Records agent invocation and builds result.
    /// </summary>
    private async Task<AgentInvocationResult> BuildAndRecordAgentInvocationAsync(
        Agent agent,
        string query,
        List<Domain.Entities.SearchResult> domainSearchResults,
        Confidence overallConfidence,
        CancellationToken cancellationToken)
    {
        // Record invocation on agent
        // Issue #1694: This handler performs vector search only (no LLM call), so token usage is empty.
        agent.RecordInvocation(query, TokenUsage.Empty);
        await _agentRepository.UpdateAsync(agent, cancellationToken).ConfigureAwait(false);

        // Build and return result
        return new AgentInvocationResult(
            invocationId: Guid.NewGuid(),
            agentId: agent.Id,
            agentName: agent.Name,
            agentType: agent.Type,
            searchResults: domainSearchResults,
            confidence: overallConfidence,
            query: query,
            executedAt: DateTime.UtcNow
        );
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
