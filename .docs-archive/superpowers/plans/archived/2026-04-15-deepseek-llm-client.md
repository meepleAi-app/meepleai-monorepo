# DeepSeek LLM Client — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a native DeepSeek LLM client so the `Balanced` RAG strategy routes directly to DeepSeek API instead of failing with "DeepSeek provider not found" and falling through to Ollama/OpenRouter emergency fallback.

**Architecture:** DeepSeek uses an OpenAI-compatible API (`https://api.deepseek.com`). The new `DeepSeekLlmClient` implements `ILlmClient` following the same pattern as `OpenRouterLlmClient`. The response JSON models (`ChatChoice`, `ChatMessage`, `ChatUsage`, `StreamChoice`, `StreamDelta`) are identical to OpenAI's format — we reuse the existing records from `OpenRouterLlmClient.cs` by extracting them to a shared file.

**Tech Stack:** .NET 9, HttpClient, System.Text.Json, SSE streaming, SecretsHelper

---

## File Map

| File | Action | Responsibility |
|------|--------|----------------|
| `apps/api/src/Api/Services/LlmClients/DeepSeekLlmClient.cs` | Create | DeepSeek API client implementing ILlmClient |
| `apps/api/src/Api/Services/LlmClients/OpenAiChatModels.cs` | Create | Shared response models (ChatChoice, ChatMessage, ChatUsage, StreamChoice, StreamDelta) |
| `apps/api/src/Api/Services/LlmClients/OpenRouterLlmClient.cs` | Modify | Remove response model records (moved to shared file) |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/DependencyInjection/KnowledgeBaseServiceExtensions.cs` | Modify | Register DeepSeekLlmClient in DI |
| `apps/api/tests/Api.Tests/Services/LlmClients/DeepSeekLlmClientTests.cs` | Create | Unit tests for DeepSeek client |
| `infra/secrets/deepseek.secret.example` | Already created | Secret template |
| `infra/compose.staging.yml` | Already modified | env_file for deepseek.secret |
| `infra/compose.dev.yml` | Already modified | env_file for deepseek.secret |
| `infra/compose.prod.yml` | Already modified | env_file for deepseek.secret |

---

### Task 1: Extract shared OpenAI-compatible response models

The DeepSeek API returns the same JSON structure as OpenAI/OpenRouter. Extract the response records from `OpenRouterLlmClient.cs` into a shared file so both clients can use them.

**Files:**
- Create: `apps/api/src/Api/Services/LlmClients/OpenAiChatModels.cs`
- Modify: `apps/api/src/Api/Services/LlmClients/OpenRouterLlmClient.cs:491-572`

- [ ] **Step 1: Create the shared models file**

Create `apps/api/src/Api/Services/LlmClients/OpenAiChatModels.cs`:

```csharp
using System.Text.Json.Serialization;

namespace Api.Services.LlmClients;

/// <summary>
/// Shared OpenAI-compatible chat completion response models.
/// Used by OpenRouter, DeepSeek, and other OpenAI-compatible providers.
/// </summary>

internal record OpenAiChatResponse
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("choices")]
    public List<OpenAiChatChoice> Choices { get; init; } = new();

    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("usage")]
    public OpenAiChatUsage? Usage { get; init; }
}

internal record OpenAiChatChoice
{
    [JsonPropertyName("message")]
    public OpenAiChatMessage? Message { get; init; }

    [JsonPropertyName("finish_reason")]
    public string FinishReason { get; init; } = string.Empty;
}

internal record OpenAiChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;
}

internal record OpenAiChatUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; init; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; init; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; init; }
}

internal record OpenAiStreamChunk
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("choices")]
    public List<OpenAiStreamChoice>? Choices { get; init; }

    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("usage")]
    public OpenAiChatUsage? Usage { get; init; }
}

internal record OpenAiStreamChoice
{
    [JsonPropertyName("delta")]
    public OpenAiStreamDelta? Delta { get; init; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; init; }

    [JsonPropertyName("index")]
    public int Index { get; init; }
}

internal record OpenAiStreamDelta
{
    [JsonPropertyName("role")]
    public string? Role { get; init; }

    [JsonPropertyName("content")]
    public string? Content { get; init; }
}
```

- [ ] **Step 2: Update OpenRouterLlmClient to use shared models**

In `OpenRouterLlmClient.cs`, remove the record definitions at the bottom of the file (lines 491-572: `OpenRouterChatResponse`, `ChatChoice`, `ChatMessage`, `ChatUsage`, `OpenRouterStreamChunk`, `StreamChoice`, `StreamDelta`).

Then update all references in the file:
- `OpenRouterChatResponse` → `OpenAiChatResponse`
- `ChatChoice` → `OpenAiChatChoice`
- `ChatMessage` → `OpenAiChatMessage`
- `ChatUsage` → `OpenAiChatUsage`
- `OpenRouterStreamChunk` → `OpenAiStreamChunk`
- `StreamChoice` → `OpenAiStreamChoice`
- `StreamDelta` → `OpenAiStreamDelta`

- [ ] **Step 3: Verify backend builds**

```bash
cd apps/api/src/Api && dotnet build
```
Expected: Build succeeds with no errors.

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Services/LlmClients/OpenAiChatModels.cs apps/api/src/Api/Services/LlmClients/OpenRouterLlmClient.cs
git commit -m "refactor(llm): extract shared OpenAI-compatible response models"
```

---

### Task 2: Implement DeepSeekLlmClient

**Files:**
- Create: `apps/api/src/Api/Services/LlmClients/DeepSeekLlmClient.cs`

- [ ] **Step 1: Create the DeepSeek client**

Create `apps/api/src/Api/Services/LlmClients/DeepSeekLlmClient.cs`. Follow the `OpenRouterLlmClient` pattern exactly but with these differences:

- `ProviderName` → `"DeepSeek"`
- Base URL → `https://api.deepseek.com/`
- API key env var → `DEEPSEEK_API_KEY` (via `SecretsHelper.GetSecretOrValue`, `required: false`)
- No `HTTP-Referer` header (not needed)
- `SupportsModel` → returns true for models starting with `"deepseek-"` (e.g. `deepseek-chat`, `deepseek-coder`, `deepseek-reasoner`)
- Health check → `GET models` (same as OpenRouter)
- `CreateChatRequest` → same OpenAI format, but NO `usage.include` field for streaming (DeepSeek includes usage in the final chunk by default)
- If `DEEPSEEK_API_KEY` is not configured, constructor should log a warning and set a `_isConfigured = false` flag. All methods should return failure/yield break when `!_isConfigured`.
- Include the same 429 retry logic as `OpenRouterLlmClient` (retry loop in `GenerateCompletionStreamAsync`)
- Use `OpenAiChatResponse`, `OpenAiStreamChunk`, etc. from the shared models file

```csharp
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Helpers;
using Api.Infrastructure;
using Api.Infrastructure.Security;
using Api.Models;

namespace Api.Services.LlmClients;

/// <summary>
/// LLM client for DeepSeek API (https://api.deepseek.com)
/// OpenAI-compatible API with cost-efficient models for RAG.
/// </summary>
internal class DeepSeekLlmClient : ILlmClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DeepSeekLlmClient> _logger;
    private readonly ILlmCostCalculator _costCalculator;
    private readonly bool _isConfigured;

    private const int DefaultTimeoutSeconds = 60;

    public string ProviderName => "DeepSeek";

    public DeepSeekLlmClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILlmCostCalculator costCalculator,
        ILogger<DeepSeekLlmClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("DeepSeek");
        _logger = logger;
        _costCalculator = costCalculator;

#pragma warning disable S1075
        const string DeepSeekApiBaseUrl = "https://api.deepseek.com/";
#pragma warning restore S1075

        var apiKey = SecretsHelper.GetSecretOrValue(config, "DEEPSEEK_API_KEY", logger, required: false);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("DEEPSEEK_API_KEY not configured — DeepSeek provider disabled");
            _isConfigured = false;
            return;
        }

        _isConfigured = true;
        _httpClient.BaseAddress = new Uri(DeepSeekApiBaseUrl);
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        _httpClient.Timeout = TimeSpan.FromSeconds(DefaultTimeoutSeconds);

        _logger.LogInformation("DeepSeekLlmClient initialized");
    }

    // ... implement GenerateCompletionAsync, GenerateCompletionStreamAsync,
    //     SupportsModel, CheckHealthAsync following OpenRouterLlmClient pattern
    //     but using OpenAi* shared models and DeepSeek-specific behavior
}
```

The full implementation should be ~350 lines, mirroring OpenRouterLlmClient but simpler (no HTTP-Referer, no OpenRouterErrorParser, no `usage.include` in request).

Key differences from OpenRouter:
- `SupportsModel`: `return modelId.StartsWith("deepseek-", StringComparison.OrdinalIgnoreCase);`
- `CreateChatRequest`: No `usage` field in payload (DeepSeek includes it by default)
- `HandleCompletionResponseAsync`: No `OpenRouterErrorParser` — just log error and return failure
- Retry logic: Same 429 retry pattern as OpenRouter (3 retries, 3s/5s/10s)

- [ ] **Step 2: Verify backend builds**

```bash
cd apps/api/src/Api && dotnet build
```

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/Services/LlmClients/DeepSeekLlmClient.cs
git commit -m "feat(llm): add DeepSeek LLM client with OpenAI-compatible API (#419)"
```

---

### Task 3: Register DeepSeekLlmClient in DI

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/DependencyInjection/KnowledgeBaseServiceExtensions.cs:188-189`

- [ ] **Step 1: Add DI registration**

In `KnowledgeBaseServiceExtensions.cs`, after line 189 (`services.AddSingleton<ILlmClient, OpenRouterLlmClient>();`), add:

```csharp
services.AddSingleton<ILlmClient, DeepSeekLlmClient>();
```

- [ ] **Step 2: Verify backend builds**

```bash
cd apps/api/src/Api && dotnet build
```

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/DependencyInjection/KnowledgeBaseServiceExtensions.cs
git commit -m "feat(llm): register DeepSeekLlmClient in DI container (#419)"
```

---

### Task 4: Write unit tests

**Files:**
- Create: `apps/api/tests/Api.Tests/Services/LlmClients/DeepSeekLlmClientTests.cs`

- [ ] **Step 1: Write tests**

Test the following scenarios:
1. `Constructor_WithoutApiKey_DisablesClient` — no DEEPSEEK_API_KEY → `_isConfigured = false`
2. `GenerateCompletionAsync_WhenNotConfigured_ReturnsFailure` — returns failure result
3. `GenerateCompletionStreamAsync_WhenNotConfigured_YieldsNothing` — yields 0 chunks
4. `SupportsModel_DeepSeekModels_ReturnsTrue` — `deepseek-chat`, `deepseek-coder` return true
5. `SupportsModel_NonDeepSeekModels_ReturnsFalse` — `gpt-4o`, `llama-3.3` return false
6. `ProviderName_ReturnsDeepSeek` — `ProviderName == "DeepSeek"`

Use the same testing patterns as `apps/api/tests/Api.Tests/Services/PasswordHashingServiceTests.cs` or other service tests in the project. Use `Mock<IHttpClientFactory>`, `Mock<IConfiguration>`, etc.

- [ ] **Step 2: Run tests**

```bash
cd apps/api/src/Api && dotnet test --filter "DeepSeekLlmClient"
```
Expected: All 6 tests pass.

- [ ] **Step 3: Commit**

```bash
git add apps/api/tests/Api.Tests/Services/LlmClients/DeepSeekLlmClientTests.cs
git commit -m "test(llm): add DeepSeek client unit tests (#419)"
```

---

### Task 5: Commit infra changes and verify

**Files:**
- `infra/secrets/deepseek.secret.example` (already created)
- `infra/compose.staging.yml` (already modified)
- `infra/compose.dev.yml` (already modified)
- `infra/compose.prod.yml` (already modified)

- [ ] **Step 1: Commit infra changes**

```bash
git add infra/secrets/deepseek.secret.example infra/compose.staging.yml infra/compose.dev.yml infra/compose.prod.yml
git commit -m "feat(infra): add DeepSeek secret template and compose env_file (#419)"
```

- [ ] **Step 2: Run full backend build + test**

```bash
cd apps/api/src/Api && dotnet build && dotnet test
```
Expected: All tests pass, including new DeepSeek tests.

---

### Task 6: Deploy and E2E verify

- [ ] **Step 1: Push to staging, copy secret, deploy**

```bash
# Push
git push -u origin feature/issue-419-deepseek-llm-client

# Copy secret to staging
scp -i ~/.ssh/meepleai-staging infra/secrets/deepseek.secret deploy@204.168.135.69:/opt/meepleai/repo/infra/secrets/

# Deploy API
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 "cd /opt/meepleai/repo && git fetch origin && git checkout feature/issue-419-deepseek-llm-client && git pull && cd infra && docker compose -f docker-compose.yml -f compose.staging.yml -f compose.traefik.yml up -d --build api"
```

- [ ] **Step 2: Verify DeepSeek client initialized**

```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 "docker logs meepleai-api 2>&1 | grep -i 'DeepSeek'"
```
Expected: `DeepSeekLlmClient initialized`

- [ ] **Step 3: Verify routing selects DeepSeek for Balanced strategy**

```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 "docker logs meepleai-api 2>&1 | grep 'Starting streaming completion via'"
```
Expected: `Starting streaming completion via DeepSeek (deepseek-chat)`

- [ ] **Step 4: E2E test via browser**

Navigate to `https://meepleai.app/chat/new?gameId=23118677-d42f-4b66-8d25-4f0d1471c630` (Ark Nova, has embeddings).
Select Auto agent → Start chat → Send "spiegami il setup del gioco".
Expected: Streaming AI response with text tokens and citations.
