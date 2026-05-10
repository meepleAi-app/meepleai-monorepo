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

    private static readonly Dictionary<string, (string EnvVar, string DefaultModel)> ProviderConfig = new(StringComparer.OrdinalIgnoreCase)
    {
        ["openrouter"] = ("OPENROUTER_API_KEY", "google/gemma-2-9b-it:free"),
        ["openai"]     = ("OPENAI_API_KEY",     "gpt-4o-mini"),
        ["deepseek"]   = ("DEEPSEEK_API_KEY",   "deepseek-chat"),
        ["ollama"]     = ("OLLAMA_API_KEY",     "llama3.2"),     // env unused, kept for symmetry
    };

    public ProviderProbeService(ProviderProbeExecutorFactory factory, IProviderProbeAuditRepository auditRepo)
    {
        _factory = factory;
        _auditRepo = auditRepo;
    }

    public async Task<ProviderProbeResultDto> ProbeAsync(string providerName, Guid actorId, CancellationToken cancellationToken)
    {
        var probedAt = DateTime.UtcNow;
        var executor = _factory.GetExecutor(providerName);
        if (executor is null || !ProviderConfig.TryGetValue(providerName, out var cfg))
            throw new UnknownProviderException(providerName);

        var apiKey = Environment.GetEnvironmentVariable(cfg.EnvVar) ?? string.Empty;
        var fingerprint = TokenFingerprint.Compute(apiKey);

        // Ollama is local — doesn't require API key
        var requiresKey = !string.Equals(providerName, "ollama", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(apiKey) && requiresKey)
        {
            await _auditRepo.AddAsync(ProviderProbeAuditEntry.Create(
                providerName, actorId, fingerprint, ProbeOutcome.NotConfigured, "not_configured", 0), cancellationToken).ConfigureAwait(false);
            return new ProviderProbeResultDto(
                ProviderName: providerName,
                TokenConfigured: false,
                TokenAuthenticated: false,
                ModelAvailable: false,
                TokenFingerprint: null,
                ErrorCode: "not_configured",
                ErrorMessage: "API key environment variable not set",
                LatencyMs: 0,
                ProbedAt: probedAt);
        }

        var result = await executor.ExecuteAsync(apiKey, cfg.DefaultModel, cancellationToken).ConfigureAwait(false);

        await _auditRepo.AddAsync(ProviderProbeAuditEntry.Create(
            providerName, actorId, fingerprint, result.Outcome, result.ErrorCode, result.LatencyMs), cancellationToken).ConfigureAwait(false);

        var authenticated = result.Outcome is ProbeOutcome.Success or ProbeOutcome.ModelMissing;

        return new ProviderProbeResultDto(
            ProviderName: providerName,
            TokenConfigured: !string.IsNullOrEmpty(apiKey) || !requiresKey,
            TokenAuthenticated: authenticated,
            ModelAvailable: result.ModelAvailable,
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
