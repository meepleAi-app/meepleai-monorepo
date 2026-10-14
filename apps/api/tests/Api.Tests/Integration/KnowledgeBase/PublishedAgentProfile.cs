using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// Pubblica una versione nuova del profilo dell'agente, come farà l'admin: una bozza ricavata da
/// quella pubblicata, poi la pubblicazione.
/// </summary>
internal static class PublishedAgentProfile
{
    public static async Task PublishAsync(
        IServiceProvider services, Func<AgentProfileContent, AgentProfileContent> change, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var profile = await scope.ServiceProvider.GetRequiredService<IAgentProfileRepository>().GetAsync(ct);
        profile.Should().NotBeNull("le migrazioni pubblicano la prima versione del profilo");
        profile!.CreateDraft(change(profile.Published.Content));
        profile.PublishDraft(new HashSet<string>(StringComparer.Ordinal));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Lo stesso contenuto, con altri pesi della fusione.</summary>
    public static AgentProfileContent WithFusionWeights(AgentProfileContent c, decimal vectorWeight, decimal keywordWeight) =>
        AgentProfileContent.Create(
            c.Name,
            c.Description,
            c.Persona,
            c.PrimaryModelId,
            c.FallbackModelId,
            c.Temperature,
            c.MaxResponseTokens,
            c.CitationStyle,
            c.SystemPrompts,
            c.FallbackLanguage,
            c.CandidatePoolSize,
            c.FinalTopK,
            c.MinScore,
            vectorWeight,
            keywordWeight,
            c.RerankerEnabled,
            c.AllowedCategories.ToList(),
            c.SessionAssistant);
}
