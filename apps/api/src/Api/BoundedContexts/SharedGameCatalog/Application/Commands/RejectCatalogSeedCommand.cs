using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to reject a catalog seed draft. Soft-deletes the draft (audit-retained
/// per spec §4.2) and emits <see cref="Domain.Events.CatalogSeedRejectedEvent"/>
/// for downstream metrics / notification.
/// Spec: 2026-06-04-admin-catalog-seed-design.md §4.2 / §6.
/// Issue #1903 — M4.5.
/// </summary>
internal sealed record RejectCatalogSeedCommand(
    Guid DraftId,
    Guid RejectedByUserId,
    string Reason) : ICommand;
