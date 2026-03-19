using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handler for GetPendingTypologiesCountQuery (Admin dashboard badge).
/// Returns count of typologies awaiting approval.
/// Issue #3381: Typology Approval Workflow Endpoint.
/// </summary>
internal sealed class GetPendingTypologiesCountQueryHandler : IRequestHandler<GetPendingTypologiesCountQuery, int>
{
    private readonly IAgentTypologyRepository _repository;

    public GetPendingTypologiesCountQueryHandler(IAgentTypologyRepository repository) =>
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<int> Handle(
        GetPendingTypologiesCountQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var allTypologies = await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);

        // Count pending typologies
        return allTypologies.Count(t => t.Status == TypologyStatus.Pending);
    }
}
