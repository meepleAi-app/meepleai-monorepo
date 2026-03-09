using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to replace a deprecated model with a suggested replacement across all affected strategies.
/// Issue #5499: Part of Epic #5490 - Model Versioning &amp; Availability Monitoring.
/// </summary>
/// <param name="DeprecatedModelId">The model ID being replaced.</param>
/// <param name="ReplacementModelId">The model ID to replace it with.</param>
internal record ApplyModelReplacementCommand(
    string DeprecatedModelId,
    string ReplacementModelId
) : ICommand<ApplyModelReplacementResult>;

/// <summary>
/// Result of applying a model replacement.
/// </summary>
/// <param name="UpdatedStrategies">Strategies that were updated.</param>
/// <param name="TotalUpdated">Number of strategy mappings updated.</param>
internal record ApplyModelReplacementResult(
    string[] UpdatedStrategies,
    int TotalUpdated);
