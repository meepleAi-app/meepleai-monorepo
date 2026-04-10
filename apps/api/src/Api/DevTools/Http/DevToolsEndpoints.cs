using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;

namespace Api.DevTools.Http;

internal static class DevToolsEndpoints
{
    public static Ok<GetTogglesResponse> GetToggles(IMockToggleReader reader)
    {
        var toggles = reader.GetAll();
        var known = KnownMockServices.All;
        return TypedResults.Ok(new GetTogglesResponse
        {
            Toggles = toggles,
            KnownServices = known,
        });
    }

    public static Results<Ok<PatchTogglesResponse>, BadRequest<DevToolsErrorResponse>> PatchToggles(
        PatchTogglesRequest request,
        IMockToggleReader reader,
        IMockToggleWriter writer,
        ILogger<DevToolsRoot> logger)
    {
        if (request.Toggles.Count == 0)
        {
            return TypedResults.BadRequest(new DevToolsErrorResponse
            {
                Error = "empty-request",
                Message = "PATCH /dev/toggles requires at least one toggle in the body",
            });
        }

        var unknownKeys = request.Toggles.Keys
            .Where(k => !KnownMockServices.All.Contains(k, System.StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (unknownKeys.Count > 0)
        {
            return TypedResults.BadRequest(new DevToolsErrorResponse
            {
                Error = "unknown-service",
                Message = $"Unknown mock service(s): {string.Join(", ", unknownKeys)}. Known: {string.Join(", ", KnownMockServices.All)}",
                UnknownKeys = unknownKeys,
            });
        }

        var updated = new List<string>();
        foreach (var kv in request.Toggles)
        {
            writer.Set(kv.Key, kv.Value);
            updated.Add(kv.Key);
            logger.LogInformation("DevTools: toggle '{Service}' set to {Value} (via PATCH /dev/toggles)", kv.Key, kv.Value);
        }

        return TypedResults.Ok(new PatchTogglesResponse
        {
            Updated = updated,
            Toggles = reader.GetAll(),
        });
    }

    public static Ok<GetTogglesResponse> ResetToggles(
        IMockToggleReader reader,
        MockToggleStateProvider provider,
        ILogger<DevToolsRoot> logger)
    {
        provider.ResetToDefaults();
        logger.LogInformation("DevTools: all toggles reset to env defaults");
        return TypedResults.Ok(new GetTogglesResponse
        {
            Toggles = reader.GetAll(),
            KnownServices = KnownMockServices.All,
        });
    }

    // Marker type used as generic parameter for ILogger<T> — intentionally empty.
#pragma warning disable S2094 // Classes should not be empty
    internal sealed class DevToolsRoot { }
#pragma warning restore S2094
}
