using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.Models;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Orchestrates the playground chat pipeline: RAG retrieval, strategy-based LLM execution,
/// cost tracking, and telemetry assembly.
/// </summary>
internal interface IPlaygroundChatOrchestrator
{
    /// <summary>
    /// Executes the full playground pipeline and returns a stream of SSE events.
    /// </summary>
    IAsyncEnumerable<RagStreamingEvent> ExecuteAsync(
        PlaygroundChatRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Input model for the playground chat pipeline.
/// Decouples the command from the orchestrator.
/// </summary>
internal sealed record PlaygroundChatRequest(
    Guid AgentDefinitionId,
    string Message,
    Guid? GameId,
    string? Strategy,
    string? ModelOverride,
    string? ProviderOverride = null);
