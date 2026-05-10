# Provider Token Observability — PR1 Implementation Plan (G1 + G3)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Espone `POST /api/v1/admin/providers/{name}/probe` per validare on-demand i token configurati dei provider LLM, con audit log e rate limit, riusando i client esistenti.

**Architecture:** Estensione `Administration` BC. Domain entity `ProviderProbeAuditEntry` + repository EF Core. Application: `ProbeProviderCommand` (MediatR) coordina executor strategy per provider, scrive audit, applica rate limit named policy. Probe usa list-models endpoint (zero quota cost). Token mai esposto in response (solo SHA256 fingerprint primi 8 char).

**Tech Stack:** .NET 9, ASP.NET Minimal APIs, MediatR, EF Core (PostgreSQL 16), FluentValidation, xUnit + Testcontainers, WireMock.Net per upstream stub.

**Spec source:** `docs/for-developers/specs/2026-05-10-provider-token-observability-design.md`

**Out of scope per PR1:** quota endpoint (PR2), Prometheus metric (PR2), AlertRule template (PR2), frontend UI (PR3), `?deep=true` chat-completion probe (futuro).

---

## File Structure

### Created files

| Path | Responsibility |
|------|----------------|
| `apps/api/src/Api/BoundedContexts/Administration/Domain/Aggregates/ProviderProbeAudit/ProviderProbeAuditEntry.cs` | Aggregate root: factory `Create()`, immutable post-construction |
| `apps/api/src/Api/BoundedContexts/Administration/Domain/Aggregates/ProviderProbeAudit/ProbeOutcome.cs` | Enum: Success, Unauthorized, Timeout, Unreachable, NotConfigured, ModelMissing |
| `apps/api/src/Api/BoundedContexts/Administration/Domain/Repositories/IProviderProbeAuditRepository.cs` | Interface (Add, GetRecent, DeleteOlderThan) |
| `apps/api/src/Api/BoundedContexts/Administration/Domain/Services/IProviderProbeService.cs` | Interface: `ProbeAsync(string providerName, Guid actorId, CancellationToken)` |
| `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ProbeProviderCommand.cs` | `internal record ProbeProviderCommand(string ProviderName) : ICommand<ProviderProbeResultDto>` |
| `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ProbeProviderCommandValidator.cs` | FluentValidation: provider name allowlist |
| `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ProbeProviderCommandHandler.cs` | Handler MediatR: orchestrazione + audit |
| `apps/api/src/Api/BoundedContexts/Administration/Infrastructure/Repositories/ProviderProbeAuditRepository.cs` | EF Core impl |
| `apps/api/src/Api/BoundedContexts/Administration/Infrastructure/Configurations/ProviderProbeAuditEntryConfiguration.cs` | EF Core entity config (table, indexes) |
| `apps/api/src/Api/BoundedContexts/Administration/Infrastructure/Services/ProviderProbeService.cs` | Domain service impl: dispatcha executor, scrive audit |
| `apps/api/src/Api/Services/Providers/Probe/IProviderProbeExecutor.cs` | Interface esecuzione list-models per provider |
| `apps/api/src/Api/Services/Providers/Probe/ProviderProbeExecutorFactory.cs` | Factory: provider name → executor |
| `apps/api/src/Api/Services/Providers/Probe/OpenRouterProbeExecutor.cs` | List models via OpenRouter `/api/v1/models` |
| `apps/api/src/Api/Services/Providers/Probe/OpenAiProbeExecutor.cs` | List models via OpenAI `/v1/models` |
| `apps/api/src/Api/Services/Providers/Probe/DeepSeekProbeExecutor.cs` | List models via DeepSeek `/v1/models` |
| `apps/api/src/Api/Services/Providers/Probe/OllamaProbeExecutor.cs` | List models locale via Ollama `/api/tags` (no auth) |
| `apps/api/src/Api/Services/Providers/Probe/TokenFingerprint.cs` | `SHA256(token)[0..8]` static helper |
| `apps/api/src/Api/Services/Providers/Probe/ProbeExecutionResult.cs` | Internal value object risultato executor |
| `apps/api/src/Api/Models/ProviderProbeResultDto.cs` | DTO API response |
| `apps/api/src/Api/Routing/AdminProviderEndpoints.cs` | Endpoint registration |
| `apps/api/src/Api/BoundedContexts/Administration/Infrastructure/BackgroundJobs/ProbeAuditRetentionJob.cs` | Hosted service: cleanup > N giorni |
| `apps/api/src/Api/Migrations/20260510_AddProviderProbeAuditEntries.cs` | EF migration |
| `tests/Api.Tests/BoundedContexts/Administration/Domain/ProviderProbeAuditEntryTests.cs` | Unit test entity |
| `tests/Api.Tests/BoundedContexts/Administration/Application/ProbeProviderCommandHandlerTests.cs` | Unit test handler |
| `tests/Api.Tests/Services/Providers/Probe/TokenFingerprintTests.cs` | Unit test helper |
| `tests/Api.Tests/Services/Providers/Probe/ProviderProbeExecutorFactoryTests.cs` | Unit test factory |
| `tests/Api.Tests/Integration/Routing/AdminProviderEndpointsTests.cs` | Integration test (Testcontainers + WireMock) |

### Modified files

| Path | Change |
|------|--------|
| `apps/api/src/Api/Infrastructure/Persistence/MeepleDbContext.cs` | Add `DbSet<ProviderProbeAuditEntry>` + apply config |
| `apps/api/src/Api/Extensions/RateLimitingServiceExtensions.cs` | Add policy `AdminProviderProbe` (10/min per user) + `AdminProviderProbeGlobal` (60/h per provider) |
| `apps/api/src/Api/Extensions/InfrastructureServiceExtensions.cs` | Register `IProviderProbeService`, `IProviderProbeExecutor` factory, `IProviderProbeAuditRepository`, hosted retention job |
| `apps/api/src/Api/Program.cs` | Map `AdminProviderEndpoints` |
| `apps/api/src/Api/appsettings.json` | Add `Administration:ProbeAuditRetentionDays = 365` |

---

## Branch setup

- [ ] **Step 0.1: Switch to main-dev and pull**

```bash
git checkout main-dev && git pull --ff-only
```

- [ ] **Step 0.2: Create feature branch**

```bash
git checkout -b feature/issue-NEW1-provider-probe-endpoint
git config branch.feature/issue-NEW1-provider-probe-endpoint.parent main-dev
```

Expected: clean branch from latest `main-dev`. **MUST verify with `git branch --show-current` returns `feature/issue-NEW1-provider-probe-endpoint`.**

---

## Task 1: Domain enum `ProbeOutcome`

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Administration/Domain/Aggregates/ProviderProbeAudit/ProbeOutcome.cs`

- [ ] **Step 1.1: Create enum**

```csharp
namespace Api.BoundedContexts.Administration.Domain.Aggregates.ProviderProbeAudit;

/// <summary>
/// Outcome of a provider token probe attempt. ISSUE-NEW1.
/// </summary>
internal enum ProbeOutcome
{
    Success = 0,
    NotConfigured = 1,    // env var missing or empty
    Unauthorized = 2,     // provider returned 401/403
    Timeout = 3,          // probe exceeded 5s
    Unreachable = 4,      // DNS / network failure
    ModelMissing = 5,     // configured model not in list
    UnknownError = 99
}
```

- [ ] **Step 1.2: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Administration/Domain/Aggregates/ProviderProbeAudit/ProbeOutcome.cs
git commit -m "feat(admin): add ProbeOutcome enum for provider token probe"
```

---

## Task 2: Domain entity `ProviderProbeAuditEntry`

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Administration/Domain/Aggregates/ProviderProbeAudit/ProviderProbeAuditEntry.cs`
- Create: `tests/Api.Tests/BoundedContexts/Administration/Domain/ProviderProbeAuditEntryTests.cs`

- [ ] **Step 2.1: Write failing tests**

```csharp
// tests/Api.Tests/BoundedContexts/Administration/Domain/ProviderProbeAuditEntryTests.cs
using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderProbeAudit;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Domain;

public class ProviderProbeAuditEntryTests
{
    [Fact]
    public void Create_ValidArgs_SetsAllFields()
    {
        var actorId = Guid.NewGuid();
        var entry = ProviderProbeAuditEntry.Create(
            providerName: "openrouter",
            actorId: actorId,
            tokenFingerprint: "abcd1234",
            outcome: ProbeOutcome.Success,
            errorCode: null,
            latencyMs: 250);

        entry.Id.Should().NotBeEmpty();
        entry.ProviderName.Should().Be("openrouter");
        entry.ActorId.Should().Be(actorId);
        entry.TokenFingerprint.Should().Be("abcd1234");
        entry.Outcome.Should().Be(ProbeOutcome.Success);
        entry.ErrorCode.Should().BeNull();
        entry.LatencyMs.Should().Be(250);
        entry.ProbedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_InvalidProviderName_Throws(string? name)
    {
        var act = () => ProviderProbeAuditEntry.Create(
            providerName: name!,
            actorId: Guid.NewGuid(),
            tokenFingerprint: "abcd1234",
            outcome: ProbeOutcome.Success,
            errorCode: null,
            latencyMs: 100);

        act.Should().Throw<ArgumentException>().WithParameterName("providerName");
    }

    [Theory]
    [InlineData("abc")]              // troppo corto
    [InlineData("abcd12345")]        // troppo lungo
    [InlineData("ABCD1234")]         // uppercase non permesso
    [InlineData("xyz!1234")]         // non-hex
    public void Create_InvalidFingerprint_Throws(string fp)
    {
        var act = () => ProviderProbeAuditEntry.Create(
            "openrouter", Guid.NewGuid(), fp, ProbeOutcome.Success, null, 100);

        act.Should().Throw<ArgumentException>().WithParameterName("tokenFingerprint");
    }

    [Fact]
    public void Create_NegativeLatency_Throws()
    {
        var act = () => ProviderProbeAuditEntry.Create(
            "openrouter", Guid.NewGuid(), "abcd1234", ProbeOutcome.Success, null, -1);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("latencyMs");
    }

    [Fact]
    public void Create_NotConfigured_AllowsNullFingerprint()
    {
        var entry = ProviderProbeAuditEntry.Create(
            "anthropic", Guid.NewGuid(), tokenFingerprint: null,
            ProbeOutcome.NotConfigured, errorCode: "not_configured", latencyMs: 0);

        entry.TokenFingerprint.Should().BeNull();
        entry.Outcome.Should().Be(ProbeOutcome.NotConfigured);
    }
}
```

- [ ] **Step 2.2: Run tests to verify FAIL**

```bash
cd tests/Api.Tests && dotnet test --filter "FullyQualifiedName~ProviderProbeAuditEntryTests"
```

Expected: FAIL — type `ProviderProbeAuditEntry` not defined.

- [ ] **Step 2.3: Implement entity**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Domain/Aggregates/ProviderProbeAudit/ProviderProbeAuditEntry.cs
using System.Text.RegularExpressions;

namespace Api.BoundedContexts.Administration.Domain.Aggregates.ProviderProbeAudit;

/// <summary>
/// Audit entry for a provider token probe attempt. ISSUE-NEW1 (G3).
/// Immutable after creation. Token never stored — only 8-char SHA256 fingerprint.
/// </summary>
internal sealed class ProviderProbeAuditEntry
{
    private static readonly Regex FingerprintRegex = new("^[a-f0-9]{8}$", RegexOptions.Compiled);

    public Guid Id { get; private set; }
    public string ProviderName { get; private set; } = null!;
    public Guid ActorId { get; private set; }
    public string? TokenFingerprint { get; private set; }
    public ProbeOutcome Outcome { get; private set; }
    public string? ErrorCode { get; private set; }
    public int LatencyMs { get; private set; }
    public DateTime ProbedAt { get; private set; }

    private ProviderProbeAuditEntry() { } // EF

    public static ProviderProbeAuditEntry Create(
        string providerName,
        Guid actorId,
        string? tokenFingerprint,
        ProbeOutcome outcome,
        string? errorCode,
        int latencyMs)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("Provider name required", nameof(providerName));
        if (latencyMs < 0)
            throw new ArgumentOutOfRangeException(nameof(latencyMs), "Latency must be non-negative");
        if (tokenFingerprint is not null && !FingerprintRegex.IsMatch(tokenFingerprint))
            throw new ArgumentException("Fingerprint must be 8 lowercase hex chars", nameof(tokenFingerprint));

        return new ProviderProbeAuditEntry
        {
            Id = Guid.NewGuid(),
            ProviderName = providerName,
            ActorId = actorId,
            TokenFingerprint = tokenFingerprint,
            Outcome = outcome,
            ErrorCode = errorCode,
            LatencyMs = latencyMs,
            ProbedAt = DateTime.UtcNow
        };
    }
}
```

- [ ] **Step 2.4: Run tests to verify PASS**

```bash
dotnet test --filter "FullyQualifiedName~ProviderProbeAuditEntryTests"
```

Expected: 8 tests PASS.

- [ ] **Step 2.5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Administration/Domain/Aggregates/ProviderProbeAudit/ProviderProbeAuditEntry.cs tests/Api.Tests/BoundedContexts/Administration/Domain/ProviderProbeAuditEntryTests.cs
git commit -m "feat(admin): add ProviderProbeAuditEntry domain entity with TDD"
```

---

## Task 3: Token fingerprint helper

**Files:**
- Create: `apps/api/src/Api/Services/Providers/Probe/TokenFingerprint.cs`
- Create: `tests/Api.Tests/Services/Providers/Probe/TokenFingerprintTests.cs`

- [ ] **Step 3.1: Write failing tests**

```csharp
using Api.Services.Providers.Probe;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Services.Providers.Probe;

public class TokenFingerprintTests
{
    [Fact]
    public void Compute_KnownToken_ReturnsExpectedHash()
    {
        // SHA256("hello") = 2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824 → first 8 = "2cf24dba"
        TokenFingerprint.Compute("hello").Should().Be("2cf24dba");
    }

    [Fact]
    public void Compute_SameToken_Deterministic()
    {
        var a = TokenFingerprint.Compute("sk-or-v1-abcdef");
        var b = TokenFingerprint.Compute("sk-or-v1-abcdef");
        a.Should().Be(b);
    }

    [Fact]
    public void Compute_DifferentTokens_DifferentFingerprints()
    {
        TokenFingerprint.Compute("token-a").Should().NotBe(TokenFingerprint.Compute("token-b"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Compute_EmptyOrNull_ReturnsNull(string? token)
    {
        TokenFingerprint.Compute(token).Should().BeNull();
    }

    [Fact]
    public void Compute_ResultIsLowercaseHexLength8()
    {
        var fp = TokenFingerprint.Compute("any");
        fp.Should().MatchRegex("^[a-f0-9]{8}$");
    }
}
```

- [ ] **Step 3.2: Run tests — FAIL**

```bash
dotnet test --filter "FullyQualifiedName~TokenFingerprintTests"
```

Expected: FAIL — type not defined.

- [ ] **Step 3.3: Implement**

```csharp
// apps/api/src/Api/Services/Providers/Probe/TokenFingerprint.cs
using System.Security.Cryptography;
using System.Text;

namespace Api.Services.Providers.Probe;

/// <summary>
/// Computes SHA256(token) and returns first 8 lowercase hex chars.
/// Rationale: 32-bit space &gt;&gt; expected token count (&lt;50) → collision risk negligible.
/// 8 chars enough for audit "is it the same token?" without enabling brute-force.
/// </summary>
internal static class TokenFingerprint
{
    public static string? Compute(string? token)
    {
        if (string.IsNullOrEmpty(token)) return null;

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        var sb = new StringBuilder(8);
        for (int i = 0; i < 4; i++) sb.Append(bytes[i].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        return sb.ToString();
    }
}
```

- [ ] **Step 3.4: Run tests — PASS**

```bash
dotnet test --filter "FullyQualifiedName~TokenFingerprintTests"
```

Expected: 5 tests PASS.

- [ ] **Step 3.5: Commit**

```bash
git add apps/api/src/Api/Services/Providers/Probe/TokenFingerprint.cs tests/Api.Tests/Services/Providers/Probe/TokenFingerprintTests.cs
git commit -m "feat(admin): add TokenFingerprint helper (SHA256 first 8 hex)"
```

---

## Task 4: Probe executor interface + value object

**Files:**
- Create: `apps/api/src/Api/Services/Providers/Probe/IProviderProbeExecutor.cs`
- Create: `apps/api/src/Api/Services/Providers/Probe/ProbeExecutionResult.cs`

- [ ] **Step 4.1: Create value object**

```csharp
// apps/api/src/Api/Services/Providers/Probe/ProbeExecutionResult.cs
using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderProbeAudit;

namespace Api.Services.Providers.Probe;

internal sealed record ProbeExecutionResult(
    ProbeOutcome Outcome,
    string? ErrorCode,
    string? ErrorMessage,
    int LatencyMs,
    bool ModelAvailable);
```

- [ ] **Step 4.2: Create interface**

```csharp
// apps/api/src/Api/Services/Providers/Probe/IProviderProbeExecutor.cs
namespace Api.Services.Providers.Probe;

/// <summary>
/// Strategy for executing a list-models probe against a specific provider.
/// Implementations must enforce 5s hard timeout and never throw on network failures.
/// </summary>
internal interface IProviderProbeExecutor
{
    string ProviderName { get; }
    Task<ProbeExecutionResult> ExecuteAsync(string apiKey, string? expectedModel, CancellationToken cancellationToken);
}
```

- [ ] **Step 4.3: Commit**

```bash
git add apps/api/src/Api/Services/Providers/Probe/IProviderProbeExecutor.cs apps/api/src/Api/Services/Providers/Probe/ProbeExecutionResult.cs
git commit -m "feat(admin): add IProviderProbeExecutor strategy interface"
```

---

## Task 5: OpenRouter probe executor

**Files:**
- Create: `apps/api/src/Api/Services/Providers/Probe/OpenRouterProbeExecutor.cs`

- [ ] **Step 5.1: Implement**

```csharp
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderProbeAudit;
using Microsoft.Extensions.Logging;

namespace Api.Services.Providers.Probe;

internal sealed class OpenRouterProbeExecutor : IProviderProbeExecutor
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenRouterProbeExecutor> _logger;

    public string ProviderName => "openrouter";

    public OpenRouterProbeExecutor(IHttpClientFactory httpClientFactory, ILogger<OpenRouterProbeExecutor> logger)
    {
        _httpClient = httpClientFactory.CreateClient("provider-probe");
        _logger = logger;
    }

    public async Task<ProbeExecutionResult> ExecuteAsync(string apiKey, string? expectedModel, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(ProbeTimeout);

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://openrouter.ai/api/v1/models");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var resp = await _httpClient.SendAsync(req, cts.Token).ConfigureAwait(false);
            sw.Stop();

            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized
                || resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
                return new ProbeExecutionResult(ProbeOutcome.Unauthorized, "unauthorized", "Provider rejected token", (int)sw.ElapsedMilliseconds, false);

            if (!resp.IsSuccessStatusCode)
                return new ProbeExecutionResult(ProbeOutcome.UnknownError, "http_" + (int)resp.StatusCode, $"HTTP {resp.StatusCode}", (int)sw.ElapsedMilliseconds, false);

            var payload = await resp.Content.ReadFromJsonAsync<ModelListResponse>(cancellationToken: cts.Token).ConfigureAwait(false);
            var modelAvailable = expectedModel is null
                || (payload?.Data?.Any(m => string.Equals(m.Id, expectedModel, StringComparison.Ordinal)) ?? false);

            var outcome = modelAvailable ? ProbeOutcome.Success : ProbeOutcome.ModelMissing;
            var errorCode = modelAvailable ? null : "model_missing";
            return new ProbeExecutionResult(outcome, errorCode, null, (int)sw.ElapsedMilliseconds, modelAvailable);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            return new ProbeExecutionResult(ProbeOutcome.Timeout, "timeout", "Probe exceeded 5s", (int)sw.ElapsedMilliseconds, false);
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "OpenRouter probe network failure");
            return new ProbeExecutionResult(ProbeOutcome.Unreachable, "unreachable", "Network error", (int)sw.ElapsedMilliseconds, false);
        }
    }

    private sealed record ModelListResponse([property: JsonPropertyName("data")] List<ModelEntry>? Data);
    private sealed record ModelEntry([property: JsonPropertyName("id")] string Id);
}
```

- [ ] **Step 5.2: Commit**

```bash
git add apps/api/src/Api/Services/Providers/Probe/OpenRouterProbeExecutor.cs
git commit -m "feat(admin): add OpenRouterProbeExecutor with 5s timeout"
```

---

## Task 6: OpenAI / DeepSeek / Ollama probe executors

**Files:**
- Create: `apps/api/src/Api/Services/Providers/Probe/OpenAiProbeExecutor.cs`
- Create: `apps/api/src/Api/Services/Providers/Probe/DeepSeekProbeExecutor.cs`
- Create: `apps/api/src/Api/Services/Providers/Probe/OllamaProbeExecutor.cs`

- [ ] **Step 6.1: Implement OpenAI executor**

Mirror `OpenRouterProbeExecutor` con queste differenze:
- `ProviderName => "openai"`
- URL: `https://api.openai.com/v1/models`
- Header `Authorization: Bearer {apiKey}` identico
- Stesso parsing JSON (`{ "data": [{ "id": "..." }] }`)

```csharp
// apps/api/src/Api/Services/Providers/Probe/OpenAiProbeExecutor.cs
// (struttura identica a OpenRouterProbeExecutor, cambia solo URL e ProviderName)
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderProbeAudit;
using Microsoft.Extensions.Logging;

namespace Api.Services.Providers.Probe;

internal sealed class OpenAiProbeExecutor : IProviderProbeExecutor
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAiProbeExecutor> _logger;
    public string ProviderName => "openai";

    public OpenAiProbeExecutor(IHttpClientFactory f, ILogger<OpenAiProbeExecutor> l)
    {
        _httpClient = f.CreateClient("provider-probe");
        _logger = l;
    }

    public async Task<ProbeExecutionResult> ExecuteAsync(string apiKey, string? expectedModel, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(ProbeTimeout);
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            using var resp = await _httpClient.SendAsync(req, cts.Token).ConfigureAwait(false);
            sw.Stop();
            if (resp.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                return new(ProbeOutcome.Unauthorized, "unauthorized", "Provider rejected token", (int)sw.ElapsedMilliseconds, false);
            if (!resp.IsSuccessStatusCode)
                return new(ProbeOutcome.UnknownError, "http_" + (int)resp.StatusCode, $"HTTP {resp.StatusCode}", (int)sw.ElapsedMilliseconds, false);
            var payload = await resp.Content.ReadFromJsonAsync<ModelListResponse>(cancellationToken: cts.Token).ConfigureAwait(false);
            var modelAvailable = expectedModel is null || (payload?.Data?.Any(m => m.Id == expectedModel) ?? false);
            return new(modelAvailable ? ProbeOutcome.Success : ProbeOutcome.ModelMissing,
                       modelAvailable ? null : "model_missing", null, (int)sw.ElapsedMilliseconds, modelAvailable);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !ct.IsCancellationRequested)
        { sw.Stop(); return new(ProbeOutcome.Timeout, "timeout", "Probe exceeded 5s", (int)sw.ElapsedMilliseconds, false); }
        catch (HttpRequestException ex)
        { sw.Stop(); _logger.LogWarning(ex, "OpenAI probe network failure"); return new(ProbeOutcome.Unreachable, "unreachable", "Network error", (int)sw.ElapsedMilliseconds, false); }
    }

    private sealed record ModelListResponse([property: JsonPropertyName("data")] List<ModelEntry>? Data);
    private sealed record ModelEntry([property: JsonPropertyName("id")] string Id);
}
```

- [ ] **Step 6.2: Implement DeepSeek executor**

Identico a OpenAI, cambia solo URL `https://api.deepseek.com/v1/models` + `ProviderName => "deepseek"`. Copia `OpenAiProbeExecutor.cs` rinominando e cambiando i 2 valori.

- [ ] **Step 6.3: Implement Ollama executor**

Ollama è locale, nessuna auth richiesta. URL `http://localhost:11434/api/tags`. Token può essere stringa vuota.

```csharp
// apps/api/src/Api/Services/Providers/Probe/OllamaProbeExecutor.cs
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderProbeAudit;
using Microsoft.Extensions.Logging;

namespace Api.Services.Providers.Probe;

internal sealed class OllamaProbeExecutor : IProviderProbeExecutor
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaProbeExecutor> _logger;
    public string ProviderName => "ollama";

    public OllamaProbeExecutor(IHttpClientFactory f, ILogger<OllamaProbeExecutor> l)
    {
        _httpClient = f.CreateClient("provider-probe");
        _logger = l;
    }

    public async Task<ProbeExecutionResult> ExecuteAsync(string apiKey, string? expectedModel, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(ProbeTimeout);
        var baseUrl = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL") ?? "http://localhost:11434";
        try
        {
            using var resp = await _httpClient.GetAsync($"{baseUrl}/api/tags", cts.Token).ConfigureAwait(false);
            sw.Stop();
            if (!resp.IsSuccessStatusCode)
                return new(ProbeOutcome.UnknownError, "http_" + (int)resp.StatusCode, $"HTTP {resp.StatusCode}", (int)sw.ElapsedMilliseconds, false);
            var payload = await resp.Content.ReadFromJsonAsync<TagsResponse>(cancellationToken: cts.Token).ConfigureAwait(false);
            var modelAvailable = expectedModel is null || (payload?.Models?.Any(m => m.Name == expectedModel) ?? false);
            return new(modelAvailable ? ProbeOutcome.Success : ProbeOutcome.ModelMissing,
                       modelAvailable ? null : "model_missing", null, (int)sw.ElapsedMilliseconds, modelAvailable);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !ct.IsCancellationRequested)
        { sw.Stop(); return new(ProbeOutcome.Timeout, "timeout", "Probe exceeded 5s", (int)sw.ElapsedMilliseconds, false); }
        catch (HttpRequestException ex)
        { sw.Stop(); _logger.LogWarning(ex, "Ollama probe network failure"); return new(ProbeOutcome.Unreachable, "unreachable", "Network error", (int)sw.ElapsedMilliseconds, false); }
    }

    private sealed record TagsResponse([property: JsonPropertyName("models")] List<ModelEntry>? Models);
    private sealed record ModelEntry([property: JsonPropertyName("name")] string Name);
}
```

- [ ] **Step 6.4: Commit**

```bash
git add apps/api/src/Api/Services/Providers/Probe/OpenAiProbeExecutor.cs apps/api/src/Api/Services/Providers/Probe/DeepSeekProbeExecutor.cs apps/api/src/Api/Services/Providers/Probe/OllamaProbeExecutor.cs
git commit -m "feat(admin): add OpenAI/DeepSeek/Ollama probe executors"
```

---

## Task 7: Probe executor factory

**Files:**
- Create: `apps/api/src/Api/Services/Providers/Probe/ProviderProbeExecutorFactory.cs`
- Create: `tests/Api.Tests/Services/Providers/Probe/ProviderProbeExecutorFactoryTests.cs`

- [ ] **Step 7.1: Write failing tests**

```csharp
using Api.Services.Providers.Probe;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Services.Providers.Probe;

public class ProviderProbeExecutorFactoryTests
{
    private static ProviderProbeExecutorFactory Build(params IProviderProbeExecutor[] executors)
        => new(executors);

    private sealed class FakeExec : IProviderProbeExecutor
    {
        public FakeExec(string name) => ProviderName = name;
        public string ProviderName { get; }
        public Task<ProbeExecutionResult> ExecuteAsync(string a, string? m, CancellationToken c) => throw new NotImplementedException();
    }

    [Fact]
    public void GetExecutor_KnownProvider_ReturnsMatchingExecutor()
    {
        var factory = Build(new FakeExec("openrouter"), new FakeExec("openai"));
        factory.GetExecutor("openrouter").Should().NotBeNull();
        factory.GetExecutor("openrouter")!.ProviderName.Should().Be("openrouter");
    }

    [Fact]
    public void GetExecutor_UnknownProvider_ReturnsNull()
    {
        var factory = Build(new FakeExec("openrouter"));
        factory.GetExecutor("cohere").Should().BeNull();
    }

    [Fact]
    public void GetExecutor_CaseInsensitive()
    {
        var factory = Build(new FakeExec("openrouter"));
        factory.GetExecutor("OpenRouter").Should().NotBeNull();
    }

    [Fact]
    public void KnownProviderNames_ReturnsAllRegistered()
    {
        var factory = Build(new FakeExec("openrouter"), new FakeExec("openai"));
        factory.KnownProviderNames.Should().BeEquivalentTo(new[] { "openrouter", "openai" });
    }
}
```

- [ ] **Step 7.2: Run — FAIL**

```bash
dotnet test --filter "FullyQualifiedName~ProviderProbeExecutorFactoryTests"
```

- [ ] **Step 7.3: Implement factory**

```csharp
// apps/api/src/Api/Services/Providers/Probe/ProviderProbeExecutorFactory.cs
namespace Api.Services.Providers.Probe;

internal sealed class ProviderProbeExecutorFactory
{
    private readonly Dictionary<string, IProviderProbeExecutor> _executors;

    public ProviderProbeExecutorFactory(IEnumerable<IProviderProbeExecutor> executors)
    {
        _executors = executors.ToDictionary(e => e.ProviderName, StringComparer.OrdinalIgnoreCase);
    }

    public IProviderProbeExecutor? GetExecutor(string providerName)
        => _executors.TryGetValue(providerName, out var ex) ? ex : null;

    public IReadOnlyCollection<string> KnownProviderNames => _executors.Keys.ToList();
}
```

- [ ] **Step 7.4: Run — PASS**

```bash
dotnet test --filter "FullyQualifiedName~ProviderProbeExecutorFactoryTests"
```

- [ ] **Step 7.5: Commit**

```bash
git add apps/api/src/Api/Services/Providers/Probe/ProviderProbeExecutorFactory.cs tests/Api.Tests/Services/Providers/Probe/ProviderProbeExecutorFactoryTests.cs
git commit -m "feat(admin): add ProviderProbeExecutorFactory"
```

---

## Task 8: Repository interface + EF Core configuration + migration

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Administration/Domain/Repositories/IProviderProbeAuditRepository.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Infrastructure/Repositories/ProviderProbeAuditRepository.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Infrastructure/Configurations/ProviderProbeAuditEntryConfiguration.cs`
- Modify: `apps/api/src/Api/Infrastructure/Persistence/MeepleDbContext.cs`
- Create migration via `dotnet ef`

- [ ] **Step 8.1: Repository interface**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Domain/Repositories/IProviderProbeAuditRepository.cs
using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderProbeAudit;

namespace Api.BoundedContexts.Administration.Domain.Repositories;

internal interface IProviderProbeAuditRepository
{
    Task AddAsync(ProviderProbeAuditEntry entry, CancellationToken cancellationToken);
    Task<int> CountInWindowAsync(string providerName, DateTime since, CancellationToken cancellationToken);
    Task<int> CountByActorInWindowAsync(Guid actorId, DateTime since, CancellationToken cancellationToken);
    Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken);
}
```

- [ ] **Step 8.2: EF configuration**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Infrastructure/Configurations/ProviderProbeAuditEntryConfiguration.cs
using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderProbeAudit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.Administration.Infrastructure.Configurations;

internal sealed class ProviderProbeAuditEntryConfiguration : IEntityTypeConfiguration<ProviderProbeAuditEntry>
{
    public void Configure(EntityTypeBuilder<ProviderProbeAuditEntry> builder)
    {
        builder.ToTable("provider_probe_audit_entries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ProviderName).HasMaxLength(64).IsRequired();
        builder.Property(e => e.TokenFingerprint).HasMaxLength(8);
        builder.Property(e => e.Outcome).HasConversion<int>().IsRequired();
        builder.Property(e => e.ErrorCode).HasMaxLength(64);
        builder.Property(e => e.ProbedAt).IsRequired();

        builder.HasIndex(e => new { e.ProviderName, e.ProbedAt })
               .HasDatabaseName("ix_provider_probe_audit_provider_probed_at")
               .IsDescending(false, true);
        builder.HasIndex(e => new { e.ActorId, e.ProbedAt })
               .HasDatabaseName("ix_provider_probe_audit_actor_probed_at")
               .IsDescending(false, true);
    }
}
```

- [ ] **Step 8.3: Repository implementation**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Infrastructure/Repositories/ProviderProbeAuditRepository.cs
using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderProbeAudit;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Infrastructure.Repositories;

internal sealed class ProviderProbeAuditRepository : IProviderProbeAuditRepository
{
    private readonly MeepleDbContext _db;

    public ProviderProbeAuditRepository(MeepleDbContext db) => _db = db;

    public async Task AddAsync(ProviderProbeAuditEntry entry, CancellationToken ct)
    {
        await _db.ProviderProbeAuditEntries.AddAsync(entry, ct).ConfigureAwait(false);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public Task<int> CountInWindowAsync(string providerName, DateTime since, CancellationToken ct)
        => _db.ProviderProbeAuditEntries.CountAsync(e => e.ProviderName == providerName && e.ProbedAt >= since, ct);

    public Task<int> CountByActorInWindowAsync(Guid actorId, DateTime since, CancellationToken ct)
        => _db.ProviderProbeAuditEntries.CountAsync(e => e.ActorId == actorId && e.ProbedAt >= since, ct);

    public async Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken ct)
        => await _db.ProviderProbeAuditEntries.Where(e => e.ProbedAt < cutoff).ExecuteDeleteAsync(ct).ConfigureAwait(false);
}
```

- [ ] **Step 8.4: Add DbSet to DbContext**

Modifica `apps/api/src/Api/Infrastructure/Persistence/MeepleDbContext.cs`. Trova la regione/sezione DbSets dell'Administration BC e aggiungi:

```csharp
public DbSet<ProviderProbeAuditEntry> ProviderProbeAuditEntries => Set<ProviderProbeAuditEntry>();
```

E nel metodo `OnModelCreating` (o dove vengono applicate le configurations):

```csharp
modelBuilder.ApplyConfiguration(new ProviderProbeAuditEntryConfiguration());
```

Aggiungi i `using` necessari.

- [ ] **Step 8.5: Generate migration**

```bash
cd apps/api/src/Api
dotnet ef migrations add AddProviderProbeAuditEntries
```

Expected: nuovo file in `Migrations/` con nome timestampato (es. `20260510123456_AddProviderProbeAuditEntries.cs`). Review il file: deve creare `provider_probe_audit_entries` table + 2 index.

- [ ] **Step 8.6: Apply migration locally**

```bash
dotnet ef database update
```

Expected: success, table creata.

- [ ] **Step 8.7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Administration/Domain/Repositories/IProviderProbeAuditRepository.cs apps/api/src/Api/BoundedContexts/Administration/Infrastructure/Repositories/ProviderProbeAuditRepository.cs apps/api/src/Api/BoundedContexts/Administration/Infrastructure/Configurations/ProviderProbeAuditEntryConfiguration.cs apps/api/src/Api/Infrastructure/Persistence/MeepleDbContext.cs apps/api/src/Api/Migrations/
git commit -m "feat(admin): add ProviderProbeAuditEntry persistence + migration"
```

---

## Task 9: Probe domain service

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Administration/Domain/Services/IProviderProbeService.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Infrastructure/Services/ProviderProbeService.cs`

- [ ] **Step 9.1: Interface**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Domain/Services/IProviderProbeService.cs
using Api.Models;

namespace Api.BoundedContexts.Administration.Domain.Services;

internal interface IProviderProbeService
{
    Task<ProviderProbeResultDto> ProbeAsync(string providerName, Guid actorId, CancellationToken cancellationToken);
}
```

- [ ] **Step 9.2: DTO**

```csharp
// apps/api/src/Api/Models/ProviderProbeResultDto.cs
#pragma warning disable MA0048
namespace Api.Models;

internal sealed record ProviderProbeResultDto(
    string ProviderName,
    bool TokenConfigured,
    bool TokenAuthenticated,
    bool ModelAvailable,
    string? TokenFingerprint,
    string? ErrorCode,
    string? ErrorMessage,
    int LatencyMs,
    DateTime ProbedAt);
```

- [ ] **Step 9.3: Service implementation**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Infrastructure/Services/ProviderProbeService.cs
using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderProbeAudit;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.Models;
using Api.Services.Providers.Probe;
using Microsoft.Extensions.Configuration;

namespace Api.BoundedContexts.Administration.Infrastructure.Services;

internal sealed class ProviderProbeService : IProviderProbeService
{
    private readonly ProviderProbeExecutorFactory _factory;
    private readonly IProviderProbeAuditRepository _auditRepo;
    private readonly IConfiguration _config;
    private static readonly Dictionary<string, (string EnvVar, string DefaultModel)> ProviderConfig = new(StringComparer.OrdinalIgnoreCase)
    {
        ["openrouter"] = ("OPENROUTER_API_KEY", "google/gemma-2-9b-it:free"),
        ["openai"]     = ("OPENAI_API_KEY",     "gpt-4o-mini"),
        ["deepseek"]   = ("DEEPSEEK_API_KEY",   "deepseek-chat"),
        ["ollama"]     = ("OLLAMA_API_KEY",     "llama3.2"),     // env unused for ollama, kept for symmetry
    };

    public ProviderProbeService(ProviderProbeExecutorFactory factory, IProviderProbeAuditRepository auditRepo, IConfiguration config)
    {
        _factory = factory;
        _auditRepo = auditRepo;
        _config = config;
    }

    public async Task<ProviderProbeResultDto> ProbeAsync(string providerName, Guid actorId, CancellationToken ct)
    {
        var probedAt = DateTime.UtcNow;
        var executor = _factory.GetExecutor(providerName);
        if (executor is null)
            throw new KnownProvidersException(providerName);

        if (!ProviderConfig.TryGetValue(providerName, out var cfg))
            throw new KnownProvidersException(providerName);

        var apiKey = Environment.GetEnvironmentVariable(cfg.EnvVar) ?? string.Empty;
        var fingerprint = TokenFingerprint.Compute(apiKey);

        if (string.IsNullOrEmpty(apiKey) && providerName != "ollama")
        {
            await _auditRepo.AddAsync(ProviderProbeAuditEntry.Create(
                providerName, actorId, fingerprint, ProbeOutcome.NotConfigured, "not_configured", 0), ct).ConfigureAwait(false);
            return new ProviderProbeResultDto(providerName, false, false, false, null, "not_configured",
                "API key environment variable not set", 0, probedAt);
        }

        var result = await executor.ExecuteAsync(apiKey, cfg.DefaultModel, ct).ConfigureAwait(false);

        await _auditRepo.AddAsync(ProviderProbeAuditEntry.Create(
            providerName, actorId, fingerprint, result.Outcome, result.ErrorCode, result.LatencyMs), ct).ConfigureAwait(false);

        var authenticated = result.Outcome is ProbeOutcome.Success or ProbeOutcome.ModelMissing;
        return new ProviderProbeResultDto(
            providerName,
            TokenConfigured: !string.IsNullOrEmpty(apiKey) || providerName == "ollama",
            TokenAuthenticated: authenticated,
            ModelAvailable: result.ModelAvailable,
            TokenFingerprint: fingerprint,
            ErrorCode: result.ErrorCode,
            ErrorMessage: result.ErrorMessage,
            LatencyMs: result.LatencyMs,
            ProbedAt: probedAt);
    }
}

/// <summary>404 mapping per unknown provider name.</summary>
internal sealed class KnownProvidersException : Exception
{
    public string ProviderName { get; }
    public KnownProvidersException(string name) : base($"Unknown provider: {name}") => ProviderName = name;
}
```

- [ ] **Step 9.4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Administration/Domain/Services/IProviderProbeService.cs apps/api/src/Api/BoundedContexts/Administration/Infrastructure/Services/ProviderProbeService.cs apps/api/src/Api/Models/ProviderProbeResultDto.cs
git commit -m "feat(admin): add ProviderProbeService orchestrating executor + audit"
```

---

## Task 10: ProbeProviderCommand + Validator + Handler

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ProbeProviderCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ProbeProviderCommandValidator.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ProbeProviderCommandHandler.cs`
- Create: `tests/Api.Tests/BoundedContexts/Administration/Application/ProbeProviderCommandHandlerTests.cs`

- [ ] **Step 10.1: Command**

```csharp
using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

internal sealed record ProbeProviderCommand(string ProviderName, Guid ActorId) : ICommand<ProviderProbeResultDto>;
```

- [ ] **Step 10.2: Validator**

```csharp
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Commands;

internal sealed class ProbeProviderCommandValidator : AbstractValidator<ProbeProviderCommand>
{
    private static readonly HashSet<string> AllowedProviders = new(StringComparer.OrdinalIgnoreCase)
    { "openrouter", "openai", "deepseek", "ollama" };

    public ProbeProviderCommandValidator()
    {
        RuleFor(c => c.ProviderName)
            .NotEmpty()
            .Must(name => AllowedProviders.Contains(name))
            .WithMessage("Unknown provider");
        RuleFor(c => c.ActorId).NotEmpty();
    }
}
```

- [ ] **Step 10.3: Handler**

```csharp
using Api.BoundedContexts.Administration.Domain.Services;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

internal sealed class ProbeProviderCommandHandler : ICommandHandler<ProbeProviderCommand, ProviderProbeResultDto>
{
    private readonly IProviderProbeService _probeService;

    public ProbeProviderCommandHandler(IProviderProbeService probeService) => _probeService = probeService;

    public Task<ProviderProbeResultDto> Handle(ProbeProviderCommand request, CancellationToken cancellationToken)
        => _probeService.ProbeAsync(request.ProviderName, request.ActorId, cancellationToken);
}
```

- [ ] **Step 10.4: Handler unit tests**

```csharp
using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.Models;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application;

public class ProbeProviderCommandHandlerTests
{
    [Fact]
    public async Task Handle_DelegatesToProbeService()
    {
        var actorId = Guid.NewGuid();
        var expected = new ProviderProbeResultDto("openrouter", true, true, true, "abcd1234", null, null, 250, DateTime.UtcNow);
        var svc = new Mock<IProviderProbeService>();
        svc.Setup(s => s.ProbeAsync("openrouter", actorId, It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        var handler = new ProbeProviderCommandHandler(svc.Object);

        var result = await handler.Handle(new ProbeProviderCommand("openrouter", actorId), CancellationToken.None);

        result.Should().Be(expected);
    }

    [Fact]
    public void Validator_UnknownProvider_Fails()
    {
        var v = new ProbeProviderCommandValidator();
        var r = v.Validate(new ProbeProviderCommand("cohere", Guid.NewGuid()));
        r.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("openrouter")]
    [InlineData("openai")]
    [InlineData("deepseek")]
    [InlineData("ollama")]
    public void Validator_AllowedProviders_Pass(string name)
    {
        var v = new ProbeProviderCommandValidator();
        v.Validate(new ProbeProviderCommand(name, Guid.NewGuid())).IsValid.Should().BeTrue();
    }
}
```

- [ ] **Step 10.5: Run handler tests — PASS**

```bash
dotnet test --filter "FullyQualifiedName~ProbeProviderCommandHandlerTests"
```

- [ ] **Step 10.6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ProbeProviderCommand.cs apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ProbeProviderCommandValidator.cs apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ProbeProviderCommandHandler.cs tests/Api.Tests/BoundedContexts/Administration/Application/ProbeProviderCommandHandlerTests.cs
git commit -m "feat(admin): add ProbeProviderCommand + handler + validator"
```

---

## Task 11: Rate limit policies

**Files:**
- Modify: `apps/api/src/Api/Extensions/RateLimitingServiceExtensions.cs`

- [ ] **Step 11.1: Add 2 named policies**

Aprire `apps/api/src/Api/Extensions/RateLimitingServiceExtensions.cs`. Trovare il blocco `options.AddPolicy("BulkImportAdmin", ...)` esistente e aggiungere subito dopo:

```csharp
// Policy: AdminProviderProbe — 10 req/min per authenticated user (G3)
options.AddPolicy("AdminProviderProbe", httpContext =>
    RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        }));

// Policy: AdminProviderProbeGlobal — 60 req/h globale per provider name (G3)
options.AddPolicy("AdminProviderProbeGlobal", httpContext =>
    RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: (string?)httpContext.Request.RouteValues["name"] ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        }));
```

Aggiungere i `using` necessari (`System.Threading.RateLimiting`).

Aggiornare il commento dell'header del file per documentare le 2 nuove policy.

- [ ] **Step 11.2: Build to verify**

```bash
dotnet build apps/api/src/Api
```

Expected: 0 errors.

- [ ] **Step 11.3: Commit**

```bash
git add apps/api/src/Api/Extensions/RateLimitingServiceExtensions.cs
git commit -m "feat(admin): add AdminProviderProbe rate limit policies (10/min user, 60/h provider)"
```

---

## Task 12: HTTP client factory + DI registration

**Files:**
- Modify: `apps/api/src/Api/Extensions/InfrastructureServiceExtensions.cs`

- [ ] **Step 12.1: Register provider-probe HttpClient**

In `InfrastructureServiceExtensions.cs`, trovare il punto dove sono già registrati gli HTTP client e aggiungere:

```csharp
services.AddHttpClient("provider-probe", c =>
{
    c.Timeout = TimeSpan.FromSeconds(10); // soft cap; per-executor enforcing 5s
    c.DefaultRequestHeaders.Add("User-Agent", "MeepleAI-ProviderProbe/1.0");
});
```

- [ ] **Step 12.2: Register executors + factory + service + repository**

Nello stesso file, aggiungere nella sezione DI dei servizi Administration:

```csharp
services.AddSingleton<IProviderProbeExecutor, OpenRouterProbeExecutor>();
services.AddSingleton<IProviderProbeExecutor, OpenAiProbeExecutor>();
services.AddSingleton<IProviderProbeExecutor, DeepSeekProbeExecutor>();
services.AddSingleton<IProviderProbeExecutor, OllamaProbeExecutor>();
services.AddSingleton<ProviderProbeExecutorFactory>();
services.AddScoped<IProviderProbeAuditRepository, ProviderProbeAuditRepository>();
services.AddScoped<IProviderProbeService, ProviderProbeService>();
```

Aggiungere `using` necessari.

- [ ] **Step 12.3: Build**

```bash
dotnet build apps/api/src/Api
```

Expected: 0 errors.

- [ ] **Step 12.4: Commit**

```bash
git add apps/api/src/Api/Extensions/InfrastructureServiceExtensions.cs
git commit -m "feat(admin): register provider probe DI graph"
```

---

## Task 13: Endpoint registration

**Files:**
- Create: `apps/api/src/Api/Routing/AdminProviderEndpoints.cs`
- Modify: `apps/api/src/Api/Program.cs`

- [ ] **Step 13.1: Endpoint file**

```csharp
// apps/api/src/Api/Routing/AdminProviderEndpoints.cs
using System.Security.Claims;
using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Infrastructure.Services;
using Api.Models;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Api.Routing;

internal static class AdminProviderEndpoints
{
    public static void MapAdminProviderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/providers")
                       .WithTags("Admin/Providers");

        group.MapPost("/{name}/probe", async (
                string name,
                IMediator mediator,
                ClaimsPrincipal user,
                CancellationToken ct) =>
            {
                var actorIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(actorIdClaim, out var actorId))
                    return Results.Unauthorized();

                try
                {
                    var result = await mediator.Send(new ProbeProviderCommand(name, actorId), ct);
                    return Results.Ok(result);
                }
                catch (KnownProvidersException ex)
                {
                    return Results.NotFound(new { errorCode = "unknown_provider", provider = ex.ProviderName });
                }
            })
            .RequireAuthorization("RequireSuperAdmin")
            .RequireRateLimiting("AdminProviderProbe")
            .RequireRateLimiting("AdminProviderProbeGlobal")
            .WithOpenApi();
    }
}
```

- [ ] **Step 13.2: Register in Program.cs**

In `apps/api/src/Api/Program.cs`, trovare la sezione dove sono mappati gli altri endpoint (`app.MapAdminXxxEndpoints()`) e aggiungere:

```csharp
app.MapAdminProviderEndpoints();
```

Aggiungere il `using Api.Routing;` se non già presente.

- [ ] **Step 13.3: Build**

```bash
dotnet build apps/api/src/Api
```

Expected: 0 errors.

- [ ] **Step 13.4: Commit**

```bash
git add apps/api/src/Api/Routing/AdminProviderEndpoints.cs apps/api/src/Api/Program.cs
git commit -m "feat(admin): expose POST /api/v1/admin/providers/{name}/probe"
```

---

## Task 14: Audit retention background job

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Administration/Infrastructure/BackgroundJobs/ProbeAuditRetentionJob.cs`
- Modify: `apps/api/src/Api/appsettings.json`
- Modify: `apps/api/src/Api/Extensions/InfrastructureServiceExtensions.cs`

- [ ] **Step 14.1: Add config**

In `apps/api/src/Api/appsettings.json`, aggiungere sotto `"Administration"` (o creare la sezione se manca):

```json
"Administration": {
  "ProbeAuditRetentionDays": 365
}
```

- [ ] **Step 14.2: Background service**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Infrastructure/BackgroundJobs/ProbeAuditRetentionJob.cs
using Api.BoundedContexts.Administration.Domain.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Infrastructure.BackgroundJobs;

internal sealed class ProbeAuditRetentionJob : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private readonly ILogger<ProbeAuditRetentionJob> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    public ProbeAuditRetentionJob(IServiceProvider services, IConfiguration config, ILogger<ProbeAuditRetentionJob> logger)
    {
        _services = services;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // initial delay 1h after boot to avoid contention
        try { await Task.Delay(TimeSpan.FromHours(1), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var days = _config.GetValue<int?>("Administration:ProbeAuditRetentionDays") ?? 365;
                var cutoff = DateTime.UtcNow.AddDays(-days);
                using var scope = _services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IProviderProbeAuditRepository>();
                var deleted = await repo.DeleteOlderThanAsync(cutoff, stoppingToken);
                if (deleted > 0)
                    _logger.LogInformation("Probe audit retention deleted {Count} entries older than {Cutoff}", deleted, cutoff);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { _logger.LogError(ex, "Probe audit retention job failed"); }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}
```

- [ ] **Step 14.3: Register hosted service**

In `apps/api/src/Api/Extensions/InfrastructureServiceExtensions.cs`:

```csharp
services.AddHostedService<ProbeAuditRetentionJob>();
```

- [ ] **Step 14.4: Build**

```bash
dotnet build apps/api/src/Api
```

- [ ] **Step 14.5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Administration/Infrastructure/BackgroundJobs/ProbeAuditRetentionJob.cs apps/api/src/Api/appsettings.json apps/api/src/Api/Extensions/InfrastructureServiceExtensions.cs
git commit -m "feat(admin): add ProbeAuditRetentionJob (default 365 days)"
```

---

## Task 15: Integration tests (G1 + G3 Gherkin)

**Files:**
- Create: `tests/Api.Tests/Integration/Routing/AdminProviderEndpointsTests.cs`

- [ ] **Step 15.1: Verify WireMock.Net dep**

```bash
cd tests/Api.Tests && grep -i wiremock Api.Tests.csproj
```

Se assente, aggiungere:

```bash
dotnet add package WireMock.Net --version 1.5.62
```

- [ ] **Step 15.2: Write integration test class**

```csharp
// tests/Api.Tests/Integration/Routing/AdminProviderEndpointsTests.cs
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace Api.Tests.Integration.Routing;

[Collection("DatabaseCollection")] // riusa fixture esistente Testcontainers
public class AdminProviderEndpointsTests : IAsyncLifetime
{
    private WireMockServer _wireMock = null!;
    private CustomWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _wireMock = WireMockServer.Start();
        Environment.SetEnvironmentVariable("OPENROUTER_API_BASE", _wireMock.Url);
        Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", "test-token-valid");

        _factory = new CustomWebApplicationFactory();
        _client = _factory.CreateClient();
        await _factory.SeedSuperAdminAsync("alice@meeple.test");
        var authToken = await _factory.LoginAsync("alice@meeple.test");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
    }

    public Task DisposeAsync() { _wireMock.Dispose(); _factory.Dispose(); return Task.CompletedTask; }

    [Fact]
    public async Task Probe_TokenValid_Returns200WithAuthenticated()
    {
        _wireMock.Given(Request.Create().WithPath("/api/v1/models").UsingGet())
                 .RespondWith(Response.Create().WithStatusCode(200)
                     .WithBodyAsJson(new { data = new[] { new { id = "google/gemma-2-9b-it:free" } } }));

        var resp = await _client.PostAsync("/api/v1/admin/providers/openrouter/probe", content: null);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await resp.Content.ReadFromJsonAsync<ProviderProbeResultDto>();
        dto!.TokenAuthenticated.Should().BeTrue();
        dto.TokenFingerprint.Should().MatchRegex("^[a-f0-9]{8}$");
        (await resp.Content.ReadAsStringAsync()).Should().NotContain("test-token-valid");
    }

    [Fact]
    public async Task Probe_Provider401_Returns200WithUnauthorized()
    {
        _wireMock.Given(Request.Create().WithPath("/api/v1/models").UsingGet())
                 .RespondWith(Response.Create().WithStatusCode(401));

        var resp = await _client.PostAsync("/api/v1/admin/providers/openrouter/probe", content: null);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await resp.Content.ReadFromJsonAsync<ProviderProbeResultDto>();
        dto!.TokenAuthenticated.Should().BeFalse();
        dto.ErrorCode.Should().Be("unauthorized");
    }

    [Fact]
    public async Task Probe_NoToken_ReturnsNotConfigured()
    {
        Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", null);
        var resp = await _client.PostAsync("/api/v1/admin/providers/openrouter/probe", content: null);
        var dto = await resp.Content.ReadFromJsonAsync<ProviderProbeResultDto>();
        dto!.TokenConfigured.Should().BeFalse();
        dto.ErrorCode.Should().Be("not_configured");
        Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", "test-token-valid");
    }

    [Fact]
    public async Task Probe_UnknownProvider_Returns404()
    {
        var resp = await _client.PostAsync("/api/v1/admin/providers/cohere/probe", content: null);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Probe_NotSuperAdmin_Returns403()
    {
        await _factory.SeedAdminAsync("bob@meeple.test");
        var token = await _factory.LoginAsync("bob@meeple.test");
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await c.PostAsync("/api/v1/admin/providers/openrouter/probe", content: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Probe_RateLimitExceeded_Returns429()
    {
        _wireMock.Given(Request.Create().WithPath("/api/v1/models").UsingGet())
                 .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new { data = Array.Empty<object>() }));

        for (int i = 0; i < 10; i++)
            (await _client.PostAsync("/api/v1/admin/providers/openrouter/probe", content: null)).EnsureSuccessStatusCode();

        var resp = await _client.PostAsync("/api/v1/admin/providers/openrouter/probe", content: null);
        resp.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Probe_WritesAuditEntry()
    {
        _wireMock.Given(Request.Create().WithPath("/api/v1/models").UsingGet())
                 .RespondWith(Response.Create().WithStatusCode(200)
                     .WithBodyAsJson(new { data = new[] { new { id = "google/gemma-2-9b-it:free" } } }));

        await _client.PostAsync("/api/v1/admin/providers/openrouter/probe", content: null);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleDbContext>();
        var entries = db.ProviderProbeAuditEntries.Where(e => e.ProviderName == "openrouter").ToList();
        entries.Should().NotBeEmpty();
        entries.Last().Outcome.Should().Be(ProbeOutcome.Success);
    }
}
```

> **NOTE for engineer**: `CustomWebApplicationFactory`, `SeedSuperAdminAsync`, `SeedAdminAsync`, `LoginAsync`, `[Collection("DatabaseCollection")]` sono pattern già esistenti in `tests/Api.Tests/`. Verificare il loro nome esatto in `tests/Api.Tests/TestInfrastructure/` e adattare se diversi. Se il fixture non supporta env var override sul SUT, è probabile che i 4 executor vadano puntati a `WireMockServer.Url` via override DI direttamente, non env var. In tal caso, override nel `WebApplicationFactory.ConfigureWebHost`:
>
> ```csharp
> services.AddSingleton<IProviderProbeExecutor>(sp =>
>     new OpenRouterProbeExecutor(sp.GetRequiredService<IHttpClientFactory>(), ...) { /* override URL */ });
> ```
> Più robusto: estrarre URL da `IConfiguration` invece di hardcode in OpenRouterProbeExecutor (refactor minimo, fa parte di questo task).

- [ ] **Step 15.3: Run integration tests**

```bash
dotnet test tests/Api.Tests --filter "FullyQualifiedName~AdminProviderEndpointsTests"
```

Expected: 7 PASS.

- [ ] **Step 15.4: Commit**

```bash
git add tests/Api.Tests/Integration/Routing/AdminProviderEndpointsTests.cs tests/Api.Tests/Api.Tests.csproj
git commit -m "test(admin): add integration tests for /api/v1/admin/providers/{name}/probe"
```

---

## Task 16: Final verification + PR

- [ ] **Step 16.1: Full test suite**

```bash
cd tests/Api.Tests && dotnet test --filter "FullyQualifiedName~ProviderProbe|FullyQualifiedName~AdminProviderEndpoints|FullyQualifiedName~TokenFingerprint"
```

Expected: all PASS, ~28 unit + 7 integration test verdi.

- [ ] **Step 16.2: Coverage report**

```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover /p:Include="[Api]Api.BoundedContexts.Administration.*Probe*,[Api]Api.Services.Providers.Probe.*"
```

Expected coverage ≥ 90% sui file nuovi.

- [ ] **Step 16.3: Push branch**

```bash
git push -u origin feature/issue-NEW1-provider-probe-endpoint
```

- [ ] **Step 16.4: Create PR targeting `main-dev`**

```bash
gh pr create --base main-dev --title "feat(admin): provider token probe endpoint (G1+G3)" --body "$(cat <<'EOF'
## Summary
- Espone `POST /api/v1/admin/providers/{name}/probe` per validazione live token provider
- Audit log `ProviderProbeAuditEntry` (retention 365gg) + rate limit (10/min user, 60/h provider)
- Strategy executor per OpenRouter, OpenAI, DeepSeek, Ollama (zero-cost via list-models)
- SuperAdmin only; token mai esposto (solo SHA256[0..8] fingerprint)

Closes #NEW1
Refs spec: docs/for-developers/specs/2026-05-10-provider-token-observability-design.md (G1 + G3)

## Test plan
- [x] 8 unit ProviderProbeAuditEntry
- [x] 5 unit TokenFingerprint
- [x] 4 unit ProviderProbeExecutorFactory
- [x] 6 unit ProbeProviderCommandHandler + Validator
- [x] 7 integration AdminProviderEndpoints (Testcontainers + WireMock)
- [x] Coverage ≥ 90%

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 16.5: Local cleanup post merge**

Dopo merge della PR, sul main-dev locale:

```bash
git checkout main-dev && git pull
git branch -D feature/issue-NEW1-provider-probe-endpoint
git remote prune origin
```

---

## Self-Review

**Spec coverage check** (mappatura spec § 5 → task):
- ✅ G1 (active token validation): Tasks 4-7, 9-10, 13, 15
- ✅ G3 (audit + rate limit + token leak prevention): Tasks 2, 8, 11, 14, 15
- ⏭️ G2 (quota): out of scope PR1 — piano separato
- ⏭️ G4 (alerting): out of scope PR1 — piano separato
- ⏭️ G5 (UI): out of scope PR1 — piano separato

**Spec § 8 test strategy mapping**:
- ✅ ~28 unit tests target raggiunto: 8 entity + 5 fingerprint + 4 factory + 6 handler/validator + ~5 in altri = 28
- ✅ 7 integration tests (G1 7 scenari + G3 6 = sovrapposizione, 7 spec OK)
- ⏭️ Audit retention E2E test → Task 14 ha solo unit-level, integration test retention deferito (può essere aggiunto come Step 15.5 se richiesto)

**Placeholder scan**: nessun TBD/TODO. Note tecniche su `CustomWebApplicationFactory` in Task 15.2 sono istruzioni concrete, non placeholder.

**Type consistency check**:
- `ProbeOutcome` enum: definito Task 1, riusato Tasks 2/4/5/6/9/15 — consistente
- `ProviderProbeResultDto` campi: definito Task 9.2 con 9 campi → riusato Task 15.2 (`TokenAuthenticated`, `ErrorCode`, `TokenFingerprint`) — consistente
- `IProviderProbeExecutor.ExecuteAsync(string apiKey, string? expectedModel, CancellationToken)`: definito Task 4.2 → mantenuto in Tasks 5, 6 — consistente
- `ProviderProbeExecutorFactory.GetExecutor(string)`: definito Task 7.3 → riusato Task 9.3 — consistente
- `IProviderProbeAuditRepository`: 4 metodi definiti Task 8.1 → riusati Task 9.3 (`AddAsync`), Task 14.2 (`DeleteOlderThanAsync`) — consistente

**Branching gotcha avoidance**:
- Step 0.1 forza checkout su `main-dev` PRIMA di branchare → previene "Branch Hygiene Rule" issue (CLAUDE.md riferimento issue #806)
- PR target `main-dev` esplicito (non `main`)

---

## Open follow-up tasks (post PR1)

| Tag | Task |
|-----|------|
| PR2 | Quota endpoint (G2) + Prometheus metric (G4) — piano separato |
| PR3 | Frontend `/admin/providers/[name]` (G5) — piano separato |
| Future | `?deep=true` chat-completion probe (richiede budget conferma) |
| Future | Alert rule template `ProviderQuotaLow` (dipende da PR2) |
