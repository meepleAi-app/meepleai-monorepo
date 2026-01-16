using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to reject a shared game publication.
/// Game must be in PendingApproval status to be rejected.
/// Issue #2514: Approval workflow implementation
/// </summary>
internal record RejectSharedGamePublicationCommand(
    Guid GameId,
    Guid RejectedBy,
    string Reason
) : ICommand<Unit>;
