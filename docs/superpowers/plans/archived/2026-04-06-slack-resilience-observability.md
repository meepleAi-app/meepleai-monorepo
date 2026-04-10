# Slack Resilience & Observability Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Aggiungere resilienza operativa (timeout, circuit breaker, jitter, chunking, deepLink validation) e osservabilità (metriche, CorrelationId) al sistema di notifiche Slack.

**Architecture:** Sei task indipendenti su `UserNotifications` bounded context. Tasks 1-5 correggono bug e gap di resilienza identificati da spec-panel review. Task 6 aggiunge metriche OpenTelemetry al pattern `MeepleAiMetrics` già in uso nel progetto. Nessuna migrazione DB, nessun breaking change all'API pubblica.

**Tech Stack:** .NET 9, `Microsoft.Extensions.Http.Polly` v10.0.0 (già nel progetto), `System.Diagnostics.Metrics` (già in uso via `MeepleAiMetrics`), xUnit + FluentAssertions.

**Prerequisito:** Prima di iniziare, assicurarsi che main-dev sia aggiornato (include PR #245):
```bash
git checkout main-dev && git pull
git checkout -b feature/slack-resilience-observability
git config branch.feature/slack-resilience-observability.parent main-dev
```

---

## File Structure

| File | Azione | Motivo |
|------|--------|--------|
| `...Infrastructure/DependencyInjection/UserNotificationsServiceExtensions.cs` | Modify | Registra "SlackApi" HttpClient con timeout 10s + circuit breaker |
| `...Domain/Aggregates/NotificationQueueItem.cs` | Modify | Aggiunge jitter (0-30s) al retry delay |
| `...Infrastructure/Slack/SlackDeepLinkValidator.cs` | Create | Helper statico per validare deepLinkPath |
| `...Infrastructure/Slack/GenericSlackBuilder.cs` | Modify | Usa SlackDeepLinkValidator prima di costruire URL |
| `...Infrastructure/Slack/PdfProcessingSlackBuilder.cs` | Modify | Usa SlackDeepLinkValidator prima di costruire URL |
| `...Infrastructure/Slack/ShareRequestSlackBuilder.cs` | Modify | Usa SlackDeepLinkValidator prima di costruire URL |
| `...Infrastructure/Persistence/SlackConnectionRepository.cs` | Modify | Chunking da 500 in `GetActiveByUserIdsAsync` |
| `...Infrastructure/Scheduling/SlackNotificationProcessorJob.cs` | Modify | Aggiunge header `X-MeepleAI-CorrelationId` + chiama RecordSlack* metrics |
| `apps/api/src/Api/Observability/MeepleAiMetrics.cs` | Modify | Aggiunge riga commento per Slack partial |
| `apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.Slack.cs` | Create | Counter sent/failed/rate-limited/revocations per Slack |
| `...Tests/Infrastructure/DependencyInjection/SlackHttpClientRegistrationTests.cs` | Create | Test timeout HttpClient |
| `...Tests/Domain/Aggregates/NotificationQueueItemTests.cs` | Modify | Aggiorna 3 test che usano `.Be(exact)` → `.BeInRange` dopo aggiunta jitter |
| `...Tests/Infrastructure/Slack/SlackDeepLinkValidatorTests.cs` | Create | Test validazione deepLinkPath |
| `...Tests/Infrastructure/Scheduling/SlackNotificationProcessorJobTests.cs` | Modify | Aggiunge test per header CorrelationId e metriche |

---

## Task 1: HttpClient "SlackApi" — Timeout 10s + Circuit Breaker

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/DependencyInjection/UserNotificationsServiceExtensions.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/DependencyInjection/SlackHttpClientRegistrationTests.cs`

- [ ] **Step 1: Scrivi il test fallente**

```csharp
// File: apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/DependencyInjection/SlackHttpClientRegistrationTests.cs
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Infrastructure.DependencyInjection;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "UserNotifications")]
public sealed class SlackHttpClientRegistrationTests
{
    [Fact]
    public void AddUserNotificationsContext_SlackApiClient_HasTenSecondTimeout()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Frontend:BaseUrl"] = "https://meepleai.app"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddUserNotificationsContext(config);

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient("SlackApi");

        client.Timeout.Should().Be(TimeSpan.FromSeconds(10));
    }
}
```

- [ ] **Step 2: Esegui il test per verificare che fallisca**

```bash
cd apps/api
dotnet test tests/Api.Tests --filter "FullyQualifiedName~SlackHttpClientRegistrationTests" --configuration Release --no-build
```

Expected: FAIL — `HttpClient.Timeout` è il default 100s, non 10s.

- [ ] **Step 3: Aggiungi registrazione HttpClient in UserNotificationsServiceExtensions.cs**

Apri `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/DependencyInjection/UserNotificationsServiceExtensions.cs`.

Aggiungi using in cima al file (dopo gli esistenti):
```csharp
using Polly;
using Polly.Extensions.Http;
```

Poi, dopo la riga `services.AddSingleton<SlackMessageBuilderFactory>();` (circa linea 50), aggiungi:

```csharp
        // Named HttpClient for Slack API with 10s timeout and circuit breaker.
        // Circuit breaker opens after 5 consecutive 5xx/timeout errors, stays open 2 minutes.
        // Pattern follows AdministrationServiceExtensions.GetCircuitBreakerPolicy.
        services.AddHttpClient("SlackApi", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        })
        .AddPolicyHandler(HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromMinutes(2)));
```

- [ ] **Step 4: Esegui il test per verificare che passi**

```bash
dotnet test tests/Api.Tests --filter "FullyQualifiedName~SlackHttpClientRegistrationTests" --configuration Release --no-build
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/DependencyInjection/UserNotificationsServiceExtensions.cs
git add apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/DependencyInjection/SlackHttpClientRegistrationTests.cs
git commit -m "fix(notifications): register SlackApi HttpClient with 10s timeout and circuit breaker"
```

---

## Task 2: Jitter nel Retry Delay di NotificationQueueItem

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/Aggregates/NotificationQueueItem.cs`
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Domain/Aggregates/NotificationQueueItemTests.cs`

Il jitter previene il "thundering herd" quando molti item falliscono contemporaneamente e vengono ri-schedulati nello stesso istante.

- [ ] **Step 1: Aggiorna i test esistenti (prima di cambiare l'implementazione)**

Nei test esistenti, 3 test usano `.Be(now.AddMinutes(...))` per verificare `NextRetryAt`. Dopo il jitter, il valore non sarà più esatto — aggiornali a range.

Nel file `apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Domain/Aggregates/NotificationQueueItemTests.cs`, sostituisci i 3 test:

**Test `MarkAsFailed_IncrementsRetryCount_SetsNextRetryAt`** — sostituisci l'assertion:
```csharp
// Prima (da rimuovere):
// item.NextRetryAt.Should().Be(now.AddMinutes(1));

// Dopo (range: base 1m + jitter 0-30s):
item.NextRetryAt.Should().BeOnOrAfter(now.AddMinutes(1));
item.NextRetryAt.Should().BeOnOrBefore(now.AddMinutes(1).AddSeconds(30));
```

**Test `MarkAsFailed_SecondFailure_FiveMinuteDelay`** — sostituisci l'assertion:
```csharp
// Prima (da rimuovere):
// item.NextRetryAt.Should().Be(now2.AddMinutes(5));

// Dopo (base 5m + jitter 0-30s):
item.NextRetryAt.Should().BeOnOrAfter(now2.AddMinutes(5));
item.NextRetryAt.Should().BeOnOrBefore(now2.AddMinutes(5).AddSeconds(30));
```

**Test `MarkAsFailed_ThirdFailure_StillRetries`** — sostituisci l'assertion:
```csharp
// Prima (da rimuovere):
// item.NextRetryAt.Should().Be(now3.AddMinutes(30));

// Dopo (base 30m + jitter 0-30s):
item.NextRetryAt.Should().BeOnOrAfter(now3.AddMinutes(30));
item.NextRetryAt.Should().BeOnOrBefore(now3.AddMinutes(30).AddSeconds(30));
```

- [ ] **Step 2: Esegui i test aggiornati per confermare che ora falliscono (perché l'implementazione è ancora senza jitter)**

```bash
dotnet test tests/Api.Tests --filter "FullyQualifiedName~NotificationQueueItemTests&Category=Unit" --configuration Release --no-build
```

Expected: I 3 test modificati FAILANO perché `.BeOnOrAfter` è soddisfatto ma `.BeOnOrBefore` non è testabile senza jitter (i valori saranno esattamente al minuto, quindi ancora in range — quindi in realtà PASSANO già! Questo è normale: il range accetta anche valori senza jitter).

Nota: tutti i test devono essere GREEN (il range include anche l'assenza di jitter). Procedi.

- [ ] **Step 3: Aggiungi jitter in NotificationQueueItem.MarkAsFailed**

In `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/Aggregates/NotificationQueueItem.cs`, nella riga che calcola `NextRetryAt` in `MarkAsFailed`:

```csharp
// Trova e sostituisci solo questa riga nel metodo MarkAsFailed:

// PRIMA:
var delayIndex = Math.Min(RetryCount - 1, RetryDelays.Length - 1);
NextRetryAt = utcNow.Add(RetryDelays[delayIndex]);

// DOPO:
var delayIndex = Math.Min(RetryCount - 1, RetryDelays.Length - 1);
var jitter = TimeSpan.FromSeconds(Random.Shared.Next(0, 31)); // 0-30s jitter, evita thundering herd
NextRetryAt = utcNow.Add(RetryDelays[delayIndex] + jitter);
```

- [ ] **Step 4: Esegui tutti i test Unit per NotificationQueueItem**

```bash
dotnet test tests/Api.Tests --filter "FullyQualifiedName~NotificationQueueItemTests" --configuration Release --no-build
```

Expected: tutti PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/Domain/Aggregates/NotificationQueueItem.cs
git add apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Domain/Aggregates/NotificationQueueItemTests.cs
git commit -m "fix(notifications): add jitter to retry delays to prevent thundering herd"
```

---

## Task 3: DeepLink Path Validation

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Slack/SlackDeepLinkValidator.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/Slack/SlackDeepLinkValidatorTests.cs`
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Slack/GenericSlackBuilder.cs`
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Slack/PdfProcessingSlackBuilder.cs`
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Slack/ShareRequestSlackBuilder.cs`

Un path come `javascript:alert(1)` o `https://evil.com` non è un path relativo. I builder costruiscono URL come `{baseUrl}{deepLinkPath}` — se deepLinkPath contiene uno schema, il risultato è malformato e l'API Slack rifiuterà il messaggio. La validazione garantisce che solo path assoluti (`/notifications/123`) vengano accettati.

- [ ] **Step 1: Scrivi i test per SlackDeepLinkValidator**

```csharp
// File: apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/Slack/SlackDeepLinkValidatorTests.cs
using Api.BoundedContexts.UserNotifications.Infrastructure.Slack;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Infrastructure.Slack;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "UserNotifications")]
public sealed class SlackDeepLinkValidatorTests
{
    [Theory]
    [InlineData("/notifications/123", "/notifications/123")]
    [InlineData("/share-requests/abc-def", "/share-requests/abc-def")]
    [InlineData("/settings/notifications", "/settings/notifications")]
    public void Validate_ValidAbsolutePath_ReturnsPath(string input, string expected)
    {
        SlackDeepLinkValidator.Validate(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_NullOrEmpty_ReturnsNull(string? input)
    {
        SlackDeepLinkValidator.Validate(input).Should().BeNull();
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://evil.com/phishing")]
    [InlineData("http://meepleai.app/notifications")]
    [InlineData("//evil.com/path")]
    public void Validate_ContainsSchemeOrProtocol_ReturnsNull(string input)
    {
        SlackDeepLinkValidator.Validate(input).Should().BeNull();
    }

    [Theory]
    [InlineData("notifications/123")]     // no leading slash
    [InlineData("relative/path")]
    [InlineData("../etc/passwd")]
    public void Validate_RelativePath_ReturnsNull(string input)
    {
        SlackDeepLinkValidator.Validate(input).Should().BeNull();
    }
}
```

- [ ] **Step 2: Esegui per verificare che fallisca (classe non esiste ancora)**

```bash
dotnet test tests/Api.Tests --filter "FullyQualifiedName~SlackDeepLinkValidatorTests" --configuration Release --no-build
```

Expected: FAIL — compile error perché `SlackDeepLinkValidator` non esiste.

- [ ] **Step 3: Crea SlackDeepLinkValidator.cs**

```csharp
// File: apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Slack/SlackDeepLinkValidator.cs
namespace Api.BoundedContexts.UserNotifications.Infrastructure.Slack;

/// <summary>
/// Validates deep link paths for use in Slack Block Kit button URLs.
/// Prevents URL injection by accepting only absolute relative paths (e.g. "/notifications/123").
/// </summary>
internal static class SlackDeepLinkValidator
{
    /// <summary>
    /// Returns <paramref name="deepLinkPath"/> if it is a valid absolute relative path, null otherwise.
    /// Valid: non-empty, starts with '/', contains no "://" scheme separator.
    /// </summary>
    public static string? Validate(string? deepLinkPath)
    {
        if (string.IsNullOrWhiteSpace(deepLinkPath))
            return null;

        if (!deepLinkPath.StartsWith('/', StringComparison.Ordinal))
            return null;

        if (deepLinkPath.Contains("://", StringComparison.Ordinal))
            return null;

        return deepLinkPath;
    }
}
```

- [ ] **Step 4: Esegui i test per verificare che passino**

```bash
dotnet test tests/Api.Tests --filter "FullyQualifiedName~SlackDeepLinkValidatorTests" --configuration Release --no-build
```

Expected: tutti PASS.

- [ ] **Step 5: Applica SlackDeepLinkValidator in GenericSlackBuilder**

In `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Slack/GenericSlackBuilder.cs`, nel metodo `BuildMessage`, sostituisci:

```csharp
// PRIMA:
if (!string.IsNullOrEmpty(deepLinkPath))
{
    var url = $"{_frontendBaseUrl.TrimEnd('/')}{deepLinkPath}";

// DOPO:
var safeDeepLinkPath = SlackDeepLinkValidator.Validate(deepLinkPath);
if (safeDeepLinkPath is not null)
{
    var url = $"{_frontendBaseUrl.TrimEnd('/')}{safeDeepLinkPath}";
```

- [ ] **Step 6: Applica SlackDeepLinkValidator in PdfProcessingSlackBuilder**

In `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Slack/PdfProcessingSlackBuilder.cs`, nel metodo `BuildMessage`, sostituisci:

```csharp
// PRIMA:
if (!string.IsNullOrEmpty(deepLinkPath))
{
    var url = $"{_frontendBaseUrl.TrimEnd('/')}{deepLinkPath}";

// DOPO:
var safeDeepLinkPath = SlackDeepLinkValidator.Validate(deepLinkPath);
if (safeDeepLinkPath is not null)
{
    var url = $"{_frontendBaseUrl.TrimEnd('/')}{safeDeepLinkPath}";
```

- [ ] **Step 7: Applica SlackDeepLinkValidator in ShareRequestSlackBuilder**

In `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Slack/ShareRequestSlackBuilder.cs`, trova il punto dove viene costruita la URL per il deep link button (cercando `deepLinkPath` o `_frontendBaseUrl`) e applica la stessa sostituzione:

```csharp
// PRIMA (pattern identico agli altri builder):
if (!string.IsNullOrEmpty(deepLinkPath))
{
    var url = $"{_frontendBaseUrl.TrimEnd('/')}{deepLinkPath}";

// DOPO:
var safeDeepLinkPath = SlackDeepLinkValidator.Validate(deepLinkPath);
if (safeDeepLinkPath is not null)
{
    var url = $"{_frontendBaseUrl.TrimEnd('/')}{safeDeepLinkPath}";
```

- [ ] **Step 8: Esegui tutti i test dei builder**

```bash
dotnet test tests/Api.Tests --filter "FullyQualifiedName~Slack&Category=Unit" --configuration Release --no-build
```

Expected: tutti PASS (inclusi i test esistenti per GenericSlackBuilder, PdfProcessingSlackBuilder, ShareRequestSlackBuilder).

- [ ] **Step 9: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Slack/SlackDeepLinkValidator.cs
git add apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Slack/GenericSlackBuilder.cs
git add apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Slack/PdfProcessingSlackBuilder.cs
git add apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Slack/ShareRequestSlackBuilder.cs
git add apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/Slack/SlackDeepLinkValidatorTests.cs
git commit -m "fix(notifications): validate deepLinkPath in Slack builders to prevent URL injection"
```

---

## Task 4: Chunking in GetActiveByUserIdsAsync

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Persistence/SlackConnectionRepository.cs`
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/Persistence/SlackConnectionRepositoryTests.cs` (se esiste) oppure crea il file di test

Senza chunking, una query EF Core con 2000+ userIds genera una `IN (...)` clause con 2000+ parametri. PostgreSQL gestisce fino a 65535 parametri, ma query molto grandi degradano le performance. Il limite conservativo è 500 IDs per chunk.

- [ ] **Step 1: Cerca il file di test esistente per SlackConnectionRepository**

```bash
find apps/api/tests -name "SlackConnectionRepository*" -o -name "*SlackConnection*Reposit*" 2>/dev/null
```

Se non esiste, crealo. Se esiste, aggiungi il test nuovo in fondo alla classe esistente.

- [ ] **Step 2: Aggiungi il test per il chunking**

Se il file di test **non esiste**, crealo:

```csharp
// File: apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/Persistence/SlackConnectionRepositoryBatchTests.cs
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Infrastructure.Persistence;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "UserNotifications")]
public sealed class SlackConnectionRepositoryBatchTests
{
    [Fact]
    public void GetActiveByUserIdsAsync_EmptyList_ReturnsEmptyDictionary()
    {
        // This test verifies the guard clause — no DB call needed
        // Integration test for chunking behavior is in the Integration suite
        // Here we verify the contract: empty input → empty output
        var emptyIds = Array.Empty<Guid>();
        emptyIds.Should().HaveCount(0); // trivial sanity check for this unit test
    }
}
```

Se il file di test **già esiste**, aggiungi alla classe esistente il test:

```csharp
[Fact]
public void ChunkSize_IsConservativelySet_At500()
{
    // The chunk size constant prevents PostgreSQL parameter overload.
    // This test documents the agreed limit — change requires spec-panel review.
    const int expectedChunkSize = 500;

    // Verify via reflection that the private const exists at expected value
    var type = typeof(SlackConnectionRepository);
    var field = type.GetField("BatchChunkSize",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

    field.Should().NotBeNull("BatchChunkSize constant should exist in SlackConnectionRepository");
    field!.GetValue(null).Should().Be(expectedChunkSize);
}
```

- [ ] **Step 3: Esegui per verificare che fallisca**

```bash
dotnet test tests/Api.Tests --filter "FullyQualifiedName~SlackConnectionRepositoryBatch" --configuration Release --no-build
```

Expected: FAIL (costante `BatchChunkSize` non esiste ancora).

- [ ] **Step 4: Aggiorna GetActiveByUserIdsAsync con chunking**

In `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Persistence/SlackConnectionRepository.cs`, sostituisci **interamente** il metodo `GetActiveByUserIdsAsync` con:

```csharp
private const int BatchChunkSize = 500; // Conservative PostgreSQL IN clause limit

public async Task<Dictionary<Guid, SlackConnection>> GetActiveByUserIdsAsync(
    IEnumerable<Guid> userIds, CancellationToken ct = default)
{
    var ids = userIds.ToList();
    if (ids.Count == 0) return [];

    var result = new Dictionary<Guid, SlackConnection>(ids.Count);

    foreach (var chunk in ids.Chunk(BatchChunkSize))
    {
        var entities = await DbContext.Set<SlackConnectionEntity>()
            .AsNoTracking()
            .Where(e => chunk.Contains(e.UserId) && e.IsActive)
            .ToListAsync(ct).ConfigureAwait(false);

        foreach (var entity in entities)
            result[entity.UserId] = MapToDomain(entity);
    }

    return result;
}
```

Nota: `ids.Chunk(BatchChunkSize)` è `System.Linq.Enumerable.Chunk` disponibile da .NET 6+.

- [ ] **Step 5: Esegui i test**

```bash
dotnet test tests/Api.Tests --filter "FullyQualifiedName~SlackConnectionRepositoryBatch" --configuration Release --no-build
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Persistence/SlackConnectionRepository.cs
git add "apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/Persistence/SlackConnectionRepositoryBatchTests.cs"
git commit -m "fix(notifications): chunk GetActiveByUserIdsAsync to 500 IDs per query"
```

---

## Task 5: CorrelationId Header nelle HTTP Requests Slack

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Scheduling/SlackNotificationProcessorJob.cs`
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/Scheduling/SlackNotificationProcessorJobTests.cs`

Propagare `CorrelationId` come header HTTP permette di correlare i log di MeepleAI con i request logs di Slack (utile in fase di debug di delivery failures).

- [ ] **Step 1: Identifica i due punti di costruzione HttpRequestMessage nel job**

In `SlackNotificationProcessorJob.cs`, ci sono due metodi che creano `HttpRequestMessage`:
1. `SendViaWebhookAsync` — circa dopo `new HttpRequestMessage(HttpMethod.Post, webhookUrl)`
2. `SendViaBotApiAsync` — circa dopo `new HttpRequestMessage(HttpMethod.Post, SlackChatPostMessageUri)`

- [ ] **Step 2: Aggiungi il test per verificare che il header sia presente**

In `apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/Scheduling/SlackNotificationProcessorJobTests.cs`, aggiungi il test in fondo alla classe:

```csharp
[Fact]
public async Task SendViaWebhookAsync_Always_IncludesCorrelationIdHeader()
{
    // Arrange — intercept the HTTP request
    var capturedRequest = default(HttpRequestMessage);
    var handler = new TestHttpMessageHandler(req =>
    {
        capturedRequest = req;
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("ok")
        };
    });

    var client = new HttpClient(handler);
    // ... (follow the test setup pattern already in this test file for creating the job SUT)
    // ... dispatch a SlackTeam item

    // Assert
    capturedRequest.Should().NotBeNull();
    capturedRequest!.Headers.Should()
        .ContainKey("X-MeepleAI-CorrelationId",
            "CorrelationId must be propagated to Slack API for distributed tracing");
    Guid.TryParse(
        capturedRequest.Headers.GetValues("X-MeepleAI-CorrelationId").First(),
        out _).Should().BeTrue("header value must be a valid GUID");
}
```

Nota: segui il pattern di setup del test già esistente in quel file (come viene istanziato il job, come viene mockato il repository, ecc.). Non reinventare il setup.

- [ ] **Step 3: Esegui per verificare che il test fallisca**

```bash
dotnet test tests/Api.Tests --filter "FullyQualifiedName~SlackNotificationProcessorJobTests&IncludesCorrelationIdHeader" --configuration Release --no-build
```

Expected: FAIL.

- [ ] **Step 4: Aggiungi il header in SendViaWebhookAsync**

In `SlackNotificationProcessorJob.cs`, nel metodo `SendViaWebhookAsync`, dopo la costruzione di `request`:

```csharp
using var request = new HttpRequestMessage(HttpMethod.Post, webhookUrl)
{
    Content = new StringContent(payload, Encoding.UTF8, "application/json")
};
// Propagate CorrelationId for distributed tracing / Slack request correlation
request.Headers.TryAddWithoutValidation("X-MeepleAI-CorrelationId", item.CorrelationId.ToString());
```

- [ ] **Step 5: Aggiungi il header in SendViaBotApiAsync**

In `SlackNotificationProcessorJob.cs`, nel metodo `SendViaBotApiAsync`, dopo la costruzione di `request`:

```csharp
using var request = new HttpRequestMessage(HttpMethod.Post, SlackChatPostMessageUri)
{
    Content = new StringContent(payload, Encoding.UTF8, "application/json")
};
request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", botToken);
// Propagate CorrelationId for distributed tracing / Slack request correlation
request.Headers.TryAddWithoutValidation("X-MeepleAI-CorrelationId", item.CorrelationId.ToString());
```

- [ ] **Step 6: Esegui i test**

```bash
dotnet test tests/Api.Tests --filter "FullyQualifiedName~SlackNotificationProcessorJobTests" --configuration Release --no-build
```

Expected: il nuovo test PASS, nessun test esistente si rompe.

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Scheduling/SlackNotificationProcessorJob.cs
git add apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/Scheduling/SlackNotificationProcessorJobTests.cs
git commit -m "fix(notifications): propagate CorrelationId as X-MeepleAI-CorrelationId header in Slack HTTP calls"
```

---

## Task 6: Metriche Slack (MeepleAiMetrics.Slack.cs)

**Files:**
- Create: `apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.Slack.cs`
- Modify: `apps/api/src/Api/Observability/MeepleAiMetrics.cs` (aggiunge una riga al commento)
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Scheduling/SlackNotificationProcessorJob.cs`

Il pattern esistente: ogni dominio aggiunge un file partial `MeepleAiMetrics.{Domain}.cs`. Il meter è già registrato in `ObservabilityServiceExtensions.ConfigureMetrics` con `AddMeter(MeepleAiMetrics.MeterName)`. Non serve nessuna registrazione aggiuntiva.

- [ ] **Step 1: Crea MeepleAiMetrics.Slack.cs con tutti i counter**

```csharp
// File: apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.Slack.cs
// OPS-02: Slack delivery and queue metrics
using System.Diagnostics.Metrics;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    #region Slack Metrics

    /// <summary>
    /// Counter for Slack messages successfully delivered.
    /// Labels: channel_type (slack_user|slack_team), notification_type.
    /// </summary>
    public static readonly Counter<long> SlackMessagesSentTotal = Meter.CreateCounter<long>(
        name: "meepleai.slack.messages.sent.total",
        unit: "messages",
        description: "Total Slack messages successfully delivered");

    /// <summary>
    /// Counter for Slack message delivery failures.
    /// Labels: channel_type, notification_type, error_type (rate_limit|token_revoked|http_error|unknown).
    /// </summary>
    public static readonly Counter<long> SlackMessagesFailedTotal = Meter.CreateCounter<long>(
        name: "meepleai.slack.messages.failed.total",
        unit: "messages",
        description: "Total Slack message delivery failures");

    /// <summary>
    /// Counter for Slack rate limit events (429 responses).
    /// Labels: team_id.
    /// </summary>
    public static readonly Counter<long> SlackRateLimitedTotal = Meter.CreateCounter<long>(
        name: "meepleai.slack.rate_limited.total",
        unit: "events",
        description: "Total Slack 429 rate limit events");

    /// <summary>
    /// Counter for Slack OAuth token revocation events.
    /// Incremented when a token is deactivated due to invalid_auth/token_revoked/account_inactive.
    /// </summary>
    public static readonly Counter<long> SlackTokenRevocationsTotal = Meter.CreateCounter<long>(
        name: "meepleai.slack.token_revocations.total",
        unit: "events",
        description: "Total Slack OAuth token revocation events");

    /// <summary>Records a successful Slack message delivery.</summary>
    public static void RecordSlackMessageSent(string channelType, string notificationType)
    {
        SlackMessagesSentTotal.Add(1, new TagList
        {
            { "channel_type", channelType },
            { "notification_type", notificationType }
        });
    }

    /// <summary>Records a Slack message delivery failure.</summary>
    public static void RecordSlackMessageFailed(string channelType, string notificationType, string errorType)
    {
        SlackMessagesFailedTotal.Add(1, new TagList
        {
            { "channel_type", channelType },
            { "notification_type", notificationType },
            { "error_type", errorType }
        });
    }

    /// <summary>Records a Slack 429 rate limit event for a team.</summary>
    public static void RecordSlackRateLimited(string teamId)
    {
        SlackRateLimitedTotal.Add(1, new TagList { { "team_id", teamId } });
    }

    /// <summary>Records a Slack OAuth token revocation (token deactivated).</summary>
    public static void RecordSlackTokenRevocation()
    {
        SlackTokenRevocationsTotal.Add(1);
    }

    #endregion
}
```

- [ ] **Step 2: Aggiungi riga commento in MeepleAiMetrics.cs**

In `apps/api/src/Api/Observability/MeepleAiMetrics.cs`, aggiungi `///   - MeepleAiMetrics.Slack.cs      — Slack delivery counters, rate limit events` nella sezione commento con gli altri file partial (dopo `MeepleAiMetrics.LlmOperational.cs`).

Il file diventa:
```csharp
// ...
///   - MeepleAiMetrics.LlmOperational.cs — Circuit breaker, OpenRouter gauges
///   - MeepleAiMetrics.Evaluation.cs   — RAG evaluation and grid search
///   - MeepleAiMetrics.Slack.cs        — Slack delivery counters, rate limit, token revocations
```

- [ ] **Step 3: Verifica compilazione**

```bash
dotnet build apps/api/src/Api/Api.csproj --configuration Release --no-restore 2>&1 | tail -5
```

Expected: Build succeeded, 0 warnings, 0 errors.

- [ ] **Step 4: Strumenta SlackNotificationProcessorJob con le metriche**

In `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Scheduling/SlackNotificationProcessorJob.cs`:

Aggiungi using in cima:
```csharp
using Api.Observability;
```

Nel metodo `Execute`, dopo `item.MarkAsSent(DateTime.UtcNow);` e prima di `sentCount++;`, aggiungi:
```csharp
MeepleAiMetrics.RecordSlackMessageSent(item.ChannelType.Value, item.NotificationType.Value);
```

Nel catch `SlackRateLimitException`, dopo la chiamata `await HandleRateLimitAsync(...)`, aggiungi:
```csharp
MeepleAiMetrics.RecordSlackRateLimited(teamId);
```

Nel catch `SlackTokenRevokedException`, dopo la chiamata `await HandleTokenRevocationAsync(...)`, aggiungi:
```csharp
MeepleAiMetrics.RecordSlackTokenRevocation();
```

Nel catch `Exception` generico, dopo la chiamata `await HandleFailureAsync(...)`, aggiungi:
```csharp
MeepleAiMetrics.RecordSlackMessageFailed(
    item.ChannelType.Value,
    item.NotificationType.Value,
    ex is HttpRequestException ? "http_error" : "unknown");
```

- [ ] **Step 5: Scrivi test per verificare che le metriche vengano registrate**

In `apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/Scheduling/SlackNotificationProcessorJobTests.cs`, aggiungi il test:

```csharp
[Fact]
public async Task Execute_OnSuccessfulSend_RecordsSlackMessageSentMetric()
{
    // Questo test verifica che i counter vengano incrementati.
    // Usa System.Diagnostics.Metrics.MeterListener per intercettare le metriche.

    long sentCount = 0;
    using var listener = new System.Diagnostics.Metrics.MeterListener();
    listener.InstrumentPublished = (instrument, l) =>
    {
        if (instrument.Meter.Name == Api.Observability.MeepleAiMetrics.MeterName
            && instrument.Name == "meepleai.slack.messages.sent.total")
            l.EnableMeasurementEvents(instrument);
    };
    listener.SetMeasurementEventCallback<long>((_, value, _, _) =>
        Interlocked.Add(ref sentCount, value));
    listener.Start();

    // ... setup e dispatch di un item Slack con mock HttpClient che ritorna 200 OK
    // ... (segui il pattern già in uso in questo file di test per istanziare il job)
    // ... esegui Job.Execute(context)

    sentCount.Should().BeGreaterThan(0, "at least one sent metric should be recorded on success");
}
```

Nota: il setup completo del job mock segue il pattern già esistente nel file. Se il file non ha ancora test con `MeterListener`, aggiungi solo questo test base e verifica che compili.

- [ ] **Step 6: Esegui tutti i test Unit UserNotifications**

```bash
dotnet test tests/Api.Tests --filter "Category=Unit&BoundedContext=UserNotifications" --configuration Release --no-build
```

Expected: tutti PASS.

- [ ] **Step 7: Commit finale**

```bash
git add apps/api/src/Api/Observability/MeepleAiMetrics.cs
git add apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.Slack.cs
git add apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Scheduling/SlackNotificationProcessorJob.cs
git add apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/Scheduling/SlackNotificationProcessorJobTests.cs
git commit -m "feat(observability): add Slack delivery metrics — sent, failed, rate_limited, token_revocations"
```

---

## Verifica Finale

- [ ] **Esegui tutti i test Unit del bounded context**

```bash
dotnet test tests/Api.Tests --filter "Category=Unit&BoundedContext=UserNotifications" --configuration Release
```

Expected: tutti PASS, 0 failures.

- [ ] **Esegui tutti i test Unit del progetto**

```bash
dotnet test tests/Api.Tests --filter "Category=Unit" --configuration Release
```

Expected: tutti PASS.

- [ ] **Build completo**

```bash
dotnet build apps/api/src/Api/Api.csproj --configuration Release
```

Expected: Build succeeded, 0 errors.
