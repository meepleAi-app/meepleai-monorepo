using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Infrastructure.HealthChecks;

/// <summary>
/// Health check that validates all critical application configuration is present and valid.
/// Returns detailed information about missing or invalid configuration values.
/// </summary>
internal class ConfigurationHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfigurationHealthCheck> _logger;
    private readonly IWebHostEnvironment _environment;

    public ConfigurationHealthCheck(
        IConfiguration configuration,
        ILogger<ConfigurationHealthCheck> logger,
        IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _logger = logger;
        _environment = environment;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var data = new Dictionary<string, object>(StringComparer.Ordinal);

        // Core Infrastructure
        ValidateDatabaseConfiguration(errors, warnings, data);
        ValidateRedisConfiguration(errors, warnings, data);

        // AI Services
        ValidateEmbeddingConfiguration(errors, warnings, data);
        ValidateAiProviderConfiguration(errors, warnings, data);

        // External Services (Development vs Production requirements differ)
        ValidateExternalServicesConfiguration(errors, warnings, data);

        // Secrets validation
        ValidateSecretsConfiguration(errors, warnings, data);

        // OAuth provider validation
        ValidateOAuthConfiguration(errors, warnings, data);

        // Environment info
        data["environment"] = _environment.EnvironmentName;
        data["configurationSources"] = GetConfigurationSources();

        if (errors.Count > 0)
        {
            var errorMessage = $"Configuration validation failed: {string.Join("; ", errors)}";
            _logger.LogError("Configuration health check failed with {ErrorCount} errors: {Errors}",
                errors.Count, string.Join(", ", errors));

            data["errors"] = errors;
            data["warnings"] = warnings;

            return Task.FromResult(HealthCheckResult.Unhealthy(errorMessage, data: data));
        }

        if (warnings.Count > 0)
        {
            var warningMessage = $"Configuration has {warnings.Count} warnings: {string.Join("; ", warnings)}";
            _logger.LogWarning("Configuration health check has warnings: {Warnings}",
                string.Join(", ", warnings));

            data["warnings"] = warnings;

            return Task.FromResult(HealthCheckResult.Degraded(warningMessage, data: data));
        }

        data["message"] = "All configuration validated successfully";
        return Task.FromResult(HealthCheckResult.Healthy("Configuration is valid", data: data));
    }

    private void ValidateDatabaseConfiguration(List<string> errors, List<string> warnings, Dictionary<string, object> data)
    {
        // Check for connection string via multiple sources
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? _configuration["ConnectionStrings__Postgres"]
            ?? _configuration.GetConnectionString("Postgres");

        // Issue #2460: Check POSTGRES_* environment variables pattern (used by SecretsHelper)
        var postgresHost = _configuration["POSTGRES_HOST"];
        var postgresUser = _configuration["POSTGRES_USER"];
        var postgresDb = _configuration["POSTGRES_DB"];
        var postgresPassword = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD")
            ?? _configuration["POSTGRES_PASSWORD"];

        var hasConnectionString = !string.IsNullOrEmpty(connectionString);

        // Issue #2460: Recognize POSTGRES_* vars as valid when password is present
        // SecretsHelper.BuildPostgresConnectionString() constructs connection string from these vars
        var hasPostgresVars = !string.IsNullOrEmpty(postgresPassword) &&
                              (!string.IsNullOrEmpty(postgresHost) || !string.IsNullOrEmpty(postgresUser) || !string.IsNullOrEmpty(postgresDb));

        if (!hasConnectionString && !hasPostgresVars)
        {
            if (_environment.IsDevelopment())
            {
                warnings.Add("Database connection not configured (acceptable in development)");
            }
            else
            {
                errors.Add("Database connection string not configured");
            }
        }

        data["database_configured"] = hasConnectionString || hasPostgresVars;
        data["database_source"] = hasConnectionString ? "connection_string" : (hasPostgresVars ? "postgres_vars" : "none");
    }

    private void ValidateRedisConfiguration(List<string> errors, List<string> warnings, Dictionary<string, object> data)
    {
        var redisUrl = _configuration["REDIS_URL"];
        var redisPasswordFile = Environment.GetEnvironmentVariable("REDIS_PASSWORD_FILE");

        if (string.IsNullOrEmpty(redisUrl))
        {
            if (_environment.IsProduction())
            {
                errors.Add("Redis URL not configured (required in production)");
            }
            else
            {
                warnings.Add("Redis URL not configured, using default localhost:6379");
            }
            data["redis_configured"] = false;
        }
        else
        {
            data["redis_configured"] = true;
            data["redis_has_auth"] = !string.IsNullOrEmpty(redisPasswordFile) ||
                                     redisUrl.Contains("password=", StringComparison.OrdinalIgnoreCase);
        }
    }


    private void ValidateEmbeddingConfiguration(List<string> errors, List<string> warnings, Dictionary<string, object> data)
    {
        var embeddingProvider = _configuration["Embedding:Provider"] ?? "local";
        var localEmbeddingUrl = _configuration["LOCAL_EMBEDDING_URL"];
        var embeddingModel = _configuration["Embedding:Model"];
        var embeddingDimensions = _configuration["Embedding:Dimensions"];

        data["embedding_provider"] = embeddingProvider;

        if (embeddingProvider.Equals("local", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(localEmbeddingUrl))
            {
                if (_environment.IsProduction())
                {
                    errors.Add("Local embedding service URL not configured (required when provider=local)");
                }
                else
                {
                    warnings.Add("Local embedding URL not configured, using default");
                }
                data["embedding_service_configured"] = false;
            }
            else
            {
                data["embedding_service_configured"] = true;
                data["embedding_url"] = MaskSensitiveUrl(localEmbeddingUrl);
            }
        }

        if (!string.IsNullOrEmpty(embeddingModel))
        {
            data["embedding_model"] = embeddingModel;
        }

        if (!string.IsNullOrEmpty(embeddingDimensions) && int.TryParse(embeddingDimensions, System.Globalization.CultureInfo.InvariantCulture, out var dims))
        {
            data["embedding_dimensions"] = dims;
        }
    }

    private void ValidateAiProviderConfiguration(List<string> errors, List<string> warnings, Dictionary<string, object> data)
    {
        var enabledProviders = new List<string>();
        var configuredProviders = new List<string>();

        // Check common AI providers
        var providers = new[] { "OpenRouter", "Ollama", "Anthropic", "OpenAI" };
        foreach (var provider in providers)
        {
            var isEnabled = _configuration.GetValue<bool>($"AiProviders:{provider}:Enabled");
            var hasBaseUrl = !string.IsNullOrEmpty(_configuration[$"AiProviders:{provider}:BaseUrl"]);
            var hasApiKey = !string.IsNullOrEmpty(_configuration[$"AiProviders:{provider}:ApiKey"]) ||
                           !string.IsNullOrEmpty(Environment.GetEnvironmentVariable($"{provider.ToUpperInvariant()}_API_KEY"));

            if (isEnabled)
            {
                enabledProviders.Add(provider);
                if (hasBaseUrl || hasApiKey)
                {
                    configuredProviders.Add(provider);
                }
            }
        }

        data["ai_providers_enabled"] = enabledProviders;
        data["ai_providers_configured"] = configuredProviders;

        var preferredProvider = _configuration["AiProviders:PreferredProvider"];
        if (!string.IsNullOrEmpty(preferredProvider))
        {
            data["ai_preferred_provider"] = preferredProvider;

            if (!enabledProviders.Contains(preferredProvider, StringComparer.OrdinalIgnoreCase))
            {
                warnings.Add($"Preferred AI provider '{preferredProvider}' is not enabled");
            }
        }

        if (enabledProviders.Count == 0 && _environment.IsProduction())
        {
            warnings.Add("No AI providers enabled");
        }
    }

    private void ValidateExternalServicesConfiguration(List<string> errors, List<string> warnings, Dictionary<string, object> data)
    {
        // n8n workflow integration
        var n8nUrl = _configuration["N8N_URL"];
        data["n8n_configured"] = !string.IsNullOrEmpty(n8nUrl);

        // Unstructured PDF service
        var unstructuredUrl = _configuration["PdfProcessing:UnstructuredApiUrl"] ??
                             _configuration["UNSTRUCTURED_API_URL"];
        data["unstructured_configured"] = !string.IsNullOrEmpty(unstructuredUrl);

        // SmolDocling service
        var smoldoclingUrl = _configuration["PdfProcessing:SmolDoclingApiUrl"] ??
                            _configuration["SMOLDOCLING_API_URL"];
        data["smoldocling_configured"] = !string.IsNullOrEmpty(smoldoclingUrl);

        // Reranker service
        var rerankerUrl = _configuration["RERANKER_URL"];
        data["reranker_configured"] = !string.IsNullOrEmpty(rerankerUrl);

        // BoardGameGeek API
        var bggApiKey = _configuration["BGG_API_KEY"] ??
                       Environment.GetEnvironmentVariable("BGG_API_KEY");
        data["bgg_api_configured"] = !string.IsNullOrEmpty(bggApiKey);
    }

    private void ValidateSecretsConfiguration(List<string> errors, List<string> warnings, Dictionary<string, object> data)
    {
        var secretsFound = new List<string>();
        var secretsMissing = new List<string>();

        // Check for critical secrets
        var requiredSecrets = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["postgres-password"] = new[] { "/run/secrets/postgres-password", "POSTGRES_PASSWORD" },
            ["redis-password"] = new[] { "/run/secrets/redis-password", "REDIS_PASSWORD" },
            ["jwt-secret"] = new[] { "/run/secrets/jwt-secret", "JWT_SECRET_KEY", "JWT_SECRET", "Jwt:Secret" }
        };

        foreach (var (secretName, sources) in requiredSecrets)
        {
            var found = false;
            foreach (var source in sources)
            {
                if (source.StartsWith("/run/secrets/", StringComparison.Ordinal))
                {
                    if (File.Exists(source))
                    {
                        found = true;
                        break;
                    }
                }
                else
                {
                    var value = Environment.GetEnvironmentVariable(source) ?? _configuration[source];
                    if (!string.IsNullOrEmpty(value))
                    {
                        found = true;
                        break;
                    }
                }
            }

            if (found)
            {
                secretsFound.Add(secretName);
            }
            else
            {
                secretsMissing.Add(secretName);
            }
        }

        data["secrets_found"] = secretsFound;
        data["secrets_missing"] = secretsMissing;

        if (secretsMissing.Count > 0 && _environment.IsProduction())
        {
            errors.Add($"Missing required secrets: {string.Join(", ", secretsMissing)}");
        }
        else if (secretsMissing.Count > 0)
        {
            warnings.Add($"Missing secrets (may use defaults): {string.Join(", ", secretsMissing)}");
        }
    }

    private void ValidateOAuthConfiguration(List<string> errors, List<string> warnings, Dictionary<string, object> data)
    {
        var oauthProviders = new[] { "Google", "Discord", "GitHub" };
        var configuredProviders = new List<string>();
        var misconfiguredProviders = new List<string>();
        var placeholderDetected = new List<string>();

        foreach (var provider in oauthProviders)
        {
            // Check environment variables FIRST (Docker secrets loaded by load-secrets-env.sh)
            // Then fall back to appsettings.json configuration
            var clientId = Environment.GetEnvironmentVariable($"{provider.ToUpperInvariant()}_OAUTH_CLIENT_ID")
                ?? _configuration[$"Authentication:OAuth:Providers:{provider}:ClientId"];

            var clientSecret = Environment.GetEnvironmentVariable($"{provider.ToUpperInvariant()}_OAUTH_CLIENT_SECRET")
                ?? _configuration[$"Authentication:OAuth:Providers:{provider}:ClientSecret"];

            // Check if configured
            var hasClientId = !string.IsNullOrWhiteSpace(clientId);
            var hasClientSecret = !string.IsNullOrWhiteSpace(clientSecret);

            if (hasClientId && hasClientSecret)
            {
                // Check for placeholder values
                var isPlaceholder = (clientId?.StartsWith("${", StringComparison.Ordinal) ?? false) ||
                                   (clientId?.Contains("your-", StringComparison.OrdinalIgnoreCase) ?? false) ||
                                   (clientId?.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase) ?? false) ||
                                   (clientSecret?.StartsWith("${", StringComparison.Ordinal) ?? false) ||
                                   (clientSecret?.Contains("your-", StringComparison.OrdinalIgnoreCase) ?? false) ||
                                   (clientSecret?.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase) ?? false);

                if (isPlaceholder)
                {
                    placeholderDetected.Add(provider);
                    warnings.Add($"OAuth {provider}: Placeholder credentials detected (update with real values)");
                }
                else
                {
                    // Basic validation: client IDs should have reasonable length
                    var validClientId = clientId!.Length >= 10 && !clientId.Contains(' ');
                    var validClientSecret = clientSecret!.Length >= 10 && !clientSecret.Contains(' ');

                    if (validClientId && validClientSecret)
                    {
                        configuredProviders.Add(provider);
                    }
                    else
                    {
                        misconfiguredProviders.Add(provider);
                        warnings.Add($"OAuth {provider}: Invalid credentials format (check for whitespace or too short)");
                    }
                }
            }
            else if (hasClientId || hasClientSecret)
            {
                // Partial configuration (missing one of client ID or secret)
                misconfiguredProviders.Add(provider);
                warnings.Add($"OAuth {provider}: Incomplete configuration (missing {(hasClientId ? "client secret" : "client ID")})");
            }
        }

        data["oauth_configured_providers"] = configuredProviders;
        data["oauth_misconfigured_providers"] = misconfiguredProviders;
        data["oauth_placeholder_providers"] = placeholderDetected;

        // Optional: warn if no OAuth providers are configured in production
        if (configuredProviders.Count == 0 && _environment.IsProduction())
        {
            warnings.Add("No OAuth providers configured (users won't be able to login with social accounts)");
        }

        // Add provider-specific details (masked)
        foreach (var provider in configuredProviders)
        {
            var clientId = Environment.GetEnvironmentVariable($"{provider.ToUpperInvariant()}_OAUTH_CLIENT_ID")
                ?? _configuration[$"Authentication:OAuth:Providers:{provider}:ClientId"];

            if (!string.IsNullOrEmpty(clientId))
            {
                // Mask client ID (show only first 4 and last 4 characters)
                var maskedId = clientId.Length > 8
                    ? $"{clientId[..4]}...{clientId[^4..]}"
                    : "***";
                data[$"oauth_{provider.ToLowerInvariant()}_client_id"] = maskedId;
            }
        }
    }

    private List<string> GetConfigurationSources()
    {
        var sources = new List<string>();

        // Check for common configuration sources
        if (File.Exists("appsettings.json"))
            sources.Add("appsettings.json");

        var envSpecificFile = $"appsettings.{_environment.EnvironmentName}.json";
        if (File.Exists(envSpecificFile))
            sources.Add(envSpecificFile);

        if (Directory.Exists("/run/secrets"))
            sources.Add("docker-secrets");

        // Environment variables are always a source
        sources.Add("environment-variables");

        return sources;
    }

    private static string MaskSensitiveUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return url;

        try
        {
            var uri = new Uri(url);
            // Mask password in URL if present
            if (!string.IsNullOrEmpty(uri.UserInfo) && uri.UserInfo.Contains(':'))
            {
                var parts = uri.UserInfo.Split(':');
                return url.Replace(parts[1], "***");
            }
            return $"{uri.Scheme}://{uri.Host}:{uri.Port}";
        }
        catch
        {
            return "[invalid-url]";
        }
    }
}
