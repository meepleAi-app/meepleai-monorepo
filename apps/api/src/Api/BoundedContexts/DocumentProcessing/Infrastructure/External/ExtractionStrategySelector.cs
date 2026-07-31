using Api.BoundedContexts.DocumentProcessing.Domain.Services;

#pragma warning disable MA0048 // File name must match type name - interface + implementation in one file
namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.External;

/// <summary>
/// DC-2 (#3419): per-request holder for the Unstructured extraction strategy. Registered
/// <b>scoped</b> so <c>PdfProcessingPipelineService</c> can set it per PDF and
/// <see cref="UnstructuredPdfTextExtractor"/> — resolved in the SAME DI scope via the
/// orchestrator's constructor graph (<c>OrchestratedPdfTextExtractor</c> →
/// <c>EnhancedPdfProcessingOrchestrator</c> → keyed extractor) — reads it. Because both
/// consumers are constructor-injected within one scope, the value the pipeline sets is visible
/// to the extractor regardless of the Quartz job's own scope. Defaults to
/// <see cref="ExtractionStrategy.Fast"/> (cheap, coordinate-aware; hi_res is reserved for tables).
/// </summary>
internal interface IExtractionStrategySelector
{
    ExtractionStrategy Current { get; set; }
}

/// <inheritdoc cref="IExtractionStrategySelector"/>
internal sealed class ExtractionStrategySelector : IExtractionStrategySelector
{
    public ExtractionStrategy Current { get; set; } = ExtractionStrategy.Fast;
}
