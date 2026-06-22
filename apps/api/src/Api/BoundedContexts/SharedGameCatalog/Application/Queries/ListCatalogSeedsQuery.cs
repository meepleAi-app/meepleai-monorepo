using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Query to list <see cref="DTOs.CatalogSeedDraftDto"/> rows with optional status
/// filter and pagination. Ordered by CreatedAt DESC (most recent first).
/// Spec: 2026-06-04-admin-catalog-seed-design.md §4.2 / §5.
/// Issue #1903 — M4.6.
/// </summary>
internal sealed record ListCatalogSeedsQuery(
    string? StatusFilter,
    int Skip = 0,
    int Take = 50) : IQuery<ListCatalogSeedsResult>;

/// <summary>
/// Paginated result of <see cref="ListCatalogSeedsQuery"/>.
/// </summary>
/// <param name="Total">Total rows matching the filter (across all pages).</param>
/// <param name="Skip">Echo of the requested skip offset.</param>
/// <param name="Take">Echo of the requested page size.</param>
/// <param name="Items">Current page of results.</param>
internal sealed record ListCatalogSeedsResult(
    int Total,
    int Skip,
    int Take,
    IReadOnlyList<CatalogSeedDraftDto> Items);
