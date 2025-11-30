using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api.Services.Rag;

/// <summary>
/// PERF-08: Generates query variations for improved recall
/// Uses rule-based expansion with synonyms and reformulations
/// </summary>
public class QueryExpansionService : IQueryExpansionService
{
    private readonly ILogger<QueryExpansionService> _logger;
    private readonly IConfigurationService? _configurationService;
    private const int DefaultMaxQueryVariations = 4;

    public QueryExpansionService(
        ILogger<QueryExpansionService> logger,
        IConfigurationService? configurationService = null)
    {
        _logger = logger;
        _configurationService = configurationService;
    }

    public async Task<List<string>> GenerateQueryVariationsAsync(
        string query,
        string language,
        CancellationToken cancellationToken = default)
    {
        // CONFIG-04: Load dynamic max variations configuration
        var maxVariations = await GetMaxQueryVariationsAsync();

        var variations = new List<string> { query }; // Always include original query

        // Rule-based query expansion patterns for board games domain
        var expansionRules = new Dictionary<string, string[]>
(StringComparer.Ordinal)
        {
            // Setup-related synonyms
            { "setup", new[] { "initial setup", "game setup", "starting position", "prepare" } },
            { "prepare", new[] { "setup", "initial setup", "getting started" } },

            // Movement-related synonyms
            { "move", new[] { "movement", "moving", "how to move", "can move" } },
            { "movement", new[] { "move", "moving pieces", "piece movement" } },

            // Action-related synonyms
            { "play", new[] { "playing", "take action", "perform action" } },
            { "action", new[] { "move", "play", "turn action" } },

            // Turn-related synonyms
            { "turn", new[] { "player turn", "round", "phase" } },
            { "round", new[] { "turn", "game round", "playing round" } },

            // Win condition synonyms
            { "win", new[] { "winning", "victory", "how to win", "win condition" } },
            { "victory", new[] { "win", "winning condition", "game end" } },

            // Rule-related synonyms
            { "rule", new[] { "rules", "regulation", "how does" } },
            { "allowed", new[] { "can I", "is it legal", "permitted" } }
        };

        // Apply expansion rules (case-insensitive)
        var queryLower = query.ToLowerInvariant();
        foreach (var rule in expansionRules)
        {
            if (queryLower.Contains(rule.Key))
            {
                foreach (var synonym in rule.Value.Take(2)) // Limit to 2 synonyms per rule
                {
                    var expandedQuery = query.Replace(rule.Key, synonym, StringComparison.OrdinalIgnoreCase);
                    if (!variations.Contains(expandedQuery, StringComparer.OrdinalIgnoreCase))
                    {
                        variations.Add(expandedQuery);
                    }
                }
            }
        }

        // Add question reformulations for common patterns
        if (queryLower.StartsWith("how") || queryLower.StartsWith("what") || queryLower.StartsWith("can"))
        {
            // "How do I X?" → "X rules", "X instructions"
            var baseQuery = query.Replace("how do i ", "", StringComparison.OrdinalIgnoreCase)
                                 .Replace("how to ", "", StringComparison.OrdinalIgnoreCase)
                                 .Replace("what is ", "", StringComparison.OrdinalIgnoreCase)
                                 .Replace("can i ", "", StringComparison.OrdinalIgnoreCase)
                                 .TrimEnd('?').Trim();

            if (!string.IsNullOrWhiteSpace(baseQuery))
            {
                variations.Add($"{baseQuery} rules");
                variations.Add($"{baseQuery} instructions");
            }
        }

        // CONFIG-04: Limit total variations using dynamic configuration
        var finalVariations = variations.Distinct(StringComparer.OrdinalIgnoreCase).Take(maxVariations).ToList();

        _logger.LogDebug("Query expansion: '{Original}' → {Count} variations (max: {Max})",
            query, finalVariations.Count, maxVariations);

        return await Task.FromResult(finalVariations);
    }

    private async Task<int> GetMaxQueryVariationsAsync()
    {
        if (_configurationService != null)
        {
            var dbValue = await _configurationService.GetValueAsync<int?>("RAG.MaxQueryVariations");
            if (dbValue.HasValue && dbValue.Value >= 1 && dbValue.Value <= 10)
            {
                return dbValue.Value;
            }
        }

        return DefaultMaxQueryVariations;
    }
}
