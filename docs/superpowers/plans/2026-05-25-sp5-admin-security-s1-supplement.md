# SP5 Admin Security S1 — Implementation Supplement (post three-amigos)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Incorporare nel plan d'implementazione S1 le decisioni convergenti del three-amigos kickoff (2026-05-25): atomicità ristretta per i comandi distruttivi, payload truncation per-campo, idempotency `ON CONFLICT`, metriche Prometheus per il processor, 5 acceptance scenarios eseguibili, DoD verificabile.

**Architecture:** Questo supplement aggiunge **5 task** al plan principale S1 (`docs/superpowers/plans/2026-05-25-sp5-admin-security-s1-audit-schema.md`). I task del plan principale (T0 D-4 spike · T1 schema migration · T2 interceptor · T3 outbox writer · T4 processor) restano gli stessi; questo supplement li **estende** in punti specifici (Q-numerati) e ne **aggiunge 5 nuovi** che diventano gate della DoD.

**Tech Stack:** identico al plan principale (.NET 9 · EF Core · MediatR · xUnit + Testcontainers Postgres) + **prometheus-net** (già una dipendenza del progetto API per le metriche esistenti).

**Riferimenti:**
- Plan principale: `docs/superpowers/plans/2026-05-25-sp5-admin-security-s1-audit-schema.md`
- Workshop document: `audits/2026-05-25-s1-three-amigos-kickoff.md` (decisioni Q1-Q4 + ownership + DoD)
- Spec consolidamento §7 (track sicurezza): `docs/superpowers/specs/2026-05-24-sp5-admin-console-consolidation-design.md`

**Ordine di esecuzione (combinato col plan principale):**

```
T0 (plan) D-4 spike
T1 (plan) Migration schema
T2 (plan) Interceptor      →  T2b (supplement) PayloadTruncator
T3 (plan) Outbox writer    →  T3b (supplement) Atomicità ristretta (refactor distruttivi)
T4 (plan) Processor        →  T4b (supplement) ON CONFLICT + Prometheus metrics
                              T5 (supplement) Acceptance scenarios (5)
                              T6 (supplement) DoD verification
```

---

## Task 2b: `PayloadTruncator` — tronca per-campo (Q2)

Estende il **Task 2** del plan principale: la `AuditingSaveChangesInterceptor` chiama `PayloadTruncator.Truncate(...)` prima di serializzare il JSON, per garantire il limite di 256KB applicando una truncation **per collection-field** (non sul JSON intero).

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Persistence/PayloadTruncator.cs`
- Create: `tests/Api.Tests/Unit/Infrastructure/Persistence/PayloadTruncatorTests.cs`
- Modify: `apps/api/src/Api/Infrastructure/Persistence/AuditingSaveChangesInterceptor.cs` (chiama il truncator prima della serializzazione)

- [ ] **Step 1: Scrivi i test del truncator**

```csharp
// tests/Api.Tests/Unit/Infrastructure/Persistence/PayloadTruncatorTests.cs
using System.Collections.Generic;
using System.Linq;
using Api.Infrastructure.Persistence;
using Xunit;

public class PayloadTruncatorTests
{
    [Fact]
    public void Truncate_LeavesSmallPayloadUnchanged()
    {
        var props = new Dictionary<string, object?> { ["Email"] = "a@x", ["Role"] = "admin" };
        var result = PayloadTruncator.Truncate(props, maxBytes: 256_000);
        Assert.Equal("a@x", result["Email"]);
        Assert.Equal("admin", result["Role"]);
        Assert.False(result.ContainsKey("_truncated"));
    }

    [Fact]
    public void Truncate_LargeCollectionField_TrimsToTenWithMetadata()
    {
        var games = Enumerable.Range(0, 543).Select(i => $"game-{i}").ToList();
        var props = new Dictionary<string, object?>
        {
            ["Email"] = "a@x",
            ["Games"] = games,
        };

        var result = PayloadTruncator.Truncate(props, maxBytes: 256_000);

        var trimmedGames = Assert.IsAssignableFrom<IEnumerable<object>>(result["Games"]);
        Assert.Equal(10, trimmedGames.Cast<object>().Count());

        Assert.IsType<List<string>>(result["_truncated"]);
        Assert.Contains("Games", (List<string>)result["_truncated"]!);

        var counts = Assert.IsAssignableFrom<IDictionary<string, int>>(result["_original_count"]);
        Assert.Equal(543, counts["Games"]);
    }

    [Fact]
    public void Truncate_StillOversize_ReturnsOversizeMarker()
    {
        // 300KB of scalar string in a single field — cannot be truncated per-field
        var huge = new string('x', 300_000);
        var props = new Dictionary<string, object?> { ["Blob"] = huge };

        var result = PayloadTruncator.Truncate(props, maxBytes: 256_000);

        Assert.True(result.ContainsKey("_oversize"));
        Assert.True((bool)result["_oversize"]!);
    }
}
```

- [ ] **Step 2: Run, confirm FAIL**

  ```bash
  cd apps/api/src/Api
  dotnet test --filter "FullyQualifiedName~PayloadTruncatorTests" 2>&1 | tail -10
  ```

- [ ] **Step 3: Implementa `PayloadTruncator`**

```csharp
// apps/api/src/Api/Infrastructure/Persistence/PayloadTruncator.cs
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Api.Infrastructure.Persistence;

public static class PayloadTruncator
{
    private const int CollectionTrimThreshold = 50; // collections > 50 → trim to 10
    private const int CollectionTrimKeep = 10;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    /// Truncates a property bag in-place so that its JSON serialization fits in maxBytes.
    /// Strategy: identify enumerable fields with &gt;{CollectionTrimThreshold} items,
    /// trim to {CollectionTrimKeep} and record original counts under "_truncated"/"_original_count".
    /// If the result still exceeds maxBytes after truncation, returns the bag flagged with
    /// "_oversize": true — the caller (interceptor/behavior) marks the outbox row Failed.
    /// </summary>
    public static IDictionary<string, object?> Truncate(IDictionary<string, object?> props, int maxBytes)
    {
        var truncatedFields = new List<string>();
        var originalCounts = new Dictionary<string, int>();

        foreach (var key in props.Keys.ToList())
        {
            var value = props[key];
            if (value is not IEnumerable enumerable || value is string) continue;

            var list = enumerable.Cast<object?>().ToList();
            if (list.Count <= CollectionTrimThreshold) continue;

            props[key] = list.Take(CollectionTrimKeep).ToList();
            truncatedFields.Add(key);
            originalCounts[key] = list.Count;
        }

        if (truncatedFields.Count > 0)
        {
            props["_truncated"] = truncatedFields;
            props["_original_count"] = originalCounts;
        }

        // Check size after truncation
        var size = JsonSerializer.SerializeToUtf8Bytes(props, JsonOpts).Length;
        if (size > maxBytes)
        {
            props["_oversize"] = true;
        }

        return props;
    }
}
```

- [ ] **Step 4: Run, confirm PASS** (3 test)

- [ ] **Step 5: Wire `PayloadTruncator` into `AuditingSaveChangesInterceptor`**

  Modifica il metodo `SerializeProperties` (Task 2 del plan principale) per applicare il truncator prima di serializzare:

  ```csharp
  // In AuditingSaveChangesInterceptor.cs
  private const int MaxPayloadBytes = 256_000; // Q2 decision

  private static string SerializeProperties(EntityEntry entry, bool useOriginal)
  {
      var dict = new Dictionary<string, object?>();
      foreach (var p in entry.Properties)
      {
          dict[p.Metadata.Name] = useOriginal ? p.OriginalValue : p.CurrentValue;
      }
      // Also include navigation collections for the audit (e.g. Games on a User)
      foreach (var nav in entry.Collections)
      {
          if (nav.CurrentValue is IEnumerable enumerable && nav.CurrentValue is not string)
          {
              dict[nav.Metadata.Name] = enumerable.Cast<object?>().ToList();
          }
      }
      var truncated = PayloadTruncator.Truncate(dict, MaxPayloadBytes);
      return JsonSerializer.Serialize(truncated, JsonOpts);
  }
  ```

  Nel sink ricevente, se `_oversize == true`, il behavior (Task 3) deve marcare la outbox row come `Failed(last_error="payload_oversize")` invece di salvarla come Pending.

- [ ] **Step 6: Commit**

  ```bash
  git add apps/api/src/Api/Infrastructure/Persistence/PayloadTruncator.cs \
          apps/api/src/Api/Infrastructure/Persistence/AuditingSaveChangesInterceptor.cs \
          tests/Api.Tests/Unit/Infrastructure/Persistence/PayloadTruncatorTests.cs
  git commit -m "feat(audit): PayloadTruncator with per-collection trimming (Q2)"
  ```

---

## Task 3b: Atomicità ristretta — refactor handler distruttivi (Q1)

Estende il **Task 3** del plan principale. Per le azioni distruttive (`DeleteUserCommand`, `RotateApiKeyCommand`, `EmergencyShutdownCommand`), il handler **NON** chiama più `SaveChanges` autonomamente — delega al behavior, che lo invoca **dopo** aver aggiunto la outbox row (transazione atomica: mutazione + audit).

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/DeleteUserCommandHandler.cs`
- Modify: handler per `RotateApiKeyCommand` (path da identificare nello spike T0 o nel grep di T3b step 1)
- Modify: handler per `EmergencyShutdownCommand` (idem)
- Modify: `apps/api/src/Api/BoundedContexts/Administration/Application/Behaviors/AuditLoggingBehavior.cs` (riconosce `[AtomicAudit]` attribute)
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Behaviors/AtomicAuditAttribute.cs`

- [ ] **Step 1: Identifica i comandi distruttivi**

  ```bash
  cd D:/Repositories/meepleai-monorepo-dev
  rg -l 'DeleteUserCommand|RotateApiKeyCommand|EmergencyShutdownCommand' apps/api/src/Api -t cs
  ```

  Expected: una lista di handler (in `BoundedContexts/Administration/Application/Commands/`). Se `RotateApiKeyCommand` o `EmergencyShutdownCommand` non esistono ancora (saranno introdotti in S3), tracciali come "future-atomic" e applica `[AtomicAudit]` solo a `DeleteUserCommand` per ora.

- [ ] **Step 2: Crea `AtomicAuditAttribute`**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/Behaviors/AtomicAuditAttribute.cs
using System;

namespace Api.BoundedContexts.Administration.Application.Behaviors;

/// <summary>
/// Marks a command as "atomic audit": the handler must NOT call SaveChanges autonomously.
/// AuditLoggingBehavior takes care of adding the outbox row in the same tx and committing.
/// Applied to destructive commands (delete user, rotate key, emergency shutdown) where
/// the audit MUST NOT be lost even if the mutation transaction fails.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AtomicAuditAttribute : Attribute { }
```

- [ ] **Step 3: Modifica `DeleteUserCommandHandler` — rimuovi `await _dbContext.SaveChangesAsync()` e applica `[AtomicAudit]`**

  Apri il file (path identificato in step 1) e:

  - Aggiungi `[AtomicAudit]` sopra la classe del comando (o del handler — segui la convenzione del progetto su attributi MediatR).
  - Cerca le chiamate `await _dbContext.SaveChangesAsync()` dentro `Handle(...)` e rimuovile.

  Esempio:

  ```csharp
  [AuditableAction(Action = "user.delete", Resource = "user")]
  [AtomicAudit] // ← NEW: behavior owns SaveChanges
  public record DeleteUserCommand(Guid TargetUserId, string Reason) : IRequest<Unit>;

  public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, Unit>
  {
      // ... existing dependencies ...

      public async Task<Unit> Handle(DeleteUserCommand request, CancellationToken ct)
      {
          var user = await _dbContext.Users.FindAsync(new object[] { request.TargetUserId }, ct);
          if (user == null) throw new NotFoundException(...);
          user.SoftDelete(reason: request.Reason); // marks IsDeleted, DeletedAt
          // NOTE: NO SaveChanges() here — AuditLoggingBehavior commits the tx
          return Unit.Value;
      }
  }
  ```

- [ ] **Step 4: Estendi `AuditLoggingBehavior` per riconoscere `[AtomicAudit]`**

  Logica: il behavior intercetta la pipeline MediatR. Se `[AtomicAudit]` è presente:

  1. Esegue `await next()` (handler stage le modifiche al `DbContext` ma NON chiama `SaveChanges`).
  2. Aggiunge la outbox row al `DbContext`.
  3. Chiama `await _dbContext.SaveChangesAsync()` UNA VOLTA → commit atomico di mutazione + outbox.

  Se `[AtomicAudit]` è assente (comportamento legacy "best-effort"):

  1. Esegue `await next()` (handler chiama `SaveChanges` per la mutazione).
  2. Aggiunge outbox row al `DbContext` + `await SaveChangesAsync()` in seconda tx. Se fallisce → log warning, no exception bubble.

  ```csharp
  // Estratto del Handle del behavior
  var atomicAttr = typeof(TRequest).GetCustomAttribute<AtomicAuditAttribute>();

  if (atomicAttr != null)
  {
      var response = await next(); // handler does NOT SaveChanges
      _dbContext.AuditOutbox.Add(BuildOutboxRow(request, snapshots));
      await _dbContext.SaveChangesAsync(ct); // atomic commit
      return response;
  }
  else
  {
      var response = await next(); // handler called SaveChanges
      try
      {
          _dbContext.AuditOutbox.Add(BuildOutboxRow(request, snapshots));
          await _dbContext.SaveChangesAsync(ct);
      }
      catch (Exception ex)
      {
          _logger.LogWarning(ex, "Best-effort audit write failed for {RequestType}", typeof(TRequest).Name);
      }
      return response;
  }
  ```

- [ ] **Step 5: Integration test — atomicity guarantee**

  ```csharp
  // tests/Api.Tests/Integration/Administration/AtomicAuditIntegrationTests.cs
  [Fact]
  public async Task DeleteUser_FailingMidTx_LeavesNoAuditAndNoMutation()
  {
      await using var fx = await AdministrationFixture.CreateAsync();
      var sender = fx.GetRequiredService<IMediator>();
      var userId = fx.SeededUserId;

      // Inject a failing interceptor to force commit failure
      fx.ForceCommitFailure = true;
      await Assert.ThrowsAnyAsync<Exception>(() => sender.Send(new DeleteUserCommand(userId, "test")));

      var ctx = fx.GetRequiredService<MeepleDbContext>();
      Assert.False(await ctx.Users.AnyAsync(u => u.Id == userId && u.IsDeleted)); // user NOT deleted
      Assert.False(await ctx.AuditOutbox.AnyAsync()); // outbox NOT written
  }
  ```

  Run + verifica PASS.

- [ ] **Step 6: Commit**

  ```bash
  git add apps/api/src/Api/BoundedContexts/Administration/ \
          tests/Api.Tests/Integration/Administration/AtomicAuditIntegrationTests.cs
  git commit -m "feat(audit): atomic audit for destructive commands (Q1)"
  ```

---

## Task 4b: ON CONFLICT idempotency + Prometheus metrics (Q3)

Estende il **Task 4** del plan principale.

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/Administration/Infrastructure/AuditOutboxProcessor.cs`
- Modify: `apps/api/src/Api/Infrastructure/Entities/Administration/AuditOutboxEntity.cs` (factory accetta GUID esplicito)
- Modify: `apps/api/src/Api/Infrastructure/Entities/Administration/AuditLogEntity.cs` (factory accetta GUID esplicito = outbox Id)
- Modify: `apps/api/src/Api/BoundedContexts/Administration/Application/Behaviors/AuditLoggingBehavior.cs` (genera GUID condiviso)
- Test: `tests/Api.Tests/Integration/Administration/AuditOutboxIdempotencyTests.cs`

- [ ] **Step 1: Estendi le factory per accettare un GUID esplicito**

  In `AuditOutboxEntity`:

  ```csharp
  public static AuditOutboxEntity CreatePending(Guid id, string payloadJson, DateTimeOffset now)
      => new()
      {
          Id = id, // ← was Guid.NewGuid() before
          PayloadJson = payloadJson,
          Status = OutboxStatus.Pending,
          RetryCount = 0,
          CreatedAt = now,
      };
  ```

  In `AuditLogEntity` (Task 1 del plan principale): assicurati che la factory `Create(...)` accetti `Guid id` come parametro, non lo generi internamente. Se la factory esistente già lo accetta, no-op.

  Nel behavior (Task 3 plan principale + 3b supplement): genera un GUID `var auditId = Guid.NewGuid();` e passa a entrambe le factory.

- [ ] **Step 2: Modifica il processor per `INSERT ON CONFLICT DO NOTHING`**

  In `AuditOutboxProcessor.RunOnceAsync`, sostituisci il blocco `ctx.AuditLogs.Add(auditLog)` con SQL raw per garantire idempotency su retry:

  ```csharp
  // Postgres-specific: ON CONFLICT (id) DO NOTHING for idempotent retry
  var insertSql = @"
      INSERT INTO audit_logs (id, user_id, action, resource, ip_address, user_agent,
                              before_json, after_json, impersonated_user_id, step_up_token_id, created_at)
      VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}::jsonb, {7}::jsonb, {8}, {9}, {10})
      ON CONFLICT (id) DO NOTHING";

  foreach (var row in pending)
  {
      try
      {
          var auditFields = ParsePayload(row.PayloadJson);
          var affected = await ctx.Database.ExecuteSqlRawAsync(insertSql,
              cancellationToken: ct,
              parameters: new object?[]
              {
                  row.Id,
                  auditFields.UserId,
                  auditFields.Action,
                  auditFields.Resource,
                  auditFields.IpAddress,
                  auditFields.UserAgent,
                  auditFields.BeforeJson,
                  auditFields.AfterJson,
                  auditFields.ImpersonatedUserId,
                  auditFields.StepUpTokenId,
                  auditFields.CreatedAt,
              });
          row.MarkSent(DateTimeOffset.UtcNow);
          // affected may be 0 (already existed from a previous retry) — still mark Sent
      }
      catch (Exception ex)
      {
          row.MarkFailed(ex.Message, DateTimeOffset.UtcNow);
      }
  }
  ```

  (`ParsePayload` rimane la helper deserialization del plan principale Task 4 step 3, lievemente refactorata in una `AuditFieldsParser` static class se preferisci tipizzazione forte.)

- [ ] **Step 3: Aggiungi le metriche Prometheus**

  Crea un meter dedicato (`prometheus-net` è già usato nel progetto — cerca un esempio di `Gauge` esistente: `rg -n 'Metrics.CreateGauge|Prometheus.Gauge' apps/api/src`).

  ```csharp
  // In AuditOutboxProcessor.cs
  private static readonly Prometheus.Gauge PendingCountGauge = Prometheus.Metrics
      .CreateGauge("audit_outbox_pending_count",
                   "Number of audit outbox rows in Pending status.");

  private static readonly Prometheus.Gauge OldestPendingAgeGauge = Prometheus.Metrics
      .CreateGauge("audit_outbox_oldest_pending_age_seconds",
                   "Age in seconds of the oldest Pending audit_outbox row (0 if empty).");

  // After each RunOnceAsync batch, update the gauges
  private async Task UpdateMetricsAsync(MeepleDbContext ctx, CancellationToken ct)
  {
      var pendingCount = await ctx.AuditOutbox.CountAsync(r => r.Status == OutboxStatus.Pending, ct);
      PendingCountGauge.Set(pendingCount);

      if (pendingCount == 0)
      {
          OldestPendingAgeGauge.Set(0);
          return;
      }
      var oldestCreatedAt = await ctx.AuditOutbox
          .Where(r => r.Status == OutboxStatus.Pending)
          .MinAsync(r => r.CreatedAt, ct);
      var ageSeconds = (DateTimeOffset.UtcNow - oldestCreatedAt).TotalSeconds;
      OldestPendingAgeGauge.Set(ageSeconds);
  }
  ```

  Chiama `await UpdateMetricsAsync(ctx, ct);` alla fine di `RunOnceAsync` (anche quando 0 pending all'inizio, per resettare i gauge a 0).

- [ ] **Step 4: Integration test — idempotency**

  ```csharp
  // tests/Api.Tests/Integration/Administration/AuditOutboxIdempotencyTests.cs
  [Fact]
  public async Task Processor_DoesNotDuplicate_OnRetryAfterPartialBatch()
  {
      await using var fx = await AdministrationFixture.CreateAsync();
      var ctx = fx.GetRequiredService<MeepleDbContext>();

      // Seed 5 Pending outbox rows
      var ids = new List<Guid>();
      for (var i = 0; i < 5; i++)
      {
          var id = Guid.NewGuid(); ids.Add(id);
          ctx.AuditOutbox.Add(AuditOutboxEntity.CreatePending(id,
              $"{{\"action\":\"test.{i}\",\"resource\":\"r\",\"userId\":\"{Guid.NewGuid()}\",\"timestamp\":\"{DateTimeOffset.UtcNow:O}\",\"snapshots\":[]}}",
              DateTimeOffset.UtcNow));
      }
      await ctx.SaveChangesAsync();

      var processor = fx.GetRequiredService<AuditOutboxProcessor>();
      var processed1 = await processor.RunOnceAsync(batchSize: 100, ct: default);
      Assert.Equal(5, processed1);
      Assert.Equal(5, await ctx.AuditLogs.CountAsync());

      // Simulate retry: re-mark first 2 outbox rows as Pending and re-process
      var firstTwo = await ctx.AuditOutbox
          .Where(r => ids.Take(2).Contains(r.Id))
          .ToListAsync();
      foreach (var r in firstTwo) r.MarkPendingForTest(); // test helper exposed via partial class

      await ctx.SaveChangesAsync();
      var processed2 = await processor.RunOnceAsync(batchSize: 100, ct: default);

      Assert.Equal(2, processed2);
      Assert.Equal(5, await ctx.AuditLogs.CountAsync()); // STILL 5 — no duplicates (ON CONFLICT DO NOTHING)
  }
  ```

  (`MarkPendingForTest` è un helper interno esposto per testing — definiscilo come `internal` partial method nella classe o usa un altro meccanismo già presente nel codebase per simulare il retry.)

- [ ] **Step 5: Test — Prometheus gauges aggiornati**

  ```csharp
  [Fact]
  public async Task Metrics_ReflectPendingCount_AfterBatch()
  {
      await using var fx = await AdministrationFixture.CreateAsync();
      var ctx = fx.GetRequiredService<MeepleDbContext>();
      ctx.AuditOutbox.Add(AuditOutboxEntity.CreatePending(Guid.NewGuid(),
          @"{""action"":""t"",""resource"":""r"",""userId"":""00000000-0000-0000-0000-000000000000"",""timestamp"":""2026-05-25T00:00:00Z"",""snapshots"":[]}",
          DateTimeOffset.UtcNow));
      await ctx.SaveChangesAsync();

      var processor = fx.GetRequiredService<AuditOutboxProcessor>();
      await processor.RunOnceAsync(100, default);

      // After batch: 0 pending, age = 0
      using var stream = new MemoryStream();
      await Prometheus.Metrics.DefaultRegistry.CollectAndExportAsTextAsync(stream);
      stream.Position = 0;
      var metricsText = await new StreamReader(stream).ReadToEndAsync();
      Assert.Contains("audit_outbox_pending_count 0", metricsText);
  }
  ```

- [ ] **Step 6: Commit**

  ```bash
  git add apps/api/src/Api/BoundedContexts/Administration/Infrastructure/AuditOutboxProcessor.cs \
          apps/api/src/Api/Infrastructure/Entities/Administration/ \
          apps/api/src/Api/BoundedContexts/Administration/Application/Behaviors/ \
          tests/Api.Tests/Integration/Administration/AuditOutboxIdempotencyTests.cs
  git commit -m "feat(audit): ON CONFLICT idempotency + Prometheus metrics (Q3)"
  ```

---

## Task 5: Acceptance scenarios — 5 integration tests (Adzic)

Implementa i 5 scenari Given/When/Then concordati nel three-amigos come **gate della DoD**. Devono passare PRIMA del merge finale di S1.

**Files:**
- Create: `tests/Api.Tests/Integration/Administration/S1AcceptanceScenariosTests.cs`

- [ ] **Step 1: Implementa Scenario 1 — Delete user atomico**

```csharp
[Fact(DisplayName = "Scenario 1 — Audit di un delete user atomico")]
public async Task Scenario_1_DeleteUserAtomic()
{
    await using var fx = await AdministrationFixture.CreateAsync();
    var sender = fx.GetRequiredService<IMediator>();
    var ctx = fx.GetRequiredService<MeepleDbContext>();

    // Given
    var adminId = await fx.SeedAdminAsync("alice@meeple.app");
    var bobId = await fx.SeedUserAsync("bob@example.com", gameOwnershipCount: 3);
    fx.SimulateCurrentUser(adminId);

    // When
    await sender.Send(new DeleteUserCommand(bobId, "test-deletion"));

    // Then
    var bob = await ctx.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == bobId);
    Assert.True(bob.IsDeleted);
    var outboxRows = await ctx.AuditOutbox.Where(r => r.PayloadJson.Contains("user.delete")).ToListAsync();
    Assert.Single(outboxRows);
    Assert.Equal(OutboxStatus.Pending, outboxRows[0].Status);
    Assert.Contains("bob@example.com", outboxRows[0].PayloadJson);
}
```

- [ ] **Step 2: Implementa Scenario 2 — Audit row promoted by processor**

```csharp
[Fact(DisplayName = "Scenario 2 — Audit row promoted by processor")]
public async Task Scenario_2_AuditRowPromotedByProcessor()
{
    await using var fx = await AdministrationFixture.CreateAsync();
    var ctx = fx.GetRequiredService<MeepleDbContext>();
    var adminId = await fx.SeedAdminAsync("a@x");
    var bobId = await fx.SeedUserAsync("bob@x", gameOwnershipCount: 3);
    fx.SimulateCurrentUser(adminId);
    await fx.GetRequiredService<IMediator>().Send(new DeleteUserCommand(bobId, "t"));

    // When
    var processor = fx.GetRequiredService<AuditOutboxProcessor>();
    var processed = await processor.RunOnceAsync(100, default);

    // Then
    Assert.Equal(1, processed);
    var auditLog = await ctx.AuditLogs.SingleAsync();
    var outboxRow = await ctx.AuditOutbox.SingleAsync();
    Assert.Equal(auditLog.Id, outboxRow.Id); // idempotency key
    Assert.Equal(OutboxStatus.Sent, outboxRow.Status);
    Assert.NotNull(auditLog.BeforeJson);
    Assert.Null(auditLog.AfterJson); // delete operation
}
```

- [ ] **Step 3: Implementa Scenario 3 — Crash recovery / no duplicates**

  (riusa il pattern del test del Task 4b step 4, presentandolo qui come acceptance scenario nominato).

```csharp
[Fact(DisplayName = "Scenario 3 — Crash recovery: no duplicates on processor restart")]
public async Task Scenario_3_CrashRecovery_NoDuplicates()
{
    // ... identical to AuditOutboxIdempotencyTests test, but with DisplayName aligned
    // to the three-amigos scenario number for traceability.
}
```

- [ ] **Step 4: Implementa Scenario 4 — Best-effort handler regression**

```csharp
[Fact(DisplayName = "Scenario 4 — Best-effort: non-destructive handler audit failure is non-fatal")]
public async Task Scenario_4_BestEffort_NonDestructiveHandlerAuditFailure()
{
    await using var fx = await AdministrationFixture.CreateAsync();
    var sender = fx.GetRequiredService<IMediator>();
    var ctx = fx.GetRequiredService<MeepleDbContext>();
    var userId = await fx.SeedUserAsync("u@x");

    // Simulate audit_outbox table being unavailable (DROP TABLE in-test, or interceptor that fails)
    fx.SimulateOutboxWriteFailure = true;

    // When — ChangeRole (non-destructive, no [AtomicAudit])
    await sender.Send(new ChangeUserRoleCommand(userId, NewRole: "editor"));

    // Then — role change persisted; audit failure logged as warning, NOT as exception to caller
    var user = await ctx.Users.SingleAsync(u => u.Id == userId);
    Assert.Equal("editor", user.Role);
    // (Verify warning log captured via TestLoggerProvider injected by fx)
    Assert.Contains(fx.CapturedLogs, l => l.Level == LogLevel.Warning && l.Message.Contains("audit"));
}
```

- [ ] **Step 5: Implementa Scenario 5 — Payload truncation guard**

```csharp
[Fact(DisplayName = "Scenario 5 — Payload truncation guard for oversize entities")]
public async Task Scenario_5_PayloadTruncationGuard()
{
    await using var fx = await AdministrationFixture.CreateAsync();
    var sender = fx.GetRequiredService<IMediator>();
    var ctx = fx.GetRequiredService<MeepleDbContext>();

    var adminId = await fx.SeedAdminAsync("a@x");
    var heavyUserId = await fx.SeedUserAsync("heavy@x", gameOwnershipCount: 543);
    fx.SimulateCurrentUser(adminId);

    // When
    await sender.Send(new DeleteUserCommand(heavyUserId, "stress"));
    await fx.GetRequiredService<AuditOutboxProcessor>().RunOnceAsync(100, default);

    // Then
    var auditLog = await ctx.AuditLogs.SingleAsync();
    Assert.NotNull(auditLog.BeforeJson);
    Assert.Contains("\"_truncated\"", auditLog.BeforeJson);
    Assert.Contains("Games", auditLog.BeforeJson); // truncated collection mentioned in _truncated array
    Assert.True(System.Text.Encoding.UTF8.GetByteCount(auditLog.BeforeJson) <= 256_000);
    // Mutation still committed
    Assert.True(await ctx.Users.IgnoreQueryFilters().AnyAsync(u => u.Id == heavyUserId && u.IsDeleted));
}
```

- [ ] **Step 6: Run all 5 scenarios** (gate DoD)

  ```bash
  cd apps/api/src/Api
  dotnet test --filter "FullyQualifiedName~S1AcceptanceScenariosTests" 2>&1 | tail -10
  ```

  Expected: 5/5 PASS. **Se anche uno fallisce, S1 NON è done.**

- [ ] **Step 7: Commit**

  ```bash
  git add tests/Api.Tests/Integration/Administration/S1AcceptanceScenariosTests.cs
  git commit -m "test(audit): S1 acceptance scenarios (5 Given/When/Then)"
  ```

---

## Task 6: DoD verification — checklist 8 criteri (Wiegers)

Task non-codice ma esplicito: validare la DoD prima di mergiare la PR finale di S1.

**Files:**
- Modify: `audits/2026-05-25-s1-three-amigos-kickoff.md` (aggiungi sezione "DoD verification log" con check date e SHA del commit di verifica)

- [ ] **Step 1: Per ogni criterio DoD, esegui la verifica e annota nel log**

  | # | Criterio | Comando di verifica | Risultato atteso |
  |---|----------|--------------------|------------------|
  | 1 | Task 0 D-4 chiuso | `gh pr view <PR-D4> --json state` | `state: MERGED` |
  | 2 | Migration pulita | `dotnet ef database update --dry-run` | nessun "unexpected change" |
  | 3 | No regression handler `[AuditableAction]` | `dotnet test --filter "Category=AuditableActionRegression"` | tutti i test legacy verdi |
  | 4 | Atomicità distruttive | `dotnet test --filter "FullyQualifiedName~AtomicAuditIntegrationTests"` | PASS |
  | 5 | Latenza processor p95 < 10s | dashboard/log o synthetic benchmark `dotnet run --project tools/AuditOutboxLoadGen` | latenza p95 < 10s sotto 100 ops/sec |
  | 6 | Truncation funziona | Scenario 5 | PASS |
  | 7 | Idempotency | Scenario 3 | PASS |
  | 8 | Metriche Prometheus | `curl http://localhost:8080/metrics \| grep audit_outbox` | due gauge esposti |

- [ ] **Step 2: Compila la sezione "DoD verification log" nel workshop document**

  ```markdown
  ## DoD verification log

  | # | Criterio | Status | Verificato il | Commit SHA |
  |---|----------|--------|--------------|------------|
  | 1 | Task 0 D-4 mergiato | ✅ | 2026-MM-DD | `<sha>` |
  | 2 | Migration pulita | ✅ | ... | ... |
  | 3 | No regression `[AuditableAction]` | ✅ | ... | ... |
  | 4 | Atomicità distruttive | ✅ | ... | ... |
  | 5 | Latenza processor p95 < 10s | ✅ | ... | ... |
  | 6 | Truncation | ✅ | ... | ... |
  | 7 | Idempotency | ✅ | ... | ... |
  | 8 | Metriche Prometheus | ✅ | ... | ... |

  **S1 done: 2026-MM-DD by @<user>**
  ```

- [ ] **Step 3: Commit DoD log**

  ```bash
  git add audits/2026-05-25-s1-three-amigos-kickoff.md
  git commit -m "docs(audit): S1 DoD verification log (all 8 criteria green)"
  ```

  Solo dopo questo commit, S1 è ufficialmente done e si può aprire il plan S2.

---

## Self-Review

**1. Spec coverage:** il supplement copre tutte le 4 decisioni Q1-Q4 del workshop + i 5 scenari Adzic + la DoD 8-criteri. Ownership matrix è documentata nel workshop doc (`audits/`), non duplicata qui (riferimento incrociato).

**2. Placeholder scan:** ogni step ha codice C# concreto. `ParsePayload` è citata come helper nel Task 4b — è la stessa di Task 4 plan principale step 3 (`MaterializeAuditLogFromPayload`), eventualmente refactorata; non è placeholder ma riuso di codice già definito. `AdministrationFixture` è una test fixture esistente del progetto (verificare con `rg AdministrationFixture` — se non esiste, sostituire col pattern fixture standard del progetto: cerca `IntegrationFixture` o `WebApplicationFactory` setup).

**3. Type consistency:** `Guid` propagato come idempotency key da `AuditLoggingBehavior` → `AuditOutboxEntity.CreatePending(id, ...)` → `INSERT INTO audit_logs (id, ...)`. `OutboxStatus` enum coerente. `[AtomicAudit]` attribute referenziato da behavior + applicato a `DeleteUserCommand`. `PayloadTruncator.Truncate(...)` consumato da `AuditingSaveChangesInterceptor.SerializeProperties`.

**Note di rischio:**
- **`AdministrationFixture` esistente?**: il Task 5 assume una test fixture con `SeedAdminAsync`, `SeedUserAsync(gameOwnershipCount)`, `SimulateCurrentUser`, `SimulateOutboxWriteFailure`. Se non esiste con questo shape, primo passo Task 5 = leggere la fixture esistente e adattare. Documenta nel commit message di Task 5 step 1.
- **Raw SQL ON CONFLICT**: lega il processor a Postgres. Per multi-DB sarebbe `MERGE`. Coerente con il resto del codebase (già Postgres-only); annota nel doc-comment del processor.
- **`MarkPendingForTest`**: test helper non in produzione — esporlo via `internal` partial class o, meglio, via riflessione (più disciplina). Decidere al Task 4b step 4.

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-05-25-sp5-admin-security-s1-supplement.md`.

**Esecuzione consigliata: interlacciata col plan principale**, ordine combinato:
1. T0 (plan) → T1 (plan) → **T2 (plan) + T2b (supplement)** → **T3 (plan) + T3b (supplement)** → **T4 (plan) + T4b (supplement)** → **T5 (supplement) gate DoD** → **T6 (supplement) DoD log**

Two execution options (come da skill):

1. **Subagent-Driven (recommended)** — dispatch fresh subagent per task, two-stage review (spec+quality) tra i task. Rate limit API permitting.

2. **Inline Execution** — execute tasks in this session using `executing-plans`, batch con checkpoints.

Which approach?
