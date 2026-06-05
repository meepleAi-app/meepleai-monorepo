using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for <see cref="GetCatalogSeedByIdQuery"/>.
/// Returns null when no draft exists (endpoint converts to 404).
/// Spec: 2026-06-04-admin-catalog-seed-design.md §4.2 / §5.
/// Issue #1903 — M4.6.
/// </summary>
internal sealed class GetCatalogSeedByIdQueryHandler
    : IQueryHandler<GetCatalogSeedByIdQuery, CatalogSeedDraftDto?>
{
    private readonly ICatalogSeedDraftRepository _repository;

    public GetCatalogSeedByIdQueryHandler(ICatalogSeedDraftRepository repository)
        => _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<CatalogSeedDraftDto?> Handle(
        GetCatalogSeedByIdQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = await _repository
            .GetByIdAsync(request.Id, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : CatalogSeedDraftDtoMapper.ToDto(entity);
    }
}
