namespace Api.BoundedContexts.DocumentProcessing.Domain.Enums;

/// <summary>
/// Represents the status of an individual processing step.
/// Issue #4730: Processing queue management.
/// </summary>
public enum StepStatus
{
    /// <summary>Step has not started yet.</summary>
    Pending = 0,

    /// <summary>Step is currently executing.</summary>
    Running = 1,

    /// <summary>Step completed successfully.</summary>
    Completed = 2,

    /// <summary>Step failed during execution.</summary>
    Failed = 3,

    /// <summary>Step was skipped (e.g., upload already done).</summary>
    Skipped = 4
}
