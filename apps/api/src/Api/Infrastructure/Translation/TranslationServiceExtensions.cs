using Microsoft.Extensions.DependencyInjection;

namespace Api.Infrastructure.Translation;

internal static class TranslationServiceExtensions
{
    public static IServiceCollection AddTranslationServices(this IServiceCollection services)
    {
        services.AddScoped<OpenRouterTranslationService>();
        services.AddScoped<INarrativeTranslationService>(sp =>
            sp.GetRequiredService<OpenRouterTranslationService>());
        services.AddScoped<IGenericTranslationService>(sp =>
            sp.GetRequiredService<OpenRouterTranslationService>());
        return services;
    }
}
