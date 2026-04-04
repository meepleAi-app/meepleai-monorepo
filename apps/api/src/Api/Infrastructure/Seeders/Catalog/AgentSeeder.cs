using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders.Catalog;

/// <summary>
/// Seeds a system-level AgentDefinition ("Rules Expert") used as the default agent for games.
/// </summary>
internal static class AgentSeeder
{
    private const string SystemDefinitionName = "Rules Expert";

    public static async Task SeedAsync(
        MeepleAiDbContext db,
        SeedManifest manifest,
        Dictionary<int, Guid> gameMap,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var existing = await db.AgentDefinitions
            .FirstOrDefaultAsync(d => d.Name == SystemDefinitionName, cancellationToken)
            .ConfigureAwait(false);

        if (existing != null)
        {
            logger.LogDebug("AgentSeeder: system definition '{Name}' already exists", SystemDefinitionName);
            return;
        }

        var definition = AgentDefinition.CreateSystem(
            name: SystemDefinitionName,
            description: "Specialized agent for board game rules interpretation and clarification.",
            type: AgentType.Custom("RAG", "Rules retrieval-augmented generation"),
            config: AgentDefinitionConfig.Create(
                model: "claude-haiku-4-5-20251001",
                maxTokens: 500,
                temperature: 0.3f),
            typologySlug: "rules-expert",
            strategy: AgentStrategy.HybridSearch());

        logger.LogInformation("AgentSeeder: seeding system AgentDefinition '{Name}'", SystemDefinitionName);
        db.AgentDefinitions.Add(definition);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
