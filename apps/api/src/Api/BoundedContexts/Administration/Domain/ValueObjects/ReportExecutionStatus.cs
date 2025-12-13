namespace Api.BoundedContexts.Administration.Domain.ValueObjects;

/// <summary>
/// Value object representing report execution status
/// ISSUE-916: Tracks lifecycle of report generation
/// </summary>
public enum ReportExecutionStatus
{
    /// <summary>Report generation is in progress</summary>
    Running = 1,

    /// <summary>Report successfully generated</summary>
    Completed = 2,

    /// <summary>Report generation failed</summary>
    Failed = 3,

    /// <summary>Report generation was cancelled</summary>
    Cancelled = 4
}
