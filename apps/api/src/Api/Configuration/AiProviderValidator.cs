using Microsoft.Extensions.Options;

namespace Api.Configuration;

/// <summary>
/// Startup validator for AI provider configuration (BGAI-021, Issue #963).
/// Validates configuration on application startup to fail fast.
/// </summary>
public class AiProviderValidator : IValidateOptions<AiProviderSettings>
{
    public ValidateOptionsResult Validate(string? name, AiProviderSettings options)
    {
        var errors = new List<string>();

        // Validation 1: At least one provider must be enabled
        if (!options.Providers.Any(p => p.Value.Enabled))
        {
            errors.Add("At least one AI provider must be enabled (AI:Providers)");
        }

        // Validation 2: PreferredProvider must exist in Providers if set
        if (!string.IsNullOrEmpty(options.PreferredProvider))
        {
            if (!options.Providers.ContainsKey(options.PreferredProvider))
            {
                errors.Add($"PreferredProvider '{options.PreferredProvider}' not found in AI:Providers");
            }
            else if (!options.Providers[options.PreferredProvider].Enabled)
            {
                errors.Add($"PreferredProvider '{options.PreferredProvider}' is disabled (AI:Providers:{options.PreferredProvider}:Enabled = false)");
            }
        }

        // Validation 3: FallbackChain providers must exist and be enabled
        if (options.FallbackChain.Any())
        {
            foreach (var provider in options.FallbackChain)
            {
                if (!options.Providers.ContainsKey(provider))
                {
                    errors.Add($"FallbackChain provider '{provider}' not found in AI:Providers");
                }
                else if (!options.Providers[provider].Enabled)
                {
                    errors.Add($"FallbackChain provider '{provider}' is disabled (AI:Providers:{provider}:Enabled = false)");
                }
            }

            // Validation 4: FallbackChain must not have duplicates
            var duplicates = options.FallbackChain
                .GroupBy(p => p)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);

            if (duplicates.Any())
            {
                errors.Add($"FallbackChain contains duplicate providers: {string.Join(", ", duplicates)}");
            }
        }

        // Validation 5: Each provider must have a BaseUrl
        foreach (var provider in options.Providers.Where(p => p.Value.Enabled))
        {
            if (string.IsNullOrWhiteSpace(provider.Value.BaseUrl))
            {
                errors.Add($"Provider '{provider.Key}' is enabled but BaseUrl is empty (AI:Providers:{provider.Key}:BaseUrl)");
            }
        }

        // Validation 6: Circuit breaker settings must be positive
        if (options.CircuitBreaker.FailureThreshold <= 0)
        {
            errors.Add($"Circuit breaker FailureThreshold must be positive (current: {options.CircuitBreaker.FailureThreshold})");
        }

        if (options.CircuitBreaker.OpenDurationSeconds <= 0)
        {
            errors.Add($"Circuit breaker OpenDurationSeconds must be positive (current: {options.CircuitBreaker.OpenDurationSeconds})");
        }

        if (options.CircuitBreaker.SuccessThreshold <= 0)
        {
            errors.Add($"Circuit breaker SuccessThreshold must be positive (current: {options.CircuitBreaker.SuccessThreshold})");
        }

        // Validation 7: Health check intervals must be positive
        foreach (var provider in options.Providers.Where(p => p.Value.Enabled))
        {
            if (provider.Value.HealthCheckIntervalSeconds <= 0)
            {
                errors.Add($"Provider '{provider.Key}' has invalid HealthCheckIntervalSeconds (must be positive, current: {provider.Value.HealthCheckIntervalSeconds})");
            }
        }

        // Return validation result
        if (errors.Any())
        {
            return ValidateOptionsResult.Fail(errors);
        }

        return ValidateOptionsResult.Success;
    }
}
