using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for GetAgentConfigurationQuery.
/// Returns the active (IsCurrent = true) configuration row.
/// If no configuration exists, returns a default configuration (same pattern as SendAgentMessageCommandHandler).
/// </summary>
internal sealed class GetAgentConfigurationQueryHandler
    : IRequestHandler<GetAgentConfigurationQuery, AgentConfigurationDto>
{
    private readonly MeepleAiDbContext _db;
    private readonly IAgentRepository _agentRepository;

    public GetAgentConfigurationQueryHandler(
        MeepleAiDbContext db,
        IAgentRepository agentRepository)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
    }

    public async Task<AgentConfigurationDto> Handle(
        GetAgentConfigurationQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Verify agent exists and user has access
        var agent = await _agentRepository.GetByIdAsync(request.AgentId, cancellationToken).ConfigureAwait(false);
        if (agent is null)
            throw new NotFoundException("Agent", request.AgentId.ToString());

        var isAdmin = string.Equals(request.UserRole, "Admin", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(request.UserRole, "Editor", StringComparison.OrdinalIgnoreCase);
        if (!isAdmin && agent.CreatedByUserId != request.UserId)
            throw new ForbiddenException("You can only view your own agent configuration");

        var config = await _db.Set<AgentConfigurationEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.AgentId == request.AgentId && c.IsCurrent,
                cancellationToken)
            .ConfigureAwait(false);

        // Return default configuration if none exists yet (agent never configured)
        if (config is null)
        {
            return new AgentConfigurationDto(
                Id: Guid.Empty,
                AgentId: request.AgentId,
                LlmModel: AgentDefaults.DefaultModel,
                LlmProvider: AgentDefaults.DefaultLlmProvider.ToString(),
                Temperature: AgentDefaults.DefaultTemperature,
                MaxTokens: AgentDefaults.DefaultMaxTokens,
                SelectedDocumentIds: [],
                IsCurrent: true,
                CreatedAt: DateTime.UtcNow);
        }

        return UpdateAgentLlmConfigurationCommandHandler.ToDto(config);
    }
}
