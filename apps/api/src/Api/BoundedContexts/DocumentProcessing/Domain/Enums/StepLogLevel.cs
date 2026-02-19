namespace Api.BoundedContexts.DocumentProcessing.Domain.Enums;

/// <summary>
/// Log severity level for processing step log entries.
/// Issue #4730: Processing queue management.
/// </summary>
public enum StepLogLevel
{
    Info = 0,
    Warning = 1,
    Error = 2
}
