using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handler for GetAgentStatusQuery.
/// Checks agent readiness for chat including KB and RAG validation.
/// </summary>
internal sealed class GetAgentStatusQueryHandler
    : IRequestHandler<GetAgentStatusQuery, AgentStatusDto?>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetAgentStatusQueryHandler> _logger;

    public GetAgentStatusQueryHandler(
        MeepleAiDbContext dbContext,
        ILogger<GetAgentStatusQueryHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgentStatusDto?> Handle(
        GetAgentStatusQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Load agent with configuration
        var agent = await _dbContext.Agents
            .FirstOrDefaultAsync(a => a.Id == request.AgentId, cancellationToken)
            .ConfigureAwait(false);

        if (agent == null)
        {
            _logger.LogWarning("Agent not found: {AgentId}", request.AgentId);
            return null;
        }

        // Check for current configuration
        var currentConfig = await _dbContext.AgentConfigurations
            .FirstOrDefaultAsync(
                c => c.AgentId == request.AgentId && c.IsCurrent,
                cancellationToken)
            .ConfigureAwait(false);

        bool hasConfiguration = currentConfig != null;
        int documentCount = 0;
        bool hasDocuments = false;

        if (hasConfiguration)
        {
            // Parse document IDs from JSON
            if (!string.IsNullOrEmpty(currentConfig!.SelectedDocumentIdsJson))
            {
                try
                {
                    var documentIds = JsonSerializer.Deserialize<List<Guid>>(
                        currentConfig.SelectedDocumentIdsJson) ?? new List<Guid>();
                    documentCount = documentIds.Count;
                    hasDocuments = documentCount > 0;
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to parse document IDs for agent {AgentId}", request.AgentId);
                }
            }
        }

        // Determine readiness and blocking reason
        bool isReady = agent.IsActive && hasConfiguration && hasDocuments;
        string? blockingReason = null;

        if (!agent.IsActive)
            blockingReason = "Agent is inactive";
        else if (!hasConfiguration)
            blockingReason = "Agent has no configuration";
        else if (!hasDocuments)
            blockingReason = "Agent has no documents in Knowledge Base";

        string ragStatus = hasDocuments ? "Ready" : "Not initialized";

        _logger.LogInformation(
            "Agent status check: {AgentId} - Ready={IsReady}, Documents={DocumentCount}",
            request.AgentId, isReady, documentCount);

        return new AgentStatusDto(
            AgentId: agent.Id,
            Name: agent.Name,
            IsActive: agent.IsActive,
            IsReady: isReady,
            HasConfiguration: hasConfiguration,
            HasDocuments: hasDocuments,
            DocumentCount: documentCount,
            RagStatus: ragStatus,
            BlockingReason: blockingReason
        );
    }
}
