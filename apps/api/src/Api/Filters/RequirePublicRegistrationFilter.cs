using Api.Services;

namespace Api.Filters;

/// <summary>
/// Endpoint filter that blocks registration when public registration is disabled.
/// Fail-closed: if configuration service is unreachable, registration is blocked.
/// </summary>
internal class RequirePublicRegistrationFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var configService = context.HttpContext.RequestServices
            .GetRequiredService<IConfigurationService>();

        // Fail closed: if config unreachable or missing, block registration
        bool publicEnabled;
        try
        {
            publicEnabled = await configService.GetValueAsync<bool?>(
                "Registration:PublicEnabled", false).ConfigureAwait(false) ?? false;
        }
        catch
        {
            publicEnabled = false; // Fail closed
        }

        if (!publicEnabled)
        {
            return Results.Json(
                new { message = "Registration is currently unavailable." },
                statusCode: 403);
        }

        return await next(context).ConfigureAwait(false);
    }
}
