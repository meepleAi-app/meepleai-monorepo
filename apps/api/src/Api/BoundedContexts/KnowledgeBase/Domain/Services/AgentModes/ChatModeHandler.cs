#pragma warning disable MA0002 // Dictionary without StringComparer
#pragma warning disable CA1826 // Use property instead of Linq Enumerable method
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.AgentModes;

/// <summary>
/// Chat Mode handler for standard rule clarifications and question answering.
/// Provides straightforward answers using RAG search results without mode-specific logic.
/// Issue #2404 - Agent Mode system
/// </summary>
internal sealed class ChatModeHandler : IAgentModeHandler
{
    private readonly ILogger<ChatModeHandler> _logger;

    public ChatModeHandler(ILogger<ChatModeHandler> logger)
    {
        _logger = logger;
    }

    public AgentMode SupportedMode => AgentMode.Chat;

    public Task<AgentModeResult> HandleAsync(
        AgentModeContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        _logger.LogInformation(
            "ChatModeHandler invoked for Agent {AgentId} with query: {Query}",
            context.Agent.Id,
            context.Query);

        // Calculate confidence from search results
        var confidence = context.SearchResults.Any()
            ? context.SearchResults.Average(r => r.RelevanceScore.Value)
            : 0.5;

        // Format search results as content
        var content = FormatSearchResults(context.SearchResults, context.Query);

        return Task.FromResult(new AgentModeResult
        {
            Mode = AgentMode.Chat,
            Content = content,
            Confidence = confidence,
            Metadata = new Dictionary<string, object>
            {
                { "resultCount", context.SearchResults.Count },
                { "topScore", context.SearchResults.FirstOrDefault()?.RelevanceScore.Value ?? 0.0 }
            }
        });
    }

    /// <summary>
    /// Formats search results into a readable response
    /// </summary>
    private static string FormatSearchResults(
        IReadOnlyList<SearchResult> results,
        string query)
    {
        if (results.Count == 0)
        {
            return $"""
                💬 **Nessun risultato trovato**

                Non ho trovato informazioni specifiche per: "{query}"

                Suggerimenti:
                - Prova a riformulare la domanda
                - Usa termini più generici
                - Verifica che i documenti giusti siano caricati
                """;
        }

        // Take top 3 results
        var topResults = results
            .OrderByDescending(r => r.RelevanceScore.Value)
            .Take(3)
            .Select((r, i) => $"""
                **Risultato {i + 1}** (Confidenza: {r.RelevanceScore.Value:P0})
                {r.TextContent}

                *Fonte: Pagina {r.PageNumber}*
                """)
            .ToList();

        return $"""
            💬 **Risposta alla tua domanda:**

            {string.Join("\n\n---\n\n", topResults)}
            """;
    }
}
