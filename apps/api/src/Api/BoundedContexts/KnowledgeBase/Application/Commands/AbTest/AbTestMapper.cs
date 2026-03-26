using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.AbTest;

/// <summary>
/// Maps AbTestSession domain entities to DTOs.
/// Issue #5494: A/B Test CQRS commands and queries.
/// </summary>
internal static class AbTestMapper
{
    /// <summary>
    /// Maps to blind DTO (no model info visible).
    /// </summary>
    public static AbTestSessionDto ToBlindDto(AbTestSession session)
    {
        return new AbTestSessionDto
        {
            Id = session.Id,
            CreatedBy = session.CreatedBy,
            Query = session.Query,
            KnowledgeBaseId = session.KnowledgeBaseId,
            Status = session.Status.ToString(),
            CreatedAt = session.CreatedAt,
            CompletedAt = session.CompletedAt,
            TotalCost = session.TotalCost,
            Variants = session.Variants.Select(ToBlindVariantDto).ToList()
        };
    }

    /// <summary>
    /// Maps to revealed DTO (includes model info — only for evaluated sessions).
    /// </summary>
    public static AbTestSessionRevealedDto ToRevealedDto(AbTestSession session)
    {
        var winner = session.GetWinner();
        return new AbTestSessionRevealedDto
        {
            Id = session.Id,
            CreatedBy = session.CreatedBy,
            Query = session.Query,
            KnowledgeBaseId = session.KnowledgeBaseId,
            Status = session.Status.ToString(),
            CreatedAt = session.CreatedAt,
            CompletedAt = session.CompletedAt,
            TotalCost = session.TotalCost,
            WinnerLabel = winner?.Label,
            WinnerModelId = winner?.ModelId,
            Variants = session.Variants.Select(ToRevealedVariantDto).ToList()
        };
    }

    private static AbTestVariantDto ToBlindVariantDto(AbTestVariant variant)
    {
        return new AbTestVariantDto
        {
            Id = variant.Id,
            Label = variant.Label,
            Response = variant.Response,
            TokensUsed = variant.TokensUsed,
            LatencyMs = variant.LatencyMs,
            CostUsd = variant.CostUsd,
            Failed = variant.Failed,
            ErrorMessage = variant.ErrorMessage,
            Evaluation = MapEvaluation(variant.Evaluation)
        };
    }

    private static AbTestVariantRevealedDto ToRevealedVariantDto(AbTestVariant variant)
    {
        return new AbTestVariantRevealedDto
        {
            Id = variant.Id,
            Label = variant.Label,
            Provider = variant.Provider,
            ModelId = variant.ModelId,
            Response = variant.Response,
            TokensUsed = variant.TokensUsed,
            LatencyMs = variant.LatencyMs,
            CostUsd = variant.CostUsd,
            Failed = variant.Failed,
            ErrorMessage = variant.ErrorMessage,
            Evaluation = MapEvaluation(variant.Evaluation)
        };
    }

    private static AbTestEvaluationDto? MapEvaluation(Domain.ValueObjects.AbTestEvaluation? eval)
    {
        if (eval is null)
            return null;

        return new AbTestEvaluationDto(
            eval.EvaluatorId,
            eval.Accuracy,
            eval.Completeness,
            eval.Clarity,
            eval.Tone,
            eval.Notes,
            eval.AverageScore,
            eval.EvaluatedAt);
    }
}
