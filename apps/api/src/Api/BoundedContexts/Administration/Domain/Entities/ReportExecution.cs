using Api.BoundedContexts.Administration.Domain.ValueObjects;

namespace Api.BoundedContexts.Administration.Domain.Entities;

/// <summary>
/// Domain entity representing a report execution instance
/// ISSUE-916: Tracks individual report executions for audit and monitoring
/// </summary>
public sealed record ReportExecution
{
    public required Guid Id { get; init; }
    public required Guid ReportId { get; init; }
    public required DateTime StartedAt { get; init; }
    public required DateTime? CompletedAt { get; init; }
    public required ReportExecutionStatus Status { get; init; }
    public required string? ErrorMessage { get; init; }
    public required string? OutputPath { get; init; }  // Storage path for generated report
    public required long? FileSizeBytes { get; init; }
    public required TimeSpan? Duration { get; init; }
    public required IReadOnlyDictionary<string, object> ExecutionMetadata { get; init; }

    /// <summary>
    /// Creates a new report execution
    /// </summary>
    public static ReportExecution Create(Guid reportId)
    {
        return new ReportExecution
        {
            Id = Guid.NewGuid(),
            ReportId = reportId,
            StartedAt = DateTime.UtcNow,
            CompletedAt = null,
            Status = ReportExecutionStatus.Running,
            ErrorMessage = null,
            OutputPath = null,
            FileSizeBytes = null,
            Duration = null,
            ExecutionMetadata = new Dictionary<string, object>()
        };
    }

    /// <summary>
    /// Marks the execution as successfully completed
    /// </summary>
    public ReportExecution Complete(string outputPath, long fileSizeBytes)
    {
        var completedAt = DateTime.UtcNow;
        return this with
        {
            CompletedAt = completedAt,
            Status = ReportExecutionStatus.Completed,
            OutputPath = outputPath,
            FileSizeBytes = fileSizeBytes,
            Duration = completedAt - StartedAt
        };
    }

    /// <summary>
    /// Marks the execution as failed
    /// </summary>
    public ReportExecution Fail(string errorMessage)
    {
        var completedAt = DateTime.UtcNow;
        return this with
        {
            CompletedAt = completedAt,
            Status = ReportExecutionStatus.Failed,
            ErrorMessage = errorMessage,
            Duration = completedAt - StartedAt
        };
    }

    /// <summary>
    /// Adds execution metadata
    /// </summary>
    public ReportExecution WithMetadata(string key, object value)
    {
        var metadata = new Dictionary<string, object>(ExecutionMetadata)
        {
            [key] = value
        };
        return this with { ExecutionMetadata = metadata };
    }
}
