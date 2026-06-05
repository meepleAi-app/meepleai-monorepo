using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for <see cref="ListCatalogSeedsQuery"/>.
/// Spec: 2026-06-04-admin-catalog-seed-design.md §4.2 / §5.
/// Issue #1903 — M4.6.
/// </summary>
internal sealed class ListCatalogSeedsQueryHandler
    : IQueryHandler<ListCatalogSeedsQuery, ListCatalogSeedsResult>
{
    private readonly ICatalogSeedDraftRepository _repository;

    public ListCatalogSeedsQueryHandler(ICatalogSeedDraftRepository repository)
        => _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<ListCatalogSeedsResult> Handle(
        ListCatalogSeedsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var total = await _repository
            .CountAsync(request.StatusFilter, cancellationToken)
            .ConfigureAwait(false);

        var entities = await _repository
            .ListAsync(request.StatusFilter, request.Skip, request.Take, cancellationToken)
            .ConfigureAwait(false);

        var items = entities.Select(CatalogSeedDraftDtoMapper.ToDto).ToList();

        return new ListCatalogSeedsResult(total, request.Skip, request.Take, items);
    }
}
