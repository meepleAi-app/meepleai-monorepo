using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Query to retrieve a single <see cref="DTOs.CatalogSeedDraftDto"/> by id.
/// Returns <c>null</c> when no draft exists for the given id (endpoint layer
/// translates null → 404).
/// Spec: 2026-06-04-admin-catalog-seed-design.md §4.2 / §5.
/// Issue #1903 — M4.6.
/// </summary>
internal sealed record GetCatalogSeedByIdQuery(Guid Id) : IQuery<CatalogSeedDraftDto?>;
