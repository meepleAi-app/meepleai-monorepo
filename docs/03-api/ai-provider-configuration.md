# AI Provider Configuration Guide

**MeepleAI Backend** - Administrator's guide to AI provider configuration and management

**Last Updated**: 2025-11-15
**Version**: 1.0
**Related Issues**: #963 (BGAI-021), #1153 (BGAI-022)

---

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Configuration Structure](#configuration-structure)
4. [Configuration Parameters](#configuration-parameters)
5. [Provider Management](#provider-management)
6. [Advanced Features](#advanced-features)
7. [Configuration Examples](#configuration-examples)
8. [Troubleshooting](#troubleshooting)
9. [Best Practices](#best-practices)

---

## Overview

The AI Provider Configuration system enables runtime control of LLM providers (Ollama, OpenRouter) without code changes. Administrators can enable/disable providers, set preferred providers, configure fallback chains, and tune circuit breaker behavior through `appsettings.json`.

### Key Features

- **Runtime Provider Toggle**: Enable/disable Ollama or OpenRouter without redeployment
- **Preferred Provider Override**: Force all users to a specific provider (bypasses user-tier routing)
- **Fallback Chain**: Custom circuit breaker fallback order
- **Startup Validation**: Fail-fast on misconfiguration with clear error messages
- **Backward Compatible**: Coexists with existing `LlmRouting` configuration

### When to Use

| Scenario | Configuration |
|----------|---------------|
| **Cost Control** | Disable OpenRouter, force Ollama only |
| **Maintenance** | Temporarily disable provider during updates |
| **Testing** | Use PreferredProvider to test specific models |
| **Performance** | Custom FallbackChain for optimal failover |
| **Emergency** | Quick provider switch during incidents |

---

## Architecture

### Design: Option C (Backward Compatible Layer)

The AI Provider Configuration coexists with the existing `LlmRouting` user-tier system. This design choice ensures zero breaking changes while adding powerful runtime control.

```
┌─────────────────────────────────────────────────────────────┐
│ AI:Provider Configuration (Runtime Control)                 │
│  - PreferredProvider: Global override (bypasses user tiers) │
│  - Providers[x].Enabled: Runtime toggle per provider        │
│  - FallbackChain: Circuit breaker fallback order            │
└─────────────────────────────────────────────────────────────┘
                 ↓ IOptions<AiProviderSettings>
        ┌────────┴─────────┬──────────────────────────┐
        ↓                  ↓                          ↓
┌──────────────────┐  ┌────────────────┐  ┌─────────────────────┐
│ Routing Strategy │  │ Service Layer  │  │ Circuit Breaker     │
│ - PreferredProv  │  │ - Enabled chk  │  │ - FallbackChain ord │
│ - Enabled check  │  │ - Fallback     │  │ - Auto-select       │
└──────────────────┘  └────────────────┘  └─────────────────────┘
        ↓
┌─────────────────────────────────────────┐
│ LlmRouting (User-Tier Routing - Legacy) │
│  - Anonymous/User: Free models          │
│  - Editor: Balanced split               │
│  - Admin: Premium models                │
└─────────────────────────────────────────┘
```

### Routing Logic Flow

**Step-by-Step Decision Process**:

```csharp
Step 1: Check PreferredProvider Override
  if (PreferredProvider configured && provider enabled)
    → Use PreferredProvider (bypasses user-tier routing)
    → Reason: "PreferredProvider override (AI:PreferredProvider = X)"

Step 2: Apply User-Tier Routing (if no PreferredProvider)
  → Use existing LlmRouting configuration
  → Map user role to tier (Anonymous → User → Editor → Admin)
  → Apply traffic split percentage (e.g., Admin: 80% OpenRouter)

Step 3: Verify Provider Enabled
  if (selected provider.Enabled == false)
    → Fallback to alternative provider (Ollama ↔ OpenRouter)
    → Reason: "Fallback from X (disabled in AI:Providers)"

Step 4: Handle All Disabled
  if (no enabled providers found)
    → throw InvalidOperationException
    → Message: "Provider X disabled and no enabled fallback found"

Step 5: Return Routing Decision
  → Provider name + model + routing reason
```

### Service Layer Integration

**Provider Availability Checks**:

```csharp
IsProviderAvailable(providerName):
  Check 1: Enabled Flag (BGAI-022)
    if (AI:Providers[provider].Enabled == false)
      → return false

  Check 2: Circuit Breaker (BGAI-020)
    if (circuit breaker open)
      → return false

  Check 3: Health Check (BGAI-020)
    if (health status unhealthy)
      → return false

  Return: true (provider available)
```

**Circuit Breaker Fallback**:

```csharp
GetClientWithCircuitBreaker(primaryProvider):
  Primary Check:
    if (IsProviderAvailable(primary))
      → return primary client

  Fallback Logic:
    if (FallbackChain configured)
      → iterate FallbackChain order
    else
      → iterate all clients (default behavior)

    foreach (fallback in order)
      if (IsProviderAvailable(fallback))
        → return fallback client

    return null (all providers unavailable)
```

---

## Configuration Structure

### Complete Configuration Example

Add this section to `appsettings.json`:

```json
{
  "AI": {
    "Comment": "BGAI-021: Runtime AI provider configuration",
    "PreferredProvider": "",
    "Providers": {
      "Ollama": {
        "Enabled": true,
        "BaseUrl": "http://localhost:11434",
        "Models": [
          "llama3:8b",
          "mistral",
          "codellama:7b"
        ],
        "HealthCheckIntervalSeconds": 60
      },
      "OpenRouter": {
        "Enabled": true,
        "BaseUrl": "https://openrouter.ai/api/v1",
        "Models": [
          "meta-llama/llama-3.3-70b-instruct:free",
          "anthropic/claude-3.5-haiku",
          "openai/gpt-4o-mini"
        ],
        "HealthCheckIntervalSeconds": 60
      }
    },
    "FallbackChain": ["Ollama", "OpenRouter"],
    "CircuitBreaker": {
      "FailureThreshold": 5,
      "OpenDurationSeconds": 30,
      "SuccessThreshold": 2
    }
  }
}
```

### Configuration Hierarchy

```
AI (root section)
├── PreferredProvider (string)
├── Providers (dictionary)
│   ├── Ollama (ProviderConfig)
│   │   ├── Enabled (bool)
│   │   ├── BaseUrl (string)
│   │   ├── Models (string[])
│   │   └── HealthCheckIntervalSeconds (int)
│   └── OpenRouter (ProviderConfig)
│       ├── Enabled (bool)
│       ├── BaseUrl (string)
│       ├── Models (string[])
│       └── HealthCheckIntervalSeconds (int)
├── FallbackChain (string[])
└── CircuitBreaker (CircuitBreakerConfig)
    ├── FailureThreshold (int)
    ├── OpenDurationSeconds (int)
    └── SuccessThreshold (int)
```

**Note**: API keys (e.g., OpenRouter) are configured via environment variables (`OPENROUTER_API_KEY` or `OPENROUTER_API_KEY_FILE`), not in the configuration hierarchy.

---

## Configuration Parameters

### PreferredProvider

**Type**: `string`
**Required**: No
**Default**: `""` (empty, uses user-tier routing)

**Description**: Global provider override that bypasses user-tier routing. When set, all users (Anonymous/User/Editor/Admin) use this provider exclusively.

**Valid Values**:
- `""` (empty): Use existing `LlmRouting` configuration (default)
- `"Ollama"`: Force all users to Ollama
- `"OpenRouter"`: Force all users to OpenRouter

**Validation Rules**:
- If set, provider must exist in `Providers` dictionary
- If set, provider must have `Enabled = true`

**Example**:
```json
{
  "AI": {
    "PreferredProvider": "Ollama"  // All users forced to Ollama
  }
}
```

**Use Cases**:
- Cost control: Force free Ollama during budget constraints
- Testing: Validate specific provider behavior
- Maintenance: Temporarily route all traffic to stable provider
- Emergency: Quick failover during provider incidents

---

### Providers

**Type**: `Dictionary<string, ProviderConfig>`
**Required**: Yes
**Default**: Both Ollama and OpenRouter enabled

**Description**: Dictionary of AI provider configurations. Each provider has individual enable/disable toggle and specific settings.

#### ProviderConfig Properties

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| **Enabled** | `bool` | Yes | `true` | Runtime toggle for provider availability |
| **BaseUrl** | `string` | Yes (if enabled) | - | Provider API endpoint URL |
| **Models** | `string[]` | No | `[]` | List of available models for this provider |
| **HealthCheckIntervalSeconds** | `int` | No | `60` | Health check frequency in seconds |

**Validation Rules**:
1. At least one provider must have `Enabled = true`
2. Enabled providers must have non-empty `BaseUrl`
3. `HealthCheckIntervalSeconds` must be positive (> 0)

**⚠️ API Key Configuration**:
API keys are **NOT** configured in `appsettings.json`. Instead, use environment variables:
- `OPENROUTER_API_KEY`: Direct environment variable (development)
- `OPENROUTER_API_KEY_FILE`: Path to Docker secret file (production)

See [API Key Configuration](#api-key-configuration) section below for details.

**Example**:
```json
{
  "AI": {
    "Providers": {
      "Ollama": {
        "Enabled": true,
        "BaseUrl": "http://localhost:11434",
        "Models": ["llama3:8b", "mistral"],
        "HealthCheckIntervalSeconds": 60
      },
      "OpenRouter": {
        "Enabled": false,  // Disabled provider
        "BaseUrl": "https://openrouter.ai/api/v1",
        "Models": ["meta-llama/llama-3.3-70b-instruct:free"],
        "HealthCheckIntervalSeconds": 120
      }
    }
  }
}
```

**Note**: OpenRouter API key is configured via `OPENROUTER_API_KEY` environment variable, not in `appsettings.json`.

---

### FallbackChain

**Type**: `string[]`
**Required**: No
**Default**: `["Ollama", "OpenRouter"]`

**Description**: Ordered list of providers for circuit breaker fallback. When a provider is unavailable (circuit open), the system tries providers in this order.

**Validation Rules**:
- All providers in chain must exist in `Providers` dictionary
- All providers in chain must have `Enabled = true`
- No duplicate providers allowed
- Order matters: first provider is tried first

**Example**:
```json
{
  "AI": {
    "FallbackChain": ["OpenRouter", "Ollama"]
    // Try OpenRouter first, fallback to Ollama if circuit open
  }
}
```

**Use Cases**:
- **Cost Optimization**: `["Ollama", "OpenRouter"]` - prefer free Ollama, fallback to paid
- **Reliability**: `["OpenRouter", "Ollama"]` - prefer cloud service, fallback to local
- **Custom Priority**: Define specific fallback order based on operational requirements

---

### CircuitBreaker

**Type**: `CircuitBreakerConfig`
**Required**: No
**Default**: See below

**Description**: Circuit breaker configuration for provider failover behavior. Controls when to open circuit (stop using provider) and when to close it (retry provider).

#### CircuitBreakerConfig Properties

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| **FailureThreshold** | `int` | No | `5` | Number of consecutive failures before opening circuit |
| **OpenDurationSeconds** | `int` | No | `30` | How long circuit stays open (seconds) |
| **SuccessThreshold** | `int` | No | `2` | Consecutive successes required to close circuit |

**Validation Rules**:
- All values must be positive (> 0)

**Example**:
```json
{
  "AI": {
    "CircuitBreaker": {
      "FailureThreshold": 5,      // Open after 5 failures
      "OpenDurationSeconds": 30,  // Stay open for 30 seconds
      "SuccessThreshold": 2       // Close after 2 successes
    }
  }
}
```

**Tuning Guidelines**:

| Scenario | FailureThreshold | OpenDurationSeconds | SuccessThreshold |
|----------|------------------|---------------------|------------------|
| **Aggressive** | 3 | 15 | 1 | Fast failover, quick recovery |
| **Balanced** | 5 | 30 | 2 | Default - good for most cases |
| **Conservative** | 10 | 60 | 3 | Tolerant of transient failures |
| **Ollama Local** | 2 | 10 | 1 | Fast failover (local expected fast) |
| **OpenRouter Cloud** | 10 | 60 | 3 | Tolerant of network issues |

---

## Provider Management

### Enabling/Disabling Providers

#### Enable Ollama Only (Cost Control)

```json
{
  "AI": {
    "Providers": {
      "Ollama": {
        "Enabled": true,
        "BaseUrl": "http://localhost:11434"
      },
      "OpenRouter": {
        "Enabled": false  // Disabled - no API costs
      }
    }
  }
}
```

**Result**: All users use Ollama free models, OpenRouter is not used.

**Restart Required**: No (change takes effect on next request)

#### Enable OpenRouter Only (Cloud Service)

```json
{
  "AI": {
    "Providers": {
      "Ollama": {
        "Enabled": false  // Disabled - no local dependency
      },
      "OpenRouter": {
        "Enabled": true,
        "BaseUrl": "https://openrouter.ai/api/v1"
      }
    }
  }
}
```

**Result**: All users use OpenRouter cloud models, no Ollama required.

**Use Case**: Production deployment without Ollama dependency.

---

### Preferred Provider Selection

#### Force All Users to Specific Provider

**Scenario**: Testing Ollama performance under production load

```json
{
  "AI": {
    "PreferredProvider": "Ollama",
    "Providers": {
      "Ollama": { "Enabled": true },
      "OpenRouter": { "Enabled": true }  // Still available for fallback
    }
  }
}
```

**Result**:
- All users (Anonymous/User/Editor/Admin) use Ollama
- User-tier routing is bypassed
- OpenRouter available as circuit breaker fallback

**Routing Decision**:
```
User: Admin (normally 80% OpenRouter in LlmRouting)
Selected: Ollama (llama3:8b)
Reason: "PreferredProvider override (AI:PreferredProvider = Ollama)"
```

#### Remove PreferredProvider (Use User-Tier Routing)

```json
{
  "AI": {
    "PreferredProvider": "",  // Empty = use LlmRouting
    "Providers": {
      "Ollama": { "Enabled": true },
      "OpenRouter": { "Enabled": true }
    }
  }
}
```

**Result**:
- Anonymous/User: ~90% Ollama free models
- Editor: ~50/50 split
- Admin: ~80% OpenRouter premium models

**Routing Decision**:
```
User: Admin
Selected: OpenRouter (80% probability)
Reason: "Admin tier routing (LlmRouting.AdminModel)"
```

---

### Fallback Chain Configuration

#### Default: Prefer Free Ollama, Fallback to Paid OpenRouter

```json
{
  "AI": {
    "FallbackChain": ["Ollama", "OpenRouter"],
    "Providers": {
      "Ollama": { "Enabled": true },
      "OpenRouter": { "Enabled": true }
    }
  }
}
```

**Behavior**:
- Primary: Ollama (free)
- If Ollama circuit opens → OpenRouter (paid)
- If both unavailable → service degraded

**Cost Impact**: Minimize paid API usage

#### Reverse: Prefer Reliable Cloud, Fallback to Local

```json
{
  "AI": {
    "FallbackChain": ["OpenRouter", "Ollama"],
    "Providers": {
      "Ollama": { "Enabled": true },
      "OpenRouter": { "Enabled": true }
    }
  }
}
```

**Behavior**:
- Primary: OpenRouter (cloud, reliable)
- If OpenRouter circuit opens → Ollama (local)
- If both unavailable → service degraded

**Reliability Impact**: Maximize uptime with cloud service

---

## Advanced Features

### API Key Configuration

**⚠️ CRITICAL**: API keys are **NOT** configured in `appsettings.json`. They are read from environment variables using the Docker Secrets pattern (SEC-708).

#### How It Works

The system reads API keys from environment variables in this priority order:

1. **`{KEY}_FILE`**: Path to a Docker secret file (production recommended)
2. **`{KEY}`**: Direct environment variable value (development only)

**Example for OpenRouter**:
- `OPENROUTER_API_KEY_FILE=/run/secrets/openrouter_api_key` (Docker Secrets)
- `OPENROUTER_API_KEY=sk-or-v1-abc123...` (Direct value)

#### Setup Methods

**Option 1: Docker Secrets (Production)**
```yaml
# docker-compose.yml
services:
  api:
    environment:
      - OPENROUTER_API_KEY_FILE=/run/secrets/openrouter_api_key
    secrets:
      - openrouter_api_key

secrets:
  openrouter_api_key:
    file: ./secrets/openrouter_api_key.txt
```

**Option 2: Environment Variables (Development)**

**Linux/macOS**:
```bash
export OPENROUTER_API_KEY="sk-or-v1-abc123..."
```

**Windows (PowerShell)**:
```powershell
$env:OPENROUTER_API_KEY = "sk-or-v1-abc123..."
```

**Docker Compose (.env file)**:
```bash
# .env file (DO NOT commit to Git)
OPENROUTER_API_KEY=sk-or-v1-abc123...
```

#### Verification

Check logs on startup to verify API key loading:

```
✅ Loaded secret 'OPENROUTER_API_KEY' from file: /run/secrets/openrouter_api_key (45 bytes)
```

Or for direct value:

```
⚠️  Loaded secret 'OPENROUTER_API_KEY' from direct configuration (not recommended for production)
```

#### Common Mistakes

❌ **WRONG**: Putting API key in `appsettings.json`
```json
{
  "AI": {
    "Providers": {
      "OpenRouter": {
        "ApiKey": "sk-or-v1-abc123..."  // ❌ This is ignored!
      }
    }
  }
}
```

✅ **CORRECT**: Using environment variable
```bash
export OPENROUTER_API_KEY="sk-or-v1-abc123..."
```

---

### Startup Validation

The system validates configuration on startup and **fails fast** with clear error messages if misconfigured.

#### Validation Rules (7 Total)

| Rule | Validation | Error Message |
|------|------------|---------------|
| **1** | At least one provider enabled | "At least one AI provider must be enabled" |
| **2** | PreferredProvider exists | "PreferredProvider 'X' not found in AI:Providers" |
| **3** | PreferredProvider enabled | "PreferredProvider 'X' is disabled" |
| **4** | FallbackChain providers exist | "FallbackChain provider 'X' not found" |
| **5** | FallbackChain providers enabled | "FallbackChain provider 'X' is disabled" |
| **6** | No FallbackChain duplicates | "FallbackChain contains duplicate providers" |
| **7** | Enabled providers have BaseUrl | "Provider 'X' is enabled but BaseUrl is empty" |

#### Example Validation Errors

**Error: All Providers Disabled**
```json
{
  "AI": {
    "Providers": {
      "Ollama": { "Enabled": false },
      "OpenRouter": { "Enabled": false }
    }
  }
}
```

**Startup Output**:
```
Microsoft.Extensions.Options.OptionsValidationException:
  At least one AI provider must be enabled
```

**Fix**: Enable at least one provider:
```json
{
  "AI": {
    "Providers": {
      "Ollama": { "Enabled": true },  // Fixed
      "OpenRouter": { "Enabled": false }
    }
  }
}
```

---

**Error: PreferredProvider Disabled**
```json
{
  "AI": {
    "PreferredProvider": "OpenRouter",
    "Providers": {
      "OpenRouter": { "Enabled": false }  // Conflict!
    }
  }
}
```

**Startup Output**:
```
Microsoft.Extensions.Options.OptionsValidationException:
  PreferredProvider 'OpenRouter' is disabled in AI:Providers
```

**Fix**: Enable the preferred provider:
```json
{
  "AI": {
    "PreferredProvider": "OpenRouter",
    "Providers": {
      "OpenRouter": { "Enabled": true }  // Fixed
    }
  }
}
```

---

**Error: Missing BaseUrl**
```json
{
  "AI": {
    "Providers": {
      "Ollama": {
        "Enabled": true,
        "BaseUrl": ""  // Invalid!
      }
    }
  }
}
```

**Startup Output**:
```
Microsoft.Extensions.Options.OptionsValidationException:
  Provider 'Ollama' is enabled but BaseUrl is empty
```

**Fix**: Provide valid BaseUrl:
```json
{
  "AI": {
    "Providers": {
      "Ollama": {
        "Enabled": true,
        "BaseUrl": "http://localhost:11434"  // Fixed
      }
    }
  }
}
```

---

### Circuit Breaker Tuning

#### Scenario: Ollama Local Instance (Fast Failover)

Ollama runs locally, so failures are immediate (not network latency). Use aggressive circuit breaker:

```json
{
  "AI": {
    "FallbackChain": ["Ollama", "OpenRouter"],
    "CircuitBreaker": {
      "FailureThreshold": 2,      // Open after 2 failures (Ollama should respond fast)
      "OpenDurationSeconds": 10,  // Retry after 10 seconds (quick recovery)
      "SuccessThreshold": 1       // Close after 1 success (Ollama stable when running)
    }
  }
}
```

**Rationale**:
- Ollama failures are immediate (process down, not network)
- Fast failover to OpenRouter minimizes user impact
- Quick recovery when Ollama restarts

---

#### Scenario: OpenRouter Cloud Service (Tolerant)

OpenRouter is a cloud service with potential network issues. Use conservative circuit breaker:

```json
{
  "AI": {
    "FallbackChain": ["OpenRouter", "Ollama"],
    "CircuitBreaker": {
      "FailureThreshold": 10,     // Tolerate transient network issues
      "OpenDurationSeconds": 60,  // Give time for cloud issues to resolve
      "SuccessThreshold": 3       // Require stable recovery before fully trusting
    }
  }
}
```

**Rationale**:
- Cloud services have transient network issues
- Higher threshold avoids premature failover
- Longer open duration allows cloud recovery
- Multiple successes ensure stability

---

## Configuration Examples

### Example 1: Development Environment (Ollama Only)

**Scenario**: Local development, minimize API costs

```json
{
  "AI": {
    "PreferredProvider": "Ollama",
    "Providers": {
      "Ollama": {
        "Enabled": true,
        "BaseUrl": "http://localhost:11434",
        "Models": ["llama3:8b", "mistral", "codellama:7b"],
        "HealthCheckIntervalSeconds": 30
      },
      "OpenRouter": {
        "Enabled": false
      }
    },
    "FallbackChain": ["Ollama"],
    "CircuitBreaker": {
      "FailureThreshold": 2,
      "OpenDurationSeconds": 10,
      "SuccessThreshold": 1
    }
  }
}
```

**Characteristics**:
- ✅ Zero API costs (Ollama free)
- ✅ Fast local responses
- ✅ Quick circuit breaker (Ollama should be stable)
- ❌ No cloud fallback (dev environment tolerance)

---

### Example 2: Production Environment (High Availability)

**Scenario**: Production deployment, prioritize uptime over cost

```json
{
  "AI": {
    "PreferredProvider": "",  // Use user-tier routing for flexibility
    "Providers": {
      "Ollama": {
        "Enabled": true,
        "BaseUrl": "http://meepleai-ollama:11434",  // Docker service name
        "Models": ["llama3:8b"],
        "HealthCheckIntervalSeconds": 60
      },
      "OpenRouter": {
        "Enabled": true,
        "BaseUrl": "https://openrouter.ai/api/v1",
        "Models": [
          "meta-llama/llama-3.3-70b-instruct:free",
          "anthropic/claude-3.5-haiku"
        ],
        "HealthCheckIntervalSeconds": 120
      }
    },
    "FallbackChain": ["Ollama", "OpenRouter"],  // Prefer free, fallback to reliable
    "CircuitBreaker": {
      "FailureThreshold": 5,
      "OpenDurationSeconds": 30,
      "SuccessThreshold": 2
    }
  }
}
```

**Characteristics**:
- ✅ High availability (dual provider)
- ✅ Cost optimization (prefer free Ollama)
- ✅ User-tier routing (Anonymous → free, Admin → premium)
- ✅ Balanced circuit breaker (tolerant but responsive)

---

### Example 3: Testing Environment (Preferred Provider Override)

**Scenario**: QA environment, test specific provider behavior

```json
{
  "AI": {
    "PreferredProvider": "OpenRouter",  // Force all users to OpenRouter
    "Providers": {
      "Ollama": {
        "Enabled": true  // Available for fallback
      },
      "OpenRouter": {
        "Enabled": true,
        "BaseUrl": "https://openrouter.ai/api/v1",
        "Models": ["openai/gpt-4o-mini"]  // Test model
      }
    },
    "FallbackChain": ["OpenRouter", "Ollama"],
    "CircuitBreaker": {
      "FailureThreshold": 3,
      "OpenDurationSeconds": 15,
      "SuccessThreshold": 1
    }
  }
}
```

**Characteristics**:
- ✅ All users forced to OpenRouter (bypass user-tier routing)
- ✅ Consistent test environment (same provider/model)
- ✅ Ollama fallback available (backup during testing)
- ✅ Sensitive circuit breaker (detect issues quickly)

---

### Example 4: Cost Emergency (OpenRouter Only)

**Scenario**: Ollama service down, emergency cloud-only mode

```json
{
  "AI": {
    "PreferredProvider": "OpenRouter",  // Force cloud provider
    "Providers": {
      "Ollama": {
        "Enabled": false  // Temporarily disabled (maintenance)
      },
      "OpenRouter": {
        "Enabled": true,
        "BaseUrl": "https://openrouter.ai/api/v1",
        "Models": [
          "meta-llama/llama-3.3-70b-instruct:free",  // Use free models
          "anthropic/claude-3.5-haiku"
        ]
      }
    },
    "FallbackChain": ["OpenRouter"],  // Single provider (no fallback)
    "CircuitBreaker": {
      "FailureThreshold": 10,
      "OpenDurationSeconds": 60,
      "SuccessThreshold": 3
    }
  }
}
```

**Characteristics**:
- ✅ Immediate failover to cloud (Ollama down)
- ✅ Use free OpenRouter models (minimize cost)
- ✅ No fallback (single provider mode)
- ⚠️ Higher latency (cloud vs local)
- ⚠️ Potential API costs (if free tier exhausted)

---

### Example 5: Performance Testing (Custom FallbackChain)

**Scenario**: Load testing, prefer high-performance provider

```json
{
  "AI": {
    "PreferredProvider": "",  // Use user-tier routing
    "Providers": {
      "Ollama": {
        "Enabled": true,
        "BaseUrl": "http://ollama-load-balanced:11434",  // Load balancer
        "Models": ["llama3:8b"],
        "HealthCheckIntervalSeconds": 15  // Frequent checks during test
      },
      "OpenRouter": {
        "Enabled": true,
        "BaseUrl": "https://openrouter.ai/api/v1",
        "Models": ["meta-llama/llama-3.3-70b-instruct:free"]
      }
    },
    "FallbackChain": ["OpenRouter", "Ollama"],  // Reversed: prefer cloud stability
    "CircuitBreaker": {
      "FailureThreshold": 15,  // Tolerate load test errors
      "OpenDurationSeconds": 5,  // Quick retry (load test scenario)
      "SuccessThreshold": 5  // Require stability under load
    }
  }
}
```

**Characteristics**:
- ✅ Load-balanced Ollama (horizontal scaling)
- ✅ Frequent health checks (detect issues fast)
- ✅ Prefer cloud stability (OpenRouter fallback first)
- ✅ High failure threshold (tolerate load test errors)

---

## Troubleshooting

### Startup Errors

#### Error: "At least one AI provider must be enabled"

**Symptom**: Application fails to start

**Cause**: Both providers have `Enabled = false`

**Solution**:
```json
{
  "AI": {
    "Providers": {
      "Ollama": { "Enabled": true },  // Enable at least one
      "OpenRouter": { "Enabled": false }
    }
  }
}
```

---

#### Error: "PreferredProvider 'X' not found in AI:Providers"

**Symptom**: Application fails to start

**Cause**: PreferredProvider references non-existent provider

**Solution**:
```json
{
  "AI": {
    "PreferredProvider": "Ollama",  // Must match Providers key
    "Providers": {
      "Ollama": { "Enabled": true },  // Key must exist
      "OpenRouter": { "Enabled": true }
    }
  }
}
```

---

#### Error: "FallbackChain contains duplicate providers"

**Symptom**: Application fails to start

**Cause**: Same provider listed multiple times in FallbackChain

**Solution**:
```json
{
  "AI": {
    "FallbackChain": ["Ollama", "OpenRouter"]  // No duplicates
    // WRONG: ["Ollama", "Ollama", "OpenRouter"]
  }
}
```

---

### Runtime Issues

#### Issue: PreferredProvider Not Working

**Symptom**: User-tier routing still active despite PreferredProvider set

**Diagnosis**:
1. Check provider exists: `AI:Providers:PreferredProvider` must exist
2. Check enabled flag: `AI:Providers:PreferredProvider:Enabled = true`
3. Check logs for routing decision reason

**Solution**:
```json
{
  "AI": {
    "PreferredProvider": "Ollama",
    "Providers": {
      "Ollama": {
        "Enabled": true,  // Must be enabled
        "BaseUrl": "http://localhost:11434"  // Must have BaseUrl
      }
    }
  }
}
```

**Verify in Logs**:
```
[Debug] Routing to PreferredProvider Ollama (llama3:8b) - overriding user-tier routing
```

---

#### Issue: Provider Disabled and No Fallback

**Symptom**: `InvalidOperationException: Provider X disabled and no enabled fallback found`

**Diagnosis**:
1. Primary provider disabled: `AI:Providers:X:Enabled = false`
2. Fallback provider also disabled or missing

**Solution** (enable fallback):
```json
{
  "AI": {
    "Providers": {
      "Ollama": { "Enabled": false },  // Primary disabled
      "OpenRouter": { "Enabled": true }  // Fallback enabled
    }
  }
}
```

**Verify in Logs**:
```
[Warning] Selected provider Ollama is disabled, trying fallback
[Info] Fallback to OpenRouter (gpt-4o-mini) - primary provider disabled
```

---

#### Issue: Circuit Breaker Stuck Open

**Symptom**: Provider not used even after recovery

**Diagnosis**:
1. Check `OpenDurationSeconds`: circuit may still be open
2. Check `SuccessThreshold`: not enough successes to close
3. Check provider health: still failing health checks

**Solution** (reduce OpenDurationSeconds):
```json
{
  "AI": {
    "CircuitBreaker": {
      "FailureThreshold": 5,
      "OpenDurationSeconds": 15,  // Reduced from 60
      "SuccessThreshold": 2
    }
  }
}
```

**Verify in Logs**:
```
[Info] Circuit breaker OPEN for Ollama (5 failures, will retry after 15s)
[Info] Circuit breaker HALF_OPEN for Ollama (testing recovery)
[Info] Circuit breaker CLOSED for Ollama (2 successes, back to normal)
```

---

#### Issue: API Key Not Loaded

**Symptom**: Application fails to start with error:
```
InvalidOperationException: OPENROUTER_API_KEY not configured.
Set either OPENROUTER_API_KEY or OPENROUTER_API_KEY_FILE environment variable.
```

**Diagnosis**:
1. Environment variable `OPENROUTER_API_KEY` not set
2. Docker secret file `OPENROUTER_API_KEY_FILE` not configured
3. Variable name mismatch (case-sensitive)

**Solution**:
```bash
# Linux/macOS
export OPENROUTER_API_KEY="sk-or-v1-abc123..."

# Windows PowerShell
$env:OPENROUTER_API_KEY = "sk-or-v1-abc123..."

# Docker Secrets
OPENROUTER_API_KEY_FILE=/run/secrets/openrouter_api_key

# Verify
echo $OPENROUTER_API_KEY
```

**Verify in Logs**:
```
✅ Loaded secret 'OPENROUTER_API_KEY' from file: /run/secrets/openrouter_api_key (45 bytes)
```

Or for direct value:
```
⚠️  Loaded secret 'OPENROUTER_API_KEY' from direct configuration (not recommended for production)
```

---

### Configuration Validation Checklist

Use this checklist to verify your configuration:

- [ ] **At least one provider enabled**
  ```json
  "Providers": { "Ollama": { "Enabled": true } }  // OR OpenRouter
  ```

- [ ] **PreferredProvider exists and enabled** (if set)
  ```json
  "PreferredProvider": "Ollama",
  "Providers": { "Ollama": { "Enabled": true } }
  ```

- [ ] **All FallbackChain providers exist and enabled**
  ```json
  "FallbackChain": ["Ollama", "OpenRouter"],
  "Providers": {
    "Ollama": { "Enabled": true },
    "OpenRouter": { "Enabled": true }
  }
  ```

- [ ] **Enabled providers have BaseUrl**
  ```json
  "Providers": {
    "Ollama": {
      "Enabled": true,
      "BaseUrl": "http://localhost:11434"  // Required
    }
  }
  ```

- [ ] **API keys in environment variables** (not in appsettings.json)
  ```bash
  export OPENROUTER_API_KEY="sk-or-v1-abc123..."
  # OR
  OPENROUTER_API_KEY_FILE=/run/secrets/openrouter_api_key
  ```

- [ ] **Circuit breaker values positive**
  ```json
  "CircuitBreaker": {
    "FailureThreshold": 5,      // > 0
    "OpenDurationSeconds": 30,  // > 0
    "SuccessThreshold": 2       // > 0
  }
  ```

---

## Best Practices

### Security

1. **Never commit API keys**
   ```bash
   # ✅ CORRECT: Use environment variables
   export OPENROUTER_API_KEY="sk-or-v1-abc123..."

   # ✅ CORRECT: Use Docker Secrets
   OPENROUTER_API_KEY_FILE=/run/secrets/openrouter_api_key
   ```

2. **Do NOT put API keys in appsettings.json**
   ```json
   // ❌ WRONG: This is completely ignored by the system!
   {
     "AI": {
       "Providers": {
         "OpenRouter": {
           "ApiKey": "sk-or-v1-abc123..."  // ❌ Never do this!
         }
       }
     }
   }
   ```

3. **Use environment-specific keys**
   ```bash
   # Development
   export OPENROUTER_API_KEY="sk-or-dev-..."

   # Production
   OPENROUTER_API_KEY_FILE=/run/secrets/openrouter_api_key
   ```

4. **Secure production secrets**
   - Use Azure Key Vault, AWS Secrets Manager, or HashiCorp Vault
   - Never store secrets in `appsettings.json`
   - Rotate API keys regularly

---

### Performance

1. **Adjust health check intervals**
   ```json
   // Development (fast feedback)
   "HealthCheckIntervalSeconds": 15

   // Production (reduce overhead)
   "HealthCheckIntervalSeconds": 120
   ```

2. **Tune circuit breaker for environment**
   - **Local Ollama**: Aggressive (low thresholds, short duration)
   - **Cloud OpenRouter**: Conservative (high thresholds, long duration)

3. **Use FallbackChain wisely**
   - **Cost Priority**: `["Ollama", "OpenRouter"]`
   - **Reliability Priority**: `["OpenRouter", "Ollama"]`

---

### Cost Optimization

1. **Prefer free models**
   ```json
   "Models": [
     "meta-llama/llama-3.3-70b-instruct:free",  // Free tier
     "anthropic/claude-3.5-haiku"  // Paid (fallback)
   ]
   ```

2. **Use PreferredProvider for cost control**
   ```json
   // Force Ollama during budget constraints
   "PreferredProvider": "Ollama"
   ```

3. **Monitor API usage**
   - Track OpenRouter API costs via logs
   - Set budget alerts in OpenRouter dashboard
   - Use LlmCostLog repository for cost analysis

---

### Operational

1. **Start with defaults, tune gradually**
   - Use default configuration initially
   - Monitor behavior in logs
   - Adjust circuit breaker based on observed failures

2. **Document changes**
   ```json
   {
     "AI": {
       "Comment": "2025-11-15: Disabled OpenRouter for cost control (Issue #123)"
     }
   }
   ```

3. **Test configuration changes in staging**
   - Never change production config without staging validation
   - Use PreferredProvider in staging to test specific providers

4. **Monitor logs during changes**
   ```bash
   # Watch routing decisions
   docker logs -f meepleai-api | grep "Routing to"

   # Watch circuit breaker events
   docker logs -f meepleai-api | grep "Circuit breaker"
   ```

---

### Backward Compatibility

**The AI:Provider configuration is fully backward compatible:**

1. **Missing AI section**: System uses existing `LlmRouting` configuration
2. **Empty PreferredProvider**: User-tier routing applies
3. **Both providers enabled**: Normal user-tier routing behavior

**No breaking changes** - existing deployments continue to work without modification.

---

## Related Documentation

- [Developer Integration Guide](../02-development/ai-provider-integration.md)
- [Circuit Breaker Implementation (BGAI-020)](../../claudedocs/bgai-020-implementation-summary.md)
- [Configuration Architecture (BGAI-021)](../../claudedocs/bgai-021-implementation-summary.md)
- [Service Integration (BGAI-022)](../../claudedocs/bgai-022-implementation-summary.md)

---

**Version**: 1.0
**Last Updated**: 2025-11-15
**Maintainer**: Engineering Team
