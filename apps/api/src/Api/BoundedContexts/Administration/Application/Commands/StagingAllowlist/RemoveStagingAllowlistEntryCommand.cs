using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.StagingAllowlist;

/// <summary>
/// Soft-deletes a staging allowlist entry by Id.
/// </summary>
/// <param name="EntryId">PK of the entry to remove.</param>
/// <param name="RemovedByUserId">User performing the removal (superadmin).</param>
public sealed record RemoveStagingAllowlistEntryCommand(
    Guid EntryId,
    Guid? RemovedByUserId) : IRequest<Unit>;
