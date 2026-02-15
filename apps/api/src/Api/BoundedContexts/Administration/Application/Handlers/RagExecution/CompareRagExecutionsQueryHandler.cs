using System.Text.Json;
using Api.BoundedContexts.Administration.Application.Commands.RagPipeline;
using Api.BoundedContexts.Administration.Application.Queries.RagExecution;
using Api.BoundedContexts.Administration.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Handlers.RagExecution;

/// <summary>
/// Handler for CompareRagExecutionsQuery.
/// Fetches two executions and produces a side-by-side diff.
/// Issue #4459: RAG Query Replay.
/// </summary>
internal sealed class CompareRagExecutionsQueryHandler(
    IRagExecutionRepository executionRepository,
    ILogger<CompareRagExecutionsQueryHandler> logger
) : IRequestHandler<CompareRagExecutionsQuery, RagExecutionComparisonDto>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<RagExecutionComparisonDto> Handle(
        CompareRagExecutionsQuery request,
        CancellationToken cancellationToken)
    {
        var exec1 = await executionRepository.GetByIdAsync(request.ExecutionId1, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("RagExecution", request.ExecutionId1.ToString());

        var exec2 = await executionRepository.GetByIdAsync(request.ExecutionId2, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("RagExecution", request.ExecutionId2.ToString());

        logger.LogInformation(
            "Admin {UserId} comparing executions {Exec1} and {Exec2}",
            request.UserId,
            request.ExecutionId1,
            request.ExecutionId2);

        var events1 = DeserializeEvents(exec1.EventsJson);
        var events2 = DeserializeEvents(exec2.EventsJson);

        var summary1 = BuildSummary(exec1);
        var summary2 = BuildSummary(exec2);

        var metricsDelta = BuildMetricsDelta(exec1, exec2);
        var blockComparisons = BuildBlockComparisons(events1, events2);
        var documentDiffs = BuildDocumentDiffs(events1, events2);

        return new RagExecutionComparisonDto
        {
            Execution1 = summary1,
            Execution2 = summary2,
            MetricsDelta = metricsDelta,
            BlockComparisons = blockComparisons,
            DocumentDiffs = documentDiffs
        };
    }

    private static ExecutionSummaryDto BuildSummary(
        Domain.Aggregates.RagExecution.RagExecution execution)
    {
        return new ExecutionSummaryDto
        {
            Id = execution.Id,
            TestQuery = execution.TestQuery,
            Success = execution.Success,
            TotalDurationMs = execution.TotalDurationMs,
            TotalTokensUsed = execution.TotalTokensUsed,
            TotalCost = execution.TotalCost,
            BlocksExecuted = execution.BlocksExecuted,
            BlocksFailed = execution.BlocksFailed,
            FinalResponse = execution.FinalResponse,
            ConfigOverridesJson = execution.ConfigOverridesJson,
            ParentExecutionId = execution.ParentExecutionId,
            ExecutedAt = execution.ExecutedAt
        };
    }

    private static MetricsDeltaDto BuildMetricsDelta(
        Domain.Aggregates.RagExecution.RagExecution exec1,
        Domain.Aggregates.RagExecution.RagExecution exec2)
    {
        var durationDelta = exec2.TotalDurationMs - exec1.TotalDurationMs;
        var tokensDelta = exec2.TotalTokensUsed - exec1.TotalTokensUsed;
        var costDelta = exec2.TotalCost - exec1.TotalCost;

        // Assess: improved if execution2 is faster, cheaper, or more successful
        var assessment = "unchanged";
        if (exec2.Success && !exec1.Success)
        {
            assessment = "improved";
        }
        else if (!exec2.Success && exec1.Success)
        {
            assessment = "degraded";
        }
        else if (durationDelta < 0 && costDelta <= 0)
        {
            assessment = "improved";
        }
        else if (durationDelta > 0 && costDelta > 0)
        {
            assessment = "degraded";
        }

        return new MetricsDeltaDto
        {
            DurationDeltaMs = durationDelta,
            TokensDelta = tokensDelta,
            CostDelta = costDelta,
            OverallAssessment = assessment
        };
    }

    private static IReadOnlyList<BlockComparisonDto> BuildBlockComparisons(
        IReadOnlyList<RagPipelineTestEvent> events1,
        IReadOnlyList<RagPipelineTestEvent> events2)
    {
        var blocks1 = ExtractBlockMetrics(events1);
        var blocks2 = ExtractBlockMetrics(events2);

        var allBlockIds = blocks1.Keys.Union(blocks2.Keys, StringComparer.Ordinal).Distinct(StringComparer.Ordinal).ToList();
        var comparisons = new List<BlockComparisonDto>();

        foreach (var blockId in allBlockIds)
        {
            blocks1.TryGetValue(blockId, out var b1);
            blocks2.TryGetValue(blockId, out var b2);

            var status = (b1, b2) switch
            {
                (null, not null) => "added",
                (not null, null) => "removed",
                (not null, not null) when b1.DurationMs > b2.DurationMs => "improved",
                (not null, not null) when b1.DurationMs < b2.DurationMs => "degraded",
                _ => "unchanged"
            };

            var blockType = b1?.BlockType ?? b2?.BlockType ?? "unknown";
            var blockName = b1?.BlockName ?? b2?.BlockName ?? blockId;

            comparisons.Add(new BlockComparisonDto
            {
                BlockId = blockId,
                BlockType = blockType,
                BlockName = blockName,
                Execution1 = b1 != null ? new BlockMetricsDto
                {
                    Success = b1.Success,
                    DurationMs = b1.DurationMs,
                    TokensUsed = b1.TokensUsed,
                    Cost = b1.Cost,
                    ValidationScore = b1.ValidationScore,
                    DocumentCount = b1.DocumentCount
                } : null,
                Execution2 = b2 != null ? new BlockMetricsDto
                {
                    Success = b2.Success,
                    DurationMs = b2.DurationMs,
                    TokensUsed = b2.TokensUsed,
                    Cost = b2.Cost,
                    ValidationScore = b2.ValidationScore,
                    DocumentCount = b2.DocumentCount
                } : null,
                Status = status
            });
        }

        return comparisons;
    }

    private static IReadOnlyList<DocumentDiffDto> BuildDocumentDiffs(
        IReadOnlyList<RagPipelineTestEvent> events1,
        IReadOnlyList<RagPipelineTestEvent> events2)
    {
        var docs1ByBlock = events1.OfType<DocumentsRetrievedEvent>()
            .ToDictionary(e => e.BlockId, e => e.Documents, StringComparer.Ordinal);
        var docs2ByBlock = events2.OfType<DocumentsRetrievedEvent>()
            .ToDictionary(e => e.BlockId, e => e.Documents, StringComparer.Ordinal);

        var allBlockIds = docs1ByBlock.Keys.Union(docs2ByBlock.Keys, StringComparer.Ordinal).Distinct(StringComparer.Ordinal);
        var diffs = new List<DocumentDiffDto>();

        foreach (var blockId in allBlockIds)
        {
            docs1ByBlock.TryGetValue(blockId, out var d1);
            docs2ByBlock.TryGetValue(blockId, out var d2);

            var docIds1 = (d1 ?? []).Select(d => d.Id).ToHashSet(StringComparer.Ordinal);
            var docIds2 = (d2 ?? []).Select(d => d.Id).ToHashSet(StringComparer.Ordinal);

            var onlyIn1 = docIds1.Except(docIds2, StringComparer.Ordinal).ToList();
            var onlyIn2 = docIds2.Except(docIds1, StringComparer.Ordinal).ToList();
            var inBoth = docIds1.Intersect(docIds2, StringComparer.Ordinal).ToList();

            var scoreChanges = new List<ScoreChangeDto>();
            foreach (var docId in inBoth)
            {
                var score1 = d1!.First(d => string.Equals(d.Id, docId, StringComparison.Ordinal)).Score;
                var score2 = d2!.First(d => string.Equals(d.Id, docId, StringComparison.Ordinal)).Score;
                scoreChanges.Add(new ScoreChangeDto
                {
                    DocumentId = docId,
                    Score1 = score1,
                    Score2 = score2,
                    Delta = score2 - score1
                });
            }

            diffs.Add(new DocumentDiffDto
            {
                BlockId = blockId,
                OnlyInExecution1 = onlyIn1,
                OnlyInExecution2 = onlyIn2,
                InBoth = inBoth,
                ScoreChanges = scoreChanges
            });
        }

        return diffs;
    }

    private static Dictionary<string, BlockInfo> ExtractBlockMetrics(
        IReadOnlyList<RagPipelineTestEvent> events)
    {
        var blocks = new Dictionary<string, BlockInfo>(StringComparer.Ordinal);

        foreach (var evt in events.OfType<BlockExecutionStartedEvent>())
        {
            blocks[evt.BlockId] = new BlockInfo
            {
                BlockType = evt.BlockType,
                BlockName = evt.BlockName
            };
        }

        foreach (var evt in events.OfType<BlockExecutionCompletedEvent>())
        {
            if (blocks.TryGetValue(evt.BlockId, out var block))
            {
                block.Success = evt.Success;
                block.DurationMs = evt.DurationMs;
                block.TokensUsed = evt.TokensUsed;
                block.Cost = evt.Cost;
            }
        }

        foreach (var evt in events.OfType<ValidationResultEvent>())
        {
            if (blocks.TryGetValue(evt.BlockId, out var block))
            {
                block.ValidationScore = evt.Score;
            }
        }

        foreach (var evt in events.OfType<DocumentsRetrievedEvent>())
        {
            if (blocks.TryGetValue(evt.BlockId, out var block))
            {
                block.DocumentCount = evt.DocumentCount;
            }
        }

        return blocks;
    }

    private static IReadOnlyList<RagPipelineTestEvent> DeserializeEvents(string eventsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(eventsJson);
            var events = new List<RagPipelineTestEvent>();

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                var eventType = element.GetProperty("eventType").GetString();
                RagPipelineTestEvent? evt = eventType switch
                {
                    nameof(PipelineTestStartedEvent) =>
                        element.Deserialize<PipelineTestStartedEvent>(JsonOptions),
                    nameof(BlockExecutionStartedEvent) =>
                        element.Deserialize<BlockExecutionStartedEvent>(JsonOptions),
                    nameof(BlockExecutionCompletedEvent) =>
                        element.Deserialize<BlockExecutionCompletedEvent>(JsonOptions),
                    nameof(DocumentsRetrievedEvent) =>
                        element.Deserialize<DocumentsRetrievedEvent>(JsonOptions),
                    nameof(ValidationResultEvent) =>
                        element.Deserialize<ValidationResultEvent>(JsonOptions),
                    nameof(PipelineTestCompletedEvent) =>
                        element.Deserialize<PipelineTestCompletedEvent>(JsonOptions),
                    _ => null
                };

                if (evt != null)
                {
                    events.Add(evt);
                }
            }

            return events;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed class BlockInfo
    {
        public string BlockType { get; set; } = string.Empty;
        public string BlockName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public int DurationMs { get; set; }
        public int TokensUsed { get; set; }
        public decimal Cost { get; set; }
        public double? ValidationScore { get; set; }
        public int? DocumentCount { get; set; }
    }
}
