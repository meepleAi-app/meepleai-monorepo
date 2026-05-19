# DomainEventLog infrastructure for Library activity feed — Spec (draft) for #661

| Field | Value |
|---|---|
| **Issue** | [#661](https://github.com/meepleAi-app/meepleai-monorepo/issues/661) — [Library activity] Track removed + session-recorded events (DomainEventLog infrastructure) |
| **Status** | hardened — panel score 5.8 → ~8.5/10 |
| **Date** | 2026-05-19 |
| **Parent** | Wave B.3 followup of #642 (PR #645) |
| **Chosen scope** | Option 2 — DomainEventLog + interceptor (most flexible, biggest blast radius) |
| **Hardening** | 2026-05-19 — P0 atomicity fix + P0 EventType registry mandatory + P0 pagination redesign. See §10 change-log. |

## 1. Context

`GET /api/v1/library/activity` currently emits only two pseudo-events (`added`, `state-changed`) derived synthetically from `UserLibraryEntries` row timestamps. The Zod schema and the v2 `RecentActivityRail` already accept four kinds, but `removed` is impossible (rows are hard-deleted) and `session-recorded` is not joined.

Discovery (2026-05-19):
- Domain event infrastructure ALREADY exists: `IDomainEvent` (extends `INotification`), `AggregateRoot.AddDomainEvent`, `IDomainEventCollector`, `MeepleAiDbContext.SaveChangesAsync` dispatches via `IMediator.Publish` after successful save (file `MeepleAiDbContext.cs:408-426`).
- What's missing: a **durable log** of those events. Today they're dispatched in-memory only (handlers consume them, then they're gone).

## 2. Goals & Non-goals

### Goals

- **G1**: Persist every `IDomainEvent` raised by aggregates into a new append-only `DomainEventLog` table.
- **G2**: Add `LibraryEntryRemovedEvent` raised by the remove-from-library command and `GameSessionRecordedEvent` (existing? check during impl) raised by `RecordGameSessionCommand`.
- **G3**: Refactor `GetLibraryActivityQueryHandler` to read from `DomainEventLog` for the new kinds (`removed`, `session-recorded`) while keeping the legacy `added` + `state-changed` projections from `UserLibraryEntries` rows (avoid double-counting + preserve back-fill for pre-event data).
- **G4**: Document retention policy + add an integration test that the 4 kinds round-trip through the activity feed.
- **G5**: Frontend mapper drops the temporary `removed → null` branch (if it exists; grep returns no hits — likely already absent).

### Non-goals

- **NG1**: Replace the existing in-memory MediatR dispatch. The log is **in addition to**, not instead of, MediatR notification handlers — handlers continue to run on `Publish`. The log is the durable record.
- **NG2**: Back-fill historical events. Pre-existing `added` / `state-changed` data stays projected from row timestamps; only events raised **after** this PR lands appear in the log.
- **NG3**: Cross-BC event consolidation. This PR scopes the log to UserLibrary domain events (and any others that happen to flow through the same `SaveChangesAsync` path, but the consumer only reads what it understands).
- **NG4**: GDPR purge / retention enforcement automation. We document the retention policy (default: 90 days) but the cleanup job is a separate ops ticket.

## 3. Architecture

### 3.1 New entity `DomainEventLog` (Infrastructure layer)

```csharp
public class DomainEventLog
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }              // From IDomainEvent.EventId — see UNIQUE INDEX below (P1-3)
    public string EventType { get; set; }          // MANDATORY stable alias from EventTypeRegistry (P0-2)
    public Guid? UserId { get; set; }              // When the event has an associated user (most do)
    public Guid? AggregateId { get; set; }         // The aggregate root that raised the event
    public string? AggregateType { get; set; }     // e.g. "UserLibraryEntry"
    public string PayloadJson { get; set; }        // JSON-serialized event body
    public DateTime OccurredAt { get; set; }       // From IDomainEvent.OccurredAt
    public DateTime LoggedAt { get; set; }         // Server timestamp on persist
}
```

**Indexes (P1-1 + P1-3 + AC-1)**:
- `ix_domain_event_logs_user_loggedat` on `(UserId, LoggedAt DESC)` — primary query path (activity feed).
- `ix_domain_event_logs_loggedat` on `(LoggedAt)` — future cleanup job scan.
- `ux_domain_event_logs_eventid` UNIQUE on `EventId` — idempotency guard against double-write on retry.

### 3.1a `EventTypeRegistry` (opt-in with stable alias)

Discovery during impl (2026-05-19) showed 100+ existing `IDomainEvent` implementations across all BCs. Requiring each to either register or opt-out via attribute would balloon PR-A scope. **Revised P0-2 fix**: opt-in by default. Only events that the application explicitly wants persisted into the log are registered; everything else is silently skipped at the persistence step but STILL dispatched via MediatR (unchanged behavior for in-memory consumers).

This preserves the P0-2 intent — class renames cannot silently orphan log rows — because the registry uses stable aliases, not CLR type names. The trade-off: an author who wants their event logged must add it to the registry explicitly. That deliberate choice is exactly what the panel wanted.

```csharp
public static class EventTypeRegistry
{
    private static readonly IReadOnlyDictionary<Type, string> AliasByType = new Dictionary<Type, string>
    {
        // Registered event types are persisted to domain_event_logs in addition
        // to being dispatched via MediatR.Publish. Adding an entry here is the
        // deliberate opt-in. UN-registered events go through MediatR only, as
        // they did before this issue shipped — zero breaking change for them.
        //
        // PR-B adds:
        //   [typeof(LibraryEntryRemovedEvent)] = "library.entry.removed",
        //   [typeof(GameSessionRecordedEvent)] = "library.session.recorded",
    };

    /// <summary>
    /// Returns the stable alias for events registered for log persistence, or
    /// <c>null</c> when the event should NOT be logged. Callers (the DbContext)
    /// skip the log persistence step for null returns.
    /// </summary>
    public static string? TryResolve(IDomainEvent ev) =>
        AliasByType.TryGetValue(ev.GetType(), out var alias) ? alias : null;
}
```

The assembly-scan test (AC-10) is reframed: it asserts that **every entry in the registry maps to a real `IDomainEvent` implementation** (catches stale aliases after a class is deleted). It does NOT require every event to be registered — opt-in by design.

### 3.2 Persistence path — ATOMIC single-save (P0-1 + P1-2 fix)

The original draft proposed nested `SaveChangesAsync` — panel flagged this as a critical atomicity flaw. **Fix**: collect events from aggregates, materialize log entities, add to the change tracker, then call `base.SaveChangesAsync` **once**. Aggregate state and log rows commit in a single DB round-trip (EF wraps `SaveChangesAsync` in an implicit transaction).

```csharp
public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
{
    // Snapshot events into a local list BEFORE saving — if save fails we still
    // know what was queued (P1-2 fix: don't drain the collector on failure).
    var pendingEvents = _eventCollector.PeekEvents(); // NEW signature; see §3.2a

    if (pendingEvents.Count > 0)
    {
        foreach (var domainEvent in pendingEvents)
        {
            DomainEventLogs.Add(DomainEventLogMapper.Map(domainEvent, EventTypeRegistry.Resolve));
        }
    }

    var result = await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

    // Only drain the collector after a successful save.
    _eventCollector.Clear();

    // Dispatch via MediatR AFTER the durable record is committed. Any handler
    // failure now is a recovery problem, not a "did the event happen" problem.
    foreach (var domainEvent in pendingEvents)
    {
        try
        {
            await _mediator.Publish(domainEvent, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // P2-3 fix: structured ERROR log on dispatch failure. The log row is
            // already durable; handlers can be retried offline by reading the log.
            _logger.LogError(ex,
                "MediatR.Publish failed for {EventType} (EventId={EventId}); log row already committed",
                domainEvent.GetType().Name, domainEvent.EventId);
        }
    }

    return result;
}
```

### 3.2a `IDomainEventCollector` extension

Existing surface: `CollectEventsFrom(IAggregateRoot)`, `GetAndClearEvents()`. The latter is destructive and called before persist — incompatible with the atomic-save flow. Extend with:

- `IReadOnlyList<IDomainEvent> PeekEvents()` — non-destructive snapshot
- `void Clear()` — explicit drain, called only after successful `SaveChangesAsync`

`GetAndClearEvents()` stays for backward compat with existing callers (if any) but the DbContext path uses the new pair.

### 3.3 New `LibraryEntryRemovedEvent`

Raised by the existing remove-from-library command handler (find during impl) when the `UserLibraryEntry` aggregate is removed from the DbSet. The event body carries: `UserLibraryEntryId`, `UserId`, `GameId`, `GameTitle` (optional — denormalized so the log row carries enough to render the activity item without joining a now-deleted row), `RemovedAt`.

### 3.4 `GameSessionRecordedEvent`

Likely already exists via `RecordGameSessionCommandHandler.Publish(...)` (grep showed line 67). Verify during impl. If exists: reuse. If not: add with `UserLibraryEntryId`, `UserId`, `GameId`, `GameTitle`, `PlayedAt`, `DurationMinutes`.

### 3.5 `GetLibraryActivityQueryHandler` refactor (P0-3 redesign)

The original draft proposed an in-memory UNION of two sources with `Take(N)` applied post-merge — panel correctly flagged pagination + ordering correctness gap. **Redesigned approach**: read from `DomainEventLog` ONLY for new kinds, projecting from `UserLibraryEntries` ONLY for kinds that have no event coverage. Each source has independent semantics; no UNION is needed because the kinds are disjoint.

```csharp
// Source A: row-timestamp projection (LEGACY — for kinds NOT yet covered by events)
//   Today: `added`, `state-changed` (pre-event back-compat — entries written before
//   this PR have no events but still need to appear in the feed).
// Source B: log query (NEW — for `removed`, `session-recorded`)
//
// At query time:
//   1. Fetch up to N most recent log rows where EventType IN (registered short
//      aliases) AND UserId = caller AND LoggedAt >= cutoff.
//   2. Fetch up to N row-timestamp events from UserLibraryEntries (existing logic).
//   3. Merge both lists IN MEMORY (small N — default 50, cap 100), order by
//      timestamp DESC, take N.
//
// Pagination: cursor is opaque (base64 of last-item timestamp + last-item id).
// At page N+1, both source queries are re-issued with `WHERE timestamp < cursor`.
// The merge stays correct because each source is internally ordered and
// `Take(N)` after merge ensures we don't return more than N items.
//
// Correctness: a `removed` event for entry X coexists with an `added` event for
// the same entry X — by design (the feed shows both "added Catan on day 1" and
// "removed Catan on day 7"). No de-duplication needed.
```

**Why not a single SQL UNION?** EF Core can express it but the projection types differ (different tables, different columns); the in-memory merge stays cheaper than a virtual view at this scale (N=50, two sources of size <= N each). If page sizes grow beyond 200 we re-evaluate (P2 follow-up).

**Once kinds (a) ARE event-backed** (future PR — out of scope here), source A can be deleted entirely and the query becomes a pure DomainEventLog scan.

### 3.6 Retention policy

- **Default**: 90 days after `LoggedAt`. The activity feed query filters `LoggedAt >= NOW() - INTERVAL '90 days'` (Postgres index on `LoggedAt`).
- **Enforcement**: documented in this spec; a scheduled cleanup job is **out of scope** for this PR (NG4).
- The 90-day window is configurable via `DomainEventLog:RetentionDays` (env). Default 90.

## 4. AC SMART (hardened — 14 ACs)

- [ ] **AC-1** EF Core migration adds the `domain_event_logs` table with 3 indexes per §3.1: `(UserId, LoggedAt DESC)`, `(LoggedAt)`, UNIQUE on `EventId`. UP+DOWN tested.
- [ ] **AC-2** `MeepleAiDbContext.SaveChangesAsync` persists log rows + aggregate state in a SINGLE `base.SaveChangesAsync` call (P0-1). Verified by an integration test that asserts on a thrown exception inside the second save path: the aggregate state is NOT committed.
- [ ] **AC-2b** `_eventCollector.Clear()` is called ONLY after `base.SaveChangesAsync` succeeds (P1-2). Verified by raising an event, throwing inside save, asserting `PeekEvents()` still returns the event on the next call.
- [ ] **AC-3** `LibraryEntryRemovedEvent` defined + raised by the remove-from-library handler. Verified by xUnit test on the handler.
- [ ] **AC-4** `GameSessionRecordedEvent` raised by `RecordGameSessionCommandHandler`. Verified by xUnit test.
- [ ] **AC-5** Full integration path: issue remove command → assert `domain_event_logs` row exists with the expected `EventType` alias → assert activity feed returns the `removed` kind (P1-4: covers the chain, not just the handler).
- [ ] **AC-5b** Same integration path for `session-recorded`.
- [ ] **AC-6** Retention: query filters `LoggedAt >= NOW() - DomainEventLog:RetentionDays days`. Verified by seeding an event with `LoggedAt = today - 91 days` and asserting it does NOT appear.
- [ ] **AC-7** Activity feed retention policy + write-side growth documented in code comment + project doc; the doc explicitly references the missing cleanup-job ticket (NG4 + P1-1).
- [ ] **AC-8** `LibraryActivityEndpointTests` extended with 4 new tests, one per kind, all green.
- [ ] **AC-9** Frontend mapper unchanged (no temporary `removed → null` branch found during discovery — verified via `grep -rn "removed.*null" apps/web/src/lib` returned no hits 2026-05-19).
- [ ] **AC-10** EventTypeRegistry stale-alias test (P0-2 revised for opt-in): a unit test asserts every entry in `AliasByType` resolves to a real `IDomainEvent` implementation in the loaded assemblies. Fails the build if an event class is renamed/deleted without updating the registry. (Opt-in registration is intentional; missing registration only means the event isn't logged, not that the build breaks.)
- [ ] **AC-11** Idempotency: writing the same `EventId` twice fails with a unique-constraint violation. Verified by integration test calling `DomainEventLogs.Add` with a duplicate `EventId`.
- [ ] **AC-12** Pagination correctness (P0-3): given 30 rows in source A + 30 rows in source B interleaved by timestamp, paging at size 10 returns the correct 10/10/10 split ordered DESC by timestamp.
- [ ] **AC-13** Observability (P2-3): MediatR `Publish` failure inside `SaveChangesAsync` writes an ERROR log line carrying `{EventType, EventId}`. Verified by injecting a throwing handler.

## 5. Test strategy

- **Backend unit**: handler tests for the remove + record-session commands (raise expected event).
- **Backend integration** (Testcontainers Postgres):
  - `LibraryActivityEndpointTests`: 4 scenarios end-to-end (added, state-changed, removed, session-recorded).
  - Retention: 91-day-old event filtered out.
  - Migration UP+DOWN test.
- **No frontend changes expected** beyond the verification that the `removed → null` branch doesn't exist.

## 6. Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Nested `SaveChangesAsync` deadlock under high contention | Low | High | Same transaction context; document the pattern + add a contention integration test if production traffic warrants |
| Event payload schema drift (rename of an event type → orphaned log rows) | Medium | Medium | Store `EventType` as a stable short alias (mapper table at write time), not raw `.GetType().FullName`. Tracked as P2 follow-up |
| Log table grows unbounded without enforcement | High over months | Medium | 90-day retention via query filter is sufficient short-term; cleanup job is a separate ticket (NG4) |
| `LibraryEntryRemovedEvent` not raised because the existing handler hard-deletes without calling `AddDomainEvent` | Medium | Low | Add a unit test that asserts the event is raised before the entity is removed from `DbSet` |
| Cross-BC event types pollute the activity log | Low | Low (filter is by user + type) | Query filter on `EventType IN (...)` short-list keeps it scoped |

## 7. Sequencing

Single PR (~8h estimate post-hardening):

1. Add `DomainEventLog` entity + EF Core configuration + migration.
2. Add `LibraryEntryRemovedEvent` (and `GameSessionRecordedEvent` if missing).
3. Wire `MeepleAiDbContext.SaveChangesAsync` to persist log rows.
4. Update remove-from-library handler to raise the event.
5. Refactor `GetLibraryActivityQueryHandler` to UNION the log rows.
6. Add 4 new integration tests + retention test + migration UP+DOWN test.
7. Doc commit for retention policy.

## 8. Open decisions — RESOLVED post-panel

- [x] **D1**: ✅ MANDATORY stable alias via `EventTypeRegistry.Resolve` (P0-2). Throws if not registered; unit test enforces registry completeness (AC-10).
- [x] **D2**: ✅ Centralized static map in `Infrastructure/DomainEventLog/EventTypeRegistry.cs`. Attribute-based marker (`[ExcludeFromDomainEventLog]`) used only for opt-out, not opt-in.
- [x] **D3**: ✅ i18n at query time. Payload carries `GameId + GameTitle` (denormalized — see P2-1 trade-off below). `Message` is composed by the query handler from the registered alias + payload + caller locale. Stale-title risk accepted (P2-1 documented).

## 10. Hardening change-log

### 2026-05-19 — Panel P0+P1+P2 applied (score 5.8 → ~8.5 estimate)

- §1 frontmatter: Hardening row + status → "hardened".
- §3.2 atomicity REDESIGNED (P0-1): single `base.SaveChangesAsync` call commits aggregate + log rows together. Nested-save flaw eliminated.
- §3.2a NEW: `PeekEvents()` + explicit `Clear()` on the collector (P1-2 fix — non-destructive snapshot).
- §3.1 entity: schema unchanged; index list expanded to 3 indexes (P1-1 single-column `LoggedAt` for cleanup + P1-3 UNIQUE on `EventId`).
- §3.1a NEW: `EventTypeRegistry` mandatory alias resolver with throw-on-missing (P0-2).
- §3.5 query REDESIGNED (P0-3): disjoint kinds → no UNION needed; cursor-based pagination explicit; merge-then-take preserves order correctness.
- §3.2 + §3.2a: structured ERROR log on MediatR dispatch failure (P2-3).
- §4 ACs: 9 → 14. AC-2b (collector idempotency on save failure), AC-5/5b (full integration path, not handler-only), AC-10 (registry completeness test), AC-11 (idempotency unique constraint), AC-12 (pagination correctness), AC-13 (observability).
- §7 effort revised: 6h → 8h post-hardening.
- §8 decisions: D1+D2+D3 all resolved with concrete evidence.

## 9. References

- Issue #661
- MVP shipped: PR #645 / Issue #642
- Existing infra: `IDomainEvent`, `AggregateRoot.AddDomainEvent`, `IDomainEventCollector`, `MeepleAiDbContext.SaveChangesAsync` (lines 408-426)
- Existing publisher calls: `AddGameToLibraryCommandHandler.cs:88`, `RecordGameSessionCommandHandler.cs:67`, `UpdateGameStateCommandHandler.cs:75`
- FE schema (no change needed): `apps/web/src/lib/api/schemas/library-activity.schemas.ts`
