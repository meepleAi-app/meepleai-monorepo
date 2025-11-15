# AI Provider Integration Guide

**MeepleAI Backend** - Developer's guide to integrating AI provider configuration in code

**Last Updated**: 2025-11-15
**Version**: 1.0
**Related Issues**: #963 (BGAI-021), #1153 (BGAI-022)

---

## Table of Contents

1. [Overview](#overview)
2. [Configuration Models](#configuration-models)
3. [Dependency Injection](#dependency-injection)
4. [Routing Integration](#routing-integration)
5. [Service Layer Integration](#service-layer-integration)
6. [Circuit Breaker Integration](#circuit-breaker-integration)
7. [Testing Patterns](#testing-patterns)
8. [Code Examples](#code-examples)
9. [Best Practices](#best-practices)

---

## Overview

The AI Provider Configuration system enables runtime control of LLM providers through `IOptions<AiProviderSettings>` dependency injection. This guide shows developers how to integrate provider configuration in routing strategies, services, and application layers.

### Architecture Integration Points

```
┌─────────────────────────────────────────┐
│ appsettings.json (Configuration Source) │
└──────────────┬──────────────────────────┘
               ↓
┌──────────────────────────────────────────┐
│ IOptions<AiProviderSettings> (DI)        │
│  - Auto-loaded on startup                │
│  - Validated by AiProviderValidator      │
│  - Cached per request                    │
└─────────┬────────────────────────────────┘
          ↓
    ┌─────┴──────────────────────┐
    ↓                            ↓
┌───────────────────┐    ┌────────────────┐
│ Routing Strategy  │    │ Service Layer  │
│ - SelectProvider  │    │ - Availability │
│ - Enabled check   │    │ - Fallback     │
└───────────────────┘    └────────────────┘
```

### Key Features for Developers

- **Type-Safe Configuration**: Strong-typed `AiProviderSettings` model
- **Dependency Injection**: Standard `IOptions<T>` pattern
- **Startup Validation**: Fail-fast on misconfiguration
- **Runtime Updates**: Configuration changes apply on next request (no restart)
- **Testability**: Easy mocking with `Options.Create()`

---

## Configuration Models

### AiProviderSettings.cs

**Location**: `src/Api/Configuration/AiProviderSettings.cs`

```csharp
namespace Api.Configuration;

/// <summary>
/// AI provider configuration settings (BGAI-021)
/// Enables runtime provider control without code changes
/// </summary>
public class AiProviderSettings
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "AI";

    /// <summary>
    /// Global preferred provider override (bypasses user-tier routing)
    /// Empty string = use LlmRouting configuration (default)
    /// </summary>
    public string PreferredProvider { get; set; } = string.Empty;

    /// <summary>
    /// Dictionary of AI provider configurations
    /// Keys: "Ollama", "OpenRouter"
    /// </summary>
    public Dictionary<string, ProviderConfig> Providers { get; set; } = new();

    /// <summary>
    /// Ordered fallback chain for circuit breaker
    /// Default: ["Ollama", "OpenRouter"]
    /// </summary>
    public List<string> FallbackChain { get; set; } = new();

    /// <summary>
    /// Circuit breaker configuration
    /// </summary>
    public CircuitBreakerConfig CircuitBreaker { get; set; } = new();
}

/// <summary>
/// Provider-specific configuration
/// </summary>
public class ProviderConfig
{
    /// <summary>
    /// Runtime enable/disable toggle
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Provider API endpoint URL
    /// Required if Enabled = true
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// API authentication key (supports environment variable substitution)
    /// Example: "${OPENROUTER_API_KEY}"
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Available models for this provider
    /// </summary>
    public List<string> Models { get; set; } = new();

    /// <summary>
    /// Health check frequency in seconds
    /// Default: 60 seconds
    /// </summary>
    public int HealthCheckIntervalSeconds { get; set; } = 60;
}

/// <summary>
/// Circuit breaker configuration
/// </summary>
public class CircuitBreakerConfig
{
    /// <summary>
    /// Consecutive failures before opening circuit
    /// Default: 5
    /// </summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>
    /// Duration circuit stays open (seconds)
    /// Default: 30 seconds
    /// </summary>
    public int OpenDurationSeconds { get; set; } = 30;

    /// <summary>
    /// Consecutive successes required to close circuit
    /// Default: 2
    /// </summary>
    public int SuccessThreshold { get; set; } = 2;
}
```

### Usage Example

```csharp
// Inject configuration
public class MyService
{
    private readonly AiProviderSettings _settings;

    public MyService(IOptions<AiProviderSettings> aiSettings)
    {
        _settings = aiSettings.Value;
    }

    public void Example()
    {
        // Access PreferredProvider
        string? preferred = _settings.PreferredProvider;

        // Check if Ollama enabled
        bool ollamaEnabled = _settings.Providers.ContainsKey("Ollama")
            && _settings.Providers["Ollama"].Enabled;

        // Get FallbackChain
        List<string> fallback = _settings.FallbackChain;

        // Access circuit breaker settings
        int failureThreshold = _settings.CircuitBreaker.FailureThreshold;
    }
}
```

---

## Dependency Injection

### Program.cs Registration

**Location**: `src/Api/Program.cs`

```csharp
// BGAI-021 (Issue #963): AI provider configuration with startup validation
builder.Services.Configure<AiProviderSettings>(
    builder.Configuration.GetSection(AiProviderSettings.SectionName));

// Register validator for fail-fast startup validation
builder.Services.AddSingleton<IValidateOptions<AiProviderSettings>, AiProviderValidator>();

// Validate configuration on startup (throws exception if invalid)
builder.Services.AddOptions<AiProviderSettings>()
    .ValidateOnStart();
```

**Key Points**:
- `Configure<T>()`: Binds `appsettings.json` section to model
- `IValidateOptions<T>`: Custom validation logic (7 rules)
- `ValidateOnStart()`: Fail-fast on startup (prevents invalid config reaching production)

---

### Injecting IOptions<AiProviderSettings>

**Constructor Injection Pattern**:

```csharp
using Api.Configuration;
using Microsoft.Extensions.Options;

public class HybridLlmService : ILlmService
{
    private readonly AiProviderSettings _aiSettings;
    private readonly ILogger<HybridLlmService> _logger;

    public HybridLlmService(
        IOptions<AiProviderSettings> aiSettings,  // NEW: Inject configuration
        ILogger<HybridLlmService> logger)
    {
        _aiSettings = aiSettings.Value;  // Extract settings
        _logger = logger;
    }

    public bool IsProviderEnabled(string providerName)
    {
        // Use injected configuration
        return _aiSettings.Providers.ContainsKey(providerName)
            && _aiSettings.Providers[providerName].Enabled;
    }
}
```

**Important**:
- Use `IOptions<T>`, not `T` directly
- Extract `.Value` in constructor for performance (cached)
- DI container automatically resolves `IOptions<T>` (no manual registration needed)

---

### Service Registration (Auto-Resolved)

**Before BGAI-022** (manual registration):
```csharp
services.AddScoped<ILlmRoutingStrategy, HybridAdaptiveRoutingStrategy>(sp =>
    new HybridAdaptiveRoutingStrategy(
        sp.GetRequiredService<ILogger<HybridAdaptiveRoutingStrategy>>(),
        sp.GetRequiredService<IConfiguration>()));
```

**After BGAI-022** (auto-resolved by DI):
```csharp
services.AddScoped<ILlmRoutingStrategy, HybridAdaptiveRoutingStrategy>();
// DI container automatically injects IOptions<AiProviderSettings>
```

**Benefit**: No need to update service registration when adding `IOptions<T>` parameters

---

## Routing Integration

### HybridAdaptiveRoutingStrategy Integration

**Location**: `src/Api/BoundedContexts/KnowledgeBase/Infrastructure/LlmClients/HybridAdaptiveRoutingStrategy.cs`

#### Constructor

```csharp
using Api.Configuration;
using Microsoft.Extensions.Options;

public class HybridAdaptiveRoutingStrategy : ILlmRoutingStrategy
{
    private readonly ILogger<HybridAdaptiveRoutingStrategy> _logger;
    private readonly IConfiguration _configuration;
    private readonly AiProviderSettings _aiSettings;  // NEW

    public HybridAdaptiveRoutingStrategy(
        ILogger<HybridAdaptiveRoutingStrategy> logger,
        IConfiguration configuration,
        IOptions<AiProviderSettings> aiSettings)  // NEW: Inject configuration
    {
        _logger = logger;
        _configuration = configuration;
        _aiSettings = aiSettings.Value;  // Extract settings
    }
}
```

---

#### SelectProvider() Method

**Complete Implementation**:

```csharp
public LlmRoutingDecision SelectProvider(User? user)
{
    // STEP 1: Check PreferredProvider Override (BGAI-022)
    if (!string.IsNullOrEmpty(_aiSettings.PreferredProvider))
    {
        var preferredProvider = _aiSettings.PreferredProvider;

        // Verify provider is enabled
        if (_aiSettings.Providers.ContainsKey(preferredProvider) &&
            _aiSettings.Providers[preferredProvider].Enabled)
        {
            var model = GetDefaultModelForProvider(preferredProvider);

            _logger.LogDebug(
                "Routing to PreferredProvider {Provider} ({Model}) - overriding user-tier routing",
                preferredProvider, model);

            return new LlmRoutingDecision(
                providerName: preferredProvider,
                modelName: model,
                reason: $"PreferredProvider override (AI:PreferredProvider = {preferredProvider})");
        }
    }

    // STEP 2: Apply User-Tier Routing (Existing LlmRouting Configuration)
    var userTier = DetermineUserTier(user);
    var (provider, model) = SelectProviderAndModelByTier(userTier);

    // STEP 3: Verify Selected Provider is Enabled (BGAI-022)
    if (_aiSettings.Providers.ContainsKey(provider) &&
        !_aiSettings.Providers[provider].Enabled)
    {
        // Provider disabled, try fallback
        _logger.LogWarning(
            "Selected provider {Provider} is disabled in AI configuration, trying fallback",
            provider);

        var alternativeProvider = provider == "Ollama" ? "OpenRouter" : "Ollama";

        if (_aiSettings.Providers.ContainsKey(alternativeProvider) &&
            _aiSettings.Providers[alternativeProvider].Enabled)
        {
            var fallbackModel = GetDefaultModelForProvider(alternativeProvider);

            _logger.LogInformation(
                "Fallback to {Provider} ({Model}) - primary provider {Primary} disabled",
                alternativeProvider, fallbackModel, provider);

            return new LlmRoutingDecision(
                providerName: alternativeProvider,
                modelName: fallbackModel,
                reason: $"Fallback from {provider} (disabled in AI:Providers)");
        }

        // No enabled providers found
        throw new InvalidOperationException(
            $"Provider {provider} is disabled in AI configuration and no enabled fallback provider found. " +
            $"Please enable at least one provider in AI:Providers section.");
    }

    // STEP 4: Return Decision (Normal Flow)
    return new LlmRoutingDecision(
        providerName: provider,
        modelName: model,
        reason: $"{userTier} tier routing (LlmRouting.{userTier}Model)");
}
```

**Key Points**:
- **Step 1**: PreferredProvider bypasses all user-tier routing
- **Step 2**: Existing LlmRouting logic (unchanged if provider enabled)
- **Step 3**: Enabled flag validation with fallback logic
- **Step 4**: Exception if all providers disabled (fail-safe)

---

#### Helper Method: GetDefaultModelForProvider

```csharp
private string GetDefaultModelForProvider(string providerName)
{
    // Get first configured model for provider
    if (_aiSettings.Providers.ContainsKey(providerName))
    {
        var models = _aiSettings.Providers[providerName].Models;
        if (models.Count > 0)
        {
            return models[0];  // First model is default
        }
    }

    // Fallback to LlmRouting configuration if no models configured
    return providerName switch
    {
        "Ollama" => _configuration.GetValue<string>("LlmRouting:AnonymousModel") ?? "llama3:8b",
        "OpenRouter" => _configuration.GetValue<string>("LlmRouting:AdminModel") ?? "gpt-4o-mini",
        _ => "unknown-model"
    };
}
```

---

## Service Layer Integration

### HybridLlmService Integration

**Location**: `src/Api/BoundedContexts/KnowledgeBase/Infrastructure/LlmClients/HybridLlmService.cs`

#### Constructor

```csharp
using Api.Configuration;
using Microsoft.Extensions.Options;

public class HybridLlmService : ILlmService
{
    private readonly IEnumerable<ILlmClient> _clients;
    private readonly ILlmRoutingStrategy _routingStrategy;
    private readonly ILlmCostLogRepository _costLogRepository;
    private readonly ILogger<HybridLlmService> _logger;
    private readonly AiProviderSettings _aiSettings;  // NEW
    private readonly ProviderHealthCheckService? _healthCheckService;

    public HybridLlmService(
        IEnumerable<ILlmClient> clients,
        ILlmRoutingStrategy routingStrategy,
        ILlmCostLogRepository costLogRepository,
        ILogger<HybridLlmService> logger,
        IOptions<AiProviderSettings> aiSettings,  // NEW: Inject configuration
        ProviderHealthCheckService? healthCheckService = null)
    {
        _clients = clients;
        _routingStrategy = routingStrategy;
        _costLogRepository = costLogRepository;
        _logger = logger;
        _aiSettings = aiSettings.Value;  // Extract settings
        _healthCheckService = healthCheckService;
    }
}
```

---

#### Provider Availability Check

**IsProviderAvailable() Enhancement**:

```csharp
/// <summary>
/// Checks if provider is available (BGAI-020 + BGAI-022)
/// Returns false if: Disabled in config, Circuit open, or Health unhealthy
/// </summary>
private bool IsProviderAvailable(string providerName)
{
    // NEW (BGAI-022): Check enabled flag FIRST
    if (_aiSettings.Providers.ContainsKey(providerName))
    {
        var config = _aiSettings.Providers[providerName];
        if (!config.Enabled)
        {
            _logger.LogDebug(
                "Provider {Provider} is disabled in AI configuration",
                providerName);
            return false;
        }
    }

    // Existing (BGAI-020): Check circuit breaker state
    if (_healthCheckService?.IsCircuitOpen(providerName) == true)
    {
        _logger.LogDebug(
            "Provider {Provider} circuit is OPEN (too many failures)",
            providerName);
        return false;
    }

    // Existing (BGAI-020): Check health status
    var health = _healthCheckService?.GetProviderHealth(providerName);
    if (health?.Status != HealthStatus.Healthy)
    {
        _logger.LogDebug(
            "Provider {Provider} health status is {Status}",
            providerName, health?.Status ?? HealthStatus.Unknown);
        return false;
    }

    return true;
}
```

**Composite Availability Logic**:
```
Provider Available = Enabled (config)
                   AND Circuit Closed (circuit breaker)
                   AND Health Healthy (health check)
```

---

## Circuit Breaker Integration

### GetClientWithCircuitBreaker() Enhancement

**FallbackChain Integration**:

```csharp
/// <summary>
/// Gets LLM client with circuit breaker fallback (BGAI-020 + BGAI-022)
/// Uses FallbackChain if configured, otherwise all clients
/// </summary>
private ILlmClient? GetClientWithCircuitBreaker(ILlmClient primaryClient)
{
    var providerName = primaryClient.ProviderName;

    // Check primary provider availability
    if (IsProviderAvailable(providerName))
    {
        return primaryClient;
    }

    // Primary unavailable, try fallback chain
    _logger.LogWarning(
        "Primary provider {Provider} unavailable, trying fallback chain",
        providerName);

    // NEW (BGAI-022): Use FallbackChain if configured
    IEnumerable<string> fallbackOrder;

    if (_aiSettings.FallbackChain?.Any() == true)
    {
        // Use configured FallbackChain order
        fallbackOrder = _aiSettings.FallbackChain
            .Where(p => p != providerName);  // Exclude primary

        _logger.LogDebug(
            "Using configured FallbackChain: {Chain}",
            string.Join(" → ", fallbackOrder));
    }
    else
    {
        // Default: iterate all clients (existing behavior)
        fallbackOrder = _clients
            .Where(c => c.ProviderName != providerName)
            .Select(c => c.ProviderName);

        _logger.LogDebug(
            "Using default fallback order (all clients): {Order}",
            string.Join(" → ", fallbackOrder));
    }

    // Iterate fallback order to find available provider
    foreach (var fallbackProviderName in fallbackOrder)
    {
        if (IsProviderAvailable(fallbackProviderName))
        {
            var fallbackClient = _clients.FirstOrDefault(c => c.ProviderName == fallbackProviderName);
            if (fallbackClient != null)
            {
                _logger.LogInformation(
                    "Using fallback provider {Fallback} (from FallbackChain) - primary {Primary} unavailable",
                    fallbackProviderName, providerName);

                return fallbackClient;
            }
        }
    }

    // All providers unavailable
    _logger.LogError(
        "All providers in FallbackChain unavailable (primary: {Primary})",
        providerName);

    return null;
}
```

**Fallback Logic Summary**:
```
1. Try primary provider
   ↓ (unavailable)
2. If FallbackChain configured:
     Try each provider in FallbackChain order
   Else:
     Try all other clients
   ↓ (all unavailable)
3. Return null (service degraded)
```

---

## Testing Patterns

### Unit Test Setup

**Location**: `tests/Api.Tests/BoundedContexts/KnowledgeBase/Infrastructure/LlmClients/HybridAdaptiveRoutingStrategyTests.cs`

#### Test Constructor with Mock Configuration

```csharp
using Api.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

public class HybridAdaptiveRoutingStrategyTests
{
    private readonly ILogger<HybridAdaptiveRoutingStrategy> _logger;
    private readonly IConfiguration _configuration;
    private readonly IOptions<AiProviderSettings> _aiSettings;

    public HybridAdaptiveRoutingStrategyTests()
    {
        // Logger setup
        _logger = new LoggerFactory().CreateLogger<HybridAdaptiveRoutingStrategy>();

        // Configuration setup (existing LlmRouting)
        var configData = new Dictionary<string, string?>
        {
            ["LlmRouting:AnonymousModel"] = "meta-llama/llama-3.3-70b-instruct:free",
            ["LlmRouting:UserModel"] = "meta-llama/llama-3.3-70b-instruct:free",
            ["LlmRouting:EditorModel"] = "llama3:8b",
            ["LlmRouting:AdminModel"] = "llama3:8b",
            ["LlmRouting:EditorOpenRouterPercent"] = "50",
            ["LlmRouting:AdminOpenRouterPercent"] = "80"
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        // NEW (BGAI-022): AI provider settings with both providers enabled (default)
        _aiSettings = Options.Create(new AiProviderSettings
        {
            PreferredProvider = "",  // Empty = use user-tier routing
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["Ollama"] = new()
                {
                    Enabled = true,
                    BaseUrl = "http://localhost:11434",
                    Models = new List<string> { "llama3:8b", "mistral" },
                    HealthCheckIntervalSeconds = 60
                },
                ["OpenRouter"] = new()
                {
                    Enabled = true,
                    BaseUrl = "https://openrouter.ai/api/v1",
                    Models = new List<string>
                    {
                        "meta-llama/llama-3.3-70b-instruct:free",
                        "anthropic/claude-3.5-haiku"
                    },
                    HealthCheckIntervalSeconds = 60
                }
            },
            FallbackChain = new List<string> { "Ollama", "OpenRouter" },
            CircuitBreaker = new CircuitBreakerConfig
            {
                FailureThreshold = 5,
                OpenDurationSeconds = 30,
                SuccessThreshold = 2
            }
        });
    }

    [Fact]
    public void Test01_AnonymousUser_RoutesFreeModel()
    {
        // Arrange
        var strategy = new HybridAdaptiveRoutingStrategy(
            _logger,
            _configuration,
            _aiSettings);  // NEW: Pass mock configuration

        // Act
        var decision = strategy.SelectProvider(user: null);

        // Assert
        Assert.Equal("OpenRouter", decision.ProviderName);
        Assert.Equal("meta-llama/llama-3.3-70b-instruct:free", decision.ModelName);
    }
}
```

**Key Points**:
- Use `Options.Create()` to create mock `IOptions<T>`
- Configure default settings in test constructor
- Override settings per test for specific scenarios

---

### Testing PreferredProvider Override

```csharp
[Fact]
public void Test11_PreferredProvider_OverridesUserTierRouting()
{
    // Arrange: Set PreferredProvider to Ollama
    var aiSettings = Options.Create(new AiProviderSettings
    {
        PreferredProvider = "Ollama",  // Override to Ollama
        Providers = new Dictionary<string, ProviderConfig>
        {
            ["Ollama"] = new() { Enabled = true, Models = new() { "llama3:8b" } },
            ["OpenRouter"] = new() { Enabled = true }
        }
    });

    var strategy = new HybridAdaptiveRoutingStrategy(_logger, _configuration, aiSettings);

    // Create Admin user (normally 80% OpenRouter in user-tier routing)
    var adminUser = new User
    {
        Id = Guid.NewGuid(),
        Email = "admin@test.com",
        Role = UserRole.Admin
    };

    // Act: Select provider 100 times
    var results = Enumerable.Range(0, 100)
        .Select(_ => strategy.SelectProvider(adminUser))
        .ToList();

    // Assert: ALL should use Ollama (100% override, not 20% from user-tier routing)
    Assert.All(results, decision =>
    {
        Assert.Equal("Ollama", decision.ProviderName);
        Assert.Equal("llama3:8b", decision.ModelName);
        Assert.Contains("PreferredProvider override", decision.Reason);
    });
}
```

---

### Testing Disabled Provider Fallback

```csharp
[Fact]
public void Test12_DisabledProvider_FallsBackToAlternative()
{
    // Arrange: Disable Ollama, force Ollama selection via EditorOpenRouterPercent = 0
    var aiSettings = Options.Create(new AiProviderSettings
    {
        PreferredProvider = "",
        Providers = new Dictionary<string, ProviderConfig>
        {
            ["Ollama"] = new() { Enabled = false },  // Disabled
            ["OpenRouter"] = new() { Enabled = true, Models = new() { "gpt-4o-mini" } }
        }
    });

    // Override config to force Ollama selection (0% OpenRouter)
    var configData = new Dictionary<string, string?>
    {
        ["LlmRouting:EditorModel"] = "llama3:8b",
        ["LlmRouting:EditorOpenRouterPercent"] = "0"  // Force Ollama selection
    };
    var config = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();

    var strategy = new HybridAdaptiveRoutingStrategy(_logger, config, aiSettings);

    // Create Editor user
    var editorUser = new User
    {
        Id = Guid.NewGuid(),
        Email = "editor@test.com",
        Role = UserRole.Editor
    };

    // Act: Select provider
    var decision = strategy.SelectProvider(editorUser);

    // Assert: Should fallback to OpenRouter (Ollama disabled)
    Assert.Equal("OpenRouter", decision.ProviderName);
    Assert.Contains("Fallback from Ollama", decision.Reason);
}
```

---

### Testing All Providers Disabled

```csharp
[Fact]
public void Test13_AllProvidersDisabled_ThrowsException()
{
    // Arrange: Disable both providers
    var aiSettings = Options.Create(new AiProviderSettings
    {
        PreferredProvider = "",
        Providers = new Dictionary<string, ProviderConfig>
        {
            ["Ollama"] = new() { Enabled = false },
            ["OpenRouter"] = new() { Enabled = false }
        }
    });

    var strategy = new HybridAdaptiveRoutingStrategy(_logger, _configuration, aiSettings);

    // Act & Assert: Should throw InvalidOperationException
    var exception = Assert.Throws<InvalidOperationException>(() =>
        strategy.SelectProvider(user: null));

    Assert.Contains("no enabled fallback provider found", exception.Message);
}
```

---

### Testing Backward Compatibility

```csharp
[Fact]
public void Test14_EmptyPreferredProvider_UsesUserTierRouting()
{
    // Arrange: Empty PreferredProvider = backward compatible
    var aiSettings = Options.Create(new AiProviderSettings
    {
        PreferredProvider = "",  // Empty = use LlmRouting
        Providers = new Dictionary<string, ProviderConfig>
        {
            ["Ollama"] = new() { Enabled = true },
            ["OpenRouter"] = new() { Enabled = true }
        }
    });

    var strategy = new HybridAdaptiveRoutingStrategy(_logger, _configuration, aiSettings);

    // Create Admin user (normally 80% OpenRouter)
    var adminUser = new User
    {
        Id = Guid.NewGuid(),
        Email = "admin@test.com",
        Role = UserRole.Admin
    };

    // Act: Select provider 100 times
    var results = Enumerable.Range(0, 100)
        .Select(_ => strategy.SelectProvider(adminUser))
        .ToList();

    var openRouterCount = results.Count(r => r.ProviderName == "OpenRouter");

    // Assert: Should use user-tier routing (~80% OpenRouter for Admin)
    Assert.InRange(openRouterCount, 70, 90);  // Allow statistical variance
}
```

---

## Code Examples

### Example 1: Check Provider Availability

```csharp
public class MyFeatureService
{
    private readonly AiProviderSettings _aiSettings;

    public MyFeatureService(IOptions<AiProviderSettings> aiSettings)
    {
        _aiSettings = aiSettings.Value;
    }

    public bool CanUseOllama()
    {
        // Check if Ollama is configured and enabled
        return _aiSettings.Providers.ContainsKey("Ollama")
            && _aiSettings.Providers["Ollama"].Enabled;
    }

    public List<string> GetAvailableProviders()
    {
        // Get list of enabled providers
        return _aiSettings.Providers
            .Where(kvp => kvp.Value.Enabled)
            .Select(kvp => kvp.Key)
            .ToList();
    }
}
```

---

### Example 2: Custom Fallback Logic

```csharp
public class CustomRoutingStrategy : ILlmRoutingStrategy
{
    private readonly AiProviderSettings _aiSettings;
    private readonly ILogger<CustomRoutingStrategy> _logger;

    public CustomRoutingStrategy(
        IOptions<AiProviderSettings> aiSettings,
        ILogger<CustomRoutingStrategy> logger)
    {
        _aiSettings = aiSettings.Value;
        _logger = logger;
    }

    public LlmRoutingDecision SelectProvider(User? user)
    {
        // Use FallbackChain as priority order
        foreach (var providerName in _aiSettings.FallbackChain)
        {
            if (_aiSettings.Providers.ContainsKey(providerName) &&
                _aiSettings.Providers[providerName].Enabled)
            {
                var model = _aiSettings.Providers[providerName].Models.FirstOrDefault()
                    ?? "default-model";

                _logger.LogInformation(
                    "Selected provider {Provider} from FallbackChain",
                    providerName);

                return new LlmRoutingDecision(
                    providerName: providerName,
                    modelName: model,
                    reason: $"FallbackChain priority order");
            }
        }

        throw new InvalidOperationException("No enabled providers in FallbackChain");
    }
}
```

---

### Example 3: Runtime Configuration Update Handling

```csharp
public class ConfigurationMonitorService : BackgroundService
{
    private readonly IOptionsMonitor<AiProviderSettings> _aiSettingsMonitor;
    private readonly ILogger<ConfigurationMonitorService> _logger;

    public ConfigurationMonitorService(
        IOptionsMonitor<AiProviderSettings> aiSettingsMonitor,  // Use IOptionsMonitor for change detection
        ILogger<ConfigurationMonitorService> logger)
    {
        _aiSettingsMonitor = aiSettingsMonitor;
        _logger = logger;

        // Register change callback
        _aiSettingsMonitor.OnChange(OnConfigurationChanged);
    }

    private void OnConfigurationChanged(AiProviderSettings newSettings)
    {
        _logger.LogInformation(
            "AI provider configuration changed. PreferredProvider: {Preferred}, Enabled providers: {Enabled}",
            newSettings.PreferredProvider,
            string.Join(", ", newSettings.Providers.Where(p => p.Value.Enabled).Select(p => p.Key)));

        // Handle configuration change (e.g., reset circuit breakers, clear caches)
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Background monitoring logic
        return Task.CompletedTask;
    }
}
```

---

### Example 4: Provider Health Check Integration

```csharp
public class ProviderHealthCheckService
{
    private readonly AiProviderSettings _aiSettings;
    private readonly ILogger<ProviderHealthCheckService> _logger;
    private readonly Dictionary<string, HealthStatus> _healthCache = new();

    public ProviderHealthCheckService(
        IOptions<AiProviderSettings> aiSettings,
        ILogger<ProviderHealthCheckService> logger)
    {
        _aiSettings = aiSettings.Value;
        _logger = logger;
    }

    public async Task<HealthStatus> CheckProviderHealthAsync(string providerName)
    {
        // Check enabled flag first
        if (!_aiSettings.Providers.ContainsKey(providerName))
        {
            _logger.LogWarning("Provider {Provider} not found in configuration", providerName);
            return HealthStatus.Unknown;
        }

        var config = _aiSettings.Providers[providerName];

        if (!config.Enabled)
        {
            _logger.LogDebug("Provider {Provider} is disabled in configuration", providerName);
            return HealthStatus.Unhealthy;
        }

        // Perform actual health check (HTTP ping to BaseUrl)
        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await httpClient.GetAsync($"{config.BaseUrl}/health");

            var health = response.IsSuccessStatusCode
                ? HealthStatus.Healthy
                : HealthStatus.Unhealthy;

            _healthCache[providerName] = health;
            return health;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed for provider {Provider}", providerName);
            _healthCache[providerName] = HealthStatus.Unhealthy;
            return HealthStatus.Unhealthy;
        }
    }
}

public enum HealthStatus
{
    Unknown,
    Healthy,
    Unhealthy
}
```

---

## Best Practices

### 1. Use IOptions<T> Pattern Correctly

**Good**:
```csharp
public MyService(IOptions<AiProviderSettings> aiSettings)
{
    _settings = aiSettings.Value;  // Extract once in constructor
}
```

**Bad**:
```csharp
public MyService(AiProviderSettings settings)  // Don't inject settings directly
{
    _settings = settings;
}
```

**Reason**: `IOptions<T>` supports configuration reloading, validation, and DI best practices

---

### 2. Check Provider Existence Before Access

**Good**:
```csharp
if (_aiSettings.Providers.ContainsKey("Ollama") &&
    _aiSettings.Providers["Ollama"].Enabled)
{
    // Safe access
}
```

**Bad**:
```csharp
if (_aiSettings.Providers["Ollama"].Enabled)  // May throw KeyNotFoundException
{
    // Unsafe
}
```

**Reason**: Provider may be removed from configuration

---

### 3. Handle Empty/Null Configuration Gracefully

**Good**:
```csharp
var fallbackOrder = _aiSettings.FallbackChain?.Any() == true
    ? _aiSettings.FallbackChain
    : new List<string> { "Ollama", "OpenRouter" };  // Default
```

**Bad**:
```csharp
var fallbackOrder = _aiSettings.FallbackChain;  // May be null
```

**Reason**: Backward compatibility with legacy deployments (missing AI section)

---

### 4. Log Configuration Decisions

**Good**:
```csharp
_logger.LogDebug(
    "Routing to PreferredProvider {Provider} ({Model}) - overriding user-tier routing",
    preferredProvider, model);
```

**Reason**: Helps debugging and monitoring configuration behavior

---

### 5. Use Options.Create() in Tests

**Good**:
```csharp
var aiSettings = Options.Create(new AiProviderSettings
{
    PreferredProvider = "Ollama",
    Providers = new Dictionary<string, ProviderConfig>
    {
        ["Ollama"] = new() { Enabled = true }
    }
});
```

**Reason**: Simple, type-safe, no mocking framework needed

---

### 6. Fail-Fast on Invalid Configuration

**Good**:
```csharp
if (!_aiSettings.Providers.Any(p => p.Value.Enabled))
{
    throw new InvalidOperationException(
        "At least one AI provider must be enabled");
}
```

**Reason**: Detect configuration errors early (startup validation handles this)

---

### 7. Document Configuration Dependencies

**Good**:
```csharp
/// <summary>
/// Selects AI provider based on AI:PreferredProvider and AI:Providers configuration.
/// Falls back to LlmRouting if PreferredProvider is empty.
/// Requires at least one enabled provider in AI:Providers.
/// </summary>
public LlmRoutingDecision SelectProvider(User? user)
```

**Reason**: Clear expectations for configuration requirements

---

## Related Documentation

- [Admin Configuration Guide](../03-api/ai-provider-configuration.md)
- [Circuit Breaker Implementation (BGAI-020)](../../claudedocs/bgai-020-implementation-summary.md)
- [Configuration Architecture (BGAI-021)](../../claudedocs/bgai-021-implementation-summary.md)
- [Service Integration (BGAI-022)](../../claudedocs/bgai-022-implementation-summary.md)

---

**Version**: 1.0
**Last Updated**: 2025-11-15
**Maintainer**: Engineering Team
