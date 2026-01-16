using System.Text.RegularExpressions;
using Api.BoundedContexts.SharedGameCatalog.Application.Configuration;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.BackgroundAnalysis;

/// <summary>
/// Embedding-based semantic chunking implementation.
/// Uses embeddings to identify semantic boundaries for optimal chunk splitting.
/// </summary>
internal sealed partial class EmbeddingBasedSemanticChunker : ISemanticChunker
{
    private readonly IEmbeddingService _embeddingService;
    private readonly BackgroundAnalysisOptions _options;
    private readonly ILogger<EmbeddingBasedSemanticChunker> _logger;

    public EmbeddingBasedSemanticChunker(
        IEmbeddingService embeddingService,
        IOptions<BackgroundAnalysisOptions> options,
        ILogger<EmbeddingBasedSemanticChunker> logger)
    {
        _embeddingService = embeddingService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SemanticChunkingResult> ChunkAsync(
        string rulebookContent,
        List<string>? sectionHeaders = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Chunking rulebook content: {Length} chars, {HeaderCount} section headers",
            rulebookContent.Length, sectionHeaders?.Count ?? 0);

        try
        {
            // Strategy 1: Try embedding-based semantic chunking
            var chunks = await ChunkWithEmbeddingsAsync(rulebookContent, sectionHeaders, cancellationToken).ConfigureAwait(false);

            if (chunks.Count > 0)
            {
                _logger.LogInformation(
                    "Successfully created {ChunkCount} chunks using embedding-based strategy",
                    chunks.Count);
                return SemanticChunkingResult.Create(chunks, ChunkingStrategy.EmbeddingBased);
            }

            _logger.LogWarning("Embedding-based chunking failed, falling back to header-based");

            // Strategy 2: Fallback to header-based chunking
            chunks = ChunkByHeaders(rulebookContent, sectionHeaders);

            if (chunks.Count > 0)
            {
                _logger.LogInformation(
                    "Created {ChunkCount} chunks using header-based strategy",
                    chunks.Count);
                return SemanticChunkingResult.Create(chunks, ChunkingStrategy.HeaderBased);
            }

            _logger.LogWarning("Header-based chunking insufficient, using fixed-size fallback");

            // Strategy 3: Final fallback to fixed-size chunking
            chunks = ChunkFixedSize(rulebookContent);
            return SemanticChunkingResult.Create(chunks, ChunkingStrategy.FixedSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during chunking, using fixed-size fallback");
            var fallbackChunks = ChunkFixedSize(rulebookContent);
            return SemanticChunkingResult.Create(fallbackChunks, ChunkingStrategy.FixedSize);
        }
    }

    private async Task<List<SemanticChunk>> ChunkWithEmbeddingsAsync(
        string content,
        List<string>? sectionHeaders,
        CancellationToken ct)
    {
        // Step 1: Split into candidate sections using headers or paragraphs
        var candidates = ExtractCandidateSections(content, sectionHeaders);

        if (candidates.Count <= 1)
        {
            _logger.LogInformation("Only {Count} candidate section, skipping embedding-based chunking", candidates.Count);
            return [];
        }

        // Step 2: Generate embeddings for each candidate section
        var candidateTexts = candidates.Select(c => c.Content).ToList();
        var embeddingResult = await _embeddingService.GenerateEmbeddingsAsync(candidateTexts, ct).ConfigureAwait(false);

        if (!embeddingResult.Success || embeddingResult.Embeddings.Count != candidates.Count)
        {
            _logger.LogWarning(
                "Embedding generation failed or mismatch: {Success}, {Count}/{Expected}",
                embeddingResult.Success, embeddingResult.Embeddings.Count, candidates.Count);
            return [];
        }

        // Step 3: Identify semantic boundaries using embedding similarity
        var chunks = new List<SemanticChunk>();
        var currentChunk = new List<CandidateSection> { candidates[0] };
        var currentEmbedding = embeddingResult.Embeddings[0];

        for (int i = 1; i < candidates.Count; i++)
        {
            var similarity = CosineSimilarity(currentEmbedding, embeddingResult.Embeddings[i]);

            // Low similarity = semantic boundary → create new chunk
            if (similarity < _options.SemanticSimilarityThreshold || GetTotalLength(currentChunk) > _options.MaxChunkSize)
            {
                chunks.Add(CreateChunkFromCandidates(currentChunk, chunks.Count));
                currentChunk = [candidates[i]];
                currentEmbedding = embeddingResult.Embeddings[i];
            }
            else
            {
                currentChunk.Add(candidates[i]);
                // Update embedding to average (simple approach)
                currentEmbedding = AverageEmbeddings([currentEmbedding, embeddingResult.Embeddings[i]]);
            }
        }

        // Add final chunk
        if (currentChunk.Count > 0)
        {
            chunks.Add(CreateChunkFromCandidates(currentChunk, chunks.Count));
        }

        return chunks;
    }

    private List<CandidateSection> ExtractCandidateSections(string content, List<string>? sectionHeaders)
    {
        var candidates = new List<CandidateSection>();

        // Try header-guided split first
        if (sectionHeaders?.Count > 0)
        {
            foreach (var header in sectionHeaders)
            {
                var headerIndex = content.IndexOf(header, StringComparison.OrdinalIgnoreCase);
                if (headerIndex >= 0)
                {
                    candidates.Add(new CandidateSection(header, headerIndex));
                }
            }

            // Sort by position
            candidates = [.. candidates.OrderBy(c => c.StartIndex)];

            // Extract text between headers
            for (int i = 0; i < candidates.Count; i++)
            {
                var start = candidates[i].StartIndex;
                var end = i < candidates.Count - 1
                    ? candidates[i + 1].StartIndex
                    : content.Length;

                candidates[i] = candidates[i] with
                {
                    Content = content[start..end].Trim()
                };
            }

            return candidates.Where(c => c.Content.Length >= 100).ToList();
        }

        // Fallback: Split by double newlines (paragraphs)
        var paragraphs = content.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var position = 0;

        foreach (var para in paragraphs)
        {
            if (para.Length >= _options.MinimumSectionSize)
            {
                candidates.Add(new CandidateSection(null, position) { Content = para });
            }
            position += para.Length + 2; // +2 for "\n\n"
        }

        return candidates;
    }

    private List<SemanticChunk> ChunkByHeaders(string content, List<string>? sectionHeaders)
    {
        if (sectionHeaders == null || sectionHeaders.Count == 0)
        {
            // Extract headers via regex
            sectionHeaders = HeaderRegex()
                .Matches(content)
                .Select(m => m.Groups[1].Value.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        if (sectionHeaders.Count == 0)
            return [];

        var chunks = new List<SemanticChunk>();
        var contextHeaders = new List<string>();

        for (int i = 0; i < sectionHeaders.Count; i++)
        {
            var header = sectionHeaders[i];
            var headerIndex = content.IndexOf(header, StringComparison.OrdinalIgnoreCase);

            if (headerIndex < 0)
                continue;

            var start = headerIndex;
            var end = i < sectionHeaders.Count - 1
                ? content.IndexOf(sectionHeaders[i + 1], StringComparison.OrdinalIgnoreCase)
                : content.Length;

            if (end < 0)
                end = content.Length;

            var chunkContent = content[start..end].Trim();

            if (chunkContent.Length >= _options.MinimumSectionSize)
            {
                chunks.Add(SemanticChunk.Create(
                    chunks.Count,
                    chunkContent,
                    start,
                    end,
                    header,
                    [.. contextHeaders]));

                contextHeaders.Add(header);
            }
        }

        return chunks;
    }

    private List<SemanticChunk> ChunkFixedSize(string content)
    {
        var chunks = new List<SemanticChunk>();
        var position = 0;

        while (position < content.Length)
        {
            var chunkSize = Math.Min(_options.MaxChunkSize, content.Length - position);
            var chunkContent = content.Substring(position, chunkSize);

            chunks.Add(SemanticChunk.Create(
                chunks.Count,
                chunkContent,
                position,
                position + chunkSize));

            position += chunkSize - _options.OverlapSize;
        }

        return chunks;
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            return 0;

        double dotProduct = 0;
        double normA = 0;
        double normB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (Math.Abs(normA) < 1e-10 || Math.Abs(normB) < 1e-10)
            return 0;

        return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    private static float[] AverageEmbeddings(float[][] embeddings)
    {
        if (embeddings.Length == 0)
            return [];

        var dimensions = embeddings[0].Length;
        var result = new float[dimensions];

        for (int i = 0; i < dimensions; i++)
        {
            result[i] = embeddings.Average(e => e[i]);
        }

        return result;
    }

    private static int GetTotalLength(List<CandidateSection> sections) =>
        sections.Sum(s => s.Content.Length);

    private static SemanticChunk CreateChunkFromCandidates(List<CandidateSection> candidates, int chunkIndex)
    {
        var combinedContent = string.Join("\n\n", candidates.Select(c => c.Content));
        var firstSection = candidates[0];
        var lastSection = candidates[^1];

        var headers = candidates
            .Select(c => c.Header)
            .Where(h => !string.IsNullOrEmpty(h))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return SemanticChunk.Create(
            chunkIndex,
            combinedContent,
            firstSection.StartIndex,
            lastSection.StartIndex + lastSection.Content.Length,
            headers.Count > 0 ? headers[0] : null,
            headers);
    }

    [GeneratedRegex(@"^#{1,3}\s+(.+)$|^([A-Z][A-Z\s]{2,})$", RegexOptions.Multiline | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HeaderRegex();

    private sealed record CandidateSection(string? Header, int StartIndex)
    {
        public string Content { get; init; } = string.Empty;
    }
}