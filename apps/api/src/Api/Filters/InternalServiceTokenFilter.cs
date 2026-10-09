using System.Security.Cryptography;
using System.Text;

namespace Api.Filters;

/// <summary>
/// Autenticazione servizio-a-servizio per il gruppo <c>/api/v1/internal</c> (spec azure-local §4.3): un token
/// condiviso in <c>INTERNAL_SERVICE_TOKEN</c>, confrontato a tempo costante. Senza token configurato il gruppo NON
/// esiste (404 per tutti): un deployment che dimentica la variabile non espone un endpoint aperto (Review Focus 4).
/// </summary>
internal sealed class InternalServiceTokenFilter : IEndpointFilter
{
    public const string HeaderName = "X-Internal-Service-Token";
    public const string ConfigKey = "INTERNAL_SERVICE_TOKEN";

    private readonly IConfiguration _configuration;

    public InternalServiceTokenFilter(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var expected = _configuration[ConfigKey];
        if (string.IsNullOrWhiteSpace(expected))
        {
            return Results.NotFound();
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var provided) || string.IsNullOrEmpty(provided))
        {
            return Results.Unauthorized();
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(provided.ToString());
        if (expectedBytes.Length != providedBytes.Length || !CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        return await next(context).ConfigureAwait(false);
    }
}
