namespace Api.BoundedContexts.GameManagement.Application.DTOs.GameSessionContext;

/// <summary>
/// Indicates the level of AI-assisted features available for a game session
/// based on the presence of indexed PDFs and vector documents.
/// Issue #5579: GameSessionContext cross-context orchestrator.
/// </summary>
public enum SessionDegradationLevel
{
    /// <summary>All games (primary + expansions) have indexed PDFs and vector documents.</summary>
    Full = 0,

    /// <summary>Primary game has PDF but one or more expansions are missing PDFs.</summary>
    Partial = 1,

    /// <summary>Primary game has no PDF — only basic game info available.</summary>
    BasicOnly = 2,

    /// <summary>No vector documents at all — AI features completely unavailable.</summary>
    NoAI = 3
}
