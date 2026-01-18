using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to submit a shared game for approval.
/// Game must be in Draft status to be submitted.
/// Issue #2514: Approval workflow implementation
/// </summary>
internal record SubmitSharedGameForApprovalCommand(
    Guid GameId,
    Guid SubmittedBy
) : ICommand<Unit>;
