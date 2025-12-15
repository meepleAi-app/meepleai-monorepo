using Api.SharedKernel.Application.Interfaces;
using Api.Models;

namespace Api.BoundedContexts.Administration.Application.Queries.PromptEvaluation;

/// <summary>
/// Query to retrieve historical evaluation results for a prompt template
/// ADMIN-01 Phase 4: Prompt Testing Framework
/// </summary>
internal sealed record GetEvaluationHistoryQuery : IQuery<List<PromptEvaluationResult>>
{
    /// <summary>Template ID to get evaluation history for</summary>
    public required Guid TemplateId { get; init; }

    /// <summary>Maximum number of results to return (default: 10)</summary>
    public int Limit { get; init; } = 10;
}
