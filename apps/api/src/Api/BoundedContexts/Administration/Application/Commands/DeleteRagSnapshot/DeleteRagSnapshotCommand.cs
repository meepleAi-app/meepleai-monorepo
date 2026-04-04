using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.DeleteRagSnapshot;

/// <summary>
/// Command to delete a RAG backup snapshot from storage.
/// The "latest" snapshot is managed automatically and cannot be targeted by ID directly,
/// but callers are responsible for guard logic on the UI side.
/// </summary>
internal sealed record DeleteRagSnapshotCommand : IRequest
{
    /// <summary>
    /// The snapshot identifier (e.g. "2026-03-28T12-00-00Z"). Not the full path.
    /// </summary>
    public required string SnapshotId { get; init; }
}
