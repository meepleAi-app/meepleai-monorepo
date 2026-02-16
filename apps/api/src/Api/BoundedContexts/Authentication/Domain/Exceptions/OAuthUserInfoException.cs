using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.Authentication.Domain.Exceptions;

/// <summary>
/// Exception thrown when retrieving OAuth user information fails.
/// Replaces InvalidOperationException for better HTTP status mapping (Issue #2568 pattern).
/// </summary>
internal sealed class OAuthUserInfoException : DomainException
{
    public string Provider { get; }

    public OAuthUserInfoException(string provider, string message)
        : base($"Failed to retrieve user info from OAuth provider '{provider}': {message}")
    {
        Provider = provider;
    }

    public OAuthUserInfoException(string provider, string message, Exception innerException)
        : base($"Failed to retrieve user info from OAuth provider '{provider}': {message}", innerException)
    {
        Provider = provider;
    }
}
