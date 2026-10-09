using Azure.Core;

namespace Api.Infrastructure.AzureSdk;

/// <summary>
/// Credenziale fissa per il Key Vault emulator (https, bearer ignorato). NON per App Configuration: lì il
/// bearer su http è rifiutato dall'SDK prima di ogni richiesta, e la via è la connection string HMAC.
/// Mai usata nel profilo Azure vero.
/// </summary>
internal sealed class StaticTokenCredential : TokenCredential
{
    private readonly string _token;

    public StaticTokenCredential(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        _token = token;
    }

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
        new(_token, DateTimeOffset.MaxValue);

    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
        new(GetToken(requestContext, cancellationToken));
}
