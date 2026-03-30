using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handler for CreateAgentCommand.
/// Creates a new agent with specified configuration.
/// </summary>
internal class CreateAgentCommandHandler : IRequestHandler<CreateAgentCommand, AgentDto>
{
    private readonly IAgentRepository _agentRepository;
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<CreateAgentCommandHandler> _logger;

    public CreateAgentCommandHandler(
        IAgentRepository agentRepository,
        MeepleAiDbContext db,
        ILogger<CreateAgentCommandHandler> logger)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgentDto> Handle(
        CreateAgentCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Validate name uniqueness
        var exists = await _agentRepository.ExistsAsync(request.Name, cancellationToken).ConfigureAwait(false);
        if (exists)
        {
            throw new InvalidOperationException($"Agent with name '{request.Name}' already exists");
        }

        // Parse agent type
        var agentType = AgentType.Parse(request.AgentType);

        // Create strategy
        var strategy = AgentStrategy.HybridSearch();

        // Create agent aggregate
        var agent = new Agent(
            id: Guid.NewGuid(),
            name: request.Name,
            type: agentType,
            strategy: strategy,
            isActive: request.IsActive
        );

        // Create default AgentConfiguration so the agent is immediately chat-ready
        var defaultConfig = new AgentConfigurationEntity
        {
            Id = Guid.NewGuid(),
            AgentId = agent.Id,
            LlmProvider = AgentDefaults.DefaultLlmProvider,
            LlmModel = AgentDefaults.DefaultModel,
            AgentMode = 0, // Chat
            SelectedDocumentIdsJson = "[]",
            Temperature = AgentDefaults.DefaultTemperature,
            MaxTokens = AgentDefaults.DefaultMaxTokens,
            IsCurrent = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.Empty // admin context, no user
        };

        // Persist agent + config atomically
        var executionStrategy = _db.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(async ct =>
        {
            using var transaction = await _db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            await _agentRepository.AddAsync(agent, ct).ConfigureAwait(false);
            _db.Set<AgentConfigurationEntity>().Add(defaultConfig);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
            return true;
        }, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Created agent {AgentId} with name '{Name}' and type '{Type}'",
            agent.Id,
            agent.Name,
            agent.Type.Value);

        return ToDto(agent);
    }

    internal static AgentDto ToDto(Agent agent)
    {
        return new AgentDto(
            Id: agent.Id,
            Name: agent.Name,
            Type: agent.Type.Value,
            StrategyName: agent.Strategy.Name,
            StrategyParameters: agent.Strategy.Parameters,
            IsActive: agent.IsActive,
            CreatedAt: agent.CreatedAt,
            LastInvokedAt: agent.LastInvokedAt,
            InvocationCount: agent.InvocationCount,
            IsRecentlyUsed: agent.IsRecentlyUsed,
            IsIdle: agent.IsIdle,
            GameId: agent.GameId,
            CreatedByUserId: agent.CreatedByUserId
        );
    }
}
