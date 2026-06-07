using System.Diagnostics.CodeAnalysis;

namespace Api.Middleware.Exceptions;

/// <summary>
/// Thrown by <c>RotateProviderKeyCommandHandler</c> when the pre-flight probe of the new API
/// key against the provider fails. The rotation transaction rolls back; the previous key remains
/// active. Maps to HTTP 502 Bad Gateway with subcode <c>provider_probe_failed</c>.
/// Issue #1859.
/// </summary>
public sealed class ProviderProbeFailedException : HttpException
{
    public string ProviderName { get; }

    [SetsRequiredMembers]
    public ProviderProbeFailedException(string providerName, string reason)
        : base(502, "provider_probe_failed", $"Probe failed for provider '{providerName}': {reason}")
    {
        ProviderName = providerName;
    }
}
