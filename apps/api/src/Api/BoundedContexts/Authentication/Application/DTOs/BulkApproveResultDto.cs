namespace Api.BoundedContexts.Authentication.Application.DTOs;

public record BulkApproveResultDto(
    int Processed,
    int Succeeded,
    int Failed,
    IReadOnlyList<BulkApproveItemResult> Results);

public record BulkApproveItemResult(
    Guid Id,
    string Status,
    string? Error = null);
