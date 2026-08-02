using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Covers;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Epic #3470 — removes the cover assignment for a UI context, resetting it to the
/// resolver's implicit precedence. Idempotent: removing an absent assignment is a
/// no-op (still 204), only a missing game is a 404.
/// </summary>
internal record RemoveCoverAssignmentCommand(
    Guid GameId,
    CoverContext Context,
    Guid AdminId) : ICommand<Unit>;
