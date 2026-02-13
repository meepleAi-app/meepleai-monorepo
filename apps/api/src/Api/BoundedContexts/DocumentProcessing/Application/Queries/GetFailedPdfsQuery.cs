using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Query to get paginated failed PDFs with filters.
/// Issue #3715.3: Failed PDF viewer with role-based access.
/// </summary>
public record GetFailedPdfsQuery(
    int Page,
    int PageSize,
    Guid? UserId,
    string? ErrorCategory,
    string? Tier,
    DateTime? StartDate,
    DateTime? EndDate,
    string UserRole
) : IRequest<PaginatedFailedPdfsDto>;

public record PaginatedFailedPdfsDto
{
    public required List<FailedPdfDto> Items { get; init; }
    public required int TotalCount { get; init; }
    public required int Page { get; init; }
    public required int TotalPages { get; init; }
}

public record FailedPdfDto
{
    public required Guid Id { get; init; }
    public required string FileName { get; init; }
    public required string? GameTitle { get; init; }
    public required string? UserEmail { get; init; }
    public required string Tier { get; init; }
    public required DateTime UploadedAt { get; init; }
    public required DateTime? FailedAt { get; init; }
    public required string FailedAtState { get; init; }
    public required string ErrorCategory { get; init; }
    public required string ErrorMessage { get; init; }
    public required int RetryCount { get; init; }
    public required bool CanRetry { get; init; }
}
