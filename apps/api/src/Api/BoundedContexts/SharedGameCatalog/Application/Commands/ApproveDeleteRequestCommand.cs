using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to approve a delete request for a shared game.
/// Only admins can approve delete requests.
/// Approving a delete request will immediately delete the game.
/// </summary>
internal record ApproveDeleteRequestCommand(
    Guid RequestId,
    Guid ApprovedBy,
    string? Comment
) : ICommand<Unit>;
