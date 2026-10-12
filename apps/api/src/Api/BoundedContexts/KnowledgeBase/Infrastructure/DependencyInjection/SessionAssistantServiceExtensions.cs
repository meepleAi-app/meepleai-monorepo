using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.DependencyInjection;

/// <summary>ADR-097: i servizi dell'assistente in partita, in un posto solo e verificabile da un test.</summary>
internal static class SessionAssistantServiceExtensions
{
    public static IServiceCollection AddSessionAssistant(this IServiceCollection services)
    {
        services.AddScoped<IAssistantChatClientFactory, AssistantChatClientFactory>();
        services.AddScoped<IAssistantPlayerMatcher, AssistantPlayerMatcher>();
        return services;
    }
}
