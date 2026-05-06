# Slack Notifications Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Correggere tutti i bug e miglioramenti identificati dall'analisi spec-panel sul sistema di notifiche Slack di MeepleAI (9 fix, da critici a minor).

**Architecture:** Approccio payload-based per i builder fix (aggiungere `Status` a `ShareRequestPayload` e `IsCancelled` a `GameNightPayload`) anziché modificare l'interfaccia `ISlackMessageBuilder` — blast radius minimo. Domain-first per `MarkAsRateLimited` e `DeepLinkPath`. EF Core migration per la nuova colonna. Fix `TimeProvider`-injection nei validator/builder. Fix `NotificationDispatcher` con severity mapping e title mapping.

**Tech Stack:** .NET 9, EF Core 9, Quartz.NET, Slack Block Kit, xUnit, FluentAssertions, FakeTimeProvider (Microsoft.Extensions.Time.Testing), Moq

---

## File Map

| Azione | File |
|--------|------|
| Modify | `Domain/ValueObjects/NotificationPayloads.cs` |
| Modify | `Domain/ValueObjects/NotificationType.cs` |
| Modify | `Domain/Aggregates/NotificationQueueItem.cs` |
| Modify | `Domain/Repositories/ISlackConnectionRepository.cs` |
| Modify | `Infrastructure/Entities/UserNotifications/NotificationQueueEntity.cs` |
| Modify | `Infrastructure/EntityConfigurations/UserNotifications/NotificationQueueEntityConfiguration.cs` |
| Modify | `Infrastructure/Persistence/NotificationQueueRepository.cs` |
| Modify | `Infrastructure/Persistence/SlackConnectionRepository.cs` |
| Modify | `Infrastructure/Slack/ShareRequestSlackBuilder.cs` |
| Modify | `Infrastructure/Slack/GameNightSlackBuilder.cs` |
| Modify | `Infrastructure/Slack/SlackSignatureValidator.cs` |
| Modify | `Infrastructure/Slack/AdminAlertSlackBuilder.cs` |
| Modify | `Infrastructure/Services/NotificationDispatcher.cs` |
| Modify | `Infrastructure/Scheduling/SlackNotificationProcessorJob.cs` |
| Modify | `Infrastructure/DependencyInjection/UserNotificationsServiceExtensions.cs` |
| Create | EF Core migration: `AddDeepLinkPathToNotificationQueue` |
| Modify | `Tests/.../Slack/ShareRequestSlackBuilderTests.cs` |
| Modify | `Tests/.../Slack/GameNightSlackBuilderTests.cs` |
| Modify | `Tests/.../Domain/Aggregates/NotificationQueueItemTests.cs` |

---

## Task 1: Fix `ShareRequestPayload` — Aggiungere `Status`

**Problema:** `ShareRequestSlackBuilder.BuildMessage()` produce SEMPRE header "📥 Nuova Share Request" e bottoni Approva/Rifiuta, anche quando `NotificationType` è `Approved` o `Rejected`.

**Strategia:** Aggiungere `Status` a `ShareRequestPayload` (default `"created"` per backward-compat JSON) e rendere il builder context-aware.

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/ValueObjects/NotificationPayloads.cs`
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Slack/ShareRequestSlackBuilder.cs`
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/Slack/ShareRequestSlackBuilderTests.cs`

- [ ] **Step 1.1: Scrivi i test failing**

In `ShareRequestSlackBuilderTests.cs`, aggiungi i seguenti test dopo `BuildMessage_ButtonValuesContainOnlyGuid`:

```csharp
[Fact]
public void BuildMessage_WhenStatusIsCreated_ShowsApproveRejectButtons()
{
    var payload = new ShareRequestPayload(
        Guid.NewGuid(), "Mario", "Catan", null, Status: "created");

    var result = _sut.BuildMessage(payload, null);
    var json = JsonSerializer.Serialize(result);
    var doc = JsonDocument.Parse(json);
    var blocks = doc.RootElement.GetProperty("blocks");

    // Header: "Nuova Share Request"
    blocks[0].GetProperty("text").GetProperty("text").GetString()
        .Should().Contain("Nuova Share Request");

    // Actions block with 3 elements (Approve, Reject, Open)
    blocks.GetArrayLength().Should().Be(3);
    var elements = blocks[2].GetProperty("elements");
    elements.GetArrayLength().Should().Be(3);
    elements[0].GetProperty("action_id").GetString().Should().Be("share_request_approve");
    elements[1].GetProperty("action_id").GetString().Should().Be("share_request_reject");
}

[Fact]
public void BuildMessage_WhenStatusIsApproved_ShowsConfirmationWithoutActionButtons()
{
    var payload = new ShareRequestPayload(
        Guid.NewGuid(), "Mario", "Catan", null, Status: "approved");

    var result = _sut.BuildMessage(payload, null);
    var json = JsonSerializer.Serialize(result);
    var doc = JsonDocument.Parse(json);
    var blocks = doc.RootElement.GetProperty("blocks");

    // Header: "Approvata"
    blocks[0].GetProperty("text").GetProperty("text").GetString()
        .Should().Contain("Approvata");

    // 2 blocks only: header + section (no actions)
    blocks.GetArrayLength().Should().Be(2);
}

[Fact]
public void BuildMessage_WhenStatusIsRejected_ShowsRejectionWithoutActionButtons()
{
    var payload = new ShareRequestPayload(
        Guid.NewGuid(), "Mario", "Catan", null, Status: "rejected");

    var result = _sut.BuildMessage(payload, null);
    var json = JsonSerializer.Serialize(result);
    var doc = JsonDocument.Parse(json);
    var blocks = doc.RootElement.GetProperty("blocks");

    blocks[0].GetProperty("text").GetProperty("text").GetString()
        .Should().Contain("Rifiutata");
    blocks.GetArrayLength().Should().Be(2);
}
```

- [ ] **Step 1.2: Esegui i test per verificare che falliscono**

```bash
cd apps/api
dotnet test --filter "ShareRequestSlackBuilderTests" -v n 2>&1 | tail -20
```
Atteso: errori di compilazione su `Status` non esistente.

- [ ] **Step 1.3: Aggiorna `NotificationPayloads.cs` — aggiungi `Status` a `ShareRequestPayload`**

File: `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/ValueObjects/NotificationPayloads.cs`

Sostituisci:
```csharp
public record ShareRequestPayload(
    Guid ShareRequestId,
    string RequesterName,
    string GameTitle,
    string? GameImageUrl) : INotificationPayload;
```

Con:
```csharp
public record ShareRequestPayload(
    Guid ShareRequestId,
    string RequesterName,
    string GameTitle,
    string? GameImageUrl,
    string Status = "created") : INotificationPayload;
```

- [ ] **Step 1.4: Aggiorna `ShareRequestSlackBuilder.cs` — layout context-aware**

File: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Slack/ShareRequestSlackBuilder.cs`

Sostituisci l'intero metodo `BuildMessage`:
```csharp
public object BuildMessage(INotificationPayload payload, string? deepLinkPath)
{
    if (payload is not ShareRequestPayload sr)
    {
        throw new ArgumentException($"Expected {nameof(ShareRequestPayload)} but received {payload.GetType().Name}", nameof(payload));
    }

    var isCreated = string.Equals(sr.Status, "created", StringComparison.OrdinalIgnoreCase);
    var isApproved = string.Equals(sr.Status, "approved", StringComparison.OrdinalIgnoreCase);

    var (headerEmoji, headerText) = (isCreated, isApproved) switch
    {
        (true, _)  => ("\ud83d\udce5", "Nuova Share Request"),
        (_, true)  => ("\u2705", "Approvata"),
        _          => ("\u274c", "Rifiutata")
    };

    var blocks = new List<object>
    {
        new
        {
            type = "header",
            text = new { type = "plain_text", text = $"{headerEmoji} {headerText}", emoji = true }
        },
        BuildSectionBlock(sr)
    };

    if (isCreated)
    {
        var timestamp = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var blockId = $"sr:{sr.ShareRequestId}:{timestamp}";
        var deepLink = !string.IsNullOrEmpty(deepLinkPath)
            ? $"{_frontendBaseUrl.TrimEnd('/')}{deepLinkPath}"
            : $"{_frontendBaseUrl.TrimEnd('/')}/share-requests/{sr.ShareRequestId}";

        blocks.Add(new
        {
            type = "actions",
            block_id = blockId,
            elements = new object[]
            {
                new
                {
                    type = "button",
                    text = new { type = "plain_text", text = "\u2705 Approva", emoji = true },
                    style = "primary",
                    action_id = "share_request_approve",
                    value = sr.ShareRequestId.ToString()
                },
                new
                {
                    type = "button",
                    text = new { type = "plain_text", text = "\u274c Rifiuta", emoji = true },
                    style = "danger",
                    action_id = "share_request_reject",
                    value = sr.ShareRequestId.ToString()
                },
                new
                {
                    type = "button",
                    text = new { type = "plain_text", text = "\ud83d\udd17 Apri in MeepleAI", emoji = true },
                    action_id = "open_meepleai",
                    url = deepLink
                }
            }
        });
    }

    return new { blocks };
}
```

- [ ] **Step 1.5: Aggiorna i test esistenti che usano la factory `ShareRequestPayload` senza `Status`**

In `ShareRequestSlackBuilderTests.cs`, tutti i costruttori `new ShareRequestPayload(...)` già esistenti compilano perché `Status` ha default `"created"`. Verifica che i test `BuildMessage_ProducesApproveRejectOpenButtons` e `BuildMessage_ButtonValuesContainOnlyGuid` si aspettino ancora 3 blocchi. Se falliscono aggiornali: queste fixture non passano `Status` quindi usano il default `"created"` → deve ancora produrre 3 blocchi.

- [ ] **Step 1.6: Esegui i test per verificare che passano**

```bash
cd apps/api
dotnet test --filter "ShareRequestSlackBuilderTests" -v n 2>&1 | tail -30
```
Atteso: tutti i test passano.

- [ ] **Step 1.7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/Domain/ValueObjects/NotificationPayloads.cs \
        apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Slack/ShareRequestSlackBuilder.cs \
        apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/Slack/ShareRequestSlackBuilderTests.cs
git commit -m "fix(notifications): ShareRequestSlackBuilder — layout context-aware per stato approvazione/rifiuto"
```

---

## Task 2: Fix `GameNightPayload` — Aggiungere `IsCancelled`

**Problema:** `GameNightSlackBuilder.BuildMessage()` produce SEMPRE bottoni RSVP "Partecipo / Non Partecipo / Forse", anche per `GameNightCancelled`.

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/ValueObjects/NotificationPayloads.cs`
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Slack/GameNightSlackBuilder.cs`
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/Slack/GameNightSlackBuilderTests.cs`

- [ ] **Step 2.1: Scrivi i test failing**

In `GameNightSlackBuilderTests.cs`, aggiungi dopo i test esistenti:

```csharp
[Fact]
public void BuildMessage_WhenIsCancelledTrue_ShowsCancellationLayoutWithoutRsvpButtons()
{
    // Arrange
    var payload = new GameNightPayload(
        Guid.NewGuid(),
        "Serata Catan",
        new DateTime(2026, 5, 10, 20, 0, 0, DateTimeKind.Utc),
        "Mario",
        IsCancelled: true);

    // Act
    var result = _sut.BuildMessage(payload, null);
    var json = JsonSerializer.Serialize(result);
    var doc = JsonDocument.Parse(json);
    var blocks = doc.RootElement.GetProperty("blocks");

    // Assert — 2 blocks: header + section (no RSVP actions)
    blocks.GetArrayLength().Should().Be(2);

    var header = blocks[0];
    header.GetProperty("text").GetProperty("text").GetString()
        .Should().Contain("Annullata");

    // No actions block
    for (int i = 0; i < blocks.GetArrayLength(); i++)
    {
        blocks[i].GetProperty("type").GetString().Should().NotBe("actions");
    }
}

[Fact]
public void BuildMessage_WhenIsCancelledFalse_ShowsRsvpButtons()
{
    // Arrange
    var payload = new GameNightPayload(
        Guid.NewGuid(),
        "Serata Catan",
        new DateTime(2026, 5, 10, 20, 0, 0, DateTimeKind.Utc),
        "Mario",
        IsCancelled: false);

    // Act
    var result = _sut.BuildMessage(payload, null);
    var json = JsonSerializer.Serialize(result);
    var doc = JsonDocument.Parse(json);
    var blocks = doc.RootElement.GetProperty("blocks");

    // Assert — 3 blocks: header + section + actions
    blocks.GetArrayLength().Should().Be(3);
    blocks[2].GetProperty("type").GetString().Should().Be("actions");
    var elements = blocks[2].GetProperty("elements");
    elements[0].GetProperty("action_id").GetString().Should().Be("game_night_rsvp_yes");
}
```

- [ ] **Step 2.2: Esegui i test per verificare che falliscono**

```bash
cd apps/api
dotnet test --filter "GameNightSlackBuilderTests" -v n 2>&1 | tail -20
```
Atteso: errori di compilazione su `IsCancelled` non esistente.

- [ ] **Step 2.3: Aggiorna `NotificationPayloads.cs` — aggiungi `IsCancelled` a `GameNightPayload`**

File: `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/ValueObjects/NotificationPayloads.cs`

Sostituisci:
```csharp
public record GameNightPayload(
    Guid GameNightId,
    string Title,
    DateTime ScheduledAt,
    string OrganizerName) : INotificationPayload;
```

Con:
```csharp
public record GameNightPayload(
    Guid GameNightId,
    string Title,
    DateTime ScheduledAt,
    string OrganizerName,
    bool IsCancelled = false) : INotificationPayload;
```

- [ ] **Step 2.4: Aggiorna `GameNightSlackBuilder.cs` — layout cancellation-aware**

File: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Slack/GameNightSlackBuilder.cs`

Sostituisci il metodo `BuildMessage`:
```csharp
public object BuildMessage(INotificationPayload payload, string? deepLinkPath)
{
    if (payload is not GameNightPayload gn)
    {
        throw new ArgumentException($"Expected {nameof(GameNightPayload)} but received {payload.GetType().Name}", nameof(payload));
    }

    var scheduledDate = gn.ScheduledAt.ToString("dddd d MMMM yyyy, HH:mm", CultureInfo.InvariantCulture);

    if (gn.IsCancelled)
    {
        return new
        {
            blocks = new object[]
            {
                new
                {
                    type = "header",
                    text = new { type = "plain_text", text = $"\u274c Annullata: {gn.Title}", emoji = true }
                },
                new
                {
                    type = "section",
                    text = new
                    {
                        type = "mrkdwn",
                        text = $"\ud83d\udcc5 *Era prevista per*: {scheduledDate}\n\ud83d\udc64 *Organizzatore*: {gn.OrganizerName}\n\n_La serata è stata annullata._"
                    }
                }
            }
        };
    }

    var timestamp = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
    var blockId = $"gn:{gn.GameNightId}:{timestamp}";

    var blocks = new List<object>
    {
        new
        {
            type = "header",
            text = new { type = "plain_text", text = $"\ud83c\udfb2 {gn.Title}", emoji = true }
        },
        new
        {
            type = "section",
            text = new
            {
                type = "mrkdwn",
                text = $"\ud83d\udcc5 *Data*: {scheduledDate}\n\ud83d\udc64 *Organizzatore*: {gn.OrganizerName}"
            }
        },
        new
        {
            type = "actions",
            block_id = blockId,
            elements = new object[]
            {
                new
                {
                    type = "button",
                    text = new { type = "plain_text", text = "\u2705 Partecipo", emoji = true },
                    style = "primary",
                    action_id = "game_night_rsvp_yes",
                    value = gn.GameNightId.ToString()
                },
                new
                {
                    type = "button",
                    text = new { type = "plain_text", text = "\u274c Non partecipo", emoji = true },
                    style = "danger",
                    action_id = "game_night_rsvp_no",
                    value = gn.GameNightId.ToString()
                },
                new
                {
                    type = "button",
                    text = new { type = "plain_text", text = "\ud83e\udd14 Forse", emoji = true },
                    action_id = "game_night_rsvp_maybe",
                    value = gn.GameNightId.ToString()
                }
            }
        }
    };

    return new { blocks };
}
```

- [ ] **Step 2.5: Esegui i test per verificare che passano**

```bash
cd apps/api
dotnet test --filter "GameNightSlackBuilderTests" -v n 2>&1 | tail -30
```
Atteso: tutti i test passano.

- [ ] **Step 2.6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/Domain/ValueObjects/NotificationPayloads.cs \
        apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Slack/GameNightSlackBuilder.cs \
        apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/Slack/GameNightSlackBuilderTests.cs
git commit -m "fix(notifications): GameNightSlackBuilder — no RSVP buttons per game night cancellata"
```

---

## Task 3: Fix `SlackSignatureValidator` — Inject `TimeProvider`

**Problema:** `DateTimeOffset.UtcNow` hardcoded, non testabile. Il replay-check `> 300s` non può essere verificato in unit test senza manipolare il clock reale.

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Slack/SlackSignatureValidator.cs`
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/DependencyInjection/UserNotificationsServiceExtensions.cs`

> **Nota:** Non esiste ancora un test file per `SlackSignatureValidator`. Andrà creato.

- [ ] **Step 3.1: Scrivi il test file**

Crea file: `apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/Slack/SlackSignatureValidatorTests.cs`

```csharp
using Api.BoundedContexts.UserNotifications.Infrastructure.Configuration;
using Api.BoundedContexts.UserNotifications.Infrastructure.Slack;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Infrastructure.Slack;

[Trait("Category", "Unit")]
public sealed class SlackSignatureValidatorTests
{
    private const string TestSigningSecret = "8f742231b10e8888abcd99baed85bc4ab719a2dv";

    private static SlackSignatureValidator CreateSut(DateTimeOffset now)
    {
        var config = Options.Create(new SlackNotificationConfiguration
        {
            SigningSecret = TestSigningSecret,
            ClientId = "client",
            ClientSecret = "secret",
            RedirectUri = "https://meepleai.app/slack/callback"
        });
        var timeProvider = new FakeTimeProvider(now);
        return new SlackSignatureValidator(config, timeProvider);
    }

    private static string ComputeSignature(string timestamp, string body)
    {
        var baseString = $"v0:{timestamp}:{body}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(TestSigningSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(baseString));
        return "v0=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    [Fact]
    public void Validate_WithValidSignatureAndFreshTimestamp_ReturnsTrue()
    {
        var now = new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);
        var timestamp = now.ToUnixTimeSeconds().ToString();
        var body = "payload=%7B%22type%22%3A%22block_actions%22%7D";
        var sig = ComputeSignature(timestamp, body);

        var sut = CreateSut(now);
        sut.Validate(timestamp, body, sig).Should().BeTrue();
    }

    [Fact]
    public void Validate_WithExpiredTimestamp_ReturnsFalse()
    {
        var now = new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);
        // Timestamp 6 minutes old — exceeds 5-minute window
        var oldTimestamp = now.AddMinutes(-6).ToUnixTimeSeconds().ToString();
        var body = "payload=test";
        var sig = ComputeSignature(oldTimestamp, body);

        var sut = CreateSut(now);
        sut.Validate(oldTimestamp, body, sig).Should().BeFalse();
    }

    [Fact]
    public void Validate_WithFutureTimestamp_ReturnsFalse()
    {
        var now = new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);
        // Timestamp 6 minutes in the future (replay prevention)
        var futureTimestamp = now.AddMinutes(6).ToUnixTimeSeconds().ToString();
        var body = "payload=test";
        var sig = ComputeSignature(futureTimestamp, body);

        var sut = CreateSut(now);
        sut.Validate(futureTimestamp, body, sig).Should().BeFalse();
    }

    [Fact]
    public void Validate_WithWrongSignature_ReturnsFalse()
    {
        var now = new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);
        var timestamp = now.ToUnixTimeSeconds().ToString();

        var sut = CreateSut(now);
        sut.Validate(timestamp, "payload=test", "v0=000wrongsignature").Should().BeFalse();
    }

    [Fact]
    public void Validate_WithEmptyTimestamp_ReturnsFalse()
    {
        var sut = CreateSut(DateTimeOffset.UtcNow);
        sut.Validate("", "body", "v0=sig").Should().BeFalse();
    }
}
```

- [ ] **Step 3.2: Esegui i test per verificare che falliscono**

```bash
cd apps/api
dotnet test --filter "SlackSignatureValidatorTests" -v n 2>&1 | tail -20
```
Atteso: errore di compilazione — `SlackSignatureValidator` non accetta `TimeProvider`.

- [ ] **Step 3.3: Aggiorna `SlackSignatureValidator.cs`**

File: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Slack/SlackSignatureValidator.cs`

Sostituisci il file con:
```csharp
using System.Security.Cryptography;
using System.Text;
using Api.BoundedContexts.UserNotifications.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Slack;

/// <summary>
/// Validates Slack request signatures using HMAC-SHA256.
/// Slack signs every interaction payload with the app's signing secret.
/// See: https://api.slack.com/authentication/verifying-requests-from-slack
/// </summary>
internal class SlackSignatureValidator
{
    private readonly string _signingSecret;
    private readonly TimeProvider _timeProvider;

    public SlackSignatureValidator(IOptions<SlackNotificationConfiguration> config, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(config);
        _signingSecret = config.Value.SigningSecret;
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>
    /// Validates the Slack request signature against the computed HMAC-SHA256 hash.
    /// Uses constant-time comparison to prevent timing attacks.
    /// </summary>
    public bool Validate(string timestamp, string body, string signature)
    {
        if (string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(signature))
            return false;

        if (!long.TryParse(timestamp, System.Globalization.CultureInfo.InvariantCulture, out var ts))
            return false;

        var requestTime = DateTimeOffset.FromUnixTimeSeconds(ts);
        var now = _timeProvider.GetUtcNow();
        if (Math.Abs((now - requestTime).TotalSeconds) > 300)
            return false;

        var baseString = $"v0:{timestamp}:{body}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_signingSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(baseString));
        var computed = "v0=" + Convert.ToHexString(hash).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed),
            Encoding.UTF8.GetBytes(signature));
    }
}
```

- [ ] **Step 3.4: Aggiorna `UserNotificationsServiceExtensions.cs` — registra `SlackSignatureValidator` con `TimeProvider`**

File: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/DependencyInjection/UserNotificationsServiceExtensions.cs`

Trova la riga che registra `SlackSignatureValidator` come singleton. La registrazione tramite il DI container .NET risolve automaticamente `TimeProvider` se registrato (viene registrato come `TimeProvider.System` in `Program.cs`). Nessuna modifica esplicita è necessaria se l'interfaccia del costruttore è cambiata.

Verifica che in `UserNotificationsServiceExtensions.cs` esista una riga tipo:
```csharp
services.AddSingleton<SlackSignatureValidator>();
```
Questa funziona automaticamente perché `TimeProvider` è già registrato nel container come `TimeProvider.System`.

- [ ] **Step 3.5: Esegui i test**

```bash
cd apps/api
dotnet test --filter "SlackSignatureValidatorTests" -v n 2>&1 | tail -30
```
Atteso: tutti e 5 i test passano.

- [ ] **Step 3.6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Slack/SlackSignatureValidator.cs \
        apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/Slack/SlackSignatureValidatorTests.cs
git commit -m "fix(notifications): SlackSignatureValidator — inject TimeProvider, replay protection testabile"
```

---

## Task 4: Fix `AdminAlertSlackBuilder` — Inject `TimeProvider`

**Problema:** `DateTime.UtcNow` hardcoded nella timestamp del context block, non testabile.

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Slack/AdminAlertSlackBuilder.cs`

> **Nota:** Controlla se esiste già un test file per `AdminAlertSlackBuilder`. Se non esiste, crealo.

- [ ] **Step 4.1: Scrivi il test file**

Crea (o aggiorna) `apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/Slack/AdminAlertSlackBuilderTests.cs`:

```csharp
using System.Text.Json;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.BoundedContexts.UserNotifications.Infrastructure.Slack;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Infrastructure.Slack;

[Trait("Category", "Unit")]
public sealed class AdminAlertSlackBuilderTests
{
    private static readonly DateTimeOffset FixedTime = new(2026, 3, 15, 10, 30, 0, TimeSpan.Zero);

    private static AdminAlertSlackBuilder CreateSut()
        => new(new FakeTimeProvider(FixedTime));

    [Theory]
    [InlineData("admin_new_share_request")]
    [InlineData("admin_stale_share_requests")]
    [InlineData("admin_review_lock_expiring")]
    [InlineData("admin_system_health_alert")]
    [InlineData("admin_model_status_changed")]
    [InlineData("admin_openrouter_threshold_alert")]
    [InlineData("admin_openrouter_daily_summary")]
    [InlineData("admin_shared_game_submitted")]
    public void CanHandle_AdminTypes_ReturnsTrue(string typeValue)
    {
        CreateSut().CanHandle(NotificationType.FromString(typeValue)).Should().BeTrue();
    }

    [Fact]
    public void CanHandle_NonAdminType_ReturnsFalse()
    {
        CreateSut().CanHandle(NotificationType.BadgeEarned).Should().BeFalse();
    }

    [Fact]
    public void BuildMessage_ContainsFixedTimestampInContextBlock()
    {
        var payload = new GenericPayload("Test Alert", "Something happened.");

        var result = CreateSut().BuildMessage(payload, null);
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);

        // AdminAlertSlackBuilder uses attachments
        var blocks = doc.RootElement.GetProperty("attachments")[0].GetProperty("blocks");
        var contextText = blocks[2].GetProperty("elements")[0].GetProperty("text").GetString();

        contextText.Should().Contain("2026-03-15 10:30");
    }

    [Fact]
    public void BuildMessage_CriticalTitle_ProducesDangerAttachment()
    {
        var payload = new GenericPayload("CRITICAL Circuit Breaker Open", "Service degraded.");

        var result = CreateSut().BuildMessage(payload, null);
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("attachments")[0].GetProperty("color").GetString()
            .Should().Be("danger");
    }

    [Fact]
    public void BuildMessage_WarningTitle_ProducesWarningAttachment()
    {
        var payload = new GenericPayload("WARNING Budget RPM approaching", "80% of quota used.");

        var result = CreateSut().BuildMessage(payload, null);
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("attachments")[0].GetProperty("color").GetString()
            .Should().Be("warning");
    }

    [Fact]
    public void BuildMessage_WithWrongPayloadType_ThrowsArgumentException()
    {
        var act = () => CreateSut().BuildMessage(new BadgePayload(Guid.NewGuid(), "X", "Y"), null);
        act.Should().Throw<ArgumentException>().WithMessage("*GenericPayload*BadgePayload*");
    }
}
```

- [ ] **Step 4.2: Esegui i test per verificare che falliscono**

```bash
cd apps/api
dotnet test --filter "AdminAlertSlackBuilderTests" -v n 2>&1 | tail -20
```
Atteso: errore di compilazione — `AdminAlertSlackBuilder` non accetta `TimeProvider`.

- [ ] **Step 4.3: Aggiorna `AdminAlertSlackBuilder.cs` — inject `TimeProvider`**

File: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Slack/AdminAlertSlackBuilder.cs`

In cima alla classe, aggiungi il campo e aggiorna il costruttore:
```csharp
internal sealed class AdminAlertSlackBuilder : ISlackMessageBuilder
{
    private readonly TimeProvider _timeProvider;

    public AdminAlertSlackBuilder(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }
```

Nel metodo `BuildMessage`, sostituisci la riga con `DateTime.UtcNow`:
```csharp
// Prima:
new { type = "mrkdwn", text = $"MeepleAI Monitoring | {DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC" }

// Dopo:
new { type = "mrkdwn", text = $"MeepleAI Monitoring | {_timeProvider.GetUtcNow().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC" }
```

- [ ] **Step 4.4: Esegui i test**

```bash
cd apps/api
dotnet test --filter "AdminAlertSlackBuilderTests" -v n 2>&1 | tail -30
```
Atteso: tutti i test passano.

- [ ] **Step 4.5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Slack/AdminAlertSlackBuilder.cs \
        apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/Slack/AdminAlertSlackBuilderTests.cs
git commit -m "fix(notifications): AdminAlertSlackBuilder — inject TimeProvider, timestamp testabile"
```

---

## Task 5: `NotificationQueueItem.MarkAsRateLimited()` — non incrementa RetryCount

**Problema:** `HandleRateLimitAsync` chiama `MarkAsFailed()` che incrementa `RetryCount`, consumando uno slot di retry per qualcosa che non è un fallimento applicativo. Serve un metodo dedicato `MarkAsRateLimited()`.

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/Aggregates/NotificationQueueItem.cs`
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Domain/Aggregates/NotificationQueueItemTests.cs`

- [ ] **Step 5.1: Scrivi i test failing**

In `NotificationQueueItemTests.cs`, aggiungi dopo `SetNextRetryAt_SetsField`:

```csharp
[Fact]
public void MarkAsRateLimited_FromProcessing_DoesNotIncrementRetryCount()
{
    // Arrange
    var item = CreateDefaultItem();
    item.MarkAsProcessing();
    var expectedRetryCount = item.RetryCount; // 0

    // Act
    var retryAt = new DateTime(2026, 3, 15, 12, 5, 0, DateTimeKind.Utc);
    item.MarkAsRateLimited(retryAt);

    // Assert
    item.RetryCount.Should().Be(expectedRetryCount); // still 0
    item.Status.Should().Be(NotificationQueueStatus.Failed);
    item.NextRetryAt.Should().Be(retryAt);
    item.LastError.Should().Contain("rate limit");
}

[Fact]
public void MarkAsRateLimited_FromPending_Throws()
{
    // Arrange
    var item = CreateDefaultItem();
    var fixedRetryAt = new DateTime(2026, 3, 15, 12, 0, 30, DateTimeKind.Utc);

    // Act & Assert
    var act = () => item.MarkAsRateLimited(fixedRetryAt);
    act.Should().Throw<InvalidOperationException>();
}

[Fact]
public void MarkAsRateLimited_MultipleTimes_NeverExceedsMaxRetries()
{
    var item = CreateDefaultItem();
    var fixedRetryAt = new DateTime(2026, 3, 15, 12, 0, 30, DateTimeKind.Utc);

    for (int i = 0; i < 10; i++)
    {
        item.MarkAsProcessing();
        item.MarkAsRateLimited(fixedRetryAt);
    }

    item.RetryCount.Should().Be(0);
    item.Status.Should().Be(NotificationQueueStatus.Failed);
}
```

- [ ] **Step 5.2: Esegui i test per verificare che falliscono**

```bash
cd apps/api
dotnet test --filter "NotificationQueueItemTests" -v n 2>&1 | tail -20
```
Atteso: errore di compilazione — `MarkAsRateLimited` non esiste.

- [ ] **Step 5.3: Aggiungi `MarkAsRateLimited` a `NotificationQueueItem.cs`**

File: `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/Aggregates/NotificationQueueItem.cs`

Aggiungi dopo il metodo `SetNextRetryAt`:
```csharp
/// <summary>
/// Marks the notification as temporarily unavailable due to API rate limiting.
/// Does NOT increment RetryCount — rate limit backpressure is not a delivery failure.
/// The item will be retried after the specified time.
/// </summary>
public void MarkAsRateLimited(DateTime retryAt)
{
    if (!Status.IsProcessing)
        throw new InvalidOperationException($"Cannot mark as rate-limited from status '{Status.Value}'");

    Status = NotificationQueueStatus.Failed;
    NextRetryAt = retryAt;
    LastError = "rate limit — retry after backoff";
}
```

- [ ] **Step 5.4: Esegui i test**

```bash
cd apps/api
dotnet test --filter "NotificationQueueItemTests" -v n 2>&1 | tail -30
```
Atteso: tutti i test passano.

- [ ] **Step 5.5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/Domain/Aggregates/NotificationQueueItem.cs \
        apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Domain/Aggregates/NotificationQueueItemTests.cs
git commit -m "fix(notifications): NotificationQueueItem — aggiungi MarkAsRateLimited che non incrementa RetryCount"
```

---

## Task 6: `DeepLinkPath` — propagare dalla chain domain → entity → repo → dispatcher

**Problema:** `DeepLinkPath` è presente su `NotificationMessage` ma NON viene passato a `NotificationQueueItem.Create()`. Il job poi chiama `BuildMessage(item.Payload, null)` sempre con `null`, rendendo inutili i deep link.

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/Aggregates/NotificationQueueItem.cs`
- Modify: `apps/api/src/Api/Infrastructure/Entities/UserNotifications/NotificationQueueEntity.cs`
- Modify: `apps/api/src/Api/Infrastructure/EntityConfigurations/UserNotifications/NotificationQueueEntityConfiguration.cs`
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Persistence/NotificationQueueRepository.cs`
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Services/NotificationDispatcher.cs`
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Scheduling/SlackNotificationProcessorJob.cs`
- Create: EF Core migration

- [ ] **Step 6.1: Scrivi i test failing per `NotificationQueueItem`**

In `NotificationQueueItemTests.cs`, aggiorna l'helper `CreateDefaultItem` e aggiungi:

```csharp
// Aggiorna il metodo helper per supportare deepLinkPath
private static NotificationQueueItem CreateDefaultItem(
    NotificationChannelType? channelType = null,
    Guid? recipientUserId = null,
    bool nullRecipient = false,
    NotificationType? notificationType = null,
    INotificationPayload? payload = null,
    string? slackChannelTarget = null,
    string? slackTeamId = null,
    string? deepLinkPath = null)  // NUOVO PARAMETRO
{
    return NotificationQueueItem.Create(
        channelType ?? NotificationChannelType.SlackUser,
        nullRecipient ? null : (recipientUserId ?? DefaultRecipientUserId),
        notificationType ?? NotificationType.DocumentReady,
        payload ?? new GenericPayload("Test", "Body"),
        slackChannelTarget,
        slackTeamId,
        deepLinkPath: deepLinkPath);  // NUOVO
}

[Fact]
public void Create_WithDeepLinkPath_SetsProperty()
{
    // Act
    var item = CreateDefaultItem(deepLinkPath: "/library/documents/abc123");

    // Assert
    item.DeepLinkPath.Should().Be("/library/documents/abc123");
}

[Fact]
public void Create_WithoutDeepLinkPath_PropertyIsNull()
{
    var item = CreateDefaultItem();
    item.DeepLinkPath.Should().BeNull();
}
```

- [ ] **Step 6.2: Esegui i test per verificare che falliscono**

```bash
cd apps/api
dotnet test --filter "NotificationQueueItemTests" -v n 2>&1 | tail -20
```

- [ ] **Step 6.3: Aggiungi `DeepLinkPath` a `NotificationQueueItem.cs`**

File: `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/Aggregates/NotificationQueueItem.cs`

1. Aggiungi la property dopo `CorrelationId`:
```csharp
public string? DeepLinkPath { get; private set; }
```

2. Aggiungi il parametro al costruttore privato:
```csharp
private NotificationQueueItem(
    Guid id,
    NotificationChannelType channelType,
    Guid? recipientUserId,
    NotificationType notificationType,
    INotificationPayload payload,
    string? slackChannelTarget,
    string? slackTeamId,
    Guid correlationId,
    string? deepLinkPath = null,   // NUOVO
    DateTime? createdAt = null)
    : base(id)
{
    // ... (esistente) ...
    DeepLinkPath = deepLinkPath;   // NUOVO
}
```

3. Aggiorna il factory method `Create`:
```csharp
public static NotificationQueueItem Create(
    NotificationChannelType channelType,
    Guid? recipientUserId,
    NotificationType notificationType,
    INotificationPayload payload,
    string? slackChannelTarget = null,
    string? slackTeamId = null,
    Guid? correlationId = null,
    string? deepLinkPath = null,   // NUOVO
    DateTime? createdAt = null)
{
    return new NotificationQueueItem(
        Guid.NewGuid(),
        channelType,
        recipientUserId,
        notificationType,
        payload,
        slackChannelTarget,
        slackTeamId,
        correlationId ?? Guid.NewGuid(),
        deepLinkPath,               // NUOVO
        createdAt);
}
```

4. Aggiorna `Reconstitute` per includere `deepLinkPath`:
```csharp
internal static NotificationQueueItem Reconstitute(
    Guid id,
    NotificationChannelType channelType,
    Guid? recipientUserId,
    NotificationType notificationType,
    INotificationPayload payload,
    string? slackChannelTarget,
    string? slackTeamId,
    NotificationQueueStatus status,
    int retryCount,
    int maxRetries,
    DateTime? nextRetryAt,
    string? lastError,
    DateTime createdAt,
    DateTime? processedAt,
    Guid correlationId,
    string? deepLinkPath = null)   // NUOVO
{
    var item = new NotificationQueueItem(
        id, channelType, recipientUserId, notificationType,
        payload, slackChannelTarget, slackTeamId, correlationId, deepLinkPath);
    item.Status = status;
    item.RetryCount = retryCount;
    item.MaxRetries = maxRetries;
    item.NextRetryAt = nextRetryAt;
    item.LastError = lastError;
    item.CreatedAt = createdAt;
    item.ProcessedAt = processedAt;
    return item;
}
```

- [ ] **Step 6.4: Aggiungi `DeepLinkPath` a `NotificationQueueEntity.cs`**

File: `apps/api/src/Api/Infrastructure/Entities/UserNotifications/NotificationQueueEntity.cs`

Aggiungi dopo la property `CorrelationId`:
```csharp
[MaxLength(500)]
[Column("deep_link_path")]
public string? DeepLinkPath { get; set; }
```

- [ ] **Step 6.5: Aggiorna `NotificationQueueRepository.cs` — MapToPersistence e MapToDomain**

File: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Persistence/NotificationQueueRepository.cs`

In `MapToPersistence`, aggiungi:
```csharp
DeepLinkPath = item.DeepLinkPath,   // NUOVO
```

In `MapToDomain`, aggiungi `deepLinkPath: entity.DeepLinkPath` alla chiamata `Reconstitute`:
```csharp
return NotificationQueueItem.Reconstitute(
    id: entity.Id,
    // ... parametri esistenti ...
    correlationId: entity.CorrelationId,
    deepLinkPath: entity.DeepLinkPath);   // NUOVO
```

- [ ] **Step 6.6: Aggiorna `NotificationDispatcher.cs` — passa `DeepLinkPath` a `Create`**

File: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Services/NotificationDispatcher.cs`

Nella creazione di `emailItem` (sezione email, linea ~87):
```csharp
var emailItem = NotificationQueueItem.Create(
    channelType: NotificationChannelType.Email,
    recipientUserId: message.RecipientUserId,
    notificationType: message.Type,
    payload: message.Payload,
    correlationId: correlationId,
    deepLinkPath: message.DeepLinkPath);   // NUOVO
```

Nella creazione di `slackItem` (sezione Slack DM, linea ~114):
```csharp
var slackItem = NotificationQueueItem.Create(
    channelType: NotificationChannelType.SlackUser,
    recipientUserId: message.RecipientUserId,
    notificationType: message.Type,
    payload: message.Payload,
    slackChannelTarget: slackConnection.DmChannelId,
    slackTeamId: slackConnection.SlackTeamId,
    correlationId: correlationId,
    deepLinkPath: message.DeepLinkPath);   // NUOVO
```

Nella creazione di `teamItem` (sezione Slack team, linea ~146):
```csharp
var teamItem = NotificationQueueItem.Create(
    channelType: NotificationChannelType.SlackTeam,
    recipientUserId: null,
    notificationType: message.Type,
    payload: message.Payload,
    slackChannelTarget: settings.WebhookUrl,
    slackTeamId: teamId,
    correlationId: correlationId,
    deepLinkPath: message.DeepLinkPath);   // NUOVO
```

- [ ] **Step 6.7: Aggiorna `SlackNotificationProcessorJob.cs` — usa `item.DeepLinkPath`**

File: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Scheduling/SlackNotificationProcessorJob.cs`

In `SendViaWebhookAsync` (linea ~202):
```csharp
// Prima:
var message = builder.BuildMessage(item.Payload, null);
// Dopo:
var message = builder.BuildMessage(item.Payload, item.DeepLinkPath);
```

In `SendViaBotApiAsync` (linea ~248):
```csharp
// Prima:
var blockKitMessage = builder.BuildMessage(item.Payload, null);
// Dopo:
var blockKitMessage = builder.BuildMessage(item.Payload, item.DeepLinkPath);
```

- [ ] **Step 6.8: Crea migration EF Core**

```bash
cd apps/api/src/Api
dotnet ef migrations add AddDeepLinkPathToNotificationQueue
```

Verifica il file generato in `Infrastructure/Migrations/` — deve contenere:
```csharp
migrationBuilder.AddColumn<string>(
    name: "deep_link_path",
    table: "notification_queue_items",
    type: "character varying(500)",
    maxLength: 500,
    nullable: true);
```

- [ ] **Step 6.9: Esegui i test**

```bash
cd apps/api
dotnet test --filter "NotificationQueueItemTests" -v n 2>&1 | tail -30
```
Atteso: tutti i test passano.

- [ ] **Step 6.10: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/Domain/Aggregates/NotificationQueueItem.cs \
        apps/api/src/Api/Infrastructure/Entities/UserNotifications/NotificationQueueEntity.cs \
        apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Persistence/NotificationQueueRepository.cs \
        apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Services/NotificationDispatcher.cs \
        apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Scheduling/SlackNotificationProcessorJob.cs \
        apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Domain/Aggregates/NotificationQueueItemTests.cs \
        apps/api/src/Api/Infrastructure/Migrations/
git commit -m "feat(notifications): DeepLinkPath propagato da NotificationMessage → queue → Slack builder"
```

---

## Task 7: `NotificationType.SlackConnectionRevoked` — aggiungi il tipo

**Problema:** `HandleTokenRevocationAsync` usa `NotificationType.DocumentProcessingFailed` per la notifica in-app "Slack Disconnected" — tipo semanticamente sbagliato, fuori posto in analytics e filtri.

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/ValueObjects/NotificationType.cs`
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Scheduling/SlackNotificationProcessorJob.cs`

> **Nota:** Non richiede migration — `NotificationType` è un value object stringa. La nuova stringa viene usata solo per notifiche in-app.

- [ ] **Step 7.1: Aggiungi `SlackConnectionRevoked` a `NotificationType.cs`**

File: `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/ValueObjects/NotificationType.cs`

Aggiungi dopo `AdminAccessRequestCreated`:
```csharp
// Slack connection forcibly revoked by workspace admin
public static readonly NotificationType SlackConnectionRevoked = new("slack_connection_revoked");
```

Aggiungi nel `FromString` switch, dopo il case `admin_access_request_created`:
```csharp
"slack_connection_revoked" => SlackConnectionRevoked,
```

Aggiungi proprietà convenience:
```csharp
public bool IsSlackConnectionRevoked => string.Equals(Value, SlackConnectionRevoked.Value, StringComparison.Ordinal);
```

- [ ] **Step 7.2: Aggiorna `SlackNotificationProcessorJob.cs` — usa `SlackConnectionRevoked`**

File: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Scheduling/SlackNotificationProcessorJob.cs`

In `HandleTokenRevocationAsync` (linea ~343):
```csharp
// Prima:
var notification = new Notification(
    id: Guid.NewGuid(),
    userId: item.RecipientUserId.Value,
    type: NotificationType.DocumentProcessingFailed,
    // ...

// Dopo:
var notification = new Notification(
    id: Guid.NewGuid(),
    userId: item.RecipientUserId.Value,
    type: NotificationType.SlackConnectionRevoked,
    // ...
```

- [ ] **Step 7.3: Verifica build**

```bash
cd apps/api
dotnet build 2>&1 | grep -E "error|warning" | grep -v "pragma" | head -20
```
Atteso: nessun errore.

- [ ] **Step 7.4: Esegui test filtrati su NotificationType**

```bash
cd apps/api
dotnet test --filter "Category=Unit" -v n 2>&1 | grep -E "FAIL|PASS|Error" | tail -20
```

- [ ] **Step 7.5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/Domain/ValueObjects/NotificationType.cs \
        apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Scheduling/SlackNotificationProcessorJob.cs
git commit -m "fix(notifications): usa NotificationType.SlackConnectionRevoked per disconnessione forzata Slack"
```

---

## Task 8: Fix `SlackNotificationProcessorJob` — rate limit + batch token lookup

**Problema 1:** `HandleRateLimitAsync` usa `MarkAsFailed` invece del nuovo `MarkAsRateLimited`.
**Problema 2:** Token lookup per ogni item (N+1 query) — batch all'inizio del job.

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/Repositories/ISlackConnectionRepository.cs`
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Persistence/SlackConnectionRepository.cs`
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Scheduling/SlackNotificationProcessorJob.cs`

- [ ] **Step 8.1: Aggiungi `GetActiveByUserIdsAsync` all'interfaccia repository**

File: `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/Repositories/ISlackConnectionRepository.cs`

Aggiungi dopo `GetActiveByUserIdAsync`:
```csharp
/// <summary>
/// Batch-fetches active Slack connections for multiple users.
/// Returns a dictionary keyed by UserId for O(1) lookup.
/// </summary>
Task<Dictionary<Guid, SlackConnection>> GetActiveByUserIdsAsync(
    IEnumerable<Guid> userIds,
    CancellationToken ct = default);
```

- [ ] **Step 8.2: Implementa in `SlackConnectionRepository.cs`**

File: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Persistence/SlackConnectionRepository.cs`

Aggiungi dopo `GetActiveByUserIdAsync`:
```csharp
public async Task<Dictionary<Guid, SlackConnection>> GetActiveByUserIdsAsync(
    IEnumerable<Guid> userIds,
    CancellationToken ct = default)
{
    var ids = userIds.ToList();
    if (ids.Count == 0)
        return [];

    var entities = await DbContext.Set<SlackConnectionEntity>()
        .AsNoTracking()
        .Where(e => ids.Contains(e.UserId) && e.IsActive)
        .ToListAsync(ct).ConfigureAwait(false);

    return entities.ToDictionary(e => e.UserId, MapToDomain);
}
```

- [ ] **Step 8.3: Aggiorna `SlackNotificationProcessorJob.cs` — batch tokens + MarkAsRateLimited**

File: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Scheduling/SlackNotificationProcessorJob.cs`

**Modifica 1 — batch token lookup.** Dopo la riga `var allItems = slackUserItems.Concat(slackTeamItems).ToList();` (linea ~72), aggiungi:

```csharp
// Batch-load bot tokens per tutti gli SlackUser items in una sola query
var userIds = allItems
    .Where(i => i.ChannelType == NotificationChannelType.SlackUser && i.RecipientUserId.HasValue)
    .Select(i => i.RecipientUserId!.Value)
    .Distinct()
    .ToList();

var connectionsByUserId = await _slackConnectionRepository
    .GetActiveByUserIdsAsync(userIds, context.CancellationToken)
    .ConfigureAwait(false);
```

**Modifica 2 — usa la cache in `SendViaBotApiAsync`.** Aggiungi `connectionsByUserId` come parametro passato a `SendSlackMessageAsync` → `SendViaBotApiAsync`. Crea un overload o passa il dizionario nel `Execute()` come closure:

Cambia la firma di `SendViaBotApiAsync`:
```csharp
private async Task SendViaBotApiAsync(
    HttpClient client,
    NotificationQueueItem item,
    IReadOnlyDictionary<Guid, SlackConnection> connectionCache,
    CancellationToken ct)
```

Sostituisci il blocco di lookup token (linee ~236-245):
```csharp
// Prima: lookup per ogni item
string? botToken = null;
if (item.RecipientUserId.HasValue)
{
    var connection = await _slackConnectionRepository
        .GetActiveByUserIdAsync(item.RecipientUserId.Value, ct)
        .ConfigureAwait(false);
    botToken = connection?.BotAccessToken;
}

// Dopo: usa cache
string? botToken = item.RecipientUserId.HasValue
    && connectionCache.TryGetValue(item.RecipientUserId.Value, out var conn)
    ? conn.BotAccessToken
    : null;
```

Aggiorna `SendSlackMessageAsync` per passare la cache:
```csharp
private async Task SendSlackMessageAsync(
    NotificationQueueItem item,
    IReadOnlyDictionary<Guid, SlackConnection> connectionCache,
    CancellationToken ct)
{
    using var client = _httpClientFactory.CreateClient("SlackApi");

    if (item.ChannelType == NotificationChannelType.SlackTeam)
        await SendViaWebhookAsync(client, item, ct).ConfigureAwait(false);
    else
        await SendViaBotApiAsync(client, item, connectionCache, ct).ConfigureAwait(false);
}
```

Aggiorna la call site nel loop principale:
```csharp
await SendSlackMessageAsync(item, connectionsByUserId, context.CancellationToken).ConfigureAwait(false);
```

**Modifica 3 — usa `MarkAsRateLimited` in `HandleRateLimitAsync`:**
```csharp
private async Task HandleRateLimitAsync(
    NotificationQueueItem currentItem,
    int retryAfterSeconds,
    CancellationToken ct)
{
    var retryAt = DateTime.UtcNow.AddSeconds(retryAfterSeconds);

    try
    {
        currentItem.MarkAsRateLimited(retryAt);   // FIX: era MarkAsFailed
        await _queueRepository.UpdateAsync(currentItem, ct).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to update rate-limited item {ItemId}", currentItem.Id);
    }
}
```

- [ ] **Step 8.4: Build**

```bash
cd apps/api
dotnet build 2>&1 | grep -E "^.*error" | head -20
```
Atteso: nessun errore.

- [ ] **Step 8.5: Esegui tutti i test Unit**

```bash
cd apps/api
dotnet test --filter "Category=Unit" -v n 2>&1 | tail -30
```

- [ ] **Step 8.6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/Domain/Repositories/ISlackConnectionRepository.cs \
        apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Persistence/SlackConnectionRepository.cs \
        apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Scheduling/SlackNotificationProcessorJob.cs
git commit -m "fix(notifications): SlackJob — batch token lookup (N+1 → 1), MarkAsRateLimited, deepLinkPath"
```

---

## Task 9: Fix `NotificationDispatcher` — severity mapping e title mapping

**Problema 1:** `severity: NotificationSeverity.Info` hardcoded — tutte le notifiche sono Info anche se critiche.
**Problema 2:** `title: message.Payload.GetType().Name` espone il tipo C# ("PdfProcessingPayload") all'utente.
**Problema 3:** `IsSlackEnabledForType` default `return true` — i tipi Admin vengono inviati via Slack DM agli utenti.

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Services/NotificationDispatcher.cs`

> **Nota:** Non esistono unit test specifici per `NotificationDispatcher`. Vanno creati.

- [ ] **Step 9.1: Scrivi i test**

Crea file: `apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/Services/NotificationDispatcherTests.cs`

```csharp
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.BoundedContexts.UserNotifications.Infrastructure.Configuration;
using Api.BoundedContexts.UserNotifications.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Infrastructure.Services;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "UserNotifications")]
public sealed class NotificationDispatcherTests
{
    private readonly Mock<INotificationRepository> _notificationRepo = new();
    private readonly Mock<INotificationQueueRepository> _queueRepo = new();
    private readonly Mock<INotificationPreferencesRepository> _prefsRepo = new();
    private readonly Mock<ISlackConnectionRepository> _slackConnRepo = new();
    private readonly NotificationDispatcher _sut;

    public NotificationDispatcherTests()
    {
        var slackConfig = Options.Create(new SlackNotificationConfiguration
        {
            TeamChannels = [],
            ClientId = "client",
            ClientSecret = "secret",
            RedirectUri = "https://meepleai.app/slack/callback",
            SigningSecret = "secret"
        });

        _notificationRepo
            .Setup(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _queueRepo
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<NotificationQueueItem>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _prefsRepo
            .Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationPreferences?)null);

        _sut = new NotificationDispatcher(
            _notificationRepo.Object,
            _queueRepo.Object,
            _prefsRepo.Object,
            _slackConnRepo.Object,
            slackConfig,
            NullLogger<NotificationDispatcher>.Instance);
    }

    [Theory]
    [InlineData("document_processing_failed", "Error")]
    [InlineData("session_terminated", "Warning")]
    [InlineData("document_ready", "Success")]
    [InlineData("badge_earned", "Info")]
    public async Task DispatchAsync_MapsCorrectSeverityForType(string typeValue, string expectedSeverity)
    {
        // Arrange
        var message = new NotificationMessage
        {
            Type = NotificationType.FromString(typeValue),
            RecipientUserId = Guid.NewGuid(),
            Payload = new GenericPayload("Title", "Body")
        };

        Notification? captured = null;
        _notificationRepo
            .Setup(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback<Notification, CancellationToken>((n, _) => captured = n)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.DispatchAsync(message);

        // Assert
        captured.Should().NotBeNull();
        captured!.Severity.Value.Should().Be(expectedSeverity.ToLowerInvariant());
    }

    [Theory]
    [InlineData("document_ready", "Documento pronto")]
    [InlineData("document_processing_failed", "Elaborazione fallita")]
    [InlineData("badge_earned", "Badge ottenuto")]
    [InlineData("share_request_created", "Nuova Share Request")]
    [InlineData("game_night_invitation", "Invito Serata")]
    public async Task DispatchAsync_UsesFriendlyTitleNotTypeName(string typeValue, string expectedTitleFragment)
    {
        // Arrange
        var message = new NotificationMessage
        {
            Type = NotificationType.FromString(typeValue),
            RecipientUserId = Guid.NewGuid(),
            Payload = new GenericPayload("Title", "Body")
        };

        Notification? captured = null;
        _notificationRepo
            .Setup(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback<Notification, CancellationToken>((n, _) => captured = n)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.DispatchAsync(message);

        // Assert
        captured!.Title.Should().Contain(expectedTitleFragment);
        captured.Title.Should().NotContain("Payload"); // no C# type names
    }

    [Theory]
    [InlineData("admin_new_share_request")]
    [InlineData("admin_system_health_alert")]
    [InlineData("admin_openrouter_threshold_alert")]
    [InlineData("admin_model_status_changed")]
    public async Task DispatchAsync_AdminTypes_NotSentViaSlackDm(string typeValue)
    {
        // Arrange: user has Slack enabled with active connection
        var userId = Guid.NewGuid();
        var prefs = CreatePrefsWithSlackEnabled(userId);
        _prefsRepo
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);

        // SlackConnection.Create: userId, slackUserId, slackTeamId, slackTeamName, botAccessToken, dmChannelId
        var connection = SlackConnection.Create(userId, "U123", "T456", "TestTeam", "xoxb-token", "D789");
        _slackConnRepo
            .Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection);

        var message = new NotificationMessage
        {
            Type = NotificationType.FromString(typeValue),
            RecipientUserId = userId,
            Payload = new GenericPayload("Admin Alert", "Details")
        };

        IEnumerable<NotificationQueueItem>? capturedItems = null;
        _queueRepo
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<NotificationQueueItem>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<NotificationQueueItem>, CancellationToken>((items, _) => capturedItems = items)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.DispatchAsync(message);

        // Assert — no SlackUser channel items for admin types
        capturedItems?.Any(i => i.ChannelType == NotificationChannelType.SlackUser)
            .Should().BeFalse($"admin type '{typeValue}' should not be delivered via Slack DM");
    }

    private static NotificationPreferences CreatePrefsWithSlackEnabled(Guid userId)
    {
        var prefs = new NotificationPreferences(userId);
        prefs.UpdateSlackPreferences(
            enabled: true,
            onDocumentReady: true,
            onDocumentFailed: true,
            onRetryAvailable: false,
            onGameNightInvitation: true,
            onGameNightReminder: true,
            onShareRequestCreated: true,
            onShareRequestApproved: true,
            onBadgeEarned: true);
        return prefs;
    }
}
```

- [ ] **Step 9.2: Esegui i test per verificare che falliscono**

```bash
cd apps/api
dotnet test --filter "NotificationDispatcherTests" -v n 2>&1 | tail -30
```
Atteso: test falliscono su severity, title e admin Slack DM.

- [ ] **Step 9.3: Aggiorna `NotificationDispatcher.cs` — severity mapping**

File: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Services/NotificationDispatcher.cs`

Aggiungi il metodo helper prima di `IsEmailEnabledForType`:
```csharp
/// <summary>
/// Maps notification type to display severity.
/// </summary>
private static NotificationSeverity ResolveSeverity(NotificationType type)
{
    if (type == NotificationType.DocumentProcessingFailed
        || type == NotificationType.AdminSystemHealthAlert
        || type == NotificationType.AdminOpenRouterThresholdAlert)
        return NotificationSeverity.Error;

    if (type == NotificationType.SessionTerminated
        || type == NotificationType.AdminReviewLockExpiring
        || type == NotificationType.AdminStaleShareRequests
        || type == NotificationType.RateLimitReached
        || type == NotificationType.SlackConnectionRevoked)
        return NotificationSeverity.Warning;

    if (type == NotificationType.DocumentReady
        || type == NotificationType.RuleSpecGenerated
        || type == NotificationType.BadgeEarned
        || type == NotificationType.AgentReady
        || type == NotificationType.GdprDataExportReady)
        return NotificationSeverity.Success;

    return NotificationSeverity.Info;
}
```

Aggiungi il metodo `ResolveTitle`:
```csharp
/// <summary>
/// Returns a human-readable title for the notification type.
/// </summary>
private static string ResolveTitle(NotificationType type)
{
    if (type == NotificationType.DocumentReady) return "Documento pronto";
    if (type == NotificationType.RuleSpecGenerated) return "Specifiche generate";
    if (type == NotificationType.DocumentProcessingFailed) return "Elaborazione fallita";
    if (type == NotificationType.ShareRequestCreated) return "Nuova Share Request";
    if (type == NotificationType.ShareRequestApproved) return "Share Request approvata";
    if (type == NotificationType.ShareRequestRejected) return "Share Request rifiutata";
    if (type == NotificationType.ShareRequestChangesRequested) return "Modifiche richieste";
    if (type == NotificationType.BadgeEarned) return "Badge ottenuto";
    if (type == NotificationType.GameNightInvitation) return "Invito Serata";
    if (type == NotificationType.GameNightRsvpReceived) return "RSVP ricevuto";
    if (type == NotificationType.GameNightReminder) return "Promemoria Serata";
    if (type == NotificationType.GameNightCancelled) return "Serata annullata";
    if (type == NotificationType.AgentReady) return "Agente pronto";
    if (type == NotificationType.LoanReminder) return "Promemoria prestito";
    if (type == NotificationType.RateLimitApproaching) return "Quota in avvicinamento";
    if (type == NotificationType.RateLimitReached) return "Quota raggiunta";
    if (type == NotificationType.SessionTerminated) return "Sessione terminata";
    if (type == NotificationType.GdprDataExportReady) return "Export dati pronto";
    if (type == NotificationType.GdprAccountDeleted) return "Account eliminato";
    if (type == NotificationType.GdprAiConsentUpdated) return "Consenso AI aggiornato";
    if (type == NotificationType.SlackConnectionRevoked) return "Slack disconnesso";

    // Admin types
    if (type == NotificationType.AdminNewShareRequest) return "[Admin] Nuova Share Request";
    if (type == NotificationType.AdminStaleShareRequests) return "[Admin] Share Request in attesa";
    if (type == NotificationType.AdminReviewLockExpiring) return "[Admin] Lock revisione in scadenza";
    if (type == NotificationType.AdminSharedGameSubmitted) return "[Admin] Gioco condiviso inviato";
    if (type == NotificationType.AdminOpenRouterThresholdAlert) return "[Admin] Soglia OpenRouter";
    if (type == NotificationType.AdminOpenRouterDailySummary) return "[Admin] Digest giornaliero";
    if (type == NotificationType.AdminSystemHealthAlert) return "[Admin] Alerta sistema";
    if (type == NotificationType.AdminModelStatusChanged) return "[Admin] Stato modello cambiato";

    return "Notifica MeepleAI";
}
```

Aggiorna la creazione della `notification` in `DispatchAsync`:
```csharp
var notification = new Notification(
    id: Guid.NewGuid(),
    userId: message.RecipientUserId,
    type: message.Type,
    severity: ResolveSeverity(message.Type),        // FIX: era NotificationSeverity.Info
    title: ResolveTitle(message.Type),              // FIX: era message.Payload.GetType().Name
    message: message.Payload.ToString() ?? string.Empty,
    link: message.DeepLinkPath,
    metadata: metadataJson,
    correlationId: correlationId);
```

- [ ] **Step 9.4: Aggiorna `IsSlackEnabledForType` — escludi tipi Admin dal DM**

File: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Services/NotificationDispatcher.cs`

Sostituisci il metodo `IsSlackEnabledForType`:
```csharp
private static bool IsSlackEnabledForType(NotificationPreferences prefs, NotificationType type)
{
    // Admin types are never sent to individual users via DM
    if (type == NotificationType.AdminNewShareRequest
        || type == NotificationType.AdminStaleShareRequests
        || type == NotificationType.AdminReviewLockExpiring
        || type == NotificationType.AdminSharedGameSubmitted
        || type == NotificationType.AdminOpenRouterThresholdAlert
        || type == NotificationType.AdminOpenRouterDailySummary
        || type == NotificationType.AdminSystemHealthAlert
        || type == NotificationType.AdminModelStatusChanged
        || type == NotificationType.AdminAccessRequestCreated
        || type == NotificationType.AdminManualNotification
        || type == NotificationType.AdminPdfProcessingStarted)
        return false;

    if (type == NotificationType.DocumentReady || type == NotificationType.RuleSpecGenerated)
        return prefs.SlackOnDocumentReady;
    if (type == NotificationType.DocumentProcessingFailed)
        return prefs.SlackOnDocumentFailed;
    if (type == NotificationType.ShareRequestCreated)
        return prefs.SlackOnShareRequestCreated;
    if (type == NotificationType.ShareRequestApproved)
        return prefs.SlackOnShareRequestApproved;
    if (type == NotificationType.BadgeEarned)
        return prefs.SlackOnBadgeEarned;
    if (type == NotificationType.GameNightInvitation || type == NotificationType.GameNightRsvpReceived)
        return prefs.SlackOnGameNightInvitation;
    if (type == NotificationType.GameNightReminder)
        return prefs.SlackOnGameNightReminder;

    // Default: send Slack for types not explicitly configured
    return true;
}
```

- [ ] **Step 9.5: Esegui i test**

```bash
cd apps/api
dotnet test --filter "NotificationDispatcherTests" -v n 2>&1 | tail -30
```
Atteso: tutti i test passano.

- [ ] **Step 9.6: Esegui tutti i test Unit**

```bash
cd apps/api
dotnet test --filter "Category=Unit" -v n 2>&1 | tail -20
```
Atteso: nessuna regressione.

- [ ] **Step 9.7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Services/NotificationDispatcher.cs \
        apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/Services/NotificationDispatcherTests.cs
git commit -m "fix(notifications): NotificationDispatcher — severity/title mapping, escludi admin da Slack DM"
```

---

## Task 10: DI Wiring — aggiorna `UserNotificationsServiceExtensions.cs`

**Obiettivo:** Assicurare che tutti i builder e validator aggiornati siano correttamente registrati nel container DI dopo le modifiche (nuovi parametri `TimeProvider` per `AdminAlertSlackBuilder`).

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/DependencyInjection/UserNotificationsServiceExtensions.cs`

- [ ] **Step 10.1: Verifica e aggiorna le registrazioni**

File: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/DependencyInjection/UserNotificationsServiceExtensions.cs`

Apri il file e verifica che `AdminAlertSlackBuilder` sia registrato come Singleton. Se è registrato esplicitamente senza factory:
```csharp
services.AddSingleton<AdminAlertSlackBuilder>();
```
Questa riga funziona: il container DI risolve `TimeProvider` automaticamente se `services.AddSingleton(TimeProvider.System)` è presente in `Program.cs`.

Se non è registrato tramite `AddSingleton<AdminAlertSlackBuilder>()` ma tramite una lista/factory, aggiorna la factory per passare `TimeProvider`.

- [ ] **Step 10.2: Build e test completo**

```bash
cd apps/api
dotnet build 2>&1 | grep -E "error" | grep -v "pragma" | head -20
dotnet test --filter "BoundedContext=UserNotifications" -v n 2>&1 | tail -40
```
Atteso: build pulito, tutti i test UserNotifications passano.

- [ ] **Step 10.3: Commit finale**

```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/DependencyInjection/UserNotificationsServiceExtensions.cs
git commit -m "chore(notifications): verifica DI wiring per TimeProvider nei builder Slack aggiornati"
```

---

## Checklist di verifica finale

Prima di aprire la PR:

```bash
cd apps/api
dotnet build 2>&1 | grep " error " | grep -v "pragma"
dotnet test --filter "BoundedContext=UserNotifications" -v n 2>&1 | grep -E "FAIL|Passed|Failed"
dotnet test --filter "Category=Unit" 2>&1 | tail -5
```

Atteso:
- `0 Error(s)` in build
- `0 Failed` nei test UserNotifications
- Tutti i nuovi test specifici passano
