namespace Api.BoundedContexts.GameManagement.Application.DTOs.GameSessionContext;

/// <summary>
/// Lightweight DTO summarizing a RulebookAnalysis for use in GameSessionContext.
/// Contains only the fields needed by the game night experience, not the full analysis.
/// Issue #5579: GameSessionContext cross-context orchestrator.
/// </summary>
public sealed record RulebookAnalysisSummaryDto(
    Guid GameId,
    string GameTitle,
    string Summary,
    IReadOnlyList<string> KeyMechanics,
    string? CurrentPhaseName,
    IReadOnlyList<string> PhaseNames);
