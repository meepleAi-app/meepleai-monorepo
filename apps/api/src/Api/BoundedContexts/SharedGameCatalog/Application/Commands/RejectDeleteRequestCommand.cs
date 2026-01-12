using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to reject a delete request for a shared game.
/// Only admins can reject delete requests.
/// Rejecting will deny the deletion and keep the game active.
/// </summary>
internal record RejectDeleteRequestCommand(
    Guid RequestId,
    Guid RejectedBy,
    string Reason
) : ICommand<Unit>;
