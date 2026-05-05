using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Heuristic implementation of <see cref="IHouseRuleMatcher"/> that uses word-overlap
/// scoring to detect whether the user's question relates to a stored house rule.
/// Cross-BC read: reads from AgentMemory.IGameMemoryRepository.
/// Degrades gracefully (returns null) on any repository failure.
/// </summary>
internal sealed class HeuristicHouseRuleMatcher(
    IGameMemoryRepository gameMemoryRepository,
    ILogger<HeuristicHouseRuleMatcher> logger) : IHouseRuleMatcher
{
    private const float MinOverlapThreshold = 0.30f;

    public async Task<string?> FindMatchingHouseRuleAsync(
        Guid gameId, Guid? userId, string question, CancellationToken ct = default)
    {
        if (!userId.HasValue) return null;

        try
        {
            var memory = await gameMemoryRepository
                .GetByGameAndOwnerAsync(gameId, userId.Value, ct).ConfigureAwait(false);

            if (memory is null || memory.HouseRules.Count == 0) return null;

            var questionWords = ExtractWords(question);
            if (questionWords.Count == 0) return null;

            foreach (var rule in memory.HouseRules)
            {
                var ruleWords = ExtractWords(rule.Description);
                var overlap = ComputeOverlap(questionWords, ruleWords);
                if (overlap >= MinOverlapThreshold)
                {
                    logger.LogDebug(
                        "[HouseRuleMatcher] Matched rule (overlap={Overlap:P0}): {Rule}",
                        overlap, rule.Description);
                    return rule.Description;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[HouseRuleMatcher] Failed to read house rules for game {GameId}", gameId);
        }

        return null;
    }

    private static HashSet<string> ExtractWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return text.ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(w => w.Length > 3)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static float ComputeOverlap(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0f;
        var intersection = a.Count(w => b.Contains(w));
        return (float)intersection / Math.Min(a.Count, b.Count);
    }
}
