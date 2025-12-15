using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to delete a user account.
/// Prevents self-deletion and deletion of the last admin.
/// </summary>
internal record DeleteUserCommand(
    string UserId,
    string RequestingUserId
) : ICommand;
