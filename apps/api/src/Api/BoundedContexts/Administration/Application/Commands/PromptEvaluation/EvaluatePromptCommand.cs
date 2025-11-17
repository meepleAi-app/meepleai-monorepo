using Api.BoundedContexts.Administration.Application.Abstractions;
using Api.Models;

namespace Api.BoundedContexts.Administration.Application.Commands.PromptEvaluation;

/// <summary>
/// Command to evaluate a prompt version using a test dataset
/// Runs automated tests and calculates quality metrics
/// ADMIN-01 Phase 4: Prompt Testing Framework
/// </summary>
public sealed record EvaluatePromptCommand : ICommand<PromptEvaluationResult>
{
    /// <summary>Prompt template ID</summary>
    public required string TemplateId { get; init; }

    /// <summary>Prompt version ID to evaluate</summary>
    public required string VersionId { get; init; }

    /// <summary>Path to test dataset JSON file</summary>
    public required string DatasetPath { get; init; }

    /// <summary>Whether to store results in database (default: true)</summary>
    public bool StoreResults { get; init; } = true;
}
