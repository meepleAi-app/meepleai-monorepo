using Api.SharedKernel.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to reset passwords for multiple users in bulk.
/// </summary>
/// <param name="UserIds">List of user IDs to reset passwords for (max 1000).</param>
/// <param name="NewPassword">The new password to set for all users.</param>
/// <param name="RequesterId">The ID of the admin requesting the operation.</param>
internal record BulkPasswordResetCommand(
    IReadOnlyList<Guid> UserIds,
    string NewPassword,
    Guid RequesterId
) : ICommand<BulkOperationResult>;
