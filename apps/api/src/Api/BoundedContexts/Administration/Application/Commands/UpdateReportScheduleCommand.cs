using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to update or cancel a scheduled report
/// ISSUE-916: Schedule management
/// </summary>
internal sealed record UpdateReportScheduleCommand : ICommand<bool>
{
    public required Guid ReportId { get; init; }
    public required string? ScheduleExpression { get; init; } // null to cancel
    public required bool IsActive { get; init; }
}
