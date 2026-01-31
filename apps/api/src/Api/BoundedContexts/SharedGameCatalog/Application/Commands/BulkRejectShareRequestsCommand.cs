using Api.BoundedContexts.Administration.Application.Commands;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to reject multiple share requests in bulk (editor dashboard).
/// Issue #2893: Bulk rejection for efficient editor workflow.
/// </summary>
/// <param name="ShareRequestIds">List of share request IDs to reject (max 20).</param>
/// <param name="EditorId">The ID of the editor performing the bulk rejection.</param>
/// <param name="Reason">The reason for rejection (required, applied to all).</param>
internal record BulkRejectShareRequestsCommand(
    IReadOnlyList<Guid> ShareRequestIds,
    Guid EditorId,
    string Reason
) : ICommand<BulkOperationResult>;
