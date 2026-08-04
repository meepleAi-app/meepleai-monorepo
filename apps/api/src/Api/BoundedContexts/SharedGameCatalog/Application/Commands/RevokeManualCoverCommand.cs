using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Epic #3470 Slice 3a-3 — revokes a game's admin-set manual cover: clears the manual cover
/// columns + license attestation and best-effort deletes the R2 object. Idempotent: revoking a
/// game with no manual cover is a no-op (still 204); only a missing game is a 404.
/// </summary>
internal record RevokeManualCoverCommand(
    Guid GameId,
    Guid AdminId) : ICommand<Unit>;
