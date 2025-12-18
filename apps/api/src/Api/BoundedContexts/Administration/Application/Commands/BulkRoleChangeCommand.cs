using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to change role for multiple users in bulk.
/// </summary>
/// <param name="UserIds">List of user IDs to change role for (max 1000).</param>
/// <param name="NewRole">The new role to assign (admin/user/editor).</param>
/// <param name="RequesterId">The ID of the admin requesting the operation.</param>
internal record BulkRoleChangeCommand(
    IReadOnlyList<Guid> UserIds,
    string NewRole,
    Guid RequesterId
) : ICommand<BulkOperationResult>;
