using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.AbTest;

/// <summary>
/// Command to evaluate variants in an A/B test session.
/// Issue #5494: A/B Test CQRS commands and queries.
/// </summary>
internal sealed record EvaluateAbTestCommand(
    Guid SessionId,
    Guid EvaluatorId,
    List<VariantEvaluationInput> Evaluations
) : ICommand<AbTestSessionRevealedDto>;

/// <summary>
/// Input for a single variant evaluation.
/// </summary>
internal sealed record VariantEvaluationInput(
    string Label,
    int Accuracy,
    int Completeness,
    int Clarity,
    int Tone,
    string? Notes = null);
