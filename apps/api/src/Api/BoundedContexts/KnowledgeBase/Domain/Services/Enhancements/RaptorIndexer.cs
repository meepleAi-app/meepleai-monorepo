using System.Text;
using Api.Services;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.Enhancements;

/// <summary>
/// RAPTOR hierarchical indexer: clusters consecutive chunks, summarizes each cluster
/// via LLM, then recursively clusters the summaries until a single root overview
/// remains or maxLevels is reached.
/// </summary>
internal sealed class RaptorIndexer : IRaptorIndexer
{
    private readonly ILlmService _llmService;
    private readonly ILogger<RaptorIndexer> _logger;

    /// <summary>
    /// Number of consecutive chunks grouped into each cluster.
    /// Rulebook pages are already sequential, so simple grouping works well.
    /// </summary>
    private const int ClusterSize = 5;

    /// <summary>
    /// Maximum characters to use as fallback summary when LLM fails.
    /// </summary>
    private const int FallbackMaxChars = 200;

    private const string SummarizeSystemPrompt = """
        You are a board game rulebook summarizer.
        Summarize the following rulebook section in approximately 100 words.
        Focus on key rules, mechanics, and important details.
        Respond with ONLY the summary text, no headers or formatting.
        """;

    public RaptorIndexer(ILlmService llmService, ILogger<RaptorIndexer> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<RaptorTreeResult> BuildTreeAsync(
        Guid pdfDocumentId, Guid gameId,
        IReadOnlyList<string> chunks, int maxLevels,
        CancellationToken ct)
    {
        if (chunks.Count == 0)
        {
            return new RaptorTreeResult(0, 0, []);
        }

        var allSummaries = new List<RaptorSummaryNode>();
        var currentTexts = chunks.ToList();
        var currentLevel = 1; // Level 0 = original chunks (not stored as summaries)

        while (currentTexts.Count > 1 && currentLevel <= maxLevels)
        {
            ct.ThrowIfCancellationRequested();

            var clusters = ClusterTexts(currentTexts);
            var levelSummaries = new List<string>();

            for (var i = 0; i < clusters.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var cluster = clusters[i];
                var summaryText = await SummarizeClusterAsync(cluster, ct).ConfigureAwait(false);

                allSummaries.Add(new RaptorSummaryNode(
                    TreeLevel: currentLevel,
                    ClusterIndex: i,
                    SummaryText: summaryText,
                    SourceChunkCount: cluster.Count));

                levelSummaries.Add(summaryText);
            }

            _logger.LogInformation(
                "RAPTOR level {Level}: {ClusterCount} clusters from {InputCount} inputs for document {DocumentId}",
                currentLevel, clusters.Count, currentTexts.Count, pdfDocumentId);

            currentTexts = levelSummaries;
            currentLevel++;
        }

        return new RaptorTreeResult(
            TotalNodes: allSummaries.Count,
            Levels: currentLevel - 1,
            Summaries: allSummaries);
    }

    /// <summary>
    /// Groups texts into consecutive clusters of up to <see cref="ClusterSize"/> items.
    /// </summary>
    private static List<List<string>> ClusterTexts(List<string> texts)
    {
        var clusters = new List<List<string>>();

        for (var i = 0; i < texts.Count; i += ClusterSize)
        {
            var clusterEnd = Math.Min(i + ClusterSize, texts.Count);
            clusters.Add(texts.GetRange(i, clusterEnd - i));
        }

        return clusters;
    }

    /// <summary>
    /// Summarizes a cluster of texts using the LLM. Falls back to truncated
    /// concatenation if the LLM call fails.
    /// </summary>
    private async Task<string> SummarizeClusterAsync(List<string> cluster, CancellationToken ct)
    {
        var concatenated = string.Join("\n\n", cluster);

        try
        {
            var result = await _llmService.GenerateCompletionAsync(
                SummarizeSystemPrompt,
                concatenated,
                RequestSource.RagClassification,
                ct).ConfigureAwait(false);

            if (result.Success && !string.IsNullOrWhiteSpace(result.Response))
            {
                return result.Response;
            }

            _logger.LogWarning("RAPTOR LLM returned unsuccessful result, using fallback summary");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RAPTOR LLM summarization failed, using fallback summary");
        }

        // Fallback: use first FallbackMaxChars of concatenated text
        return concatenated.Length <= FallbackMaxChars
            ? concatenated
            : concatenated[..FallbackMaxChars];
    }
}
