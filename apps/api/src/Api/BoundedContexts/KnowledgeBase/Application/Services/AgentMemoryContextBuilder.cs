using System.Globalization;
using System.Text;
using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.Services;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Builds agent memory context (house rules, group preferences, notes) for injection
/// into RAG chat system prompts during active sessions.
/// Cross-context read-only dependency on AgentMemory bounded context.
/// </summary>
internal sealed class AgentMemoryContextBuilder : IAgentMemoryContextBuilder
{
    private const string FeatureFlag = "Features:AgentMemory.Enabled";

    private readonly IGameMemoryRepository _gameMemoryRepository;
    private readonly IGroupMemoryRepository _groupMemoryRepository;
    private readonly IFeatureFlagService _featureFlagService;
    private readonly ILogger<AgentMemoryContextBuilder> _logger;

    public AgentMemoryContextBuilder(
        IGameMemoryRepository gameMemoryRepository,
        IGroupMemoryRepository groupMemoryRepository,
        IFeatureFlagService featureFlagService,
        ILogger<AgentMemoryContextBuilder> logger)
    {
        _gameMemoryRepository = gameMemoryRepository ?? throw new ArgumentNullException(nameof(gameMemoryRepository));
        _groupMemoryRepository = groupMemoryRepository ?? throw new ArgumentNullException(nameof(groupMemoryRepository));
        _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Builds the agent memory context string to append to the system prompt.
    /// Returns null if the feature is disabled or no memory data exists.
    /// </summary>
    /// <param name="gameId">The game being played</param>
    /// <param name="ownerId">The user who owns the game memory (session creator)</param>
    /// <param name="groupId">Optional group ID for group preferences</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Formatted context string, or null if nothing to inject</returns>
    public async Task<string?> BuildContextAsync(Guid gameId, Guid ownerId, Guid? groupId, CancellationToken ct)
    {
        // Gate behind feature flag
        var isEnabled = await _featureFlagService.IsEnabledAsync(FeatureFlag).ConfigureAwait(false);
        if (!isEnabled)
        {
            _logger.LogDebug("AgentMemory feature flag is disabled, skipping context injection");
            return null;
        }

        try
        {
            var sb = new StringBuilder();
            var hasContent = false;

            // Fetch game memory (house rules + notes)
            var gameMemory = await _gameMemoryRepository
                .GetByGameAndOwnerAsync(gameId, ownerId, ct)
                .ConfigureAwait(false);

            if (gameMemory != null)
            {
                // House rules
                if (gameMemory.HouseRules.Count > 0)
                {
                    sb.AppendLine("House Rules for this game:");
                    foreach (var rule in gameMemory.HouseRules)
                    {
                        var sourceLabel = rule.Source switch
                        {
                            AgentMemory.Domain.Enums.HouseRuleSource.UserAdded => "from player",
                            AgentMemory.Domain.Enums.HouseRuleSource.DisputeOverride =>
                                string.Create(CultureInfo.InvariantCulture, $"from dispute override on {rule.AddedAt:MM/dd}"),
                            _ => "custom"
                        };
                        sb.AppendLine(CultureInfo.InvariantCulture, $"- \"{rule.Description}\" ({sourceLabel})");
                    }
                    sb.AppendLine();
                    hasContent = true;
                }

                // Notes
                if (gameMemory.Notes.Count > 0)
                {
                    sb.AppendLine("Notes:");
                    foreach (var note in gameMemory.Notes)
                    {
                        sb.AppendLine(CultureInfo.InvariantCulture, $"- \"{note.Content}\"");
                    }
                    sb.AppendLine();
                    hasContent = true;
                }
            }

            // Fetch group memory (preferences)
            if (groupId.HasValue)
            {
                var groupMemory = await _groupMemoryRepository
                    .GetByIdAsync(groupId.Value, ct)
                    .ConfigureAwait(false);

                if (groupMemory != null)
                {
                    var hasPreferences = groupMemory.Preferences.MaxDuration.HasValue
                        || groupMemory.Preferences.PreferredComplexity.HasValue
                        || !string.IsNullOrWhiteSpace(groupMemory.Preferences.CustomNotes);

                    if (hasPreferences)
                    {
                        sb.AppendLine(CultureInfo.InvariantCulture, $"Group Preferences ({groupMemory.Name}):");

                        if (groupMemory.Preferences.MaxDuration.HasValue)
                        {
                            var duration = groupMemory.Preferences.MaxDuration.Value;
                            var durationText = duration.TotalHours >= 1
                                ? string.Create(CultureInfo.InvariantCulture, $"{duration.TotalHours:0.#} hours")
                                : string.Create(CultureInfo.InvariantCulture, $"{duration.TotalMinutes:0} minutes");
                            sb.AppendLine(CultureInfo.InvariantCulture, $"- Max duration: {durationText}");
                        }

                        if (groupMemory.Preferences.PreferredComplexity.HasValue)
                        {
                            sb.AppendLine(CultureInfo.InvariantCulture, $"- Preferred complexity: {groupMemory.Preferences.PreferredComplexity.Value}");
                        }

                        if (!string.IsNullOrWhiteSpace(groupMemory.Preferences.CustomNotes))
                        {
                            sb.AppendLine(CultureInfo.InvariantCulture, $"- Notes: {groupMemory.Preferences.CustomNotes}");
                        }

                        sb.AppendLine();
                        hasContent = true;
                    }
                }
            }

            if (!hasContent)
            {
                _logger.LogDebug(
                    "No agent memory data found for game {GameId}, owner {OwnerId}, group {GroupId}",
                    gameId, ownerId, groupId);
                return null;
            }

            var context = sb.ToString().TrimEnd();

            _logger.LogInformation(
                "Built agent memory context for game {GameId}: {ContextLength} chars",
                gameId, context.Length);

            return context;
        }
        catch (Exception ex)
        {
            // Graceful degradation: log and continue without memory context
            _logger.LogWarning(ex,
                "Failed to build agent memory context for game {GameId}, continuing without it",
                gameId);
            return null;
        }
    }
}
