using Api.SharedKernel.Application.Interfaces;
using Api.Models;

namespace Api.BoundedContexts.Administration.Application.Commands.PromptEvaluation;

/// <summary>
/// Command to perform A/B comparison between two prompt versions
/// Evaluates both versions and generates recommendation (Activate, Reject, or Manual Review)
/// ADMIN-01 Phase 4: Prompt Testing Framework
/// </summary>
internal sealed record ComparePromptVersionsCommand : ICommand<PromptComparisonResult>
{
    /// <summary>Prompt template ID</summary>
    public required string TemplateId { get; init; }

    /// <summary>Baseline version ID (usually current active)</summary>
    public required string BaselineVersionId { get; init; }

    /// <summary>Candidate version ID (new version to test)</summary>
    public required string CandidateVersionId { get; init; }

    /// <summary>Path to test dataset JSON file</summary>
    public required string DatasetPath { get; init; }
}
