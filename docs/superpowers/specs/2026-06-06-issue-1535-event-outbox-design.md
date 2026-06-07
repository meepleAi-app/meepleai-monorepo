# Issue #1535 — Post-Commit Domain-Event Dispatch via Durable Outbox

**Date:** 2026-06-06
**Issue:** [#1535](https://github.com/meepleAi-app/meepleai-monorepo/issues/1535) — tech-debt(audit): move domain-event dispatch post-commit for `[AtomicAudit]` safety
**Kickoff:** [`audits/2026-06-06-issue-1535-event-outbox-kickoff.md`](../../../audits/2026-06-06-issue-1535-event-outbox-kickoff.md)
**Plan:** [`docs/superpowers/plans/2026-06-06-issue-1535-event-outbox.md`](../plans/2026-06-06-issue-1535-event-outbox.md)
**Status:** Design locked (Q1–Q5 decided 2026-06-06)
**Type:** Architectural decision + implementation spec

---

## Sommario

Spostare il dispatch dei domain event **fuori** da `MeepleAiDbContext.SaveChangesAsync` e in un **post-commit dispatcher** basato sul Transactional Outbox Pattern. La nuova tabella `domain_event_outbox` cattura *tutti* gli eventi raccolti da `IDomainEventCollector` nello stesso `SaveChangesAsync` che committa lo stato aggregato. Un `DomainEventOutboxProcessor` BackgroundService drena le righe Pending → invoca `MediatR.Publish` → marca Sent. Garanzia: **se la transazione fa rollback, l'outbox row non esiste, quindi nessun side-effect esce dal sistema**.

### Cosa cambia

| Componente | Prima | Dopo |
|---|---|---|
| `MeepleAiDbContext.SaveChangesAsync` (linee 481–512) | `await _mediator.Publish(domainEvent)` in-tx | `db.DomainEventOutbox.Add(outboxRow)` in-tx |
| Dispatch | Sincrono, in-process, in-tx | Asincrono, in-process, post-commit (max 5s ritardo) |
| `[AtomicAudit]` constraint | "no observable external side-effects via events" | Constraint rimosso |
| `RotateProviderKeyCommand` | `[AtomicAudit]` rifiutato per side-effect Redis | `[AtomicAudit]` re-decorato |
| Consumer contract | Implicit at-most-once dispatch | Documented: must be idempotent |

### Cosa NON cambia

- `domain_event_logs` (Issue #661): tabella append-only intatta, copre activity feed (subset registry)
- `IDomainEventCollector` API (Add/Peek/Clear): firma invariata
- `EventTypeRegistry`: invariato (10 eventi registrati per persistence)
- `audit_outbox` + `AuditOutboxProcessor`: invariati, pattern di riferimento
- Consumers (`INotificationHandler<TEvent>`): nessun cambio di firma; cambio di contratto comportamentale (idempotency richiesta esplicitamente)

---

## Motivazione (riassunto del problema)

Vedi kickoff § "Contesto". TL;DR:

1. `MeepleAiDbContext.SaveChangesAsync` linea 487 chiama `await _mediator.Publish(domainEvent)` **dentro** la stessa method call che ha appena fatto `base.SaveChangesAsync` (linea 476).
2. Per `[AtomicAudit]` commands, il SaveChanges è dentro una `tx` aperta da `AuditLoggingBehavior.HandleAtomicAsync` → `base.SaveChangesAsync` flusha le righe nella tx senza committare; ma `MediatR.Publish` esegue subito.
3. Se la `tx.CommitAsync` (linea 192 di `AuditLoggingBehavior`) fallisce o se `NpgsqlRetryingExecutionStrategy` retrya, **i side-effect del MediatR.Publish sono già usciti dal sistema** (Redis pub/sub, email, audit log downstream).
4. PR #1532 ha mitigato la re-emission *intra-strategy* con `IDomainEventCollector.Clear()` ad ogni attempt, ma non può annullare i dispatch passati.

Il pattern canonico per chiudere questo gap è il **Transactional Outbox** (Hohpe, Microservices Patterns, Gregor Hohpe & Bobby Woolf 2003; modernizzato da Chris Richardson 2018). Il sistema ha già un'implementazione di riferimento per i log audit (`audit_outbox` + `AuditOutboxProcessor`, PR #1532).

---

## Architettura

### Diagramma high-level

```
┌──────────────────────────────────────────────────────────────────────────┐
│                         Request scope (HTTP)                              │
│                                                                           │
│   ┌─────────────────────────────────────────────────────────────────┐    │
│   │              AuditLoggingBehavior.HandleAtomicAsync             │    │
│   │   ┌─────────────────────────────────────────────────────────┐   │    │
│   │   │              outer DB tx                                 │   │    │
│   │   │   ┌─────────────────────────────────────────────────┐   │   │    │
│   │   │   │   command handler                               │   │   │    │
│   │   │   │   ├─ aggregate.RaiseEvent(...)                 │   │   │    │
│   │   │   │   │   └─ IDomainEventCollector.Add(...)        │   │   │    │
│   │   │   │   └─ db.SaveChangesAsync()                    │   │   │    │
│   │   │   │      ├─ base.SaveChangesAsync()  ← aggregate  │   │   │    │
│   │   │   │      │                            ← log (opt) │   │   │    │
│   │   │   │      └─ outbox.AddRange(events)  ← NEW        │   │   │    │
│   │   │   │         _eventCollector.Clear()              │   │   │    │
│   │   │   │         (NO MediatR.Publish)  ← REMOVED       │   │   │    │
│   │   │   └─────────────────────────────────────────────────┘   │   │    │
│   │   │   └─ AuditService.EnqueueAuditAtomicAsync()             │   │    │
│   │   └─ tx.CommitAsync()  ← atomic: aggregate+log+outbox+audit  │   │    │
│   └─────────────────────────────────────────────────────────────────┘    │
│                                                                           │
└──────────────────────────────────────────────────────────────────────────┘
                                       │
                                       ▼ (decoupled, post-commit)
┌──────────────────────────────────────────────────────────────────────────┐
│         DomainEventOutboxProcessor (BackgroundService, 5s poll)          │
│                                                                           │
│   FOREACH row IN outbox WHERE status=Pending AND next_attempt_at<=NOW    │
│     ├─ deserialize payload → IDomainEvent                                │
│     ├─ try MediatR.Publish(event)                                        │
│     │   ├─ ok  → MarkSent() ✅                                            │
│     │   └─ err → attempts++, last_error, next_attempt_at = exp_backoff   │
│     │              if attempts >= 10 → MarkFailed()                       │
│     └─ commit batch (single tx)                                          │
└──────────────────────────────────────────────────────────────────────────┘
```

### ADR-style decisions

#### ADR-1535-A: Standalone `domain_event_outbox` table (no extension of `domain_event_logs`)

**Decision:** create a new table `domain_event_outbox` with full payload duplication.

**Status:** ACCEPTED

**Context:**
- `domain_event_logs` (#661) has UNIQUE on `EventId` and a JSONB `PayloadJson` column → would *technically* be enough for the dispatcher to read.
- But it's append-only by contract (#1590 P0-2); adding `Status` mutates that.
- Coverage gap: `domain_event_logs` is opt-in via `EventTypeRegistry` (10 events); the outbox is all-in (100+ events).

**Consequences:**
- ✅ Append-only log remains immutable.
- ✅ Outbox covers all events regardless of registry.
- ✅ Different lifecycles: log = forever (activity feed), outbox = TTL-clean after 30gg (operational state).
- ⚠️ Payload duplication for the registered subset (10 event types): same JSON in both tables. Storage cost: marginal (≈ 10–50 KB per row × ~few thousand/day in steady state = <100 MB/year).
- ⚠️ Outbox row delete must NOT cascade to log (separate concerns).

#### ADR-1535-B: All-in dispatch (no opt-in marker)

**Decision:** every `IDomainEvent` raised via `IDomainEventCollector.Add(...)` is dispatched post-commit. No `IExternalDomainEvent` marker, no `EventTypeRegistry.RequiresPostCommit` flag.

**Status:** ACCEPTED (with Fowler reservation: monitor latency in staging)

**Context:**
- Opt-in is more surgical but requires classifying 100+ existing events.
- Mis-classification ("oh I forgot to mark this as external") = silent ghost-dispatch bug.
- Single rule, single mental model: "if the tx doesn't commit, no event leaves the system."

**Consequences:**
- ✅ Uniform mental model; impossible to forget classification.
- ✅ Removes `[AtomicAudit]` side-effect constraint entirely.
- ⚠️ All in-process consumers (e.g. cache invalidation) now have 0–5s latency. Acceptable for measured use cases (see § Migration impact assessment).
- ⚠️ Consumer contract change: must be idempotent. Documented + enforced via lint (T6).

#### ADR-1535-C: MaxAttempts=10 + exponential backoff + Status=Failed terminal

**Decision:** retry up to 10 times with exponential backoff (1s × 2^attempts, capped at 64s, ±20% jitter). On 11th failure → `Status=Failed`, no further retries, ops alert.

**Status:** ACCEPTED

**Context:**
- `AuditOutboxProcessor` leaves poison rows in `Pending` forever → hot-loop on next poll, no backoff, no alert distinction.
- `domain_event_outbox` carries side-effect dispatch — re-trying a deterministic failure forever is harmful.

**Consequences:**
- ✅ Bounded retry time: max ~5min (1+2+4+8+16+32+64+64+64+64s with jitter).
- ✅ Failed rows are explicit ops signal (separate gauge from Pending).
- ⚠️ Configuration knob (`DomainEventOutbox:MaxAttempts`, `DomainEventOutbox:InitialBackoffMs`) — exposed in `appsettings.json`.

#### ADR-1535-D: Best-effort FIFO ordering, no per-aggregate sequencing

**Decision:** processor orders by `next_attempt_at ASC, enqueued_at ASC`. No per-aggregate lock or sequence.

**Status:** ACCEPTED (with Newman reservation: revisit if event-sourcing replay use case emerges)

**Context:**
- Per-aggregate sequencing requires advisory locks or per-aggregate worker lanes → kills batch throughput.
- Current consumers (audited): no strict ordering dependency between events on the same aggregate across separate transactions.

**Consequences:**
- ✅ Simple, high-throughput batch processing.
- ⚠️ Future event-sourcing consumer may need opt-in via `EventTypeRegistry.RequiresOrderingByAggregate=true` (out-of-scope MVP, tracked as follow-up).

#### ADR-1535-E: Consumer idempotency contract (documented, not enforced at compile-time)

**Decision:** all `INotificationHandler<TEvent>` consumers MUST be idempotent (same event twice → same observable outcome). Documented in `docs/for-developers/architecture/domain-events-post-commit-contract.md`. NOT enforced via type marker or static analyzer in v1.

**Status:** ACCEPTED

**Context:**
- At-least-once delivery is unavoidable in distributed systems (crash between `Publish` and `MarkSent`).
- Compile-time enforcement (e.g. `IIdempotentHandler<T>`) is intrusive and gameable.
- Existing consumers reviewed in T0 audit (see plan § T0): all 23 are *naturally* idempotent (cache invalidation, INSERT…ON CONFLICT audit row, SSE broadcast = stateless).

**Consequences:**
- ✅ No code-level intrusion.
- ⚠️ Relies on developer discipline + plan T0 audit; future consumers must follow the contract.
- ⚠️ Mitigation: add unit-test template in T0 doc for "consumer X dispatched twice → produces single observable effect".

---

## Detailed design

### Entity

```csharp
namespace Api.Infrastructure.Entities.DomainEventOutbox;

/// <summary>
/// Outbox row for post-commit domain-event dispatch.
/// Issue #1535 — replaces inline MediatR.Publish from MeepleAiDbContext.SaveChangesAsync.
///
/// Lifecycle:
///   1. INSERT (Status=Pending) inside the same SaveChangesAsync as the aggregate mutation.
///   2. DomainEventOutboxProcessor (5s poll) reads rows with Status=Pending AND
///      (next_attempt_at IS NULL OR next_attempt_at <= NOW).
///   3. Deserialize PayloadJson → IDomainEvent → MediatR.Publish.
///   4. On success: MarkSent() (Status=Sent, DispatchedAt=NOW).
///   5. On failure: MarkRetry(ex.Message) (attempts++, next_attempt_at = exp backoff)
///      OR MarkFailed(ex.Message) when attempts >= MaxAttempts.
/// </summary>
public sealed class DomainEventOutboxEntity
{
    public Guid Id { get; private set; }                  // == IDomainEvent.EventId
    public string EventType { get; private set; } = "";   // EventTypeRegistry alias OR CLR FullName
    public string PayloadJson { get; private set; } = ""; // jsonb
    public int PayloadVersion { get; private set; } = 1;
    public DomainEventOutboxStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }       // truncated to 2048 chars
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset EnqueuedAt { get; private set; }
    public DateTimeOffset? DispatchedAt { get; private set; }
    public DateTimeOffset? NextAttemptAt { get; private set; }
    public string? CorrelationId { get; private set; }    // request scope id

    private DomainEventOutboxEntity() { /* EF */ }

    public static DomainEventOutboxEntity Enqueue(
        IDomainEvent ev,
        string eventType,
        string payloadJson,
        int payloadVersion,
        string? correlationId,
        DateTimeOffset now)
    {
        return new DomainEventOutboxEntity
        {
            Id              = ev.EventId,
            EventType       = eventType,
            PayloadJson     = payloadJson,
            PayloadVersion  = payloadVersion,
            Status          = DomainEventOutboxStatus.Pending,
            Attempts        = 0,
            OccurredAt      = ev.OccurredAt,
            EnqueuedAt      = now,
            DispatchedAt    = null,
            NextAttemptAt   = null,
            CorrelationId   = correlationId,
        };
    }

    public void MarkSent(DateTimeOffset now)
    {
        if (Status != DomainEventOutboxStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot MarkSent from status {Status}.");
        Status = DomainEventOutboxStatus.Sent;
        DispatchedAt = now;
        NextAttemptAt = null;
        LastError = null;
    }

    public void MarkRetry(string error, DateTimeOffset nextAttemptAt, DateTimeOffset now)
    {
        if (Status != DomainEventOutboxStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot MarkRetry from status {Status}.");
        Attempts++;
        LastError = Truncate(error, 2048);
        NextAttemptAt = nextAttemptAt;
    }

    public void MarkFailed(string error, DateTimeOffset now)
    {
        if (Status != DomainEventOutboxStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot MarkFailed from status {Status}.");
        Status = DomainEventOutboxStatus.Failed;
        Attempts++;
        LastError = Truncate(error, 2048);
        NextAttemptAt = null;
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..max];
}

public enum DomainEventOutboxStatus : byte
{
    Pending = 0,
    Sent    = 1,
    Failed  = 2,
}
```

### EF configuration

```csharp
internal sealed class DomainEventOutboxEntityConfiguration
    : IEntityTypeConfiguration<DomainEventOutboxEntity>
{
    public void Configure(EntityTypeBuilder<DomainEventOutboxEntity> b)
    {
        b.ToTable("domain_event_outbox");
        b.HasKey(e => e.Id);

        b.Property(e => e.EventType).IsRequired().HasMaxLength(256);
        b.Property(e => e.PayloadJson).HasColumnType("jsonb").IsRequired();
        b.Property(e => e.PayloadVersion).IsRequired().HasDefaultValue(1);
        b.Property(e => e.Status).IsRequired().HasConversion<byte>();
        b.Property(e => e.Attempts).IsRequired().HasDefaultValue(0);
        b.Property(e => e.LastError).HasMaxLength(2048);
        b.Property(e => e.OccurredAt).IsRequired();
        b.Property(e => e.EnqueuedAt).IsRequired();
        b.Property(e => e.CorrelationId).HasMaxLength(128);

        // Hot path: processor reads Pending rows ready for retry.
        // Partial index keeps it tiny (Sent/Failed rows excluded).
        b.HasIndex(e => new { e.NextAttemptAt, e.EnqueuedAt })
            .HasDatabaseName("ix_domain_event_outbox_pending")
            .HasFilter("status = 0");

        // Ops dashboard: list recent failures.
        b.HasIndex(e => e.EnqueuedAt)
            .IsDescending()
            .HasDatabaseName("ix_domain_event_outbox_failed_recent")
            .HasFilter("status = 2");
    }
}
```

### `MeepleAiDbContext.SaveChangesAsync` refactor

Current (lines 449–514) → target diff:

```diff
 public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
 {
     var pendingEvents = _eventCollector.PeekEvents() ?? Array.Empty<IDomainEvent>();

-    // Step 2: materialize log entities for registered events.
+    // Step 2a: materialize log entities for registered events (Issue #661 — unchanged).
     if (pendingEvents.Count > 0)
     {
         foreach (var domainEvent in pendingEvents)
         {
             var logEntity = DomainEventLogMapper.Map(domainEvent);
             if (logEntity is not null)
             {
                 DomainEventLogs.Add(logEntity);
                 MeepleAiMetrics.DomainEventsInserted.Add(
                     1, new KeyValuePair<string, object?>("event_type", logEntity.EventType));
             }
         }
+
+        // Step 2b: materialize outbox rows for ALL events (Issue #1535 — post-commit dispatch).
+        var now = _timeProvider.GetUtcNow();
+        var correlationId = _correlationProvider.Current;
+        foreach (var domainEvent in pendingEvents)
+        {
+            var eventType = EventTypeRegistry.TryResolve(domainEvent)
+                         ?? domainEvent.GetType().FullName!;
+            var payloadJson = JsonSerializer.Serialize(
+                (object)domainEvent, domainEvent.GetType(), DomainEventJsonOptions.Default);
+            var outboxRow = DomainEventOutboxEntity.Enqueue(
+                domainEvent, eventType, payloadJson, payloadVersion: 1, correlationId, now);
+            DomainEventOutbox.Add(outboxRow);
+            MeepleAiMetrics.DomainEventOutboxEnqueued.Add(
+                1, new KeyValuePair<string, object?>("event_type", eventType));
+        }
     }

     // Step 3: single SaveChangesAsync — aggregate state + log + outbox commit atomically.
     var result = await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

     // Step 4: drain the collector ONLY after successful save.
     _eventCollector.Clear();

-    // Step 5: dispatch via MediatR. The log record is already durable…
-    foreach (var domainEvent in pendingEvents)
-    {
-        try { await _mediator.Publish(domainEvent, cancellationToken)…; }
-        catch (Exception ex) { _logger?.LogError(ex, …); …; }
-    }
+    // Step 5: NO MediatR.Publish here — DomainEventOutboxProcessor dispatches post-commit.
+    // Issue #1535 — removes the side-effect-before-commit race condition.

     return result;
 }
```

### Processor

```csharp
namespace Api.Infrastructure.DomainEventOutbox;

internal sealed class DomainEventOutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DomainEventOutboxProcessor> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly DomainEventOutboxOptions _options;
    private readonly IDomainEventOutboxHealthTracker _healthTracker;

    public async Task<int> RunOnceAsync(int batchSize, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var typeResolver = scope.ServiceProvider.GetRequiredService<IDomainEventTypeResolver>();

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            var now = _timeProvider.GetUtcNow();

            // FIFO by readiness then enqueue order, partial index drives the plan.
            var pending = await db.DomainEventOutbox
                .AsTracking()
                .Where(r => r.Status == DomainEventOutboxStatus.Pending
                         && (r.NextAttemptAt == null || r.NextAttemptAt <= now))
                .OrderBy(r => r.NextAttemptAt ?? r.EnqueuedAt)
                .ThenBy(r => r.EnqueuedAt)
                .Take(batchSize)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            if (pending.Count == 0) { await UpdateHealthAsync(db, ct); return 0; }

            await using var tx = await db.Database
                .BeginTransactionAsync(ct).ConfigureAwait(false);

            foreach (var row in pending)
            {
                IDomainEvent? evt = null;
                try
                {
                    var clrType = typeResolver.Resolve(row.EventType)
                        ?? throw new InvalidOperationException(
                            $"Unknown event type alias: {row.EventType}");
                    evt = (IDomainEvent)JsonSerializer.Deserialize(
                        row.PayloadJson, clrType, DomainEventJsonOptions.Default)!;

                    using (PushCorrelation(row.CorrelationId))
                    {
                        await mediator.Publish(evt, ct).ConfigureAwait(false);
                    }
                    row.MarkSent(now);
                    MeepleAiMetrics.DomainEventOutboxDispatched
                        .Add(1, new KeyValuePair<string, object?>("event_type", row.EventType));
                }
                catch (Exception ex)
                {
                    if (row.Attempts + 1 >= _options.MaxAttempts)
                    {
                        row.MarkFailed(ex.Message, now);
                        MeepleAiMetrics.DomainEventOutboxFailedTerminal
                            .Add(1, new KeyValuePair<string, object?>("event_type", row.EventType));
                        _logger.LogError(ex,
                            "Domain event {EventType} (EventId={EventId}) FAILED terminally after {Attempts} attempts",
                            row.EventType, row.Id, row.Attempts + 1);
                    }
                    else
                    {
                        var backoff = ComputeBackoff(row.Attempts + 1);
                        row.MarkRetry(ex.Message, now + backoff, now);
                        MeepleAiMetrics.DomainEventOutboxRetried
                            .Add(1, new KeyValuePair<string, object?>("event_type", row.EventType));
                    }
                }
            }

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            await tx.CommitAsync(ct).ConfigureAwait(false);
            await UpdateHealthAsync(db, ct).ConfigureAwait(false);
            return pending.Count;
        }).ConfigureAwait(false);
    }

    private TimeSpan ComputeBackoff(int attempt)
    {
        // Exponential: 1s, 2s, 4s, 8s, 16s, 32s, 64s, 64s, 64s, 64s
        var seconds = Math.Min(
            _options.InitialBackoffMs / 1000.0 * Math.Pow(2, attempt - 1),
            _options.MaxBackoffSeconds);
        var jitter = (Random.Shared.NextDouble() * 0.4 - 0.2); // ±20%
        return TimeSpan.FromSeconds(seconds * (1 + jitter));
    }
}

public sealed class DomainEventOutboxOptions
{
    public int PollIntervalSeconds { get; init; } = 5;
    public int BatchSize { get; init; } = 100;
    public int MaxAttempts { get; init; } = 10;
    public int InitialBackoffMs { get; init; } = 1000;
    public double MaxBackoffSeconds { get; init; } = 64.0;
}
```

### Type resolution

The processor needs CLR `Type` from string `EventType`. Implementation: scan `Api` assembly for `IDomainEvent` implementations at startup, build a dictionary `{ alias|FullName → Type }`.

```csharp
public interface IDomainEventTypeResolver
{
    Type? Resolve(string eventType);
}

internal sealed class DomainEventTypeResolver : IDomainEventTypeResolver
{
    private readonly Dictionary<string, Type> _byAlias;
    private readonly Dictionary<string, Type> _byFullName;

    public DomainEventTypeResolver()
    {
        var eventTypes = typeof(IDomainEvent).Assembly
            .GetTypes()
            .Where(t => !t.IsAbstract && typeof(IDomainEvent).IsAssignableFrom(t))
            .ToArray();

        _byAlias = EventTypeRegistry.AliasByType
            .ToDictionary(kvp => kvp.Value, kvp => kvp.Key, StringComparer.Ordinal);

        _byFullName = eventTypes
            .ToDictionary(t => t.FullName!, t => t, StringComparer.Ordinal);
    }

    public Type? Resolve(string eventType)
        => _byAlias.TryGetValue(eventType, out var byAlias)
            ? byAlias
            : _byFullName.GetValueOrDefault(eventType);
}
```

**Risk:** CLR type rename orphans outbox rows whose `EventType` is the old FullName.
**Mitigation:** outbox rows have TTL (30gg) — Sent rows are purged, Failed rows are dashboard-visible.

### JSON serialization

```csharp
internal static class DomainEventJsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        IgnoreReadOnlyFields = false,
        IncludeFields = false,
    };
}
```

**Required event contract:**
- Deserializable from JSON via `JsonSerializer.Deserialize(payload, type, options)`.
- T0 audit verifies all 100+ existing `IDomainEvent` implementations are POCO records with parameterless or annotated constructors.

### Observability (Prometheus)

Following `MeepleAiMetrics.AuditOutbox` naming convention:

```csharp
public static class DomainEventOutboxMetrics
{
    public static readonly Counter<long> DomainEventOutboxEnqueued =
        Meter.CreateCounter<long>(
            "meepleai_domain_event_outbox_enqueued_total",
            "events", "Count of rows INSERTed into domain_event_outbox.");

    public static readonly Counter<long> DomainEventOutboxDispatched =
        Meter.CreateCounter<long>(
            "meepleai_domain_event_outbox_dispatched_total",
            "events", "Count of rows successfully MarkSent.");

    public static readonly Counter<long> DomainEventOutboxRetried =
        Meter.CreateCounter<long>(
            "meepleai_domain_event_outbox_retried_total",
            "events", "Count of MarkRetry transitions (transient failures).");

    public static readonly Counter<long> DomainEventOutboxFailedTerminal =
        Meter.CreateCounter<long>(
            "meepleai_domain_event_outbox_failed_terminal_total",
            "events", "Count of MarkFailed transitions (exhausted retries).");

    // Gauges populated by IDomainEventOutboxHealthTracker on every poll.
    public static readonly ObservableGauge<long> PendingCount = ...;
    public static readonly ObservableGauge<double> OldestPendingAgeSeconds = ...;
    public static readonly ObservableGauge<long> FailedCount = ...;
}
```

**Alert rules (Prometheus):**

```yaml
groups:
  - name: domain_event_outbox
    rules:
      - alert: DomainEventOutboxBacklog
        expr: meepleai_domain_event_outbox_pending_count > 1000
        for: 5m
        labels: { severity: warning }
        annotations:
          summary: "Domain event outbox backlog > 1000 for 5min"
          description: "Pending rows: {{ $value }}. Check processor health."

      - alert: DomainEventOutboxStale
        expr: meepleai_domain_event_outbox_oldest_pending_age_seconds > 300
        for: 2m
        labels: { severity: critical }
        annotations:
          summary: "Domain event outbox processor degraded"
          description: "Oldest pending row is {{ $value | humanizeDuration }} old."

      - alert: DomainEventOutboxFailedSpike
        expr: increase(meepleai_domain_event_outbox_failed_terminal_total[10m]) > 50
        for: 5m
        labels: { severity: critical }
        annotations:
          summary: "Domain event outbox terminal failures spike (>50 in 10min)"
          description: "Check failed rows on /admin/monitor?tab=events."
```

---

## Migration impact assessment

### Existing `IDomainEvent` consumers (T0 audit)

Plan T0 enumerates all `INotificationHandler<TEvent>` and tags each as `naturally idempotent` / `requires-review`. Preview based on grep:

| Consumer | Behavior | Idempotent? |
|---|---|---|
| `DomainEventAuditHandler<TEvent>` (#1534, #1788) | INSERT row in `audit_logs` via `ON CONFLICT DO NOTHING` (open-generic) | ✅ yes (PK guard) |
| Cache invalidation handlers (`AgentDefinitionUpdatedEvent`, `PdfMetadataChangedEvent`) | `IHybridCacheService.RemoveByTagAsync(tag)` | ✅ yes (idempotent by definition) |
| Activity feed handlers (registry-enabled) | Materialized by `DomainEventAuditHandler` → already covered above | ✅ yes |
| SSE/SignalR broadcasters | Push to connected clients; double-push = client dedupes by `EventId` | ⚠️ requires client-side dedup verification (T0 sub-task) |
| Email notifications (`AlertFiredEvent`) | Enqueue in `email_queue` → check existence by `EventId` | ⚠️ requires verification of `email_queue` idempotency |
| Webhook (n8n integration) | POST to external — n8n endpoint must dedupe by `EventId` | ⚠️ requires n8n configuration verification |

T0 audit completes the matrix; any ⚠️ row blocks merge.

### Latency impact

| Use case | Today (sync MediatR) | After (5s avg poll) | Acceptable? |
|---|---|---|---|
| Cache invalidation | 1–5 ms | 0–5 s | ✅ — cache is eventually consistent |
| Audit log INSERT | 5–20 ms | 0–5 s | ✅ — audit visible within UX-acceptable window |
| Email queue enqueue | 1–10 ms | 0–5 s | ✅ — email is async anyway |
| SSE broadcast | 1–5 ms | 0–5 s | ✅ — feed update within UX-acceptable window |
| Webhook fire (n8n) | 50–500 ms (network) | 50–500 ms + 0–5s | ✅ — n8n is async by design |

Peak latency p95 SLA: **< 10s** (DoD-9, measured in staging).

### Deployment strategy (in-flight events)

Two-phase deploy to avoid race during rollout:

**Phase A (T8 ship):**
1. Deploy code with feature flag `EventDispatch:Mode = "Hybrid"` (default).
2. Hybrid mode: `MeepleAiDbContext.SaveChangesAsync` writes outbox row AND continues to `MediatR.Publish` inline (duplicate work, no behavior change).
3. Consumers see 2× dispatch — verifies idempotency hypothesis. Failed assumptions surface in staging.
4. Soak in staging 24h, monitor `_dispatched_total` vs `_enqueued_total` (must be 1:1 modulo retries).

**Phase B (T9 cutover, separate PR):**
1. Flip flag to `EventDispatch:Mode = "OutboxOnly"`.
2. Inline `MediatR.Publish` removed (else branch).
3. Soak prod 7gg.
4. Phase C (T10, follow-up PR): delete the flag + dead code.

---

## Rollback plan

If outbox processor has critical bug post-deploy:

> **Post-T10 update**: the `InlineOnly` mode was removed in T10 cleanup. The canonical
> rollback path is now `DomainEventOutbox:Mode = "Hybrid"` — see
> `audits/2026-06-07-issue-1535-phase-b-cutover-pr-draft.md` § Rollback path for the
> current production runbook. The Hybrid rollback restores inline `MediatR.Publish`
> alongside the outbox; consumers see 2× dispatch but the system continues to function.

1. **Immediate (legacy spec — DO NOT FOLLOW):** flip `EventDispatch:Mode = "InlineOnly"`. **Replaced:** flip `DomainEventOutbox:Mode = "Hybrid"`. The outbox processor continues to drain accumulated rows; the inline dispatch path resumes for new events.
2. **Diagnostic:** investigate poison rows via `/api/v1/admin/event-outbox/failed` (endpoint shipped in T6).
3. **Recovery:** patch processor, flip back to `OutboxOnly`, replay terminal Failed rows via `POST /api/v1/admin/event-outbox/{id}/retry`.
4. **Last resort:** truncate `domain_event_outbox` (loses pending work; consumers either tolerate loss or trigger manual replay from `domain_event_logs` for the registered subset).

---

## Out of scope (follow-up issues)

- **OQ-2:** `SELECT ... FOR UPDATE SKIP LOCKED` for multi-instance work-stealing (track if duplicate dispatch rate > 5% in staging).
- **OQ-4:** `/admin/monitor?tab=events` UI page implementation (BE entity + endpoints are in scope; FE page is its own work-package).
- **Per-aggregate ordering** (ADR-1535-D reservation): opt-in via `EventTypeRegistry.RequiresOrderingByAggregate`, dedicated processor lane.
- **OutboxBase abstraction**: shared `OutboxProcessorBase<TPayload>` between `audit_outbox` and `domain_event_outbox` — premature now (Newman); revisit when a 3rd outbox emerges.
- **Compile-time idempotency contract** (`IIdempotentHandler<T>` marker): heavy; revisit if consumer reviews reveal recurring idempotency bugs.

---

## References

- Issue #661 — durable domain event log (`domain_event_logs`)
- PR #1532 — SP5 S1 audit schema + outbox + atomic destructive audit
- PR #1788 — DomainEventAuditHandler dual-path consolidation (Issue #1534)
- PR #1934 — RotateProviderKeyCommand (#1859) — concrete example of `[AtomicAudit]` rejection due to external side-effect
- ADR-051 — Mechanic Extractor async pipeline (BackgroundService pattern reference)
- Hohpe & Woolf, *Enterprise Integration Patterns* (2003) — Transactional Outbox
- Chris Richardson, *Microservices Patterns* (2018) — Outbox + Idempotent Receiver
- Nygard, *Release It!* (2nd ed., 2018) — retry budgets, dead-letter, circuit breakers
