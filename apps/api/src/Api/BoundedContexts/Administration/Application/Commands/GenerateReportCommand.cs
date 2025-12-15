using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to generate a report on-demand
/// ISSUE-916: Report generation
/// </summary>
internal sealed record GenerateReportCommand : ICommand<GenerateReportResult>
{
    public required ReportTemplate Template { get; init; }
    public required ReportFormat Format { get; init; }
    public required IReadOnlyDictionary<string, object> Parameters { get; init; }
}

/// <summary>
/// Result of report generation
/// </summary>
internal sealed record GenerateReportResult(
    Guid ExecutionId,
    string FileName,
    byte[] Content,
    long FileSizeBytes);
