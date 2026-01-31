using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for GetPendingTypologiesQuery (Admin approval queue).
/// Issue #3178: AGT-004 Typology Query Handlers.
/// </summary>
internal sealed class GetPendingTypologiesQueryHandler : IRequestHandler<GetPendingTypologiesQuery, List<AgentTypologyDto>>
{
    private readonly IAgentTypologyRepository _repository;

    public GetPendingTypologiesQueryHandler(IAgentTypologyRepository repository) =>
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<List<AgentTypologyDto>> Handle(
        GetPendingTypologiesQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var allTypologies = await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);

        // Filter for Pending status only
        var pendingTypologies = allTypologies
            .Where(t => t.Status == TypologyStatus.Pending)
            .OrderBy(t => t.CreatedAt) // Oldest first for approval queue
            .ToList();

        // Map to DTOs
        return pendingTypologies.Select(t => new AgentTypologyDto(
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
