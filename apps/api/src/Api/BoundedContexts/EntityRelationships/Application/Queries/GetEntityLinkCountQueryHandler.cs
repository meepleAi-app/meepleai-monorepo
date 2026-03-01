using Api.BoundedContexts.EntityRelationships.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.EntityRelationships.Application.Queries;

/// <summary>
/// Handler for GetEntityLinkCountQuery (Issue #5136).
///
/// Returns the total count of links for the entity (as source or as target of
/// bidirectional links). Used by the MeepleCard badge.
/// </summary>
internal sealed class GetEntityLinkCountQueryHandler
    : IQueryHandler<GetEntityLinkCountQuery, int>
{
    private readonly IEntityLinkRepository _repository;

    public GetEntityLinkCountQueryHandler(IEntityLinkRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<int> Handle(
        GetEntityLinkCountQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await _repository.GetCountForEntityAsync(
            query.EntityType,
            query.EntityId,
            cancellationToken).ConfigureAwait(false);
    }
}
