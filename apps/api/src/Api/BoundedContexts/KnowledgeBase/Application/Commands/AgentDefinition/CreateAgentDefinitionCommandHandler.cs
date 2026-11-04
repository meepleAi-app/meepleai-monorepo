using Api.BoundedContexts.KnowledgeBase.Application.Commands.AgentDefinition;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs.AgentDefinition;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.AgentDefinition;

/// <summary>
/// Handler for CreateAgentDefinitionCommand.
/// Issue #3808 (Epic #3687)
/// </summary>
internal sealed class CreateAgentDefinitionCommandHandler
    : IRequestHandler<CreateAgentDefinitionCommand, AgentDefinitionDto>
{
    private readonly IAgentDefinitionRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateAgentDefinitionCommandHandler> _logger;

    public CreateAgentDefinitionCommandHandler(
        IAgentDefinitionRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<CreateAgentDefinitionCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgentDefinitionDto> Handle(
        CreateAgentDefinitionCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Validate name uniqueness
        var exists = await _repository.ExistsAsync(request.Name, cancellationToken).ConfigureAwait(false);
        if (exists)
            throw new ConflictException($"AgentDefinition with name '{request.Name}' already exists");

        // Create config value object
        var config = AgentDefinitionConfig.Create(request.Model, request.MaxTokens, request.Temperature);

        // Create strategy (default to HybridSearch if not provided or unrecognized)
        var strategy = ResolveStrategy(request.StrategyName);

        // Create prompts
        var prompts = request.Prompts?
            .Select(p => AgentPromptTemplate.Create(p.Role, p.Content))
            .ToList();

        // Create tools
        var tools = request.Tools?
            .Select(t => AgentToolConfig.Create(t.Name, t.Settings))
            .ToList();

        // Create aggregate
        var agentDefinition = Domain.Entities.AgentDefinition.Create(
            request.Name,
            request.Description,
            config,
            strategy,
            prompts,
            tools);

        // Issue #5140: Apply KbCardIds if provided in the command
        if (request.KbCardIds is { Count: > 0 })
            agentDefinition.UpdateKbCardIds(request.KbCardIds);

        // Persist (ADR-056: explicit UoW save)
        await _repository.AddAsync(agentDefinition, cancellationToken).ConfigureAwait(false);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Issue #4138: this translation came from CreateUserAgentCommandHandler, retired
            // with the user-facing creation routes. It does NOT belong to that flow — it
            // belongs to the unique index, which is still here and still unfiltered.
            //
            // AgentDefinition.Name carries a global unique index that is NOT filtered by
            // is_deleted (AgentDefinitionConfiguration: HasIndex(a => a.Name).IsUnique()),
            // so a colliding name (active OR soft-deleted) trips Postgres 23505. The
            // ExistsAsync pre-check above cannot cover it: a soft-deleted collider is
            // invisible through the !IsDeleted query filter, and a concurrent double-submit
            // races any check. Without this, both cases leak a 500 (#2568).
            throw new ConflictException($"AgentDefinition with name '{request.Name}' already exists", ex);
        }

        _logger.LogInformation(
            "Created AgentDefinition {Id} with name '{Name}'",
            agentDefinition.Id,
            agentDefinition.Name);

        return MapToDto(agentDefinition);
    }

    // Postgres SQLSTATE 23505 = unique_violation. Mirrors the pattern used by
    // JoinWaitlistCommandHandler / RegisterCommandHandler.
    private const string UniqueViolationSqlState = "23505";

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException pgEx &&
        string.Equals(pgEx.SqlState, UniqueViolationSqlState, StringComparison.Ordinal);

    private static AgentStrategy ResolveStrategy(string? name) => name switch
    {
        "RetrievalOnly" => AgentStrategy.RetrievalOnly(),
        "SentenceWindowRAG" => AgentStrategy.SentenceWindowRAG(),
        "ColBERTReranking" => AgentStrategy.ColBERTReranking(),
        _ => AgentStrategy.HybridSearch()
    };

    private static AgentDefinitionDto MapToDto(Domain.Entities.AgentDefinition agent)
    {
        return new AgentDefinitionDto
        {
            Id = agent.Id,
            Name = agent.Name,
            Description = agent.Description,
            StrategyName = agent.Strategy.Name,
            StrategyParameters = agent.Strategy.Parameters as Dictionary<string, object> ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
            Config = new AgentConfigDto
            {
                Model = agent.Config.Model,
                MaxTokens = agent.Config.MaxTokens,
                Temperature = agent.Config.Temperature
            },
            Prompts = agent.Prompts.Select(p => new PromptTemplateDto
            {
                Role = p.Role,
                Content = p.Content
            }).ToList(),
            Tools = agent.Tools.Select(t => new ToolConfigDto
            {
                Name = t.Name,
                Settings = t.GetSettings() as Dictionary<string, object> ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            }).ToList(),
            KbCardIds = agent.KbCardIds.ToList(),
            Status = agent.Status,
            IsActive = agent.IsActive,
            CreatedAt = agent.CreatedAt,
            UpdatedAt = agent.UpdatedAt
        };
    }
}
