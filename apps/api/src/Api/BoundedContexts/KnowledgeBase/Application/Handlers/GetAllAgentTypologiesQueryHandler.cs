using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for GetAllAgentTypologiesQuery with role-based filtering.
/// Issue #3178: AGT-004 Typology Query Handlers.
/// </summary>
internal sealed class GetAllAgentTypologiesQueryHandler : IRequestHandler<GetAllAgentTypologiesQuery, List<AgentTypologyDto>>
{
    private readonly IAgentTypologyRepository _repository;

    public GetAllAgentTypologiesQueryHandler(IAgentTypologyRepository repository) =>
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<List<AgentTypologyDto>> Handle(
        GetAllAgentTypologiesQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Fetch all typologies
        var allTypologies = await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);

        // Apply role-based filtering
        var filteredTypologies = request.UserRole.ToUpperInvariant() switch
        {
            "ADMIN" => allTypologies, // Admin sees all
            "EDITOR" => allTypologies.Where(t =>
                t.Status == TypologyStatus.Approved || // All approved
                t.CreatedBy == request.UserId).ToList(), // Own drafts/pending
            _ => allTypologies.Where(t =>
                t.Status == TypologyStatus.Approved).ToList() // User sees approved only
        };

        // Map to DTOs
        return filteredTypologies.Select(t => new AgentTypologyDto(
            Id: t.Id,
            Name: t.Name,
            Description: t.Description,
            BasePrompt: t.BasePrompt,
            DefaultStrategyName: t.DefaultStrategy.Name,
            DefaultStrategyParameters: t.DefaultStrategy.Parameters,
            Status: t.Status.ToString(),
            CreatedBy: t.CreatedBy,
            ApprovedBy: t.ApprovedBy,
            CreatedAt: t.CreatedAt,
            ApprovedAt: t.ApprovedAt,
            IsDeleted: t.IsDeleted
        )).ToList();
    }
}
