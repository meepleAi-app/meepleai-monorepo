using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handles GetUserAgentSlotsQuery — returns user's agent slot allocation and usage.
/// Issue #4771: Agent Slots Endpoint + Quota System.
/// Issue #417: Frontend calls GET /api/v1/user/agent-slots → 404.
/// </summary>
internal sealed class GetUserAgentSlotsQueryHandler
    : IRequestHandler<GetUserAgentSlotsQuery, UserAgentSlotsDto>
{
    private readonly IUserLibraryRepository _libraryRepository;

    public GetUserAgentSlotsQueryHandler(IUserLibraryRepository libraryRepository)
    {
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
    }

    public async Task<UserAgentSlotsDto> Handle(
        GetUserAgentSlotsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var used = await _libraryRepository
            .GetAgentConfigCountAsync(request.UserId, cancellationToken)
            .ConfigureAwait(false);

        var total = AgentTierLimits.GetMaxAgents(request.UserTier, request.UserRole);

        // Cap total for display (Enterprise/Admin gets int.MaxValue — show a sensible number)
        var displayTotal = total == int.MaxValue ? used + 100 : total;
        var available = Math.Max(0, displayTotal - used);

        // Build slot list: occupied slots (index 0..used-1) + available slots (used..total-1)
        var slots = new List<AgentSlotDto>();
        for (var i = 0; i < Math.Min(used, displayTotal); i++)
        {
            slots.Add(new AgentSlotDto(
                SlotIndex: i,
                AgentId: null,   // Individual agent details require a separate query
                AgentName: null,
                GameId: null,
                Status: "active"
            ));
        }

        for (var i = used; i < Math.Min(displayTotal, used + 20); i++)
        {
            slots.Add(new AgentSlotDto(
                SlotIndex: i,
                AgentId: null,
                AgentName: null,
                GameId: null,
                Status: "available"
            ));
        }

        return new UserAgentSlotsDto(
            Total: displayTotal,
            Used: used,
            Available: available,
            Slots: slots
        );
    }
}
