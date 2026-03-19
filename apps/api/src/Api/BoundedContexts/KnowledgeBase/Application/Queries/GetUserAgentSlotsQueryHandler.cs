using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for GetUserAgentSlotsQuery.
/// Returns the user's agent slot allocation, usage, and per-slot details.
/// Issue #4771: Agent Slots Endpoint + Quota System.
/// </summary>
internal sealed class GetUserAgentSlotsQueryHandler : IRequestHandler<GetUserAgentSlotsQuery, UserAgentSlotsDto>
{
    private readonly IAgentRepository _agentRepository;
    private readonly ILogger<GetUserAgentSlotsQueryHandler> _logger;

    public GetUserAgentSlotsQueryHandler(
        IAgentRepository agentRepository,
        ILogger<GetUserAgentSlotsQueryHandler> logger)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UserAgentSlotsDto> Handle(
        GetUserAgentSlotsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var maxAgents = AgentTierLimits.GetMaxAgents(request.UserTier, request.UserRole);
        var userAgents = await _agentRepository.GetByUserIdAsync(request.UserId, cancellationToken)
            .ConfigureAwait(false);

        // Only count active agents towards used slots
        var activeAgents = userAgents.Where(a => a.IsActive).OrderBy(a => a.CreatedAt).ToList();
        var used = activeAgents.Count;

        // Cap display total for unlimited tiers (Admin/Editor) to used + reasonable buffer
        var total = maxAgents == int.MaxValue ? Math.Max(used + 10, 50) : maxAgents;
        var available = maxAgents == int.MaxValue ? int.MaxValue : Math.Max(0, maxAgents - used);

        var slots = new List<AgentSlotDto>();

        // Occupied slots
        for (var i = 0; i < activeAgents.Count; i++)
        {
            var agent = activeAgents[i];
            slots.Add(new AgentSlotDto(
                SlotIndex: i + 1,
                AgentId: agent.Id,
                AgentName: agent.Name,
                GameId: agent.GameId,
                Status: "active"
            ));
        }

        // Available (empty) slots - only for non-unlimited tiers
        if (maxAgents != int.MaxValue)
        {
            for (var i = used; i < maxAgents; i++)
            {
                slots.Add(new AgentSlotDto(
                    SlotIndex: i + 1,
                    AgentId: null,
                    AgentName: null,
                    GameId: null,
                    Status: "available"
                ));
            }
        }

        _logger.LogDebug(
            "User {UserId} agent slots: {Used}/{Total} (tier: {Tier})",
            request.UserId, used, total, request.UserTier);

        return new UserAgentSlotsDto(
            Total: total,
            Used: used,
            Available: available,
            Slots: slots
        );
    }
}
