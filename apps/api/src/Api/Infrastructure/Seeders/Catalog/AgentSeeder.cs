using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders.Catalog;

/// <summary>
/// Seeds agent infrastructure for games with seedAgent=true in the manifest.
/// Merges legacy DefaultAgentSeeder + CatanPocAgentSeeder patterns into a single manifest-driven seeder.
/// Creates: AgentTypology + PromptTemplate, Agent (linked to game), AgentConfiguration,
/// GameSession, AgentSession — the full entity chain for a working agent.
/// Idempotent: skips if Agent with matching name already exists for the game.
/// </summary>
internal static class AgentSeeder
{
    private const string DefaultTypologyName = "Rules Expert";
    private const string DefaultAgentType = "RAG";
    private const string DefaultStrategyName = "SingleModel";

    private const string TypologyBasePrompt =
        "You are a Rules Expert for board games. Your primary function is to provide accurate, " +
        "authoritative interpretations of game rules. You prioritize official rulebook text over " +
        "common house rules, clearly distinguish between official rules and common variants, " +
        "and help resolve rule disputes with precise citations.";

    private const string SystemPromptTemplate = """
        You are MeepleAssistant, a professional board game consultant and expert advisor.

        # ROLE & EXPERTISE
        You provide authoritative guidance on board games including:
        - Rules clarification and interpretation
        - Strategic analysis and optimal play patterns
        - Game recommendations based on player preferences
        - Comparative analysis between similar games
        - House rules evaluation and suggestions

        # KNOWLEDGE BASE INTEGRATION
        {RAG_CONTEXT}

        When CONTEXT is provided above:
        - Base your response EXCLUSIVELY on the provided context
        - Cite specific sections using [Source: <document_name>] format
        - If context is insufficient, clearly state limitations
        - Never fabricate information not present in context

        When NO CONTEXT is provided:
        - Respond using your general board game knowledge
        - Clearly indicate you're providing general information
        - Recommend consulting official rules for tournament play

        # RESPONSE FORMAT
        1. **Direct Answer**: Address the specific rule or question
        2. **Explanation**: Supporting details and context
        3. **Additional Insights**: Related information if valuable
        4. **Sources**: Citation if using RAG context, or "General knowledge" otherwise
        """;

    /// <summary>
    /// Seeds agent entities for all games with seedAgent=true in the manifest.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="manifest">The seed manifest with game and agent configuration.</param>
    /// <param name="gameMap">BggId-to-GameEntityId mapping from GameSeeder.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task SeedAsync(
        MeepleAiDbContext db,
        SeedManifest manifest,
        Dictionary<int, Guid> gameMap,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var agentConfig = manifest.Catalog.DefaultAgent;
        if (agentConfig is null)
        {
            logger.LogDebug("AgentSeeder: no defaultAgent section in manifest, skipping");
            return;
        }

        var agentGames = manifest.Catalog.Games
            .Where(g => g.SeedAgent && gameMap.ContainsKey(g.BggId))
            .ToList();

        if (agentGames.Count == 0)
        {
            logger.LogDebug("AgentSeeder: no games with seedAgent=true found in gameMap, skipping");
            return;
        }

        // Find admin user for CreatedBy FKs
        var adminUser = await db.Users
            .FirstOrDefaultAsync(u => u.Role == "admin", cancellationToken)
            .ConfigureAwait(false);

        if (adminUser is null)
        {
            logger.LogWarning("AgentSeeder: admin user not found, skipping agent seeding");
            return;
        }

        var now = DateTime.UtcNow;
        var adminUserId = adminUser.Id;

        // Ensure AgentTypology exists (shared across all seeded agents)
        var typology = await EnsureAgentTypologyAsync(db, adminUserId, now, logger, cancellationToken)
            .ConfigureAwait(false);

        var seededCount = 0;

        foreach (var game in agentGames)
        {
            var gameEntityId = gameMap[game.BggId];
            var agentName = $"{agentConfig.Name} - {game.Title}";

            // Idempotency: skip if agent with this name already exists
            var existingAgent = await db.Set<AgentEntity>()
                .FirstOrDefaultAsync(a => a.Name == agentName, cancellationToken)
                .ConfigureAwait(false);

            if (existingAgent is not null)
            {
                logger.LogDebug("Agent '{AgentName}' already exists (ID: {AgentId}), skipping",
                    agentName, existingAgent.Id);
                continue;
            }

            // Create Agent linked to game
            var agentId = Guid.NewGuid();
            var agent = new AgentEntity
            {
                Id = agentId,
                Name = agentName,
                Type = DefaultAgentType,
                StrategyName = DefaultStrategyName,
                StrategyParametersJson = "{}",
                IsActive = true,
                CreatedAt = now,
                LastInvokedAt = null,
                InvocationCount = 0,
                GameId = gameEntityId,
                CreatedByUserId = adminUserId
            };

            db.Set<AgentEntity>().Add(agent);

            // Create AgentConfiguration with manifest settings
            var configuration = new AgentConfigurationEntity
            {
                Id = Guid.NewGuid(),
                AgentId = agentId,
                LlmProvider = 0, // OpenRouter
                LlmModel = agentConfig.Model,
                AgentMode = 0, // Chat
                SelectedDocumentIdsJson = "[]",
                Temperature = (decimal)agentConfig.Temperature,
                MaxTokens = agentConfig.MaxTokens,
                SystemPromptOverride = SystemPromptTemplate,
                IsCurrent = true,
                CreatedAt = now,
                CreatedBy = adminUserId
            };

            db.Set<AgentConfigurationEntity>().Add(configuration);

            // Create GameSession (FK requirement for AgentSession)
            var gameSession = new GameSessionEntity
            {
                Id = Guid.NewGuid(),
                GameId = gameEntityId,
                CreatedByUserId = adminUserId,
                Status = "Setup",
                StartedAt = now,
                PlayersJson = """[{"PlayerName":"POC Test Player","PlayerOrder":1,"Color":"Red"}]"""
            };

            db.GameSessions.Add(gameSession);

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Create AgentSession linking everything together
            var agentSession = new AgentSessionEntity
            {
                Id = Guid.NewGuid(),
                AgentId = agentId,
                GameSessionId = gameSession.Id,
                UserId = adminUserId,
                GameId = gameEntityId,
                TypologyId = typology.Id,
                CurrentGameStateJson = """{"phase":"setup","turn":0,"players":[],"board":{}}""",
                StartedAt = now,
                EndedAt = null,
                IsActive = true
            };

            db.AgentSessions.Add(agentSession);

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            seededCount++;
            logger.LogInformation(
                "Seeded Agent '{AgentName}' for game '{GameTitle}' (AgentId: {AgentId}, GameId: {GameId})",
                agentName, game.Title, agentId, gameEntityId);
        }

        logger.LogInformation("AgentSeeder completed: {SeededCount}/{TotalCount} agents seeded",
            seededCount, agentGames.Count);
    }

    private static async Task<AgentTypologyEntity> EnsureAgentTypologyAsync(
        MeepleAiDbContext db,
        Guid adminUserId,
        DateTime now,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var existing = await db.AgentTypologies
            .FirstOrDefaultAsync(t => t.Name == DefaultTypologyName && !t.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            logger.LogDebug("AgentTypology '{Name}' already exists (ID: {Id})", DefaultTypologyName, existing.Id);
            return existing;
        }

        var typologyId = Guid.NewGuid();
        var typology = new AgentTypologyEntity
        {
            Id = typologyId,
            Name = DefaultTypologyName,
            Description = "Specialized agent typology for board game rules interpretation and clarification. Prioritizes official rulebook accuracy.",
            BasePrompt = TypologyBasePrompt,
            DefaultStrategyJson = """{"Name":"SingleModel","Parameters":{}}""",
            Status = 2, // Approved
            CreatedBy = adminUserId,
            ApprovedBy = adminUserId,
            CreatedAt = now,
            ApprovedAt = now,
            IsDeleted = false
        };

        db.AgentTypologies.Add(typology);

        // Add prompt template for the typology
        var promptTemplate = new TypologyPromptTemplateEntity
        {
            Id = Guid.NewGuid(),
            TypologyId = typologyId,
            Content = TypologyBasePrompt,
            Version = 1,
            IsCurrent = true,
            CreatedBy = adminUserId,
            CreatedAt = now
        };

        db.Set<TypologyPromptTemplateEntity>().Add(promptTemplate);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Created AgentTypology '{Name}' (ID: {Id}, Status: Approved)",
            DefaultTypologyName, typologyId);

        return typology;
    }
}
