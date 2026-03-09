using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Api.SharedKernel.Constants;
using Api.BoundedContexts.SharedGameCatalog.Application.Configuration;
using Api.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.BackgroundAnalysis;

/// <summary>
/// LLM-based implementation of overview extraction for large rulebooks.
/// Extracts high-level context before detailed chunking and analysis.
/// </summary>
internal sealed partial class LlmRulebookOverviewExtractor : IRulebookOverviewExtractor
{
    private readonly ILlmService _llmService;
    private readonly BackgroundAnalysisOptions _options;
    private readonly ILogger<LlmRulebookOverviewExtractor> _logger;

    private const string SystemPrompt = """
        You are an expert board game analyst specializing in rulebook overview extraction.

        Your task: Extract high-level game context from a rulebook to guide detailed analysis.
        This is the first phase of multi-phase analysis for complex rulebooks.

        Requirements:
        - Extract game title, summary, and main mechanics
        - Identify player count range and playtime
        - Summarize victory conditions (not full detail)
        - Extract section headers for semantic chunking guidance
        - Be concise - this is overview only, details come later

        Return JSON with this structure:
        {
            "gameTitle": "Catan",
            "gameSummary": "Competitive resource management and settlement building",
            "mainMechanics": ["Resource Management", "Trading", "Dice Rolling"],
            "victoryConditionSummary": "First to 10 victory points wins",
            "playerCountMin": 3,
            "playerCountMax": 4,
            "playtimeMinutes": 90,
            "sectionHeaders": ["Setup", "Turn Structure", "Building", "Trading", "Victory"]
        }
        """;

    public LlmRulebookOverviewExtractor(
        ILlmService llmService,
        IOptions<BackgroundAnalysisOptions> options,
        ILogger<LlmRulebookOverviewExtractor> logger)
    {
        _llmService = llmService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<OverviewExtractionResult> ExtractOverviewAsync(
        string gameName,
        string rulebookContent,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Extracting overview for {GameName}, content length: {Length} chars",
            gameName, rulebookContent.Length);

        // For overview, sample beginning (first 10k), middle section, and end (last 2k)
        // This gives good coverage without processing entire content
        var sampledContent = SampleRulebookForOverview(rulebookContent);

        var userPrompt = $"""
            Game: {gameName}

            Rulebook Content (sampled for overview):
            {sampledContent}

            Extract high-level overview to guide detailed analysis.
            Focus on game structure, main mechanics, and section organization.
            """;

        try
        {
            var result = await _llmService.GenerateJsonAsync<LlmOverviewResponse>(
                SystemPrompt,
                userPrompt,
                RequestSource.RagPipeline,
                cancellationToken).ConfigureAwait(false);

            if (result is null)
            {
                _logger.LogWarning("LLM returned null overview, using fallback");
                return CreateFallbackOverview(gameName, rulebookContent);
            }

            _logger.LogInformation("Successfully extracted overview for {GameTitle}", result.GameTitle);

            return OverviewExtractionResult.Create(
                result.GameTitle,
                result.GameSummary,
                result.MainMechanics,
                result.VictoryConditionSummary,
                result.PlayerCountMin,
                result.PlayerCountMax,
                result.PlaytimeMinutes,
                result.SectionHeaders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting overview for {GameName}", gameName);
            return CreateFallbackOverview(gameName, rulebookContent);
        }
    }

    private string SampleRulebookForOverview(string content)
    {
        var beginningChars = _options.OverviewSampleBeginning;
        var middleChars = _options.OverviewSampleMiddle;
        var endChars = _options.OverviewSampleEnd;

        if (content.Length <= beginningChars + endChars)
            return content;

        var beginning = content.AsSpan(0, Math.Min(beginningChars, content.Length));
        var end = content.AsSpan(Math.Max(0, content.Length - endChars));

        // Sample middle section
        var middleStart = content.Length / 2 - middleChars / 2;
        var middle = content.AsSpan(
            Math.Max(0, middleStart),
            Math.Min(middleChars, content.Length - middleStart));

        return $"""
            [Beginning]
            {beginning.ToString()}

            [Middle Section]
            {middle.ToString()}

            [End]
            {end.ToString()}
            """;
    }

    private OverviewExtractionResult CreateFallbackOverview(string gameName, string rulebookContent)
    {
        _logger.LogInformation("Creating fallback overview for {GameName}", gameName);

        // Extract section headers via regex as fallback
        var headers = ExtractHeadersRegex().Matches(rulebookContent)
            .Select(m => m.Value.Trim())
            .Distinct(StringComparer.Ordinal)
            .Take(20)
            .ToList();

        return OverviewExtractionResult.Create(
            gameName,
            "Board game rulebook analysis in progress",
            ["Strategy", "Turn-based"],
            "See full analysis for victory conditions",
            playerCountMin: 2,
            playerCountMax: 6,
            playtimeMinutes: 60,
            sectionHeaders: headers.Count > 0 ? headers : ["Setup", "Gameplay", "Ending"]);
    }

    [GeneratedRegex(@"^#{1,3}\s+(.+)$|^([A-Z][A-Z\s]{2,})$", RegexOptions.Multiline | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ExtractHeadersRegex();

    // DTO for LLM response (internal for testability - Issue #2525)
    internal sealed record LlmOverviewResponse
    {
        [JsonPropertyName("gameTitle")]
        public string GameTitle { get; init; } = string.Empty;

        [JsonPropertyName("gameSummary")]
        public string GameSummary { get; init; } = string.Empty;

        [JsonPropertyName("mainMechanics")]
        public List<string> MainMechanics { get; init; } = [];

        [JsonPropertyName("victoryConditionSummary")]
        public string VictoryConditionSummary { get; init; } = string.Empty;

        [JsonPropertyName("playerCountMin")]
        public int PlayerCountMin { get; init; }

        [JsonPropertyName("playerCountMax")]
        public int PlayerCountMax { get; init; }

        [JsonPropertyName("playtimeMinutes")]
        public int PlaytimeMinutes { get; init; }

        [JsonPropertyName("sectionHeaders")]
        public List<string> SectionHeaders { get; init; } = [];
    }
}
