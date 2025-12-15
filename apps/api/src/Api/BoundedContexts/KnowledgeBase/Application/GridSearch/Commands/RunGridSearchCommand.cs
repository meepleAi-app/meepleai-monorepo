using Api.BoundedContexts.KnowledgeBase.Domain.GridSearch;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.GridSearch.Commands;

/// <summary>
/// ADR-016 Phase 5: Command to run grid search evaluation across multiple configurations.
/// </summary>
internal sealed record RunGridSearchCommand : IRequest<GridSearchResult>
{
    /// <summary>
    /// Path to the dataset file to use for evaluation.
    /// If null, uses the default MeepleAI custom dataset.
    /// </summary>
    public string? DatasetPath { get; init; }

    /// <summary>
    /// Specific configurations to run. If null, runs all configurations.
    /// </summary>
    public IReadOnlyList<string>? ConfigurationIds { get; init; }

    /// <summary>
    /// Whether to run a quick evaluation (3 representative configs).
    /// </summary>
    public bool QuickMode { get; init; } = false;

    /// <summary>
    /// Maximum number of samples to evaluate per configuration.
    /// Null means evaluate all samples.
    /// </summary>
    public int? MaxSamplesPerConfig { get; init; }

    /// <summary>
    /// Whether to include detailed per-sample results in the output.
    /// </summary>
    public bool IncludeDetailedResults { get; init; } = false;
}
