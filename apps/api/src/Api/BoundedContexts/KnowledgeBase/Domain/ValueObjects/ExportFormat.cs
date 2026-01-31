namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Export format for chat thread data.
/// </summary>
internal enum ExportFormat
{
    /// <summary>
    /// JSON format - machine-readable, structured data.
    /// </summary>
    Json,

    /// <summary>
    /// Markdown format - human-readable, formatted text.
    /// </summary>
    Markdown
}
