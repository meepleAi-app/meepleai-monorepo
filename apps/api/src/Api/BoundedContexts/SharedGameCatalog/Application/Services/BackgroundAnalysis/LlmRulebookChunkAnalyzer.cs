using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.SharedGameCatalog.Application.Configuration;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.BackgroundAnalysis;

/// <summary>
/// LLM-based implementation of chunk analyzer with parallel processing.
/// Analyzes chunks independently for speed while maintaining context.
/// </summary>
internal sealed class LlmRulebookChunkAnalyzer : IRulebookChunkAnalyzer
{
    private readonly ILlmService _llmService;
    private readonly ILogger<LlmRulebookChunkAnalyzer> _logger;

    private const string SystemPrompt = """
        You are an expert board game analyst specializing in rulebook chunk analysis.

        You receive a chunk from a larger rulebook for detailed analysis.
        Extract structured information specific to this chunk's content.

        Context: You have game overview and are analyzing ONE section in detail.

        Return JSON with this structure:
        {
            "extractedMechanics": ["Worker Placement", "Resource Conversion"],
            "resources": [
                {
                    "name": "Wood",
                    "type": "Building Material",
                    "usage": "Build structures",
                    "isLimited": true
                }
            ],
            "gamePhases": [
                {
                    "name": "Production Phase",
                    "description": "Collect resources",
                    "order": 1,
                    "isOptional": false
                }
            ],
            "commonQuestions": ["How many resources can I collect?"],
            "chunkSummary": "This section covers resource production and collection"
        }

        Rules:
        - Only extract what's explicitly in this chunk
        - Reference game context when needed for coherence
        - Keep summaries concise (1-2 sentences)
        - Empty arrays if no matches found
        """;

    public LlmRulebookChunkAnalyzer(
        ILlmService llmService,
        ILogger<LlmRulebookChunkAnalyzer> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<ChunkAnalysisResult> AnalyzeChunkAsync(
        SemanticChunk chunk,
        GameContext gameContext,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Analyzing chunk {ChunkIndex}: {Length} chars, section: {Section}",
            chunk.ChunkIndex, chunk.CharacterCount, chunk.SectionHeader ?? "none");

        var userPrompt = $"""
            Game: {gameContext.GameTitle}
            Overview: {gameContext.GameSummary}
            Known Mechanics: {string.Join(", ", gameContext.MainMechanics)}

            Chunk #{chunk.ChunkIndex + 1}:
            Section: {chunk.SectionHeader ?? "Unknown"}
            Context Headers: {string.Join(" > ", chunk.ContextHeaders)}

            Content:
            {chunk.Content}

            Analyze this chunk and extract all structured information.
            """;

        try
        {
            var result = await _llmService.GenerateJsonAsync<LlmChunkResponse>(
                SystemPrompt,
                userPrompt,
                cancellationToken).ConfigureAwait(false);

            if (result is null)
            {
                _logger.LogWarning("LLM returned null for chunk {ChunkIndex}", chunk.ChunkIndex);
                return ChunkAnalysisResult.CreateFailure(chunk.ChunkIndex, "LLM returned null response");
            }

            // Convert to domain objects
            var resources = result.Resources
                .Select(r => Resource.Create(r.Name, r.Type, r.Usage, r.IsLimited))
                .ToList();

            var gamePhases = result.GamePhases
                .Select(p => GamePhase.Create(p.Name, p.Description, p.Order, p.IsOptional))
                .ToList();

            _logger.LogDebug(
                "Chunk {ChunkIndex} analysis complete: {Mechanics} mechanics, {Resources} resources, {Phases} phases",
                chunk.ChunkIndex, result.ExtractedMechanics.Count, resources.Count, gamePhases.Count);

            return ChunkAnalysisResult.CreateSuccess(
                chunk.ChunkIndex,
                result.ExtractedMechanics,
                resources,
                gamePhases,
                result.CommonQuestions,
                result.ChunkSummary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing chunk {ChunkIndex}", chunk.ChunkIndex);
            return ChunkAnalysisResult.CreateFailure(chunk.ChunkIndex, ex.Message);
        }
    }

    public async Task<ParallelAnalysisResult> AnalyzeChunksParallelAsync(
        List<SemanticChunk> chunks,
        GameContext gameContext,
        int maxParallelism = 3,
        Func<int, int, Task>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting parallel analysis of {ChunkCount} chunks with max parallelism {MaxParallel}",
            chunks.Count, maxParallelism);

        var results = new ChunkAnalysisResult[chunks.Count];
        var processedCount = 0;
#pragma warning disable MA0158 // Lock is appropriate for .NET < 9 compatibility
        var lockObj = new object();
#pragma warning restore MA0158

        // Use SemaphoreSlim for concurrency control
        using var semaphore = new SemaphoreSlim(maxParallelism, maxParallelism);

        var tasks = chunks.Select(async chunk =>
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var result = await AnalyzeChunkAsync(chunk, gameContext, cancellationToken).ConfigureAwait(false);
                results[chunk.ChunkIndex] = result;

#pragma warning disable MA0158 // Lock is appropriate here for atomic callback+logging
                lock (lockObj)
#pragma warning restore MA0158
                {
                    processedCount++;
                    var currentCount = processedCount;
                    var totalCount = chunks.Count;

                    _logger.LogInformation(
                        "Chunk {Current}/{Total} analyzed ({Percentage:F1}%): {Success}",
                        currentCount, totalCount,
                        (currentCount / (double)totalCount) * 100,
                        result.Success ? "SUCCESS" : "FAILED");
                }

                // Invoke callback outside lock to prevent deadlock
                if (progressCallback is not null)
                {
                    try
                    {
                        await progressCallback(processedCount, chunks.Count).ConfigureAwait(false);
                    }
                    catch (Exception cbEx)
                    {
                        _logger.LogWarning(cbEx, "Progress callback failed for chunk {Current}/{Total}", processedCount, chunks.Count);
                    }
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);

        var finalResults = results.ToList();
        var parallelResult = ParallelAnalysisResult.Create(finalResults);

        _logger.LogInformation(
            "Parallel analysis complete: {Success}/{Total} successful ({Rate:F1}% success rate)",
            parallelResult.SuccessCount, finalResults.Count, parallelResult.SuccessRate * 100);

        return parallelResult;
    }

    // DTO for LLM response
    private sealed record LlmChunkResponse
    {
        [JsonPropertyName("extractedMechanics")]
        public List<string> ExtractedMechanics { get; init; } = [];

        [JsonPropertyName("resources")]
        public List<LlmResourceDto> Resources { get; init; } = [];

        [JsonPropertyName("gamePhases")]
        public List<LlmGamePhaseDto> GamePhases { get; init; } = [];

        [JsonPropertyName("commonQuestions")]
        public List<string> CommonQuestions { get; init; } = [];

        [JsonPropertyName("chunkSummary")]
        public string ChunkSummary { get; init; } = string.Empty;
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