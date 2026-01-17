using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to approve a shared game publication.
/// Game must be in PendingApproval status to be approved.
/// Issue #2514: Approval workflow implementation
/// </summary>
internal record ApproveSharedGamePublicationCommand(
    Guid GameId,
    Guid ApprovedBy
) : ICommand<Unit>;
