using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handler for <see cref="RestoreUserAgentCommand"/>.
///
/// Restore uses <see cref="IAgentDefinitionRepository.GetByIdIgnoreDeletedAsync"/> to bypass
/// the global EF query filter that excludes soft-deleted agents.
/// ChatThreads that were closed during the soft-delete remain closed (by design — per spec).
///
/// Issue #904: SG3 — Agent CRUD lifecycle + soft-delete cascade.
/// </summary>
internal sealed class RestoreUserAgentCommandHandler
    : IRequestHandler<RestoreUserAgentCommand, AgentDto>
{
    private readonly IAgentDefinitionRepository _agentRepository;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly ILogger<RestoreUserAgentCommandHandler> _logger;

    public RestoreUserAgentCommandHandler(
        IAgentDefinitionRepository agentRepository,
        ISharedGameRepository sharedGameRepository,
        ILogger<RestoreUserAgentCommandHandler> logger)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgentDto> Handle(RestoreUserAgentCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Use IgnoreQueryFilters variant to load soft-deleted agents
        var agent = await _agentRepository.GetByIdIgnoreDeletedAsync(request.AgentId, cancellationToken).ConfigureAwait(false);
        if (agent is null)
            throw new NotFoundException($"AgentDefinition {request.AgentId} not found");

        if (!agent.IsDeleted)
            throw new BadRequestException($"AgentDefinition {request.AgentId} is not soft-deleted and cannot be restored.");

        agent.Restore();
        await _agentRepository.UpdateAsync(agent, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "AgentDefinition {AgentId} restored by user {UserId}",
            request.AgentId, request.UserId);

        // Resolve game name if agent has a GameId
        string? gameName = null;
        if (agent.GameId.HasValue)
        {
            var gameNames = await _sharedGameRepository
                .GetNamesByIdsAsync(new[] { agent.GameId.Value }, cancellationToken)
                .ConfigureAwait(false);
            gameNames.TryGetValue(agent.GameId.Value, out gameName);
        }

        var recentThreshold = DateTime.UtcNow.AddHours(-24);
        var idleThreshold = DateTime.UtcNow.AddDays(-7);

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
            IsRecentlyUsed: agent.LastInvokedAt.HasValue && agent.LastInvokedAt.Value > recentThreshold,
            IsIdle: !agent.LastInvokedAt.HasValue || agent.LastInvokedAt.Value < idleThreshold,
            GameId: agent.GameId,
            GameName: gameName,
            CreatedByUserId: request.UserId
        );
    }
}
