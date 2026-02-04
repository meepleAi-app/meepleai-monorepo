// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3415 - DAG Orchestrator
// =============================================================================

using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Pipeline.Models;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Pipeline.Services;

/// <summary>
/// Orchestrates the execution of a pipeline DAG.
/// </summary>
public interface IDagOrchestrator
{
    /// <summary>
    /// Executes a pipeline with the given input.
    /// </summary>
    /// <param name="pipeline">The pipeline definition to execute.</param>
    /// <param name="input">The initial input for the pipeline.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The pipeline execution result.</returns>
    Task<PipelineResult> ExecuteAsync(
        PipelineDefinition pipeline,
        PluginInput input,
        CancellationToken cancellationToken = default);
}
