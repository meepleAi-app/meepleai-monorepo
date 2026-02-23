using Api.BoundedContexts.EntityRelationships.Application.DTOs;
using Api.BoundedContexts.EntityRelationships.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.EntityRelationships.Application.Queries;

/// <summary>
/// Handler for GetEntityLinksQuery (Issue #5135).
///
/// Retrieves all EntityLinks for the given entity (as source or as target of
/// bidirectional links). Populates IsOwner on each DTO when RequestingUserId is provided.
/// </summary>
internal sealed class GetEntityLinksQueryHandler
    : IQueryHandler<GetEntityLinksQuery, IReadOnlyList<EntityLinkDto>>
{
    private readonly IEntityLinkRepository _repository;

    public GetEntityLinksQueryHandler(IEntityLinkRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<IReadOnlyList<EntityLinkDto>> Handle(
        GetEntityLinksQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var links = await _repository.GetForEntityAsync(
            query.EntityType,
            query.EntityId,
            query.Scope,
            query.LinkType,
            query.TargetEntityType,
            cancellationToken).ConfigureAwait(false);

        return links
            .Select(link =>
            {
                var dto = EntityLinkDto.FromEntity(link);
                if (query.RequestingUserId.HasValue)
                    dto = dto with { IsOwner = link.OwnerUserId == query.RequestingUserId.Value };
                return dto;
            })
            .ToList()
            .AsReadOnly();
    }
}
