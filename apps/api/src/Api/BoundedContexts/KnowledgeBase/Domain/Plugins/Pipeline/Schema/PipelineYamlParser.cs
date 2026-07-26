// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3416 - Pipeline Definition Schema
// =============================================================================

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Pipeline.Schema;

/// <summary>
/// Exception thrown when pipeline parsing fails.
/// </summary>
public sealed class PipelineParseException : Exception
{
    /// <summary>
    /// Creates a new parse exception.
    /// </summary>
    public PipelineParseException(string message) : base(message) { }

    /// <summary>
    /// Creates a new parse exception with inner exception.
    /// </summary>
    public PipelineParseException(string message, Exception innerException)
        : base(message, innerException) { }
}
