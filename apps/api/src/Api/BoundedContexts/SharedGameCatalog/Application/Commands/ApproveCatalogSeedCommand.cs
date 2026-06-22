using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to approve a catalog seed draft. The draft must be in <c>Fetched</c>
/// status (provider data already retrieved). Approval transitions the draft to
/// <c>Approved</c> and emits a <see cref="Domain.Events.CatalogSeedApprovedEvent"/>
/// for downstream SharedGameCatalogEntry materialization (handled in M5).
/// Spec: 2026-06-04-admin-catalog-seed-design.md §4.2 / §6.
/// Issue #1903 — M4.4.
/// </summary>
internal sealed record ApproveCatalogSeedCommand(
    Guid DraftId,
    Guid ApprovedByUserId) : ICommand<ApproveCatalogSeedResult>;

/// <summary>
/// Result of approving a catalog seed draft.
/// </summary>
/// <param name="DraftId">The id of the approved draft.</param>
/// <param name="ResultingSharedGameId">
/// Placeholder id assigned to <c>CatalogSeedDraftEntity.ResultingSharedGameId</c>.
/// The actual <c>SharedGameCatalogEntry</c> row is created asynchronously by the
/// <c>CatalogSeedApprovedEventHandler</c> (see M5); that handler is responsible
/// for updating this id to the real entry once materialized.
/// </param>
internal sealed record ApproveCatalogSeedResult(Guid DraftId, Guid ResultingSharedGameId);
