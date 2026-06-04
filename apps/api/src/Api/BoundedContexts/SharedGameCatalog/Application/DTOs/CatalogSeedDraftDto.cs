using Api.Infrastructure.Entities.SharedGameCatalog;

namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// Read model for <see cref="CatalogSeedDraftEntity"/>. Used by admin UI listings
/// and detail views. Excludes the full ProvenanceJson / RawPayloadJson bulk payload
/// — those are fetched separately by a dedicated detail endpoint in M4.7.
/// Spec: 2026-06-04-admin-catalog-seed-design.md §4.2 / §5.
/// Issue #1903 — M4.6.
/// </summary>
internal sealed record CatalogSeedDraftDto(
    Guid Id,
    int? BggId,
    string? WikidataQid,
    string? SearchTermInput,
    string Status,
    string? ErrorMessage,
    Guid? ResultingSharedGameId,
    Guid CreatedByUserId,
    DateTime CreatedAt,
    DateTime? FetchedAt,
    DateTime? ApprovedAt,
    Guid? ApprovedByUserId);

/// <summary>
/// Helpers to project entities into DTOs.
/// </summary>
internal static class CatalogSeedDraftDtoMapper
{
    public static CatalogSeedDraftDto ToDto(CatalogSeedDraftEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return new CatalogSeedDraftDto(
            entity.Id,
            entity.BggId,
            entity.WikidataQid,
            entity.SearchTermInput,
            entity.Status,
            entity.ErrorMessage,
            entity.ResultingSharedGameId,
            entity.CreatedByUserId,
            entity.CreatedAt,
            entity.FetchedAt,
            entity.ApprovedAt,
            entity.ApprovedByUserId);
    }
}
