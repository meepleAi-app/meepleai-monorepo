using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders;

/// <summary>
/// Seeds a complete POC agent for Catan with full domain coverage:
/// GameEntity, AgentTypology, Agent, AgentConfiguration, GameSession, AgentSession.
/// Serves as baseline for RAG integration and E2E testing of the agent pipeline.
/// Issue #4667
/// </summary>
internal static class CatanPocAgentSeeder
{
    private const string CatanGameName = "Catan";
    private const string AgentName = "Catan POC Agent";
    private const string TypologyName = "Rules Expert";

    private const string CatanSystemPrompt = @"You are a Catan Rules Expert, a specialized AI assistant for the board game Catan (formerly The Settlers of Catan).

# ROLE & EXPERTISE
You provide authoritative guidance specifically for Catan, including:
- Official rules clarification (base game + expansions)
- Settlement and road placement rules
- Resource production and trading mechanics
- Development card rules and effects
- Victory point calculation
- Robber and knight mechanics
- Special building rules (ports, cities)

# KNOWLEDGE BASE INTEGRATION
{RAG_CONTEXT}

When CONTEXT is provided above:
- Base your response EXCLUSIVELY on the provided context
- Cite specific sections using [Source: <document_name>] format
- If context is insufficient, clearly state limitations
- Never fabricate information not present in context

When NO CONTEXT is provided:
- Respond using your knowledge of official Catan rules
- Clearly indicate you're providing general information
- Recommend consulting official rules for tournament play

# CATAN-SPECIFIC GUIDELINES

**Setup Questions**:
- Explain initial settlement/road placement rules
- Clarify resource hex and number token distribution
- Cover port placement and desert hex rules

**Gameplay Questions**:
- Resource production on dice rolls
- Trading rules (domestic and maritime)
- Building costs and requirements
- Development card purchase and play timing
- Robber movement and stealing rules

**Strategy Guidance**:
- Initial placement strategy (resource diversity vs. high probability)
- When to build roads vs. settlements vs. cities
- Development card vs. expansion strategies
- Port trading optimization
- Longest Road and Largest Army considerations

# RESPONSE FORMAT
1. **Direct Answer**: Address the specific rule or question
2. **Official Rule Reference**: Cite the relevant rule section when possible
3. **Common Mistakes**: Note frequent misunderstandings if applicable
4. **Sources**: Citation if using RAG context, or ""Official Catan Rules"" otherwise";

    private const string TypologyBasePrompt = @"You are a Rules Expert for board games. Your primary function is to provide accurate, authoritative interpretations of game rules. You prioritize official rulebook text over common house rules, clearly distinguish between official rules and common variants, and help resolve rule disputes with precise citations.";

    /// <summary>
    /// Seeds the complete Catan POC agent with all related entities.
    /// Requires SharedGame "Catan" to exist (seeded by SharedGameSeeder).
    /// Requires admin user to exist (seeded by AutoConfigurationService).
    /// </summary>
    public static async Task SeedAsync(
        MeepleAiDbContext db,
        ILogger logger,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("🌱 Starting Catan POC Agent seed (Issue #4667)");
        var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;

        // 1. Find admin user (required for CreatedBy FKs)
        var adminUser = await db.Users
            .FirstOrDefaultAsync(u => u.Role == "admin", cancellationToken)
            .ConfigureAwait(false);

        if (adminUser == null)
        {
            logger.LogWarning("Admin user not found. Skipping Catan POC Agent seeding.");
            return;
        }

        // 2. Find SharedGame "Catan" (seeded by SharedGameSeeder)
        var sharedGame = await db.SharedGames
            .AsNoTracking()
            .FirstOrDefaultAsync(g => EF.Functions.ILike(g.Title, CatanGameName) && !g.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        if (sharedGame == null)
        {
            logger.LogWarning("SharedGame '{GameName}' not found. Run SharedGameSeeder first.", CatanGameName);
            return;
        }

        // 3. Create or find GameEntity "Catan" (needed for StrategyPattern + AgentSession FKs)
        var gameEntity = await SeedGameEntityAsync(db, sharedGame, now, logger, cancellationToken)
            .ConfigureAwait(false);

        // 4. Seed AgentTypology "Rules Expert"
        var typology = await SeedAgentTypologyAsync(db, adminUser.Id, now, logger, cancellationToken)
            .ConfigureAwait(false);

        // 5. Seed Agent + Configuration
        var agent = await SeedAgentWithConfigurationAsync(db, adminUser.Id, now, logger, cancellationToken)
            .ConfigureAwait(false);

        // 6. Seed GameSession + AgentSession (links everything together)
        await SeedAgentSessionAsync(db, agent, gameEntity, typology, adminUser.Id, now, logger, cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation("🌱 Catan POC Agent seed completed successfully");
    }

    private static async Task<GameEntity> SeedGameEntityAsync(
        MeepleAiDbContext db,
        SharedGameEntity sharedGame,
        DateTime now,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var existing = await db.Games
            .FirstOrDefaultAsync(g => g.Name == CatanGameName, cancellationToken)
            .ConfigureAwait(false);

        if (existing != null)
        {
            logger.LogDebug("GameEntity '{GameName}' already exists (ID: {GameId})", CatanGameName, existing.Id);
            return existing;
        }

        var gameEntity = new GameEntity
        {
            Id = Guid.NewGuid(),
            Name = CatanGameName,
            Publisher = "Kosmos",
            YearPublished = 1995,
            MinPlayers = 3,
            MaxPlayers = 4,
            MinPlayTimeMinutes = 60,
            MaxPlayTimeMinutes = 120,
            BggId = 13,
            Language = "en",
            VersionType = "base",
            VersionNumber = "1.0",
            SharedGameId = sharedGame.Id,
            ImageUrl = sharedGame.ImageUrl,
            CreatedAt = now
        };

        db.Games.Add(gameEntity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("✅ Created GameEntity '{GameName}' (ID: {GameId}) linked to SharedGame",
            CatanGameName, gameEntity.Id);

        return gameEntity;
    }

    private static async Task<AgentTypologyEntity> SeedAgentTypologyAsync(
        MeepleAiDbContext db,
        Guid adminUserId,
        DateTime now,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var existing = await db.AgentTypologies
            .FirstOrDefaultAsync(t => t.Name == TypologyName && !t.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        if (existing != null)
        {
            logger.LogDebug("AgentTypology '{Name}' already exists (ID: {Id})", TypologyName, existing.Id);
            return existing;
        }

        var typologyId = Guid.NewGuid();
        var typology = new AgentTypologyEntity
        {
            Id = typologyId,
            Name = TypologyName,
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

        logger.LogInformation("✅ Created AgentTypology '{Name}' (ID: {Id}, Status: Approved)",
            TypologyName, typologyId);

        return typology;
    }

    private static async Task<AgentEntity> SeedAgentWithConfigurationAsync(
        MeepleAiDbContext db,
        Guid adminUserId,
        DateTime now,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var existing = await db.Set<AgentEntity>()
            .FirstOrDefaultAsync(a => a.Name == AgentName, cancellationToken)
            .ConfigureAwait(false);

        if (existing != null)
        {
            logger.LogDebug("Agent '{Name}' already exists (ID: {Id})", AgentName, existing.Id);
            return existing;
        }

        var agentId = Guid.NewGuid();
        var agent = new AgentEntity
        {
            Id = agentId,
            Name = AgentName,
            Type = "RAG",
            StrategyName = "SingleModel",
            StrategyParametersJson = "{}",
            IsActive = true,
            CreatedAt = now,
            LastInvokedAt = null,
            InvocationCount = 0
        };

        db.Set<AgentEntity>().Add(agent);

        var config = new AgentConfigurationEntity
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            LlmProvider = 0, // OpenRouter
            LlmModel = "anthropic/claude-3-haiku",
            AgentMode = 0, // Chat
            SelectedDocumentIdsJson = "[]",
            Temperature = 0.3m,
            MaxTokens = 2048,
            SystemPromptOverride = CatanSystemPrompt,
            IsCurrent = true,
            CreatedAt = now,
            CreatedBy = adminUserId
        };

        db.Set<AgentConfigurationEntity>().Add(config);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "✅ Created Agent '{Name}' (ID: {Id}) with AgentConfiguration (Claude 3 Haiku, Chat mode)",
            AgentName, agentId);

        return agent;
    }

    private static async Task SeedAgentSessionAsync(
        MeepleAiDbContext db,
        AgentEntity agent,
        GameEntity game,
        AgentTypologyEntity typology,
        Guid adminUserId,
        DateTime now,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        // Check if session already exists for this agent
        var existingSession = await db.AgentSessions
            .AnyAsync(s => s.AgentId == agent.Id && s.IsActive, cancellationToken)
            .ConfigureAwait(false);

        if (existingSession)
        {
            logger.LogDebug("Active AgentSession for agent '{Name}' already exists", agent.Name);
            return;
        }

        // Create GameSession first (FK requirement for AgentSession)
        var gameSession = new GameSessionEntity
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            CreatedByUserId = adminUserId,
            Status = "Setup",
            StartedAt = now,
            PlayersJson = """[{"PlayerName":"POC Test Player","PlayerOrder":1,"Color":"Red"}]"""
        };

        db.GameSessions.Add(gameSession);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Create AgentSession linking everything
        var agentSession = new AgentSessionEntity
        {
            Id = Guid.NewGuid(),
            AgentId = agent.Id,
            GameSessionId = gameSession.Id,
            UserId = adminUserId,
            GameId = game.Id,
            TypologyId = typology.Id,
            CurrentGameStateJson = """{"phase":"setup","turn":0,"players":[],"board":{}}""",
            StartedAt = now,
            EndedAt = null,
            IsActive = true
        };

        db.AgentSessions.Add(agentSession);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "✅ Created AgentSession (Agent: {AgentName}, Game: {GameName}, Typology: {TypologyName})",
            agent.Name, game.Name, typology.Name);
    }
}
