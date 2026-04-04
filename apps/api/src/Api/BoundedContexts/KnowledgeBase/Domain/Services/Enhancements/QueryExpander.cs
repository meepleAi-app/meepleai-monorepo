using Api.Services;
using Microsoft.Extensions.Logging;

#pragma warning disable MA0048 // File name must match type name - Contains expander with supporting record

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.Enhancements;

internal sealed record QueryVariations(List<string> Queries);

internal sealed class QueryExpander : IQueryExpander
{
    private readonly ILlmService _llmService;
    private readonly ILogger<QueryExpander> _logger;

    private const string SystemPrompt = """
        Generate 3 alternative phrasings of this board game question.
        Each should capture a different angle or use different terminology.
        Respond ONLY with JSON: {"queries":["v1","v2","v3"]}
        """;

    public QueryExpander(ILlmService llmService, ILogger<QueryExpander> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<List<string>> ExpandAsync(string query, CancellationToken ct)
    {
        try
        {
            var result = await _llmService.GenerateJsonAsync<QueryVariations>(
                SystemPrompt, $"Original: {query}", RequestSource.RagClassification, ct)
                .ConfigureAwait(false);

            if (result?.Queries is { Count: > 0 })
            {
                var variations = new List<string>(result.Queries.Count + 1) { query };
                variations.AddRange(result.Queries.Take(4));
                return variations;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Query expansion failed, returning original query only");
        }

        return [query];
    }
}
