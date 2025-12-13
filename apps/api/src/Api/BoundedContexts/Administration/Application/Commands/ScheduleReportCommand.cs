using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to schedule a recurring report
/// ISSUE-916: Report scheduling
/// ISSUE-918: Email delivery integration
/// </summary>
public sealed record ScheduleReportCommand : ICommand<Guid>
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required ReportTemplate Template { get; init; }
    public required ReportFormat Format { get; init; }
    public required IReadOnlyDictionary<string, object> Parameters { get; init; }
    public required string ScheduleExpression { get; init; } // Cron expression
    public required string CreatedBy { get; init; }
    public IReadOnlyList<string>? EmailRecipients { get; init; } // ISSUE-918: Optional email recipients
}
