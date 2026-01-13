using Api.Services.LlmClients;
using MediatR;

namespace Api.Routing;

/// <summary>
/// LLM provider management endpoints.
/// Issue #2391 Sprint 2
/// </summary>
internal static class LlmEndpoints
{
    public static RouteGroupBuilder MapLlmEndpoints(this RouteGroupBuilder group)
    {
        // Get available LLM providers
        group.MapGet("/llm/providers", HandleGetProviders)
            .RequireAuthorization()
            .WithName("GetLlmProviders")
            .WithSummary("Get available LLM providers")
            .Produces<List<string>>();

        return group;
    }

    private static IResult HandleGetProviders(LlmProviderFactory factory)
    {
        var providers = factory.GetAvailableProviders().ToList();
        return Results.Ok(providers);
    }
}
