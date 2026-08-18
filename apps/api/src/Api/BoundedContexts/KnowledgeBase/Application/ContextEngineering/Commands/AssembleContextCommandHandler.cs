using Api.BoundedContexts.KnowledgeBase.Application.ContextEngineering.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.ContextEngineering;
using Api.Services;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.ContextEngineering.Commands;

/// <summary>
/// Handler for assembling context from multiple sources.
/// Issue #3491: Context Engineering Framework Implementation.
/// </summary>
internal sealed class AssembleContextCommandHandler : IRequestHandler<AssembleContextCommand, AssembledContextDto>
{
    private readonly ContextAssembler _assembler;
    private readonly IEmbeddingService? _embeddingService;

    public AssembleContextCommandHandler(
        ContextAssembler assembler,
        IEmbeddingService? embeddingService = null)
    {
        _assembler = assembler ?? throw new ArgumentNullException(nameof(assembler));
        _embeddingService = embeddingService;
    }

    public async Task<AssembledContextDto> Handle(
        AssembleContextCommand request,
        CancellationToken cancellationToken)
    {
        // Generate query embedding if requested and service available
        float[]? queryEmbedding = null;
        if (request.IncludeEmbedding && _embeddingService != null)
        {
            try
            {
                var embeddingResult = await _embeddingService.GenerateEmbeddingAsync(
                    request.Query,
                    cancellationToken).ConfigureAwait(false);

                if (embeddingResult.Success && embeddingResult.Embeddings.Count > 0)
                {
                    queryEmbedding = embeddingResult.Embeddings[0];
                }
            }
            catch
            {
                // Continue without embedding if generation fails
                queryEmbedding = null;
            }
        }

        // Build assembly request
        var assemblyRequest = new ContextAssemblyRequest
        {
            Query = request.Query,
            GameId = request.GameId,
            UserId = request.UserId,
            SessionId = request.SessionId,
            MaxTotalTokens = request.MaxTotalTokens,
            MinRelevance = request.MinRelevance,
            QueryEmbedding = queryEmbedding,
            SourcePriorities = request.SourcePriorities,
            MinTokensPerSource = request.MinTokensPerSource,
            MaxTokensPerSource = request.MaxTokensPerSource
        };

        // Assemble context
        var assembled = await _assembler.AssembleAsync(
            assemblyRequest,
            cancellationToken).ConfigureAwait(false);

        // Format context string
        var formatOptions = new ContextFormatOptions
        {
            GroupBySource = true,
            IncludeSourceHeaders = true,
            IncludeMetadata = false,
            Preamble = "# Context Information\n\nThe following context has been gathered from multiple sources to assist with your query."
        };

        var formattedContext = ContextAssembler.BuildContextString(assembled, formatOptions);

        // Map to DTO
        return new AssembledContextDto
        {
            Query = assembled.Query,
            FormattedContext = formattedContext,
            Items = assembled.Items.Select(MapToItemDto).ToList(),
            TotalTokens = assembled.TotalTokens,
            Budget = MapToBudgetDto(assembled.BudgetSnapshot),
            Metrics = MapToMetricsDto(assembled.Metrics),
            AssembledAt = assembled.AssembledAt
        };
    }

    private static ContextItemDto MapToItemDto(AssembledContextItem item)
    {
        return new ContextItemDto
        {
            SourceId = item.SourceId,
            SourceName = FormatSourceName(item.SourceId),
            Content = item.Item.Content,
            Relevance = item.Item.Relevance,
            TokenCount = item.Item.TokenCount,
            ContentType = item.Item.ContentType,
            Priority = item.Priority,
            Timestamp = item.Item.Timestamp
        };
    }

    private static BudgetSnapshotDto MapToBudgetDto(ContextBudgetSnapshot snapshot)
    {
        return new BudgetSnapshotDto
        {
            TotalBudget = snapshot.TotalBudget,
            AllocatedTokens = snapshot.AllocatedTokens,
            UsedTokens = snapshot.UsedTokens,
            RemainingBudget = snapshot.RemainingBudget,
            Sources = snapshot.SourceAllocations.ToDictionary(
                kvp => kvp.Key,
                kvp => new SourceAllocationDto
                {
                    Priority = kvp.Value.Priority,
                    Allocated = kvp.Value.Allocated,
                    Used = kvp.Value.Used,
                    Remaining = kvp.Value.Remaining
                },
                StringComparer.Ordinal)
        };
    }

    private static MetricsDto MapToMetricsDto(AssemblyMetrics metrics)
    {
        return new MetricsDto
        {
            TotalDurationMs = metrics.TotalDurationMs,
            ItemsRetrieved = metrics.TotalItemsRetrieved,
            SourcesQueried = metrics.BySource.Count,
            SourcesSucceeded = metrics.BySource.Count(s => s.Value.IsAvailable && s.Value.Error == null),
            Sources = metrics.BySource.ToDictionary(
                kvp => kvp.Key,
                kvp => new SourceMetricsDto
                {
                    DurationMs = kvp.Value.DurationMs,
                    ItemCount = kvp.Value.ItemCount,
                    IsAvailable = kvp.Value.IsAvailable,
                    Error = kvp.Value.Error
                },
                StringComparer.Ordinal)
        };
    }

    private static string FormatSourceName(string sourceId)
    {
        return sourceId switch
        {
            "static_knowledge" => "Knowledge Base",
            "conversation_memory" => "Conversation History",
            "game_state" => "Game State",
            "strategy_patterns" => "Strategy Patterns",
            "tool_metadata" => "Available Tools",
            _ => sourceId.Replace("_", " ", StringComparison.Ordinal)
        };
    }
}
