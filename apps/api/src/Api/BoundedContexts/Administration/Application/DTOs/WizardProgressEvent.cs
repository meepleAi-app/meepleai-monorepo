namespace Api.BoundedContexts.Administration.Application.DTOs;

/// <summary>
/// DTO for wizard-level progress events streamed via SSE.
/// Aggregates PdfDocument state + agent existence into a unified progress model.
/// </summary>
public sealed class WizardProgressEvent
{
    /// <summary>Current wizard step (e.g., "Extracting", "Chunking", "Ready")</summary>
    public required string CurrentStep { get; init; }

    /// <summary>PDF processing state as string</summary>
    public required string PdfState { get; init; }

    /// <summary>Whether an AI agent exists for this game</summary>
    public required bool AgentExists { get; init; }

    /// <summary>Overall progress percentage (0-100)</summary>
    public required int OverallPercent { get; init; }

    /// <summary>Human-readable status message</summary>
    public required string Message { get; init; }

    /// <summary>Whether the entire wizard flow is complete</summary>
    public required bool IsComplete { get; init; }

    /// <summary>Error message if processing failed</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Processing priority (Normal or Admin)</summary>
    public required string Priority { get; init; }

    /// <summary>UTC timestamp</summary>
    public required DateTime Timestamp { get; init; }
}
