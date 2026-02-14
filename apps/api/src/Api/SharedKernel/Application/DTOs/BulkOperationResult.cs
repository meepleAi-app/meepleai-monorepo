namespace Api.SharedKernel.Application.DTOs;

/// <summary>
/// Result of a bulk operation with success/failure tracking.
/// Shared DTO for all bulk operations across bounded contexts.
/// Issue #4268: Phase 3 - Bulk Collection Actions
/// </summary>
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
public record BulkOperationResult<TData>(
    int TotalRequested,
    int SuccessCount,
    int FailedCount,
    IReadOnlyList<string> Errors,
    IReadOnlyList<TData> Data
);
