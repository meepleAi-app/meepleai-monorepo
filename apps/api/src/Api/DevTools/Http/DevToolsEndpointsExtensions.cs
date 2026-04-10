using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Api.DevTools.Http;

internal static class DevToolsEndpointsExtensions
{
    public static IEndpointRouteBuilder MapMeepleDevTools(this IEndpointRouteBuilder app)
    {
        var env = app.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
        if (!env.IsDevelopment())
        {
            return app;
        }

        var group = app.MapGroup("/dev/toggles");
        group.MapGet("/", DevToolsEndpoints.GetToggles).WithName("DevToolsGetToggles");
        group.MapPatch("/", DevToolsEndpoints.PatchToggles).WithName("DevToolsPatchToggles");
        group.MapPost("/reset", DevToolsEndpoints.ResetToggles).WithName("DevToolsResetToggles");

        return app;
    }
}
