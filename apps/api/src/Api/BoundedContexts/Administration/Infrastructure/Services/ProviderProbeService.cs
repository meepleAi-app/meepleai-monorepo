using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderProbeAudit;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.Middleware.Exceptions;
using Api.Models;
using Api.Services.Providers.Probe;

namespace Api.BoundedContexts.Administration.Infrastructure.Services;

internal sealed class ProviderProbeService : IProviderProbeService
{
    private readonly IProviderProbeExecutorFactory _factory;
    private readonly IProviderProbeAuditRepository _auditRepo;
    private readonly IProviderCredentialResolver _credentialResolver;

    public ProviderProbeService(
        IProviderProbeExecutorFactory factory,
        IProviderProbeAuditRepository auditRepo,
        IProviderCredentialResolver credentialResolver)
    {
        _factory = factory;
        _auditRepo = auditRepo;
        _credentialResolver = credentialResolver;
    }

    public async Task<ProviderProbeResultDto> ProbeAsync(string providerName, Guid actorId, string? expectedModel, CancellationToken cancellationToken)
    {
        var probedAt = DateTime.UtcNow;
        var executor = _factory.GetExecutor(providerName);
        if (executor is null)
            throw new UnknownProviderException(providerName);

        // Issue #3044: providers that require auth resolve the key via IProviderCredentialResolver
        // (DB active-row → env-var fallback) instead of reading the env var directly, so a rotated
        // key (#1859) is honoured. The requiresAuth gate is mandatory: no-auth providers (e.g.
        // ollama-local, ApiKeyEnvVar==null) must NOT call the resolver, which throws ArgumentException
        // for a provider not present in ProviderEnvVarMap.
        var requiresAuth = executor.ApiKeyEnvVar is not null;

        var apiKey = string.Empty;
        if (requiresAuth)
        {
            try
            {
                apiKey = await _credentialResolver.ResolveAsync(providerName, cancellationToken).ConfigureAwait(false);
            }
            catch (ProviderCredentialNotConfiguredException)
            {
                await _auditRepo.AddAsync(ProviderProbeAuditEntry.Create(
                    providerName, actorId, null, ProbeOutcome.NotConfigured, "not_configured", 0), cancellationToken).ConfigureAwait(false);
                return new ProviderProbeResultDto(
                    ProviderName: providerName,
                    TokenConfigured: false,
                    TokenAuthenticated: false,
                    ModelAvailable: null,
                    ExpectedModel: expectedModel,
                    TokenFingerprint: null,
                    ErrorCode: "not_configured",
                    ErrorMessage: "No active credential or env-var configured for provider",
                    LatencyMs: 0,
                    ProbedAt: probedAt);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Any other resolver failure (DataProtection decrypt / rotated key-ring →
                // CryptographicException, or an unmapped provider → ArgumentException) → graceful
                // degraded result + audit, NOT a 500.
                await _auditRepo.AddAsync(ProviderProbeAuditEntry.Create(
                    providerName, actorId, null, ProbeOutcome.UnknownError, "credential_error", 0), cancellationToken).ConfigureAwait(false);
                return new ProviderProbeResultDto(
                    ProviderName: providerName,
                    TokenConfigured: false,
                    TokenAuthenticated: false,
                    ModelAvailable: null,
                    ExpectedModel: expectedModel,
                    TokenFingerprint: null,
                    ErrorCode: "credential_error",
                    ErrorMessage: "Failed to resolve provider credential",
                    LatencyMs: 0,
                    ProbedAt: probedAt);
            }
        }

        var fingerprint = TokenFingerprint.Compute(apiKey);

        var result = await executor.ExecuteAsync(apiKey, expectedModel, cancellationToken).ConfigureAwait(false);

        await _auditRepo.AddAsync(ProviderProbeAuditEntry.Create(
            providerName, actorId, fingerprint, result.Outcome, result.ErrorCode, result.LatencyMs), cancellationToken).ConfigureAwait(false);

        var authenticated = result.Outcome is ProbeOutcome.Success;

        return new ProviderProbeResultDto(
            ProviderName: providerName,
            TokenConfigured: !requiresAuth || !string.IsNullOrEmpty(apiKey),
            TokenAuthenticated: authenticated,
            ModelAvailable: result.ModelAvailable,
            ExpectedModel: expectedModel,
            TokenFingerprint: fingerprint,
            ErrorCode: result.ErrorCode,
            ErrorMessage: result.ErrorMessage,
            LatencyMs: result.LatencyMs,
            ProbedAt: probedAt);
    }
}

/// <summary>
/// Thrown when probing an unknown provider name. Maps to 404.
/// Scope: bounded-context-internal — only thrown by ProviderProbeService and caught by AdminProviderEndpoints.
/// </summary>
#pragma warning disable S3871 // Exceptions should be public — internal here is intentional (BC-private contract)
internal sealed class UnknownProviderException : Exception
#pragma warning restore S3871
{
    public string ProviderName { get; }
    public UnknownProviderException(string name) : base($"Unknown provider: {name}") => ProviderName = name;
}
