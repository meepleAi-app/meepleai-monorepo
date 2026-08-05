using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Epic #3470 Slice 3a-3 — revokes a game's admin-set manual cover: clears the manual cover
/// columns + license attestation and best-effort deletes the R2 object. Idempotent: revoking a
/// game with no manual cover is a no-op (still 204); only a missing game is a 404.
/// <para>#3495 H6 — an optional takedown <paramref name="Reason"/> is captured in the
/// <c>cover.manual.revoked</c> audit event (committed in the same transaction as the DB null-out).</para>
/// </summary>
internal record RevokeManualCoverCommand(
    Guid GameId,
    Guid AdminId,
    string? Reason = null) : ICommand<Unit>;
