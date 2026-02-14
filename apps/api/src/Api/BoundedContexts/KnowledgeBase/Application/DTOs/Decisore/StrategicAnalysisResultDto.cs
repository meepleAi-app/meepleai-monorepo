namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs.Decisore;

/// <summary>
/// DTO for strategic analysis results from Decisore Agent.
/// Issue #3769: Complete analysis with suggestions, position evaluation, and strategic insights.
/// </summary>
public sealed record StrategicAnalysisResultDto
{
    public required List<MoveSuggestionDto> Suggestions { get; init; }
    public required double PositionStrength { get; init; }  // -1 (losing) to +1 (winning)
    public required string RiskLevel { get; init; }  // "low", "medium", "high"
    public required List<string> VictoryPaths { get; init; }
    public required double Confidence { get; init; }  // 0-1
    public required int ExecutionTimeMs { get; init; }
    public required DateTime Timestamp { get; init; }
}

/// <summary>
/// DTO for individual move suggestion with reasoning.
/// </summary>
public sealed record MoveSuggestionDto
{
    public required string Move { get; init; }  // Algebraic notation
    public required string? Position { get; init; }  // Target square
    public required double Score { get; init; }  // 0-1
    public required string Reasoning { get; init; }
    public required List<string> Pros { get; init; }
    public required List<string> Cons { get; init; }
    public required string ExpectedOutcome { get; init; }
}
