using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// DTO for RulebookAnalysis.
/// Issue #2402: Rulebook Analysis Service
/// </summary>
public record RulebookAnalysisDto(
    Guid Id,
    Guid SharedGameId,
    Guid PdfDocumentId,
    string GameTitle,
    string Summary,
    List<string> KeyMechanics,
    VictoryConditionsDto? VictoryConditions,
    List<ResourceDto> Resources,
    List<GamePhaseDto> GamePhases,
    List<string> CommonQuestions,
    decimal ConfidenceScore,
    string Version,
    bool IsActive,
    GenerationSource Source,
    DateTime AnalyzedAt,
    Guid CreatedBy
);

/// <summary>
/// DTO for VictoryConditions value object.
/// </summary>
public record VictoryConditionsDto(
    string Primary,
    List<string> Alternatives,
    bool IsPointBased,
    int? TargetPoints
);

/// <summary>
/// DTO for Resource value object.
/// </summary>
public record ResourceDto(
    string Name,
    string Type,
    string? Usage,
    bool IsLimited
);

/// <summary>
/// DTO for GamePhase value object.
/// </summary>
public record GamePhaseDto(
    string Name,
    string Description,
    int Order,
    bool IsOptional
);

/// <summary>
/// Result DTO for analyze rulebook command.
/// Issue #2454: Supports both synchronous and background analysis.
/// </summary>
public record AnalyzeRulebookResultDto(
    RulebookAnalysisDto? Analysis,
    DateTime AnalyzedAt
)
{
    /// <summary>
    /// Indicates if analysis is running in background (async).
    /// </summary>
    public bool IsBackgroundTask { get; init; }

    /// <summary>
    /// Task ID for background processing (null for synchronous).
    /// </summary>
    public string? TaskId { get; init; }

    /// <summary>
    /// Creates result for synchronous analysis (less than 30k chars).
    /// </summary>
    public static AnalyzeRulebookResultDto CreateSynchronous(RulebookAnalysisDto analysis) =>
        new(analysis, DateTime.UtcNow)
        {
            IsBackgroundTask = false
        };

    /// <summary>
    /// Creates result for background analysis (greater than 30k chars).
    /// </summary>
    public static AnalyzeRulebookResultDto CreateBackgroundTask(string taskId) =>
        new(null, DateTime.UtcNow)
        {
            IsBackgroundTask = true,
            TaskId = taskId
        };
};
