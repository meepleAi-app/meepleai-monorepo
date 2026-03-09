#pragma warning disable MA0048 // File name must match type name - Contains related A/B test DTOs
namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// DTO for A/B test session (blind mode — no model info before evaluation).
/// Issue #5494: A/B Test CQRS commands and queries.
/// </summary>
internal sealed record AbTestSessionDto
{
    public Guid Id { get; init; }
    public Guid CreatedBy { get; init; }
    public string Query { get; init; } = default!;
    public Guid? KnowledgeBaseId { get; init; }
    public string Status { get; init; } = default!;
    public DateTime CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public decimal TotalCost { get; init; }
    public IReadOnlyList<AbTestVariantDto> Variants { get; init; } = [];
}

/// <summary>
/// DTO for A/B test variant (blind mode — Provider/ModelId hidden).
/// </summary>
internal sealed record AbTestVariantDto
{
    public Guid Id { get; init; }
    public string Label { get; init; } = default!;
    public string? Response { get; init; }
    public int TokensUsed { get; init; }
    public int LatencyMs { get; init; }
    public decimal CostUsd { get; init; }
    public bool Failed { get; init; }
    public string? ErrorMessage { get; init; }
    public AbTestEvaluationDto? Evaluation { get; init; }
}

/// <summary>
/// DTO for A/B test variant (revealed mode — includes model info).
/// </summary>
internal sealed record AbTestVariantRevealedDto
{
    public Guid Id { get; init; }
    public string Label { get; init; } = default!;
    public string Provider { get; init; } = default!;
    public string ModelId { get; init; } = default!;
    public string? Response { get; init; }
    public int TokensUsed { get; init; }
    public int LatencyMs { get; init; }
    public decimal CostUsd { get; init; }
    public bool Failed { get; init; }
    public string? ErrorMessage { get; init; }
    public AbTestEvaluationDto? Evaluation { get; init; }
}

/// <summary>
/// DTO for evaluation scores.
/// </summary>
internal sealed record AbTestEvaluationDto(
    Guid EvaluatorId,
    int Accuracy,
    int Completeness,
    int Clarity,
    int Tone,
    string? Notes,
    decimal AverageScore,
    DateTime EvaluatedAt);

/// <summary>
/// DTO for revealed A/B test session (after evaluation — includes model info).
/// </summary>
internal sealed record AbTestSessionRevealedDto
{
    public Guid Id { get; init; }
    public Guid CreatedBy { get; init; }
    public string Query { get; init; } = default!;
    public Guid? KnowledgeBaseId { get; init; }
    public string Status { get; init; } = default!;
    public DateTime CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public decimal TotalCost { get; init; }
    public string? WinnerLabel { get; init; }
    public string? WinnerModelId { get; init; }
    public IReadOnlyList<AbTestVariantRevealedDto> Variants { get; init; } = [];
}

/// <summary>
/// Paginated list response.
/// </summary>
internal sealed record AbTestSessionListDto(
    IReadOnlyList<AbTestSessionDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

/// <summary>
/// Aggregated analytics DTO.
/// </summary>
internal sealed record AbTestAnalyticsDto
{
    public int TotalTests { get; init; }
    public int CompletedTests { get; init; }
    public decimal TotalCost { get; init; }
    public IReadOnlyList<ModelWinRateDto> ModelWinRates { get; init; } = [];
    public IReadOnlyList<ModelAvgScoreDto> ModelAvgScores { get; init; } = [];
}

/// <summary>
/// Win rate per model.
/// </summary>
internal sealed record ModelWinRateDto(string ModelId, int Wins, int Total, decimal WinRate);

/// <summary>
/// Average evaluation score per model.
/// </summary>
internal sealed record ModelAvgScoreDto(string ModelId, decimal AvgAccuracy, decimal AvgCompleteness, decimal AvgClarity, decimal AvgTone, decimal AvgOverall, int EvaluationCount);
