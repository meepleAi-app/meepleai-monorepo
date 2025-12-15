using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to get report execution history
/// ISSUE-916: Execution tracking
/// </summary>
internal sealed record GetReportExecutionsQuery : IQuery<IReadOnlyList<ReportExecutionDto>>
{
    public Guid? ReportId { get; init; }
    public int Limit { get; init; } = 100;
}

/// <summary>
/// DTO for report execution
/// </summary>
internal sealed record ReportExecutionDto(
    Guid Id,
    Guid ReportId,
    DateTime StartedAt,
    DateTime? CompletedAt,
    ReportExecutionStatus Status,
    string? ErrorMessage,
    string? OutputPath,
    long? FileSizeBytes,
    TimeSpan? Duration);
