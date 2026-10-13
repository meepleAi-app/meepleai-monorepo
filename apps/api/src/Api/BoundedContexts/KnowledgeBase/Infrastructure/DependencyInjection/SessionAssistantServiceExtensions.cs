using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Confirmation;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Evaluation;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Turn;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.DependencyInjection;

/// <summary>ADR-097: i servizi dell'assistente in partita, in un posto solo e verificabile da un test.</summary>
internal static class SessionAssistantServiceExtensions
{
    public static IServiceCollection AddSessionAssistant(this IServiceCollection services)
    {
        services.AddScoped<IAssistantChatClientFactory, AssistantChatClientFactory>();
        services.AddScoped<IAssistantPlayerMatcher, AssistantPlayerMatcher>();
        services.AddScoped<SessionAssistantEvaluator>();
        services.AddScoped<AssistantTurnRunner>();
        // Singleton: non ha stato e apre uno scope per ogni scrittura.
        services.AddSingleton<IAssistantProposalApplier, LiveSessionProposalApplier>();
        services.AddScoped<IAssistantSessionDirectory, LiveSessionAssistantDirectory>();
        services.AddScoped<IToolCallingModelCatalog, CatalogToolCallingModels>();
        return services;
    }
}
