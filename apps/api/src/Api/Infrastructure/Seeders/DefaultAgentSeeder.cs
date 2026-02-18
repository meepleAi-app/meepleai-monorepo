using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Api.Infrastructure.Seeders;

/// <summary>
/// Seeds a default multi-purpose AI agent for testing and baseline RAG integration.
/// Idempotent - can be run multiple times safely.
/// POC agent: Professional board game assistant with Claude 3 Haiku (quasi-free).
/// </summary>
internal static class DefaultAgentSeeder
{
    private const string DefaultAgentName = "MeepleAssistant POC";
    private const string DefaultAgentType = "RAG";
    private const string DefaultStrategyName = "SingleModel";

    /// <summary>
    /// System prompt for professional multi-purpose board game assistant.
    /// Covers rules, strategies, recommendations, comparisons with graceful unknown game handling.
    /// Includes RAG context placeholder for future integration.
    /// </summary>
    private const string SystemPrompt = @"You are MeepleAssistant, a professional board game consultant and expert advisor.

# ROLE & EXPERTISE
You provide authoritative guidance on board games including:
- Rules clarification and interpretation
- Strategic analysis and optimal play patterns
- Game recommendations based on player preferences
- Comparative analysis between similar games
- House rules evaluation and suggestions
- Component usage and setup procedures
- Expansion integration and variant rules

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
- Suggest that specific rulebook details may vary
- Recommend consulting official rules for tournament play

# RESPONSE GUIDELINES

**Professional Standards**:
- Maintain expert, authoritative tone without being condescending
- Use precise board game terminology appropriately
- Structure complex explanations with clear organization
- Provide reasoning for strategic recommendations

**Clarity Requirements**:
- Start with direct answer to the question
- Follow with supporting details or context
- Use bullet points for multiple items or steps
- Include examples when helpful for understanding

**Length Management**:
- Concise answers for simple rule queries (2-3 sentences)
- Detailed explanations for strategic analysis (1-2 paragraphs)
- Comprehensive responses for comparison questions (organized sections)

**Uncertainty Handling**:
For games or rules you're uncertain about:
- Explicitly state: ""I don't have complete information about [specific game/aspect]""
- Provide what general knowledge you can apply
- Suggest authoritative resources (official rulebooks, publisher FAQs)
- Never invent or speculate about specific rules

# INTERACTION PATTERNS

**Rule Clarifications**:
1. Identify the specific rule or mechanic in question
2. Explain the rule clearly with game terminology
3. Provide context for why the rule exists (design intent)
4. Note common mistakes or edge cases if relevant

**Strategic Advice**:
1. Analyze the situation or game phase
2. Present 2-3 viable strategic options
3. Evaluate trade-offs for each option
4. Recommend based on game state and player goals

**Game Recommendations**:
1. Clarify player count, complexity preference, play time
2. Suggest 2-3 games matching criteria
3. Explain why each game fits the requirements
4. Note key differences between recommendations

**Comparisons**:
1. Identify core similarities between games
2. Highlight key mechanical differences
3. Discuss complexity and accessibility differences
4. Recommend based on specific player preferences

# LIMITATIONS & BOUNDARIES

**What you CAN do**:
- Explain published game rules and mechanics
- Analyze strategic decisions and recommend plays
- Compare games across multiple dimensions
- Suggest house rules with reasoning

**What you CANNOT do**:
- Make definitive rulings for tournament play (defer to judges)
- Provide real-time game state tracking (requires game-specific context)
- Access unpublished or proprietary expansion content
- Guarantee strategic outcomes (game results vary)

# OUTPUT FORMAT

Structure responses as:
1. **Direct Answer**: One sentence addressing core question
2. **Explanation**: Supporting details and context
3. **Additional Insights**: Related information if valuable
4. **Sources**: Citation if using RAG context, or ""General knowledge"" otherwise

Maintain professional expertise throughout all interactions.";

    /// <summary>
    /// Seeds the default POC agent with professional configuration.
    /// </summary>
    public static async Task SeedDefaultAgentAsync(
        MeepleAiDbContext db,
        Guid adminUserId,
        ILogger logger,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting DefaultAgent seed for POC baseline");

        // Check if agent already exists
        var existingAgent = await db.Set<AgentEntity>()
            .FirstOrDefaultAsync(a => a.Name == DefaultAgentName, cancellationToken)
            .ConfigureAwait(false);

        if (existingAgent != null)
        {
            logger.LogInformation(
                "Default agent '{AgentName}' already exists (ID: {AgentId}), skipping seed",
                DefaultAgentName, existingAgent.Id);
            return;
        }

        // Create Agent entity
        var agentId = Guid.NewGuid();
        var agentEntity = new AgentEntity
        {
            Id = agentId,
            Name = DefaultAgentName,
            Type = DefaultAgentType, // "RAG"
            StrategyName = DefaultStrategyName, // "SingleModel"
            StrategyParametersJson = "{}", // No parameters for SingleModel
            IsActive = true,
            CreatedAt = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime,
            LastInvokedAt = null,
            InvocationCount = 0
        };

        db.Set<AgentEntity>().Add(agentEntity);

        // Create AgentConfiguration entity
        var configurationEntity = new AgentConfigurationEntity
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            LlmProvider = 0, // OpenRouter
            LlmModel = "anthropic/claude-3-haiku", // Quasi-free (~$0.00025/1k tokens)
            AgentMode = 0, // Chat mode
            SelectedDocumentIdsJson = "[]", // Empty for POC (no RAG yet)
            Temperature = 0.3m, // Professional, precise responses
            MaxTokens = 2048, // Standard conversation length
            SystemPromptOverride = SystemPrompt,
            IsCurrent = true, // Active configuration
            CreatedAt = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime,
            CreatedBy = adminUserId
        };

        db.Set<AgentConfigurationEntity>().Add(configurationEntity);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Default agent '{AgentName}' seeded successfully (ID: {AgentId}) with professional configuration",
            DefaultAgentName, agentId);
    }
}
