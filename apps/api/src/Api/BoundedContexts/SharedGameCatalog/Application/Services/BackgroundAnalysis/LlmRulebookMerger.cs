using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Services;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.BackgroundAnalysis;

/// <summary>
/// LLM-based implementation of rulebook merger.
/// Synthesizes overview and chunk analyses into cohesive final analysis.
/// </summary>
internal sealed class LlmRulebookMerger : IRulebookMerger
{
    private readonly ILlmService _llmService;
    private readonly ILogger<LlmRulebookMerger> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private const string SystemPrompt = """
        You are an expert board game analyst specializing in synthesis and coherence.

        Your task: Merge rulebook overview and chunk analyses into ONE cohesive analysis.

        Input: Overview + multiple chunk analyses
        Output: Single unified analysis with no duplicates

        Requirements:
        - Merge mechanics, resources, phases, questions
        - Remove duplicates (same resource/phase described multiple times)
        - Create comprehensive summary from all sources
        - Extract victory conditions from chunks if missing in overview
        - Assign confidence score based on analysis quality and completeness

        Confidence scoring:
        - 0.9-1.0: All chunks successful, comprehensive coverage
        - 0.7-0.9: Most chunks successful, good coverage
        - 0.5-0.7: Some chunk failures, partial coverage
        - 0.0-0.5: Many failures or low quality

        Return JSON matching RulebookAnalysisResult structure.
        """;

    public LlmRulebookMerger(ILlmService llmService, ILogger<LlmRulebookMerger> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<MergedRulebookAnalysis> MergeAnalysesAsync(
        OverviewExtractionResult overview,
        ParallelAnalysisResult chunkResults,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Merging analyses: overview + {ChunkCount} chunks ({SuccessRate:F1}% success rate)",
            chunkResults.Results.Count, chunkResults.SuccessRate * 100);

        // Pre-merge deduplication of obvious duplicates
        var premergedChunks = DeduplicateChunkResults(chunkResults.Results);

        var userPrompt = BuildMergePrompt(overview, premergedChunks);

        try
        {
            var result = await _llmService.GenerateJsonAsync<LlmMergedResponse>(
                SystemPrompt,
                userPrompt,
                cancellationToken).ConfigureAwait(false);

            if (result is null)
            {
                _logger.LogWarning("LLM returned null for merge, using fallback");
                return CreateFallbackMerge(overview, premergedChunks, chunkResults, stopwatch.Elapsed);
            }

            // Convert to domain objects
            var victoryConditions = result.VictoryConditions is not null
                ? VictoryConditions.Create(
                    result.VictoryConditions.Primary,
                    result.VictoryConditions.Alternatives,
                    result.VictoryConditions.IsPointBased,
                    result.VictoryConditions.TargetPoints)
                : VictoryConditions.Empty;

            var resources = result.Resources
                .Select(r => Resource.Create(r.Name, r.Type, r.Usage, r.IsLimited))
                .ToList();

            var gamePhases = result.GamePhases
                .Select(p => GamePhase.Create(p.Name, p.Description, p.Order, p.IsOptional))
                .ToList();

            var metadata = MergeMetadata.Create(
                chunkResults.Results.Count,
                chunkResults.SuccessCount,
                chunkResults.FailureCount,
                duplicatesRemoved: premergedChunks.DuplicatesRemoved,
                stopwatch.Elapsed);

            _logger.LogInformation(
                "Merge complete: {Mechanics} mechanics, {Resources} resources, {Phases} phases, confidence: {Confidence:F2}",
                result.KeyMechanics.Count, resources.Count, gamePhases.Count, result.ConfidenceScore);

            return MergedRulebookAnalysis.Create(
                result.GameTitle,
                result.Summary,
                result.KeyMechanics,
                victoryConditions,
                resources,
                gamePhases,
                result.CommonQuestions,
                result.ConfidenceScore,
                metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error merging analyses, using fallback");
            return CreateFallbackMerge(overview, premergedChunks, chunkResults, stopwatch.Elapsed);
        }
    }

    private string BuildMergePrompt(OverviewExtractionResult overview, PremergeResult premerged)
    {
        var chunkSummaries = premerged.Chunks
            .Where(c => c.Success)
            .Select(c => $"Chunk {c.ChunkIndex + 1}: {c.ChunkSummary}")
            .ToList();

        return $"""
            OVERVIEW:
            Game: {overview.GameTitle}
            Summary: {overview.GameSummary}
            Main Mechanics: {string.Join(", ", overview.MainMechanics)}
            Victory: {overview.VictoryConditionSummary}
            Players: {overview.PlayerCountMin}-{overview.PlayerCountMax}
            Playtime: {overview.PlaytimeMinutes} minutes

            CHUNK ANALYSES:
            Total Chunks: {premerged.Chunks.Count}
            Successful: {premerged.Chunks.Count(c => c.Success)}

            Chunk Summaries:
            {string.Join("\n", chunkSummaries)}

            Extracted Mechanics (pre-deduplicated):
            {string.Join(", ", premerged.AllMechanics)}

            Resources Found:
            {JsonSerializer.Serialize(premerged.AllResources, JsonOptions)}

            Game Phases Found:
            {JsonSerializer.Serialize(premerged.AllPhases, JsonOptions)}

            Common Questions:
            {string.Join("\n", premerged.AllQuestions)}

            TASK: Merge into ONE cohesive analysis.
            - Deduplicate further if needed
            - Create comprehensive summary
            - Extract victory conditions from chunks if not in overview
            - Assign confidence score based on {premerged.Chunks.Count(c => c.Success)}/{premerged.Chunks.Count} chunk success rate
            """;
    }

    private PremergeResult DeduplicateChunkResults(List<ChunkAnalysisResult> chunks)
    {
        var successfulChunks = chunks.Where(c => c.Success).ToList();

        // Combine and deduplicate mechanics
        var allMechanics = successfulChunks
            .SelectMany(c => c.ExtractedMechanics)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Deduplicate resources by name
        var allResources = successfulChunks
            .SelectMany(c => c.Resources)
            .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        // Deduplicate phases by name and re-order
        var allPhases = successfulChunks
            .SelectMany(c => c.GamePhases)
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(p => p.Order)
            .ToList();

        // Deduplicate questions
        var allQuestions = successfulChunks
            .SelectMany(c => c.CommonQuestions)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var totalItems = successfulChunks.SelectMany(c => c.ExtractedMechanics).Count()
                       + successfulChunks.SelectMany(c => c.Resources).Count()
                       + successfulChunks.SelectMany(c => c.GamePhases).Count()
                       + successfulChunks.SelectMany(c => c.CommonQuestions).Count();

        var finalItems = allMechanics.Count + allResources.Count + allPhases.Count + allQuestions.Count;
        var duplicatesRemoved = totalItems - finalItems;

        _logger.LogInformation(
            "Pre-merge deduplication: {Total} items → {Final} items ({Duplicates} duplicates removed)",
            totalItems, finalItems, duplicatesRemoved);

        return new PremergeResult
        {
            Chunks = chunks,
            AllMechanics = allMechanics,
            AllResources = allResources,
            AllPhases = allPhases,
            AllQuestions = allQuestions,
            DuplicatesRemoved = duplicatesRemoved
        };
    }

    private MergedRulebookAnalysis CreateFallbackMerge(
        OverviewExtractionResult overview,
        PremergeResult premerged,
        ParallelAnalysisResult chunkResults,
        TimeSpan analysisTime)
    {
        _logger.LogInformation("Creating fallback merge from overview and chunks");

        var summary = $"{overview.GameSummary}. Analysis completed from {chunkResults.SuccessCount} successfully analyzed sections.";

        var mechanics = premerged.AllMechanics.Count > 0
            ? premerged.AllMechanics
            : overview.MainMechanics;

        // Estimate confidence from chunk success rate
        var baseConfidence = chunkResults.SuccessRate;
        var confidenceScore = (decimal)Math.Round(baseConfidence * 0.9, 2); // Conservative estimate

        var victoryConditions = VictoryConditions.Create(
            overview.VictoryConditionSummary,
            [],
            isPointBased: false,
            targetPoints: null);

        var metadata = MergeMetadata.Create(
            chunkResults.Results.Count,
            chunkResults.SuccessCount,
            chunkResults.FailureCount,
            premerged.DuplicatesRemoved,
            analysisTime);

        return MergedRulebookAnalysis.Create(
            overview.GameTitle,
            summary,
            mechanics,
            victoryConditions,
            premerged.AllResources,
            premerged.AllPhases,
            premerged.AllQuestions,
            confidenceScore,
            metadata);
    }

    private sealed record PremergeResult
    {
        public List<ChunkAnalysisResult> Chunks { get; init; } = [];
        public List<string> AllMechanics { get; init; } = [];
        public List<Resource> AllResources { get; init; } = [];
        public List<GamePhase> AllPhases { get; init; } = [];
        public List<string> AllQuestions { get; init; } = [];
        public int DuplicatesRemoved { get; init; }
    }

    // DTO for LLM response
    private sealed record LlmMergedResponse
    {
        [JsonPropertyName("gameTitle")]
        public string GameTitle { get; init; } = string.Empty;

        [JsonPropertyName("summary")]
        public string Summary { get; init; } = string.Empty;

        [JsonPropertyName("keyMechanics")]
        public List<string> KeyMechanics { get; init; } = [];

        [JsonPropertyName("victoryConditions")]
        public LlmVictoryConditionsDto? VictoryConditions { get; init; }

        [JsonPropertyName("resources")]
        public List<LlmResourceDto> Resources { get; init; } = [];

        [JsonPropertyName("gamePhases")]
        public List<LlmGamePhaseDto> GamePhases { get; init; } = [];

        [JsonPropertyName("commonQuestions")]
        public List<string> CommonQuestions { get; init; } = [];

        [JsonPropertyName("confidenceScore")]
        public decimal ConfidenceScore { get; init; }
    }

    private sealed record LlmVictoryConditionsDto
    {
        [JsonPropertyName("primary")]
        public string Primary { get; init; } = string.Empty;

        [JsonPropertyName("alternatives")]
        public List<string> Alternatives { get; init; } = [];

        [JsonPropertyName("isPointBased")]
        public bool IsPointBased { get; init; }

        [JsonPropertyName("targetPoints")]
        public int? TargetPoints { get; init; }
    }

    private sealed record LlmResourceDto
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; init; } = string.Empty;

        [JsonPropertyName("usage")]
        public string Usage { get; init; } = string.Empty;

        [JsonPropertyName("isLimited")]
        public bool IsLimited { get; init; }
    }

    private sealed record LlmGamePhaseDto
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; init; } = string.Empty;

        [JsonPropertyName("order")]
        public int Order { get; init; }

        [JsonPropertyName("isOptional")]
        public bool IsOptional { get; init; }
    }
}
