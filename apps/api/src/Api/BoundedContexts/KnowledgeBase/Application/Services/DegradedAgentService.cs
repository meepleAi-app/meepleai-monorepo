using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Provides degraded AI agent functionality using BGG metadata when no KB cards are available.
/// Builds a context-rich system prompt from game metadata so the agent can still answer
/// basic questions about player count, complexity, categories, and general game info.
/// </summary>
public interface IDegradedAgentService
{
    /// <summary>
    /// Builds a system prompt for an agent that has no KB cards, using only BGG metadata.
    /// </summary>
    string BuildDegradedSystemPrompt(DegradedAgentContext context);

    /// <summary>
    /// Determines the capability level of an agent based on its available knowledge sources.
    /// </summary>
    Task<AgentCapability> GetAgentCapabilityAsync(Guid agentId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Context data for building a degraded agent prompt from BGG metadata.
/// </summary>
public sealed record DegradedAgentContext
{
    public string GameName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int? MinPlayers { get; init; }
    public int? MaxPlayers { get; init; }
    public int? PlayTimeMinutes { get; init; }
    public decimal? ComplexityWeight { get; init; }
    public string? YearPublished { get; init; }
    public List<string> Categories { get; init; } = new();
    public List<string> Mechanics { get; init; } = new();
    public string? Publisher { get; init; }
    public string? Designer { get; init; }
}

/// <inheritdoc />
internal sealed class DegradedAgentService : IDegradedAgentService
{
    private readonly MeepleAiDbContext _dbContext;

    public DegradedAgentService(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public string BuildDegradedSystemPrompt(DegradedAgentContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"You are an AI board game assistant for **{context.GameName}**.");
        sb.AppendLine();
        sb.AppendLine("## Important Limitations");
        sb.AppendLine("You do NOT have access to the full rulebook for this game. Your knowledge is limited to the general game information below. If asked about specific rules, card effects, or detailed mechanics, clearly state that you don't have the rulebook and suggest the user upload the PDF for full assistance.");
        sb.AppendLine();
        sb.AppendLine("## Game Information");

        if (context.MinPlayers.HasValue || context.MaxPlayers.HasValue)
        {
            var players = context.MinPlayers == context.MaxPlayers
                ? $"{context.MinPlayers} players"
                : $"{context.MinPlayers}-{context.MaxPlayers} players";
            sb.AppendLine($"- **Players**: {players}");
        }

        if (context.PlayTimeMinutes.HasValue)
            sb.AppendLine($"- **Play Time**: ~{context.PlayTimeMinutes} minutes");

        if (context.ComplexityWeight.HasValue)
            sb.AppendLine($"- **Complexity**: {context.ComplexityWeight.Value.ToString("F1", CultureInfo.InvariantCulture)}/5");

        if (!string.IsNullOrEmpty(context.YearPublished))
            sb.AppendLine($"- **Year Published**: {context.YearPublished}");

        if (!string.IsNullOrEmpty(context.Publisher))
            sb.AppendLine($"- **Publisher**: {context.Publisher}");

        if (!string.IsNullOrEmpty(context.Designer))
            sb.AppendLine($"- **Designer**: {context.Designer}");

        if (context.Categories.Count > 0)
            sb.AppendLine($"- **Categories**: {string.Join(", ", context.Categories)}");

        if (context.Mechanics.Count > 0)
            sb.AppendLine($"- **Mechanics**: {string.Join(", ", context.Mechanics)}");

        if (!string.IsNullOrEmpty(context.Description))
        {
            sb.AppendLine();
            sb.AppendLine("## Game Description");
            // Truncate very long descriptions to avoid bloating the prompt
            var desc = context.Description.Length > 1000
                ? context.Description[..1000] + "..."
                : context.Description;
            sb.AppendLine(desc);
        }

        sb.AppendLine();
        sb.AppendLine("## What You Can Help With");
        sb.AppendLine("- General game overview and setup guidance");
        sb.AppendLine("- Player count recommendations");
        sb.AppendLine("- Complexity assessment and who the game is suitable for");
        sb.AppendLine("- Similar game suggestions based on mechanics and categories");
        sb.AppendLine("- Basic strategy tips based on game type");

        return sb.ToString();
    }

    /// <inheritdoc />
    public async Task<AgentCapability> GetAgentCapabilityAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        // Load the agent definition to check KB cards
        var agent = await _dbContext.AgentDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == agentId, cancellationToken)
            .ConfigureAwait(false);

        if (agent == null)
            return AgentCapability.None();

        var hasKbCards = agent.KbCardIds.Count > 0;

        if (hasKbCards)
        {
            // Check if rulebook analysis exists for the linked shared game.
            // The FK is on the game side: SharedGameEntity.AgentDefinitionId -> AgentDefinition.Id
            var linkedSharedGameId = await _dbContext.SharedGames
                .AsNoTracking()
                .Where(g => g.AgentDefinitionId == agentId && !g.IsDeleted)
                .Select(g => (Guid?)g.Id)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            var hasRulebookAnalysis = linkedSharedGameId.HasValue && await _dbContext.RulebookAnalyses
                .AnyAsync(r => r.SharedGameId == linkedSharedGameId.Value, cancellationToken)
                .ConfigureAwait(false);

            return AgentCapability.Full(hasRulebookAnalysis);
        }

        // No KB cards — check if a game is linked to this agent (BGG metadata available).
        // The FK is on the game side: SharedGameEntity.AgentDefinitionId / PrivateGameEntity.AgentDefinitionId
        var hasSharedGame = await _dbContext.SharedGames
            .AnyAsync(g => g.AgentDefinitionId == agentId && !g.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        if (hasSharedGame)
            return AgentCapability.Degraded();

        var hasPrivateGame = await _dbContext.PrivateGames
            .AnyAsync(g => g.AgentDefinitionId == agentId && !g.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        if (hasPrivateGame)
            return AgentCapability.Degraded();

        return AgentCapability.None();
    }
}
