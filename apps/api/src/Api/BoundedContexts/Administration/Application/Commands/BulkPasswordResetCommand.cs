using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to reset passwords for multiple users in bulk.
/// </summary>
/// <param name="UserIds">List of user IDs to reset passwords for (max 1000).</param>
/// <param name="NewPassword">The new password to set for all users.</param>
/// <param name="RequesterId">The ID of the admin requesting the operation.</param>
public record BulkPasswordResetCommand(
    IReadOnlyList<Guid> UserIds,
    string NewPassword,
    Guid RequesterId
) : ICommand<BulkOperationResult>;

/// <summary>
/// Result of a bulk operation with success/failure tracking.
/// </summary>
/// <param name="TotalRequested">Total number of items requested for processing.</param>
/// <param name="SuccessCount">Number of items successfully processed.</param>
/// <param name="FailedCount">Number of items that failed processing.</param>
/// <param name="Errors">List of error messages for failed items.</param>
public record BulkOperationResult(
    int TotalRequested,
    int SuccessCount,
    int FailedCount,
    IReadOnlyList<string> Errors
);

/// <summary>
/// Generic result of a bulk operation with success/failure tracking and additional data.
/// </summary>
/// <typeparam name="TData">Type of additional data returned for each successful item.</typeparam>
/// <param name="TotalRequested">Total number of items requested for processing.</param>
/// <param name="SuccessCount">Number of items successfully processed.</param>
/// <param name="FailedCount">Number of items that failed processing.</param>
/// <param name="Errors">List of error messages for failed items.</param>
/// <param name="Data">Additional data for successful items (e.g., generated keys).</param>
public record BulkOperationResult<TData>(
    int TotalRequested,
    int SuccessCount,
    int FailedCount,
    IReadOnlyList<string> Errors,
    IReadOnlyList<TData> Data
);