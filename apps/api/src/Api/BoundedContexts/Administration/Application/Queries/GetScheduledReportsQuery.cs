using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to get all scheduled reports
/// ISSUE-916: Report listing
/// </summary>
public sealed record GetScheduledReportsQuery : IQuery<IReadOnlyList<ScheduledReportDto>>;

/// <summary>
/// DTO for scheduled report
/// </summary>
public sealed record ScheduledReportDto(
    Guid Id,
    string Name,
    string Description,
    ReportTemplate Template,
    ReportFormat Format,
    string? ScheduleExpression,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastExecutedAt,
    string CreatedBy);
