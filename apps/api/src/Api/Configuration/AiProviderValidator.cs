using Microsoft.Extensions.Options;

namespace Api.Configuration;

/// <summary>
/// Startup validator for AI provider configuration (BGAI-021, Issue #963).
/// Validates configuration on application startup to fail fast.
/// </summary>
internal class AiProviderValidator : IValidateOptions<AiProviderSettings>
{
    public ValidateOptionsResult Validate(string? name, AiProviderSettings options)
    {
        var errors = new List<string>();

        // BGAI-021 (Option C): Allow missing/empty AI section for backward compatibility
        if (options.Providers == null || options.Providers.Count == 0)
        {
            return ValidateOptionsResult.Success;
        }

        ValidatePreferredProvider(options, errors);
        ValidateFallbackChain(options, errors);
        ValidateProviders(options, errors);
        ValidateCircuitBreaker(options.CircuitBreaker, errors);

        // Validation 7: Health check intervals must be positive
        // Replaced loop with LINQ to simplify (S3267)
        errors.AddRange(options.Providers
            .Where(p => p.Value.Enabled && p.Value.HealthCheckIntervalSeconds <= 0)
            .Select(p => $"Provider '{p.Key}' has invalid HealthCheckIntervalSeconds (must be positive, current: {p.Value.HealthCheckIntervalSeconds})"));

        if (errors.Count > 0) // CA1860
        {
            return ValidateOptionsResult.Fail(errors);
        }

        return ValidateOptionsResult.Success;
    }

    private static void ValidatePreferredProvider(AiProviderSettings options, List<string> errors)
    {
        // Validation 2: PreferredProvider must exist in Providers if set
        if (!string.IsNullOrEmpty(options.PreferredProvider))
        {
            // CA1854: Prefer TryGetValue
            if (!options.Providers.TryGetValue(options.PreferredProvider, out var provider))
            {
                errors.Add($"PreferredProvider '{options.PreferredProvider}' not found in AI:Providers");
            }
            else if (!provider.Enabled)
            {
                errors.Add($"PreferredProvider '{options.PreferredProvider}' is disabled (AI:Providers:{options.PreferredProvider}:Enabled = false)");
            }
        }
    }

    private static void ValidateFallbackChain(AiProviderSettings options, List<string> errors)
    {
        // Validation 3: FallbackChain providers must exist and be enabled
        if (options.FallbackChain.Count > 0) // CA1860
        {
            foreach (var providerName in options.FallbackChain)
            {
                // CA1854: Prefer TryGetValue
                if (!options.Providers.TryGetValue(providerName, out var provider))
                {
                    errors.Add($"FallbackChain provider '{providerName}' not found in AI:Providers");
                }
                else if (!provider.Enabled)
                {
                    errors.Add($"FallbackChain provider '{providerName}' is disabled (AI:Providers:{providerName}:Enabled = false)");
                }
            }

            // Validation 4: FallbackChain must not have duplicates
            var duplicates = options.FallbackChain
                .GroupBy(p => p, StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);

            if (duplicates.Any())
            {
                errors.Add($"FallbackChain contains duplicate providers: {string.Join(", ", duplicates)}");
            }
        }
    }

    private static void ValidateProviders(AiProviderSettings options, List<string> errors)
    {
        // Validation 1: At least one provider must be enabled (if providers are configured)
        if (!options.Providers.Values.Any(p => p.Enabled))
        {
            errors.Add("At least one AI provider must be enabled (AI:Providers)");
        }

        // Validation 5: Each provider must have a BaseUrl
        // Replaced loop with LINQ (S3267)
        errors.AddRange(options.Providers
            .Where(p => p.Value.Enabled && string.IsNullOrWhiteSpace(p.Value.BaseUrl))
            .Select(p => $"Provider '{p.Key}' is enabled but BaseUrl is empty (AI:Providers:{p.Key}:BaseUrl)"));
    }

    private static void ValidateCircuitBreaker(CircuitBreakerConfig circuitBreaker, List<string> errors)
    {
        // Validation 6: Circuit breaker settings must be positive
        if (circuitBreaker.FailureThreshold <= 0)
        {
            errors.Add($"Circuit breaker FailureThreshold must be positive (current: {circuitBreaker.FailureThreshold})");
        }

        if (circuitBreaker.OpenDurationSeconds <= 0)
        {
            errors.Add($"Circuit breaker OpenDurationSeconds must be positive (current: {circuitBreaker.OpenDurationSeconds})");
        }

        if (circuitBreaker.SuccessThreshold <= 0)
        {
            errors.Add($"Circuit breaker SuccessThreshold must be positive (current: {circuitBreaker.SuccessThreshold})");
        }
    }
}
