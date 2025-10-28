using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api.Services.Rag;

/// <summary>
/// PERF-08: Fuses search results using Reciprocal Rank Fusion (RRF)
/// Combines results from multiple queries with deduplication
/// </summary>
public class SearchResultReranker : ISearchResultReranker
{
    private readonly ILogger<SearchResultReranker> _logger;
    private readonly IConfigurationService? _configurationService;
    private const int DefaultRrfK = 60;

    public SearchResultReranker(
        ILogger<SearchResultReranker> logger,
        IConfigurationService? configurationService = null)
    {
        _logger = logger;
        _configurationService = configurationService;
    }

    public async Task<List<SearchResultItem>> FuseSearchResultsAsync(List<SearchResult> searchResults)
    {
        // CONFIG-04: Load dynamic RRF constant from configuration
        var k = await GetRrfKAsync();

        // Dictionary to store RRF scores for each unique document
        var rrfScores = new Dictionary<string, (SearchResultItem item, double score)>();

        // Process each search result list
        for (int queryIndex = 0; queryIndex < searchResults.Count; queryIndex++)
        {
            var results = searchResults[queryIndex].Results;

            // Calculate RRF score for each result: 1 / (k + rank)
            for (int rank = 0; rank < results.Count; rank++)
            {
                var result = results[rank];
                var docKey = $"{result.PdfId}_{result.Page}_{result.Text.GetHashCode()}";

                var rrfScore = 1.0 / (k + rank + 1); // rank is 0-indexed, add 1 for proper formula

                // CODE-04: Use TryGetValue to avoid double dictionary lookup
                if (rrfScores.TryGetValue(docKey, out var existingEntry))
                {
                    // Document appears in multiple result sets - accumulate RRF scores
                    rrfScores[docKey] = (existingEntry.Item1, existingEntry.Item2 + rrfScore);
                }
                else
                {
                    // First time seeing this document
                    rrfScores[docKey] = (result, rrfScore);
                }
            }
        }

        // Sort by RRF score (descending) and return items
        var fusedResults = rrfScores.Values
            .OrderByDescending(x => x.score)
            .Select(x => new SearchResultItem
            {
                Text = x.item.Text,
                PdfId = x.item.PdfId,
                Page = x.item.Page,
                ChunkIndex = x.item.ChunkIndex,
                Score = (float)x.score // Use RRF score as the final relevance score
            })
            .ToList();

        _logger.LogDebug(
            "Result fusion: {InputLists} lists with {TotalResults} results → {FusedResults} unique results",
            searchResults.Count,
            searchResults.Sum(r => r.Results.Count),
            fusedResults.Count);

        return fusedResults;
    }

    private async Task<int> GetRrfKAsync()
    {
        if (_configurationService != null)
        {
            var dbValue = await _configurationService.GetValueAsync<int?>("RAG.RrfK");
            if (dbValue.HasValue && dbValue.Value >= 1 && dbValue.Value <= 100)
            {
                return dbValue.Value;
            }
        }

        return DefaultRrfK;
    }
}
