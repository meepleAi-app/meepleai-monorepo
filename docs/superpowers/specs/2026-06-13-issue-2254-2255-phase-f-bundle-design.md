# F5+F6 Phase F bundle — Design (#2254 + #2255)

**Status**: APPROVED 2026-06-13 (brainstorming session)
**Branch**: `feature/issue-2254-2255-phase-f-bundle`
**Issues**: [#2254](https://github.com/meepleAi-app/meepleai-monorepo/issues/2254) F5 bulk acknowledge UI · [#2255](https://github.com/meepleAi-app/meepleai-monorepo/issues/2255) F6 attempt-source attribution
**Parent epic**: #1823 (Wikidata cover enrichment) — Phase E shipped 2026-06-12, M17 staging dry-run ✅
**Estimate**: ~19h (2.5gg). Bundled delivery in 1 PR (decision DEC-F-1).
**Pattern reference**: F2 bulk-retry (PR #2222), F3 timeline drawer (PR #2222), F4 SSE broadcaster (PR #2227)

---

## 1. Context

Phase E closed epic #1823 at 100% of original acceptance criteria (M17 staging dry-run 8/8 gates ✅). 3 follow-up issues were filed 2026-06-12 to close residual operator UX gaps:

- **#2254 F5 bulk acknowledge UI** — operator marks a terminal dead-letter as "not actionable" (e.g. `image-bytes-not-available` for a Commons-deleted file, or `license-not-whitelisted` for a known-non-CC game) without burning an M9 scheduler tick or waiting 7 days for the DEC-3j retention sweep.
- **#2255 F6 attempt-source attribution** — visual distinguishing between scheduler-triggered and admin-triggered attempts in the F3 timeline drawer.
- **#2256 multi-pod Redis fan-out** — out of scope for this bundle (deferred until DEC-3e single-pod assumption is revisited).

This bundle ships F5+F6 in **one PR**. Both touch the same `wikidata_cover_enrichment_attempts` table, so combining migrations avoids 2 sequential ALTER TABLE round-trips. Same admin surface, same code-review pass.

## 2. Decisions

| # | Decision | Rationale |
|---|---|---|
| **DEC-F-1** | **1 PR mega bundle** (BE F5+F6 + 1 migration + FE F5 toolbar/modal + FE F6 badge) | Stessa tabella + stesso review surface + contesto Phase E fresco (mergiato ieri). Trade-off: PR più grande ~20 file, ma review pass unico è preferibile a 2 cicli sequenziali. |
| **DEC-F-2** | **1 migration combinata 3 colonne + 1 partial index** (`AddAcknowledgeAndTriggerSourceToWikidataAttempts`) | Evita 2 ALTER TABLE consecutivi su tabella attiva. Index parziale `WHERE acknowledged_at IS NOT NULL` ottimizza filter "exclude acked" del default list view senza inflare l'index su null. |
| **DEC-F-3** | **F5 aggregate pattern: mutator `Acknowledge(userId, ackedAt)` su aggregate esistente** (eccezione record-of-fact) | Approach A vs B (sibling aggregate) vs C (SQL bypass). A è semplice, 1 aggregate, idempotency in-domain. Eccezione documentata via XML doc: ack è metadata operativa orthogonal (parallelo a `DeadLetteredAt`), non un evento pipeline. |
| **DEC-F-4** | **Note F5 NOT persisted** — log-only nel structured log line (mirror F2 `TriggeredByUserId` pattern). Modal mostra textarea per "intent" operatore | Riduce scope (no colonna `acknowledge_note`), preserva pattern. F5 issue dice "Audit log of who acknowledged what — separate issue if needed". Decisione reversibile in follow-up. |
| **DEC-F-5** | **F6 user resolver: server-side LEFT JOIN, embed displayName nei DTO** | Approach (a) vs (b) FE lazy lookup vs (c) badge senza tooltip. (a) ha 1 round-trip, FE banale, costo +1 JOIN su query a basso traffico (M13 admin page). |
| **DEC-F-6** | **F5 toggle "Show acknowledged" default OFF; row acked dimmed (`opacity-60`) + chip "Acked by {fullName} on {date}"** | Default ON nasconde noise; toggle scoperta opzionale per ops auditing. Visual marker minimal: opacity + chip semantic `bg-muted` (no destructive). |
| **DEC-F-7** | **F5 endpoint shape mirror F2**: result envelope `{ ackedCount, idempotentNoOpCount, notFoundCount, rows[] }` | Coerenza con bulk-retry — operator UX uniforme, FE pattern noto. |
| **DEC-F-8** | **F6 runner signature change**: `EnrichAndRecordAsync(gameId, forceRefresh, triggeredByAdminUserId, ct)` con parametro nullable | Wave 3 M12 keep TriggeredByUserId log-only; F6 lo persiste. M9 scheduler passa `null`; M12 + F2 passano admin id. Backward-compatible per IT che usano default `null`. |

## 3. Architecture

### 3.1 Bounded context

Tutto in `Api.BoundedContexts.SharedGameCatalog`. Tocca 4 strati:
- **Domain**: `WikidataCoverEnrichmentAttempt` aggregate (mutator + factory params)
- **Application**: 1 new command + 1 query update + 1 query DTO update + runner interface change
- **Infrastructure**: 1 entity update + 1 entity config update + 1 migration + 1 repository update
- **Routing**: 1 new endpoint + 1 endpoint query param

### 3.2 Diagrams

```
[Admin UI] --POST /bulk-acknowledge--> [Endpoint] --IMediator--> [Handler]
                                                                    |
                                                            [Repo.GetByIdsAsync]
                                                                    |
                                                              for each row:
                                                            [aggregate.Acknowledge()]
                                                            [Repo.UpdateAsync]
                                                                    |
                                                            return envelope

[M9 / M12 / F2] --runner.EnrichAndRecordAsync(triggeredBy)--> persist attempt
                                                              with TriggeredByAdminUserId
```

## 4. Data model

### 4.1 Migration (`20260613XXXXXX_AddAcknowledgeAndTriggerSourceToWikidataAttempts.cs`)

```sql
ALTER TABLE wikidata_cover_enrichment_attempts
  ADD COLUMN acknowledged_at              timestamp with time zone NULL,
  ADD COLUMN acknowledged_by              uuid                     NULL,
  ADD COLUMN triggered_by_admin_user_id   uuid                     NULL;

-- F5 partial index: speeds up default list view (exclude acked = WHERE acknowledged_at IS NULL)
CREATE INDEX ix_wikidata_cover_attempts_acknowledged_at
  ON wikidata_cover_enrichment_attempts (acknowledged_at)
  WHERE acknowledged_at IS NOT NULL;
```

**No FK** on `users` for `acknowledged_by` / `triggered_by_admin_user_id` — mirror `created_by` pattern in `audit_logs` (preserves hard-delete tolerance). User lookup happens at query-time via JOIN.

### 4.2 Aggregate `WikidataCoverEnrichmentAttempt`

```csharp
// F5 mutator state (private setters preserved)
public DateTime? AcknowledgedAt { get; private set; }
public Guid?     AcknowledgedBy { get; private set; }

// F6 attribution state
public Guid?     TriggeredByAdminUserId { get; private set; }

/// <summary>
/// F5 — operator marks dead-letter as "not actionable". Idempotent.
/// EXCEPTION to record-of-fact pattern: ack is operational metadata,
/// not a pipeline event (parallel to DeadLetteredAt). Preserves original
/// ack on re-call (no overwrite).
/// </summary>
public void Acknowledge(Guid userId, DateTime ackedAt)
{
    if (Outcome != WikidataCoverEnrichmentOutcome.DeadLetter)
        throw new InvalidOperationException(
            $"Only DeadLetter attempts can be acknowledged; current Outcome={Outcome}.");
    if (userId == Guid.Empty)
        throw new ArgumentException("UserId cannot be Guid.Empty.", nameof(userId));
    if (AcknowledgedAt is not null) return; // idempotent
    AcknowledgedAt = ackedAt;
    AcknowledgedBy = userId;
}

// F6 — factory methods accept optional triggeredByAdminUserId
public static WikidataCoverEnrichmentAttempt RecordSuccess(
    Guid sharedGameId, int retryCount, DateTime attemptedAt,
    Guid? triggeredByAdminUserId = null) => ...;

// (same for RecordSkipped / RecordFailedWithRetry / RecordDeadLetter)

// Reconstitute hydrates AcknowledgedAt/AcknowledgedBy/TriggeredByAdminUserId
public static WikidataCoverEnrichmentAttempt Reconstitute(
    Guid id, Guid sharedGameId, DateTime attemptedAt,
    WikidataCoverEnrichmentOutcome outcome, string reason, string? details,
    int retryCount, DateTime? nextRetryAt, DateTime? deadLetteredAt,
    DateTime? acknowledgedAt, Guid? acknowledgedBy,         // F5
    Guid? triggeredByAdminUserId) => ...;                    // F6
```

### 4.3 EF Core entity + config

- `WikidataCoverEnrichmentAttemptEntity`: 3 nuove properties (`AcknowledgedAt`, `AcknowledgedBy`, `TriggeredByAdminUserId`) nullable.
- `WikidataCoverEnrichmentAttemptEntityConfiguration`: column mappings + partial index via `HasIndex(e => e.AcknowledgedAt).HasFilter("acknowledged_at IS NOT NULL").HasDatabaseName("ix_wikidata_cover_attempts_acknowledged_at")`.
- `WikidataCoverEnrichmentAttemptRepository`: `Reconstitute` mapping update (3 new args).

## 5. CQRS

### 5.1 F5 Command — `AdminBulkAcknowledgeWikidataCoverCommand`

```csharp
internal sealed record AdminBulkAcknowledgeWikidataCoverCommand(
    IReadOnlyList<Guid> AttemptIds,
    string? Note,                         // optional, max 500 chars, log-only
    Guid TriggeredByUserId)               // admin user id (log-only, mirror F2)
    : ICommand<AdminBulkAcknowledgeResult>;

public sealed record AdminBulkAcknowledgeResult(
    int AckedCount,                       // newly acknowledged this batch
    int IdempotentNoOpCount,              // already acknowledged, no-op
    int NotFoundCount,                    // attempt id not found / swept
    IReadOnlyList<AdminBulkAcknowledgeRow> Rows);

public sealed record AdminBulkAcknowledgeRow(
    Guid AttemptId,
    Guid? GameId,
    string Outcome,                       // "acked" | "already-acked" | "not-found" | "wrong-state"
    string? Reason);
```

**Handler** (mirror `AdminBulkRetryWikidataCoverCommandHandler` pattern):
- Repo lookup `GetByIdsAsync(attemptIds, ct)` returns dictionary
- Per-row try/catch:
  - Missing → row outcome `not-found`, increment `notFoundCount`
  - Found + not DeadLetter → row outcome `wrong-state`, log warning
  - Found + already acked → row outcome `already-acked`, increment `idempotentNoOpCount`
  - Found + DeadLetter + not-yet-acked → `aggregate.Acknowledge(userId, now)` + `repo.UpdateAsync`, row outcome `acked`, increment `ackedCount`
- `OperationCanceledException` rethrown (mirror F2)
- Note string logged via structured `_logger.LogInformation("AdminBulkAcknowledge: user={UserId} count={Count} note={Note}", ...)` — NOT persisted (DEC-F-4)

**Validator** `AdminBulkAcknowledgeWikidataCoverCommandValidator`:
- `AttemptIds`: NotEmpty, max 50 (DEC-3e cap), no Guid.Empty
- `Note`: optional, max 500 chars (when non-null)
- `TriggeredByUserId`: NotEmpty Guid

### 5.2 F5 Query update — `GetWikidataDeadLetterAttemptsQuery`

```csharp
internal sealed record GetWikidataDeadLetterAttemptsQuery(
    int Skip,
    int Take,
    string? ReasonFilter,
    bool IncludeAcknowledged = false)         // F5 NEW (default backward-compat)
    : IRequest<WikidataDeadLetterAttemptsResult>;

public sealed record WikidataDeadLetterAttemptDto(
    Guid Id, Guid SharedGameId, string GameTitle,
    DateTime AttemptedAt, DateTime DeadLetteredAt,
    string Reason, string? Details, int RetryCount,
    // F5 fields
    DateTime? AcknowledgedAt,
    Guid?     AcknowledgedBy,
    string?   AcknowledgedByFullName,         // server-side LEFT JOIN users.full_name
    // F6 fields
    Guid?     TriggeredByAdminUserId,
    string?   TriggeredByAdminFullName);      // server-side LEFT JOIN users.full_name
```

**Repository signature change**:
```csharp
Task<WikidataDeadLetterPage> GetDeadLettersAsync(
    int skip, int take, string? reasonFilter,
    bool includeAcknowledged,                 // F5 NEW
    CancellationToken ct);
```
- EF Core query: filter `Outcome == DeadLetter && (includeAcknowledged || AcknowledgedAt == null)`
- LEFT JOIN on `users` x 2 (acknowledged_by + triggered_by_admin_user_id) → select FullName projection

### 5.3 F6 Query update — `GetWikidataAttemptTimelineQuery`

```csharp
public sealed record WikidataAttemptTimelineNode(
    Guid Id, DateTime AttemptedAt, string Outcome,
    string? Reason, string? Details, int RetryCount,
    DateTime? NextRetryAt, DateTime? DeadLetteredAt,
    // F6 fields
    Guid?   TriggeredByAdminUserId,
    string? TriggeredByAdminFullName);        // server-side LEFT JOIN
```

**Repository signature change**:
```csharp
Task<IReadOnlyList<WikidataAttemptTimelineRow>> GetAttemptsByGameIdAsync(
    Guid gameId, int limit, CancellationToken ct);
// Row DTO now includes TriggeredByAdminFullName
```

### 5.4 F6 Runner interface change — `IWikidataCoverEnrichmentRunner`

```csharp
public interface IWikidataCoverEnrichmentRunner
{
    Task EnrichAndRecordAsync(
        Guid sharedGameId,
        bool forceRefresh,
        Guid? triggeredByAdminUserId,         // F6 NEW (null = M9 scheduler)
        CancellationToken ct);
}
```

Callers:
- `WikidataCoverEnrichmentJob` (M9 scheduler) → passes `null`
- `AdminEnrichWikidataCoverCommandHandler` (M12) → passes `command.TriggeredByUserId`
- `AdminBulkRetryWikidataCoverCommandHandler` (F2) → passes `request.TriggeredByUserId`

Runner internal: when creating attempt via aggregate factory, passes `triggeredByAdminUserId` parameter.

### 5.5 F4 SSE event payload update — `WikidataEnrichmentEvent`

```csharp
public sealed record WikidataEnrichmentEvent(
    Guid AttemptId, Guid SharedGameId, DateTime AttemptedAt,
    string Outcome, string Reason, int RetryCount,
    DateTime? NextRetryAt, DateTime? DeadLetteredAt,
    // F6 fields
    Guid?   TriggeredByAdminUserId,
    string? TriggeredByAdminFullName);
```

Broadcaster publishes from runner POST-SaveChanges (mirror F1 dead-letter gauge placement). FullName JOIN done in broadcast publish path (single user lookup, cached server-side per pod).

## 6. Endpoints

### 6.1 New — POST `/api/v1/admin/wikidata/enrichment/bulk-acknowledge`

- **Auth**: `RequireAdminSessionFilter` (group-level)
- **Body**: `{ attemptIds: Guid[], note?: string }`
- **Response 200**: `{ ackedCount, idempotentNoOpCount, notFoundCount, rows[] }`
- **Response 400**: validation error (empty / >50 / note > 500 chars)
- **Response 401**: not admin
- **Cancellation**: propagates via `HttpContext.RequestAborted`

### 6.2 Update — GET `/api/v1/admin/wikidata/enrichment/dead-letters`

Adds optional query param `?includeAcknowledged=true` (default false). Otherwise unchanged response shape (DTO enriched with new fields — backward-compatible additive).

## 7. Frontend

### 7.1 F5 admin page (`apps/web/src/app/admin/wikidata-enrichment/...`)

**New components**:
- `AcknowledgeSelectedButton` — sticky toolbar 3rd button (after "Retry selected" + clear-selection)
- `AcknowledgeSelectedModal` — confirmation modal with:
  - Summary "Acknowledge {count} dead-letter row(s)?"
  - Optional `<Textarea>` "Note (optional, for log only)" maxLen 500
  - Cancel + Confirm buttons; submit calls `useBulkAcknowledgeMutation`
- `ShowAcknowledgedToggle` — `<Switch>` "Show acknowledged" (default off) → toggles `includeAcknowledged` query param

**Visual marker for acked rows** (default hidden, visible only when toggle on):
- Row wrapper: `opacity-60` + Tailwind utility
- Inline chip "Acked by {fullName} on {date}" via `<Badge variant="secondary">` with `bg-muted text-muted-foreground` semantic tokens (DS-15 compliance — no hardcoded slate/gray)

**New hook**: `useBulkAcknowledgeMutation` (mirror `useBulkRetryMutation` pattern via `@tanstack/react-query`):
- `mutationFn: (input: { attemptIds: string[]; note?: string }) => api.adminWikidata.bulkAcknowledge(input)`
- `onSuccess`: invalidate `['admin', 'wikidata', 'dead-letters']` query
- Error envelope `{ kind: 'validation' | 'server', message }` mirror existing pattern

### 7.2 F6 timeline drawer (`AttemptTimelineDrawer.tsx`)

**Inline badge** when `triggeredByAdminUserId !== null`:
```tsx
<Badge variant="outline" className="ml-2 text-xs">
  admin
</Badge>
```
With border + bg semantic tokens (`border-primary/40 bg-primary/10` or similar, NOT hardcoded blue-500).

**Hover tooltip** via existing `<Tooltip>` primitive: `Triggered by admin {triggeredByAdminFullName}` (or `"Triggered by admin (deleted user)"` fallback when FullName is null but Id non-null).

## 8. Tests

### 8.1 Backend

| Suite | F5 | F6 |
|---|---|---|
| Aggregate unit | 4: Acknowledge happy / idempotent no-op / ArgumentException Guid.Empty / InvalidOp not DeadLetter | 1: Reconstitute roundtrip TriggeredByAdminUserId |
| Command handler unit | 5: single ack / batch ack / idempotency (mixed new+old) / not-found row / cancellation propagation | — |
| Query handler unit | 3: default excludes acked / includeAcknowledged=true includes them / count corrretto post-filter | 1: timeline DTO populated with admin name via JOIN mock |
| Runner unit | — | 2: scheduler path persists null / admin path persists admin id |
| Validator unit | 4: empty list / >50 / note >500 chars / TriggeredByUserId empty | — |
| Endpoint IT (Testcontainers) | 1: POST /bulk-acknowledge end-to-end → assert AcknowledgedAt/By persisted + partial index hit via EXPLAIN sanity | 1: M12 endpoint persists admin id; M9 cron path leaves null |

### 8.2 Frontend (Vitest)

| Suite | F5 | F6 |
|---|---|---|
| Hook | 3: `useBulkAcknowledgeMutation` happy / server-error envelope / invalidates dead-letter query | 2: badge shown when admin id non-null / hidden when null |
| Component smoke | 1: `AcknowledgeSelectedModal` render + submit with note + cancel | — |

### 8.3 E2E

Skeleton (not blocking, `continue-on-error: true`):
- `apps/web/e2e/admin-wikidata-bulk-acknowledge-flow.spec.ts` — login admin → select 2 dead-letters → ack with note → assert default list excludes them → toggle Show acked → assert visible
- (no separate F6 spec — coverage in admin-wikidata smoke tests already covers timeline drawer)

## 9. Effort breakdown

| Area | Effort |
|---|---|
| BE F5 (aggregate + command + handler + validator + query update + endpoint) | ~5h |
| BE F6 (aggregate factory params + runner interface + DTO updates) | ~3h |
| Migration | ~0.5h |
| FE F5 (button + modal + toggle + hook + marker) | ~4h |
| FE F6 (badge + tooltip) | ~1h |
| Tests (BE unit + IT + FE Vitest + E2E skeleton) | ~4h |
| Spec + plan + docs + PR description | ~1.5h |
| **Total** | **~19h (~2.5gg)** |

Aligned with issue estimates (10-14h + 6-8h = 16-22h).

## 10. Out of scope

- Per-row inline acknowledge button (F2 per-row retry sufficient today)
- Note persistence (DEC-F-4 — log-only, follow-up if audit needed)
- Admin "all triggers" audit log surface (F6 out-of-scope per issue)
- Multi-pod Redis fan-out backplane for SSE (#2256 — deferred until multi-pod)
- Filtering dead-letter list by source/admin (operator can already filter by Reason)

## 11. Risks & mitigations

| Risk | Mitigation |
|---|---|
| Aggregate mutator deroga al pattern record-of-fact può confondere futuri devs | XML doc esplicito sul metodo `Acknowledge()` + ADR-style comment in spec |
| 1 PR mega aumenta review surface | Subagent code-reviewer pre-merge (~0 finding target); commit splitting per BE/FE/migration leggibile |
| Partial index su acknowledged_at potrebbe non essere usato da query planner | EXPLAIN test nell'IT; fallback è full-table scan (acceptable per low-traffic admin page) |
| F6 LEFT JOIN su users in hot path SSE può rallentare publish | User lookup è cached server-side (DEC-F-5); single id resolve per attempt — overhead trascurabile |
| Backward compat: M9 cron sub-pod hydration legge rows pre-migration con TriggeredByAdminUserId=null | Nullable column, default null — pre-existing rows render senza badge (issue requirement explicit) |

## 12. Open follow-ups (post-merge)

- Issue follow-up se operatori chiedono persisted note → riapri DEC-F-4
- Issue follow-up se serve audit log "all admin triggers" (filter by `WHERE triggered_by_admin_user_id IS NOT NULL`)
- Multi-pod backplane #2256 quando si rompe DEC-3e single-pod assumption
