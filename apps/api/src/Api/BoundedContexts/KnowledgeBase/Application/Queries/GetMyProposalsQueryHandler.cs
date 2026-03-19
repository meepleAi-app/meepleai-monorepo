using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for GetMyProposalsQuery (Editor's own proposals).
/// Issue #3178: AGT-004 Typology Query Handlers.
/// </summary>
internal sealed class GetMyProposalsQueryHandler : IRequestHandler<GetMyProposalsQuery, List<AgentTypologyDto>>
{
    private readonly IAgentTypologyRepository _repository;

    public GetMyProposalsQueryHandler(IAgentTypologyRepository repository) =>
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<List<AgentTypologyDto>> Handle(
        GetMyProposalsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var allTypologies = await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);

        // Filter for Editor's own proposals
        var myProposals = allTypologies
            .Where(t => t.CreatedBy == request.EditorId)
            .OrderByDescending(t => t.CreatedAt) // Newest first
            .ToList();

        // Map to DTOs
        return myProposals.Select(t => new AgentTypologyDto(
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
