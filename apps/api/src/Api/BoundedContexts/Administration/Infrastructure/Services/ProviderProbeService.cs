using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderProbeAudit;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.Models;
using Api.Services.Providers.Probe;

namespace Api.BoundedContexts.Administration.Infrastructure.Services;

internal sealed class ProviderProbeService : IProviderProbeService
{
    private readonly ProviderProbeExecutorFactory _factory;
    private readonly IProviderProbeAuditRepository _auditRepo;

    public ProviderProbeService(ProviderProbeExecutorFactory factory, IProviderProbeAuditRepository auditRepo)
    {
        _factory = factory;
        _auditRepo = auditRepo;
    }

    public async Task<ProviderProbeResultDto> ProbeAsync(string providerName, Guid actorId, string? expectedModel, CancellationToken cancellationToken)
    {
        var probedAt = DateTime.UtcNow;
        var executor = _factory.GetExecutor(providerName);
        if (executor is null)
            throw new UnknownProviderException(providerName);

        var apiKey = executor.ApiKeyEnvVar is { } envVar
            ? Environment.GetEnvironmentVariable(envVar) ?? string.Empty
            : string.Empty;
        var fingerprint = TokenFingerprint.Compute(apiKey);
        var requiresAuth = executor.ApiKeyEnvVar is not null;

        if (requiresAuth && string.IsNullOrEmpty(apiKey))
        {
            await _auditRepo.AddAsync(ProviderProbeAuditEntry.Create(
                providerName, actorId, fingerprint, ProbeOutcome.NotConfigured, "not_configured", 0), cancellationToken).ConfigureAwait(false);
            return new ProviderProbeResultDto(
                ProviderName: providerName,
                TokenConfigured: false,
                TokenAuthenticated: false,
                ModelAvailable: null,
                ExpectedModel: expectedModel,
                TokenFingerprint: null,
                ErrorCode: "not_configured",
                ErrorMessage: "API key environment variable not set",
                LatencyMs: 0,
                ProbedAt: probedAt);
        }

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

/// <summary>Thrown when probing an unknown provider name. Maps to 404.</summary>
public sealed class UnknownProviderException : Exception
{
    public string ProviderName { get; }
    public UnknownProviderException(string name) : base($"Unknown provider: {name}") => ProviderName = name;
}
