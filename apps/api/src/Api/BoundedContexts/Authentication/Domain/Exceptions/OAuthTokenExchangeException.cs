using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.Authentication.Domain.Exceptions;

/// <summary>
/// Exception thrown when OAuth token exchange fails.
/// Replaces InvalidOperationException for better HTTP status mapping (Issue #2568 pattern).
/// </summary>
internal sealed class OAuthTokenExchangeException : DomainException
{
    public string Provider { get; }

    public OAuthTokenExchangeException(string provider, string message)
        : base($"OAuth token exchange failed for provider '{provider}': {message}")
    {
        Provider = provider;
    }

    public OAuthTokenExchangeException(string provider, string message, Exception innerException)
        : base($"OAuth token exchange failed for provider '{provider}': {message}", innerException)
    {
        Provider = provider;
    }
}
