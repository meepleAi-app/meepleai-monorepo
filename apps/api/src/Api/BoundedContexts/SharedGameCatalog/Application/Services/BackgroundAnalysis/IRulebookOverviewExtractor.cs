using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.BackgroundAnalysis;

/// <summary>
/// Service for extracting high-level overview from rulebook content.
/// First phase of multi-phase background analysis.
/// </summary>
public interface IRulebookOverviewExtractor
{
    /// <summary>
    /// Extracts a concise overview from the full rulebook content.
    /// </summary>
    /// <param name="gameName">Name of the game for context</param>
    /// <param name="rulebookContent">Full rulebook text content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Overview result with game context and summary</returns>
    Task<OverviewExtractionResult> ExtractOverviewAsync(
        string gameName,
        string rulebookContent,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of overview extraction phase.
/// </summary>
public sealed record OverviewExtractionResult
{
    public string GameTitle { get; init; } = string.Empty;
    public string GameSummary { get; init; } = string.Empty;
    public List<string> MainMechanics { get; init; } = [];
    public string VictoryConditionSummary { get; init; } = string.Empty;
    public int PlayerCountMin { get; init; }
    public int PlayerCountMax { get; init; }
    public int PlaytimeMinutes { get; init; }
    public List<string> SectionHeaders { get; init; } = [];

    public static OverviewExtractionResult Create(
        string gameTitle,
        string gameSummary,
        List<string> mainMechanics,
        string victoryConditionSummary,
        int playerCountMin,
        int playerCountMax,
        int playtimeMinutes,
        List<string> sectionHeaders) => new()
    {
        GameTitle = gameTitle,
        GameSummary = gameSummary,
        MainMechanics = mainMechanics,
        VictoryConditionSummary = victoryConditionSummary,
        PlayerCountMin = playerCountMin,
        PlayerCountMax = playerCountMax,
        PlaytimeMinutes = playtimeMinutes,
        SectionHeaders = sectionHeaders
    };
}
