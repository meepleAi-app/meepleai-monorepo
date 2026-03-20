using Api.Configuration;
using Microsoft.Extensions.Options;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.Configuration;

/// <summary>
/// Tests for AI provider configuration validation (BGAI-021, Issue #963).
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class AiProviderSettingsTests
{
    private readonly AiProviderValidator _validator = new();

    [Fact]
    public void Validate_ValidConfiguration_ReturnsSuccess()
    {
        // Arrange
        var settings = new AiProviderSettings
        {
            PreferredProvider = "Ollama",
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["Ollama"] = new() { Enabled = true, BaseUrl = "http://localhost:11434", Models = ["llama3:8b"] },
                ["OpenRouter"] = new() { Enabled = true, BaseUrl = "https://openrouter.ai/api/v1", Models = ["gpt-4"] }
            },
            FallbackChain = ["Ollama", "OpenRouter"],
            CircuitBreaker = new CircuitBreakerConfig
            {
                FailureThreshold = 5,
                OpenDurationSeconds = 30,
                SuccessThreshold = 2
            }
        };

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue(string.Join(", ", result.Failures ?? []));
    }

    [Fact]
    public void Validate_NoProvidersEnabled_ReturnsFailure()
    {
        // Arrange
        var settings = new AiProviderSettings
        {
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["Ollama"] = new() { Enabled = false, BaseUrl = "http://localhost:11434" },
                ["OpenRouter"] = new() { Enabled = false, BaseUrl = "https://openrouter.ai/api/v1" }
            }
        };

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failures!.Should().Contain(f => f.Contains("At least one AI provider must be enabled"));
    }

    [Fact]
    public void Validate_PreferredProviderNotFound_ReturnsFailure()
    {
        // Arrange
        var settings = new AiProviderSettings
        {
            PreferredProvider = "NonExistent",
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["Ollama"] = new() { Enabled = true, BaseUrl = "http://localhost:11434" }
            }
        };

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failures!.Should().Contain(f => f.Contains("PreferredProvider 'NonExistent' not found"));
    }

    [Fact]
    public void Validate_PreferredProviderDisabled_ReturnsFailure()
    {
        // Arrange
        var settings = new AiProviderSettings
        {
            PreferredProvider = "Ollama",
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["Ollama"] = new() { Enabled = false, BaseUrl = "http://localhost:11434" },
                ["OpenRouter"] = new() { Enabled = true, BaseUrl = "https://openrouter.ai/api/v1" }
            }
        };

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failures!.Should().Contain(f => f.Contains("PreferredProvider 'Ollama' is disabled"));
    }

    [Fact]
    public void Validate_FallbackChainProviderNotFound_ReturnsFailure()
    {
        // Arrange
        var settings = new AiProviderSettings
        {
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["Ollama"] = new() { Enabled = true, BaseUrl = "http://localhost:11434" }
            },
            FallbackChain = ["Ollama", "NonExistent"]
        };

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failures!.Should().Contain(f => f.Contains("FallbackChain provider 'NonExistent' not found"));
    }

    [Fact]
    public void Validate_FallbackChainProviderDisabled_ReturnsFailure()
    {
        // Arrange
        var settings = new AiProviderSettings
        {
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["Ollama"] = new() { Enabled = true, BaseUrl = "http://localhost:11434" },
                ["OpenRouter"] = new() { Enabled = false, BaseUrl = "https://openrouter.ai/api/v1" }
            },
            FallbackChain = ["Ollama", "OpenRouter"]
        };

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failures!.Should().Contain(f => f.Contains("FallbackChain provider 'OpenRouter' is disabled"));
    }

    [Fact]
    public void Validate_FallbackChainDuplicates_ReturnsFailure()
    {
        // Arrange
        var settings = new AiProviderSettings
        {
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["Ollama"] = new() { Enabled = true, BaseUrl = "http://localhost:11434" }
            },
            FallbackChain = ["Ollama", "Ollama"]
        };

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failures!.Should().Contain(f => f.Contains("FallbackChain contains duplicate providers"));
    }

    [Fact]
    public void Validate_ProviderMissingBaseUrl_ReturnsFailure()
    {
        // Arrange
        var settings = new AiProviderSettings
        {
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["Ollama"] = new() { Enabled = true, BaseUrl = "" }
            }
        };

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failures!.Should().Contain(f => f.Contains("Provider 'Ollama' is enabled but BaseUrl is empty"));
    }

    [Fact]
    public void Validate_CircuitBreakerInvalidFailureThreshold_ReturnsFailure()
    {
        // Arrange
        var settings = new AiProviderSettings
        {
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["Ollama"] = new() { Enabled = true, BaseUrl = "http://localhost:11434" }
            },
            CircuitBreaker = new CircuitBreakerConfig { FailureThreshold = 0 }
        };

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failures!.Should().Contain(f => f.Contains("Circuit breaker FailureThreshold must be positive"));
    }

    [Fact]
    public void Validate_CircuitBreakerInvalidOpenDuration_ReturnsFailure()
    {
        // Arrange
        var settings = new AiProviderSettings
        {
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["Ollama"] = new() { Enabled = true, BaseUrl = "http://localhost:11434" }
            },
            CircuitBreaker = new CircuitBreakerConfig { OpenDurationSeconds = -10 }
        };

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failures!.Should().Contain(f => f.Contains("Circuit breaker OpenDurationSeconds must be positive"));
    }

    [Fact]
    public void Validate_CircuitBreakerInvalidSuccessThreshold_ReturnsFailure()
    {
        // Arrange
        var settings = new AiProviderSettings
        {
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["Ollama"] = new() { Enabled = true, BaseUrl = "http://localhost:11434" }
            },
            CircuitBreaker = new CircuitBreakerConfig { SuccessThreshold = 0 }
        };

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failures!.Should().Contain(f => f.Contains("Circuit breaker SuccessThreshold must be positive"));
    }

    [Fact]
    public void Validate_ProviderInvalidHealthCheckInterval_ReturnsFailure()
    {
        // Arrange
        var settings = new AiProviderSettings
        {
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["Ollama"] = new() { Enabled = true, BaseUrl = "http://localhost:11434", HealthCheckIntervalSeconds = -5 }
            }
        };

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failures!.Should().Contain(f => f.Contains("Provider 'Ollama' has invalid HealthCheckIntervalSeconds"));
    }

    [Fact]
    public void Validate_EmptyPreferredProvider_IsValid()
    {
        // Arrange - Empty PreferredProvider means use user-tier routing (Option C)
        var settings = new AiProviderSettings
        {
            PreferredProvider = "",
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["Ollama"] = new() { Enabled = true, BaseUrl = "http://localhost:11434" }
            }
        };

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_MissingAiSection_IsValid()
    {
        // Arrange - Missing AI section (legacy deployment) should not fail startup (Option C backward compatibility)
        var settings = new AiProviderSettings
        {
            Providers = new Dictionary<string, ProviderConfig>() // Empty providers dictionary
        };

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue("Missing AI section should not fail validation for backward compatibility");
    }

    [Fact]
    public void Validate_NullProviders_IsValid()
    {
        // Arrange - Null providers (legacy deployment) should not fail startup
        var settings = new AiProviderSettings
        {
            Providers = null!
        };

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue("Null Providers should not fail validation for backward compatibility");
    }

    [Fact]
    public void Validate_EmptyFallbackChain_IsValid()
    {
        // Arrange - Empty FallbackChain is allowed (will use default circuit breaker logic)
        var settings = new AiProviderSettings
        {
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["Ollama"] = new() { Enabled = true, BaseUrl = "http://localhost:11434" }
            },
            FallbackChain = []
        };

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_MultipleValidationErrors_ReturnsAllFailures()
    {
        // Arrange
        var settings = new AiProviderSettings
        {
            PreferredProvider = "NonExistent",
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["Ollama"] = new() { Enabled = true, BaseUrl = "" },
                ["OpenRouter"] = new() { Enabled = false, BaseUrl = "https://openrouter.ai/api/v1" }
            },
            FallbackChain = ["Ollama", "OpenRouter", "Ollama"],
            CircuitBreaker = new CircuitBreakerConfig { FailureThreshold = -1 }
        };

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failures!.Count().Should().BeGreaterThanOrEqualTo(5);
        result.Failures!.Should().Contain(f => f.Contains("PreferredProvider 'NonExistent' not found"));
        result.Failures!.Should().Contain(f => f.Contains("BaseUrl is empty"));
        result.Failures!.Should().Contain(f => f.Contains("FallbackChain provider 'OpenRouter' is disabled"));
        result.Failures!.Should().Contain(f => f.Contains("duplicate providers"));
        result.Failures!.Should().Contain(f => f.Contains("FailureThreshold must be positive"));
    }
}

