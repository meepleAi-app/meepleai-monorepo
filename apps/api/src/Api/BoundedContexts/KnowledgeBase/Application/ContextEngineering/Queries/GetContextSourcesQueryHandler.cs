using Api.BoundedContexts.KnowledgeBase.Application.ContextEngineering.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.ContextEngineering;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.ContextEngineering.Queries;

/// <summary>
/// Handler for retrieving context source information.
/// Issue #3491: Context Engineering Framework Implementation.
/// </summary>
internal sealed class GetContextSourcesQueryHandler : IRequestHandler<GetContextSourcesQuery, IReadOnlyList<ContextSourceInfoDto>>
{
    private readonly IEnumerable<IContextSource> _sources;
    private readonly IEnumerable<IContextRetrievalStrategy> _strategies;

    public GetContextSourcesQueryHandler(
        IEnumerable<IContextSource> sources,
        IEnumerable<IContextRetrievalStrategy> strategies)
    {
        _sources = sources ?? throw new ArgumentNullException(nameof(sources));
        _strategies = strategies ?? throw new ArgumentNullException(nameof(strategies));
    }

    public async Task<IReadOnlyList<ContextSourceInfoDto>> Handle(
        GetContextSourcesQuery request,
        CancellationToken cancellationToken)
    {
        var results = new List<ContextSourceInfoDto>();
        var strategyList = _strategies.ToList();

        foreach (var source in _sources)
        {
            var isAvailable = await source.IsAvailableAsync(cancellationToken).ConfigureAwait(false);
            var strategy = FindStrategy(source.SourceId, strategyList);

            results.Add(new ContextSourceInfoDto
            {
                SourceId = source.SourceId,
                SourceName = source.SourceName,
                DefaultPriority = source.DefaultPriority,
                IsAvailable = isAvailable,
                Description = GetSourceDescription(source.SourceId),
                ContentType = GetContentType(source.SourceId),
                Strategy = strategy?.StrategyId
            });
        }

        return results.OrderByDescending(s => s.DefaultPriority).ToList();
    }

    private static IContextRetrievalStrategy? FindStrategy(
        string sourceId,
        IReadOnlyList<IContextRetrievalStrategy> strategies)
    {
        return strategies.FirstOrDefault(s =>
            s.SupportedSourceTypes.Any(t =>
                sourceId.Contains(t, StringComparison.OrdinalIgnoreCase)));
    }

    private static string GetSourceDescription(string sourceId)
    {
        return sourceId switch
        {
            "static_knowledge" => "Static knowledge base from game rules and FAQs",
            "conversation_memory" => "Recent conversation history with temporal relevance",
            "game_state" => "Current and historical game state snapshots",
            "strategy_patterns" => "Cached strategic evaluations and patterns",
            "tool_metadata" => "Available AI agent tools and capabilities",
            _ => $"Context source: {sourceId}"
        };
    }

    private static string GetContentType(string sourceId)
    {
        return sourceId switch
        {
            "static_knowledge" => "rules_and_faqs",
            "conversation_memory" => "conversation",
            "game_state" => "game_state",
            "strategy_patterns" => "strategy",
            "tool_metadata" => "tool",
            _ => "unknown"
        };
    }
}
