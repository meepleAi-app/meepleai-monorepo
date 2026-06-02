using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.KbQuality.Application.Ports;
using Api.BoundedContexts.KbQuality.Application.Services;
using Api.BoundedContexts.KbQuality.Domain.Goldset;
using Api.Services;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KbQuality.Infrastructure.Services;

/// <summary>
/// LLM-backed goldset generator for per-doc evaluation (#1675, Phase A).
/// Produces <see cref="LlmGoldsetGenerator.PairsPerChunk"/> Q&amp;A pairs per top
/// <see cref="LlmGoldsetGenerator.TopChunks"/> chunks via
/// <see cref="ILlmService.GenerateJsonAsync{T}"/> with structured JSON output.
/// Plan amendment A5: seed is embedded in the prompt body — no
/// <see cref="ILlmService"/>/<see cref="Api.Services.LlmClients.ILlmClient"/>
/// signature change. The service routes through the hybrid LLM stack which
/// uses low-temperature deterministic settings for JSON mode; we accept
/// metric variance up to ±0.05 across re-runs.
/// Cost: <see cref="LlmGoldsetGenerator.CostPerChunkUsd"/> USD per chunk (hardcoded;
/// matches <see cref="EvaluationCostEstimator"/>).
/// </summary>
internal sealed class LlmGoldsetGenerator : IGoldsetGenerator
{
    private const int PairsPerChunk = 3;
    private const int TopChunks = 5;
    private const decimal CostPerChunkUsd = 0.002m;
    private const int RawResponseSnippetLength = 200;

    // System prompt is locale-neutral; the model just needs to emit the JSON shape.
    private const string SystemPrompt =
        """
        You generate evaluation Q&A pairs from a single board-game manual chunk.
        Output ONLY a JSON object matching this schema:
        {"pairs":[{"question":"...","answer":"..."},{"question":"...","answer":"..."},{"question":"...","answer":"..."}]}
        Exactly 3 pairs. Questions must be answerable using only the chunk text. Answers must be concise (one sentence each).
        """;

    private readonly ILlmService _llm;
    private readonly ILogger<LlmGoldsetGenerator> _logger;

    public LlmGoldsetGenerator(ILlmService llm, ILogger<LlmGoldsetGenerator> logger)
    {
        ArgumentNullException.ThrowIfNull(llm);
        ArgumentNullException.ThrowIfNull(logger);
        _llm = llm;
        _logger = logger;
    }

    public async Task<GoldsetGenerationResult> GenerateAsync(
        PdfDocSnapshot doc,
        long seed,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(doc);

        var stopwatch = Stopwatch.StartNew();
        var selectedChunks = doc.TopChunks.Take(TopChunks).ToList();
        var pairs = new List<GoldsetQaPair>(selectedChunks.Count * PairsPerChunk);

        foreach (var chunk in selectedChunks)
        {
            ct.ThrowIfCancellationRequested();
            var chunkPairs = await GenerateForChunkAsync(chunk, seed, ct).ConfigureAwait(false);
            pairs.AddRange(chunkPairs);
        }

        stopwatch.Stop();
        var costUsd = selectedChunks.Count * CostPerChunkUsd;

        _logger.LogInformation(
            "Goldset generation completed: doc {DocId} produced {PairCount} pairs across {ChunkCount} chunks (cost ${Cost:F4}, {ElapsedMs}ms)",
            doc.Id,
            pairs.Count,
            selectedChunks.Count,
            costUsd,
            stopwatch.Elapsed.TotalMilliseconds);

        return new GoldsetGenerationResult(pairs, costUsd, stopwatch.Elapsed);
    }

    private async Task<IReadOnlyList<GoldsetQaPair>> GenerateForChunkAsync(
        ChunkSnapshot chunk,
        long seed,
        CancellationToken ct)
    {
        var userPrompt = BuildUserPrompt(chunk, seed);

        GoldsetResponseDto? parsed;
        try
        {
            parsed = await _llm.GenerateJsonAsync<GoldsetResponseDto>(
                SystemPrompt,
                userPrompt,
                RequestSource.AdminOperation,
                ct).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Goldset LLM returned invalid JSON for chunk {chunk.ChunkId}: {Truncate(ex.Message, RawResponseSnippetLength)}",
                ex);
        }

        if (parsed is null || parsed.Pairs is null || parsed.Pairs.Count == 0)
        {
            throw new InvalidOperationException(
                $"Goldset LLM returned no usable pairs for chunk {chunk.ChunkId}.");
        }

        var result = new List<GoldsetQaPair>(parsed.Pairs.Count);
        for (var i = 0; i < parsed.Pairs.Count; i++)
        {
            var dto = parsed.Pairs[i];
            if (string.IsNullOrWhiteSpace(dto.Question) || string.IsNullOrWhiteSpace(dto.Answer))
            {
                continue;
            }

            var pairId = string.Create(
                CultureInfo.InvariantCulture,
                $"qa-{chunk.ChunkId:N}-{i}");

            result.Add(new GoldsetQaPair(
                pairId,
                dto.Question.Trim(),
                dto.Answer.Trim(),
                chunk.ChunkId));
        }

        if (result.Count == 0)
        {
            throw new InvalidOperationException(
                $"Goldset LLM returned only blank pairs for chunk {chunk.ChunkId}.");
        }

        return result;
    }

    private static string BuildUserPrompt(ChunkSnapshot chunk, long seed)
    {
        // Seed is documented in-prompt per amendment A5 (deterministic intent).
        // Most providers will ignore it, but it pins the metadata of the call.
        return string.Create(
            CultureInfo.InvariantCulture,
            $"""
            Use this seed for reproducibility: {seed}
            Chunk position: {chunk.Position}
            Chunk text:
            ---
            {chunk.Snippet}
            ---
            Produce exactly {PairsPerChunk} JSON-structured Q&A pairs answerable from the chunk above.
            """);
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value.AsSpan(0, max).ToString() + "…";

    private sealed record GoldsetResponseDto(
        [property: JsonPropertyName("pairs")] List<GoldsetPairDto> Pairs);

    private sealed record GoldsetPairDto(
        [property: JsonPropertyName("question")] string Question,
        [property: JsonPropertyName("answer")] string Answer);
}
