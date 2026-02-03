using Api.BoundedContexts.Administration.Application.Commands;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to approve multiple share requests in bulk (editor dashboard).
/// Issue #2893: Bulk approval for efficient editor workflow.
/// </summary>
/// <param name="ShareRequestIds">List of share request IDs to approve (max 20).</param>
/// <param name="EditorId">The ID of the editor performing the bulk approval.</param>
/// <param name="TargetSharedGameId">Optional: Target shared game ID (for AdditionalContent contributions).</param>
/// <param name="AdminNotes">Optional: Notes from the editor about the bulk approval.</param>
internal record BulkApproveShareRequestsCommand(
    IReadOnlyList<Guid> ShareRequestIds,
    Guid EditorId,
    Guid? TargetSharedGameId = null,
    string? AdminNotes = null
) : ICommand<BulkOperationResult>;
