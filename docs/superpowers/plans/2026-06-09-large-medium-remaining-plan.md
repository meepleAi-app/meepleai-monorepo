# Plan 2026-06-09 — Large/Medium remaining issues implementation

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sequence + implement i 5 issue rimasti aperti post sess.46c cluster #1535 closure. Decompose ognuno in milestones eseguibili con effort, dependencies, risk profile esplicito.

**Source materials:**
- Sess.46c memory: 7 issue closed via P74+P228, cluster #1535 100% closed
- Issue bodies: #1907 #1823 #1972 #1965 #1964 (read full 2026-06-09)
- Pre-req discovery: #1859 CLOSED (unblocks #1964 Path A)

---

## Inventario issue + scope sintesi

| # | Title | Scope source | Effort body | Risk |
|---|---|---|---|---|
| **#1907** | EnrichmentAttempt persistence in BggImportQueueBackgroundService | `#1874 spec §4 item 3` | ~5-6h BE | M (424-line legacy) |
| **#1964** | RotateProviderKey × AtomicAudit E2E coverage | Path A (un-skip) o Path B (dedicated test) | ~2-4h | L (test-only) |
| **#1972** | vitest v4 migration | 15 file + 4 pattern issues | ~3gg | M (40 mock refactor) |
| **#1823** | L2 Wikidata cover enrichment CRON | LARGE multi-step: migration + SPARQL + R2 + license | ~5-7gg | H (external API + legal) |
| **#1965** | Outbox FOR UPDATE SKIP LOCKED | Multi-instance work-stealing | ~2gg | H (concurrency) — **contingent on >5% duplicate-dispatch metric** |

**Combined effort: ~10-15gg cumulative**.

---

## Dependency graph

```
#1859 (✅ CLOSED) ──────► #1964 Path A (un-skip 2 [Skip] tests)
                          │
                          ▼
                          (alternativa Path B: dedicated test, no dependency)

(indipendente) ──────► #1965 (contingent on observed metric breach)

#1874 (✅ shipped) ──────► #1907 (BackgroundService follow-up)

#1821 L1 placeholder (✅ shipped via L3 #1824) ──────► #1823 (L2 enrichment)

Dependabot PR #1794 (BLOCKED, can re-open) ──────► #1972 (vitest v4 refactor)
```

**Key insight**: solo #1965 ha block contingent (metric-based trigger). Le altre 4 sono dependency-clean post sess.46c closures.

---

## Prioritizzazione (impact × effort × risk)

| Issue | Effort | Business Value | Risk | **Priority** |
|---|---|---|---|---|
| **#1907** | 5-6h | High (data integrity tracking BGG enrichment) | M | **P1** — High ROI, scope contenuto |
| **#1964** | 2-4h | M (regression confidence #1535 cluster) | L | **P2** — Quick win, #1859 unblock |
| **#1972** | ~3gg | M (dependency hygiene, security CVE access) | M | **P3** — Important ma non urgente |
| **#1823** | ~5-7gg | High (catalog quality + UX visual) | H | **P4** — Spike + ADR upfront required |
| **#1965** | ~2gg | L (non-goal MVP) | H | **P5** — Backlog finché metric breach |

**Sequencing recommendation: Phase 1 P1+P2 → Phase 2 P3 → Phase 3 P4 → Backlog P5**.

---

## Decomposition per issue

### #1907 — EnrichmentAttempt in BggImportQueueBackgroundService (P1, ~5-6h)

**Goal**: 3° hook integration point del #1874 spec §4 — record `EnrichmentAttempt` + `MarkProcessed` per ogni iteration del 424-line `BackgroundService`.

**Branch**: `feature/issue-1907-enrichment-attempt-persistence` from main-dev.

**Architecture**:
- Inject scoped `IEnrichmentAttemptRepository` + `IEnrichmentQueueRepository` via `IServiceScopeFactory.CreateScope()` (consistent col pattern già usato per `MeepleAiDbContext`).
- Add helper `ErrorCodeClassifier.Classify(Exception)` → structured codes (`BGG_API_RATE_LIMIT_429`, `SCHEMA_MISMATCH`, etc.).
- New repo method `IEnrichmentQueueRepository.GetPendingForGameAsync(Guid sharedGameId)` (small addition, follows `GetPendingAsync` pattern).

**Milestones**:
- [ ] M1: New repo method `GetPendingForGameAsync` + unit test (~30min)
- [ ] M2: `ErrorCodeClassifier` helper + 6 unit test cases (rate-limit/timeout/schema/server-error/unknown/default) (~45min)
- [ ] M3: Inject scoped repositories in `BggImportQueueBackgroundService` constructor + iteration scope (~30min)
- [ ] M4: Success path — `EnrichmentAttempt.RecordSuccess` + add ~30min
- [ ] M5: Failure path — `EnrichmentAttempt.RecordFailure` + classification ~30min
- [ ] M6: Terminal outcome → `MarkProcessed` cascade su `EnrichmentQueueEntry` (~30min)
- [ ] M7: Integration test Acceptance Scenario H (queue→service→attempt success row + IsProcessed=true) (~1h)
- [ ] M8: Integration test Acceptance Scenario I (4 failed attempts → 4 rows visible via `GET /failed-items`) (~1h)
- [ ] M9: Regression smoke — verify existing rate-limit + retry tests still pass (~30min)

**Risk mitigation**:
- 424-line legacy `BackgroundService` → integration test BEFORE editing
- Stale-recovery + cleanup loops → don't touch, only add hooks at success/failure terminal points
- Scoped DI pattern → consistent con existing code (no scope creep)

**AC tracking** (from issue body):
- [ ] Scenario H pass
- [ ] Scenario I pass
- [ ] BackgroundService retry/rate-limit behavior unchanged

---

### #1964 — RotateProviderKey × AtomicAudit E2E (P2, ~2-4h)

**Goal**: Un-skip 2 `[Skip]` test scenari ora che #1859 è closed.

**Branch**: `feature/issue-1964-rotate-provider-key-e2e-coverage` from main-dev.

**Pre-req verified**: #1859 CLOSED (plan `2026-06-05-issue-1859-rotate-provider-key.md` shipped) → Path A unblocked.

**Path A milestones (recommended, simpler)**:
- [ ] M1: Remove `[Skip]` attribute from `RotateProviderKeyEndpointIntegrationTests.cs:195` (Scenario 1 happy path) (~5min)
- [ ] M2: Remove `[Skip]` attribute from `RotateProviderKeyEndpointIntegrationTests.cs:398` (Scenario 8 audit outbox) (~5min)
- [ ] M3: Run tests locally, fix any setup gaps (factory, fixture, mocking) (~1-2h depending on factory readiness)
- [ ] M4: Scenario 8 assertions per issue body AC:
  - Outbox row exists for `ProviderKeyRotatedEvent` after happy-path rotation
  - Outbox row does NOT exist when audit enqueue fails (sabotage simulation)
  - `IMediator.Publish` never invoked during rolled-back command
- [ ] M5: CI verde su `Backend Integration` workflow

**Path B milestones (fallback if Path A blocked)**:
- [ ] B1: New test `Scenario6_RotateProviderKey_AtomicAudit_RollbackSafety` in `Issue1535EventOutboxAcceptanceTests.cs` (~2h)
- [ ] B2: Construct `RotateProviderKeyCommand` through real pipeline (Auth + RequireTwoFactor + AtomicAuditAttribute)
- [ ] B3: Sabotage audit enqueue post-SaveChanges + assert (no outbox row + no MediatR.Publish + no Redis cache broadcast) (~1h)

**Risk mitigation**:
- Test-only change, low blast radius
- If Path A factory setup is complex, fall through to Path B (dedicated, factory-independent)

**AC tracking**:
- [ ] Both `[Skip]` removed OR Path B `Scenario6_*` shipped
- [ ] Scenario 8 explicit assertions verified

---

### #1972 — vitest v4 migration (P3, ~3gg)

**Goal**: Bump vitest 2.x → 4.1.0 (+ `@vitest/coverage-v8` + `@vitest/ui`) + refactor 15 test file con 4 distinct breaking patterns.

**Branch**: `feature/issue-1972-vitest-v4-migration` from main-dev.

**Dependabot status**: PR #1794 BLOCKED (closed for refactor). Reopen + amend or new clean PR.

**🔒 DEC-2 LOCKED 2026-06-09 (sess.46f spec-panel + user-locked)**:

- **DEC-2a Codemod scope — Option B mid-ground**: Pattern 1 (`() => ({})` constructor mock, ~40 occurrences) codemod-able via jscodeshift transformer; Pattern 2 (Blob jsdom strictness), Pattern 3 (URL global spy), Pattern 4 (simulateOpen/simulateError lifecycle) require manual judgment case-by-case.
- **DEC-2b CI gate SMART criteria**:
  - Test count: **575 SAME** target (or `560 + 15 explicit skip` if test removal justified)
  - Coverage % delta: **< 0.5pp drop** accettabile
  - Runtime regression: **< +20% slower** vs main-dev baseline measurement
  - Partial merge policy: **NO** — all shards (1/2/3 + Fast) must be green; flake retry max 1×
- **DEC-2c Rollback strategy**:
  - Pin: `vitest@4.1.0` **exact** (no minor/patch auto-upgrade)
  - Rollback path: **single-commit revert PR** + Dependabot re-open on regression
  - Coexistence: **hard cutover** (no v2+v4 side-by-side period)
  - Monitoring: **7-day post-merge CI runtime alert** via existing Prometheus dashboard
- **DEC-2d Exhaustive 15-file list**: M1 deliverable shipped as **separate audit PR ~1h** (grep `vi.fn\(\).mockImplementation\(\(\) => \(` on `apps/web/src` test files) → JSON list + pattern category per file + priority canary-first (`usePdfStatus.test.ts` first).

**Breaking changes** (per issue body):
1. `() => ({}) is not a constructor` (~40 occurrences in mock pattern)
2. `Cannot read properties of undefined (reading 'simulateOpen'/'simulateError')` (downstream effect of #1)
3. `obj must be an instance of Blob. Received an instance of Object` (jsdom strictness)
4. `[Function revokeObjectURL] is not a spy or a call to a spy!` (URL global spy setup)

**Milestones**:
- [ ] M1: Audit completo pattern di mock rotti (~1h)
  - Grep `vi.fn\(\).mockImplementation\(\(\) => \(`
  - Identifica i 15 file (issue body lista 3, altri da run CI failing PR #1794)
- [ ] M2: PoC refactor su `usePdfStatus.test.ts` (canary file, EventSource mock) (~2h)
  - Replace arrow function con `class MockEventSource` proper constructor
  - Verifica pattern downstream (`eventSource.simulateOpen()` reattivato)
- [ ] M3: Refactor `PhotoUploadModal.test.tsx` Blob handling (~1h)
  - Replace plain Object con real `new Blob([], {type: ...})` instances
  - OR mock con explicit `instanceof Blob` check
- [ ] M4: Refactor URL global spy setup (~1h)
  - Add `Object.defineProperty(URL, 'revokeObjectURL', { writable: true, value: vi.fn() })` in `setup.ts`
- [ ] M5: Batch refactor remaining 12 file pattern occurrences (~1-1.5gg)
  - Codemod where possible (jscodeshift for constructor mock pattern)
  - Manual review for edge cases
- [ ] M6: Bump vitest 2 → 4.1.0 in `apps/web/package.json` + companion packages (~30min)
- [ ] M7: Remove `coverage.all: true` from `vitest.config.ts` + verify v4 equivalent (~30min)
- [ ] M8: CI verde su Frontend Tests shard 1/2/3 + Fast (~4h iteration if flakes)

**Risk mitigation**:
- 15 file scope → break into M2/M3/M4 PoC + M5 batch
- Mock pattern is codemod-able → automate where possible
- Canary file (usePdfStatus.test.ts) prima dei resto

**AC tracking** (from issue body):
- [ ] Inventario file rotti complete (15+ documented)
- [ ] PoC su canary verde
- [ ] PR clean separata bump vitest 4 + refactor mock pattern
- [ ] CI verde su Frontend Tests shard 1/2/3 + Fast

---

### #1823 — L2 Wikidata cover enrichment CRON (P4, ~3-4gg post-discovery)

**Goal**: BackgroundService nightly + ad-hoc trigger per arricchire `shared_game_catalog` con cover images legal-clean (Wikidata + Wikimedia Commons).

**Branch**: `feature/issue-1823-wikidata-l2-enrichment` from main-dev.

**🔍 P228 partial discovery 2026-06-09 (sess.46f)** — scope effectively halved:

**✅ ALREADY SHIPPED via cluster #1903/#1821**:
- DB schema 4 columns (`wikidata_cover_r2_key`, `wikidata_cover_source_url`, `wikidata_cover_license`, `wikidata_cover_attribution`) in `SharedGameEntity.cs:53-61, 85` + `SharedGameEntityConfiguration.cs:128-158` + migration `20260608133755_InitialCreate`
- SPARQL HTTP client `WikidataCatalogProvider.cs` (HttpClient, rate-limit, retry) — shipped by #1903 catalog seed work
- CoverUrlResolver L2 layer wired (`CoverUrlResolver.cs:72-79` L4→L3→L2→L1 cascade)
- Query projections (`GetSharedGameByIdQueryHandler:385` + `GetUserLibraryQueryHandler:142`)

**❌ Residual scope ~3-4gg (vs 5-7gg originally, 40% reduction)**.

**🔒 DEC-3 LOCKED 2026-06-09 (sess.46f spec-panel + user-locked)**:

- **DEC-3a SPARQL strategy — Option A extend**: extend existing `WikidataCatalogProvider` con `FetchCoverImageAsync(qid)` method + `wdt:P18` (image property) query helper. Reuse HttpClient + rate-limit + retry already shipped. Current provider usa `P1873`/`P1872` (players); add `P18` SPARQL builder.
- **DEC-3b Commons fetcher — Option A separate client**: New `IWikimediaCommonsClient` with `HttpClientFactory` pattern uniform a Wikidata provider. Separate Bounded Context boundary (catalog seed ≠ image fetch). Testability + future reuse.
- **DEC-3c License whitelist — Option A hardcoded**: Constants in `LicenseValidator.cs` (PD/CC0/CC-BY/CC-BY-SA strict). Industry standard stable, config adds zero flexibility, audit-friendly.
- **DEC-3d Image variant pipeline — Option A ImageSharp**: Six Labors ImageSharp managed C# (no native deps). .NET 9 compatible, production-proven, deployment friction = 0. Reject SkiaSharp (native bindings burden), reject ImageMagick.NET (heavy + license).

**Pre-impl spike required** (~1gg):
- Spike Wikidata SPARQL → boardgame coverage rate (issue dichiara «30-40% expected»)
- Verify license metadata reliability (PD / CC0 / CC-BY / CC-BY-SA must be machine-readable from Commons API)
- R2 bucket policy: public-read on `covers/*` path (devops alignment)

**Architecture**:
- Migration: add columns `CoverR2Key`, `CoverSourceUrl`, `CoverLicense`, `CoverAttribution` to `shared_game_catalog`
- CQRS: `EnrichCatalogCoverCommand` + scheduler (CRON nightly) + ad-hoc trigger endpoint
- Service: `IWikidataEnrichmentService` (SPARQL client, rate-limit 5 RPS Wikidata published limit)
- Service: `ICommonsImageFetcher` (license validation + variant 200×300 webp generation)
- Dead-letter: games failing N retries keep placeholder (L1 fallback)
- FE: `<MeepleCard>` reads `CoverR2Key` + attribution footer linking to source

**Milestones**:
- [ ] M0 (spike, prerequisite): ADR `2026-06-09-wikidata-enrichment-architecture.md` con coverage rate + license strategy + R2 policy (~1gg)
- [ ] M1: Migration columns + EF Core entity update (~4h)
- [ ] M2: SPARQL client `WikidataSparqlClient` + QID resolver (exact match Title+year, fallback fuzzy) (~1gg)
- [ ] M3: Commons image fetcher + license validation helper (~1gg)
- [ ] M4: R2 upload + webp variant generation (200×300) (~4h)
- [ ] M5: CQRS `EnrichCatalogCoverCommand` + Handler (~4h)
- [ ] M6: BackgroundService scheduler CRON nightly + dead-letter logic (~4h)
- [ ] M7: Ad-hoc trigger admin endpoint `POST /api/v1/admin/catalog/enrich-cover/{gameId}` (~2h)
- [ ] M8: Integration tests (Testcontainers + mock SPARQL + mock R2) (~1gg)
- [ ] M9: FE attribution footer component on `<MeepleCard>` (~2h)
- [ ] M10: Pre-merge dry-run on staging — show N games enriched, M deferred (~4h)

**Risk mitigation**:
- External API failures → idempotent retry + dead-letter
- License edge cases → strict whitelist (only PD/CC0/CC-BY/CC-BY-SA), reject ambiguous
- Rate-limit Wikidata → 5 RPS hard cap, backoff exponential
- R2 cost → 200×300 webp only (original NOT stored unless explicit need)

**AC tracking** (from issue body):
- [ ] DB migration + 4 columns
- [ ] CQRS + scheduler + dead-letter
- [ ] R2 bucket policy
- [ ] Footer attribution component
- [ ] Idempotent re-run
- [ ] Pre-merge staging dry-run

---

### #1965 — Outbox FOR UPDATE SKIP LOCKED (P5 backlog, ~2gg)

**Goal**: Refactor `DomainEventOutboxProcessor.RunOnceAsync` da unsafe SELECT a `FOR UPDATE SKIP LOCKED` per supporto multi-pod safe.

**Branch**: `feature/issue-1965-outbox-skip-locked` from main-dev (when triggered).

**Trigger condition** (per issue body AC): «only when observed duplicate-publish rate breaches the 5% threshold in staging or production over a sliding 7-day window»:

```promql
(
  sum(rate(meepleai_domain_event_outbox_dispatched_total[7d]))
  /
  sum(rate(meepleai_domain_event_outbox_enqueued_total[7d]))
) > 1.05
```

**Status**: **Backlog finché metric breach observed**. Pattern P124-aware: NON implementare premature ottimizzazione. Consumer idempotency già verified (sess.46c cluster #1535 closure) → safe net in place.

**Milestones (when triggered)**:
- [ ] M1: Verify metric breach in staging/prod over 7-day window (operational check) (~30min)
- [ ] M2: Replace `db.DomainEventOutbox.AsTracking().Where().OrderBy()` con `FromSqlInterpolated($"... FOR UPDATE SKIP LOCKED")` (~2h)
- [ ] M3: Un-skip `Scenario5_ConcurrentDispatch_MultiInstance_BoundedDuplicates` + adapt assertions to exactly-once (~2h)
- [ ] M4: Performance regression test — single-instance throughput ≥ baseline (~4h)
- [ ] M5: Update consumer-contract doc `docs/for-developers/architecture/domain-events-post-commit-contract.md` (~1h)
- [ ] M6: Add Prometheus alert rule for exactly-once regression (~2h)

**Risk mitigation**:
- Concurrency primitive → Testcontainers Postgres mandatory (not in-memory)
- PostgreSQL-specific syntax → wrap in repo method, avoid leaky abstraction
- Defer until business case observed → avoid premature optimization

**AC tracking** (from issue body):
- [ ] Threshold metric breach documented (precondition)
- [ ] FOR UPDATE SKIP LOCKED pattern applied
- [ ] Scenario5_* un-skip + adapted
- [ ] Consumer contract doc updated
- [ ] Alert rule active

---

## Sequencing recommendation — 4 phase

### Phase 1 (Week 1, ~1.5gg cumulative) — Quick wins BE

**Scope**: #1907 + #1964 — bite-size BE work, scope contenuto, dependency clean.

- Day 1: #1907 EnrichmentAttempt (~5-6h) — 1 PR
- Day 1.5: #1964 RotateProviderKey E2E Path A (~2-4h) — 1 PR

**Output**: 2 PR shipped, sblocca BGG enrichment metric tracking + #1535 cluster regression confidence.

### Phase 2 (Week 2, ~3gg) — FE dependency hygiene

**Scope**: #1972 vitest v4 migration.

- Day 1: M1+M2 audit + canary PoC
- Day 2: M3+M4 Blob + URL spy refactor
- Day 3: M5 batch refactor + M6+M7 bump + CI green

**Output**: 1 PR shipped, dependency security CVE access restored, FE test infrastructure modernized.

### Phase 3 (Week 3-4, ~5-7gg) — Catalog L2 enrichment

**Scope**: #1823 Wikidata enrichment. Pre-requirement: ADR spike Week 3 Day 1.

- Week 3 Day 1: M0 spike + ADR landed
- Week 3 Day 2-3: M1+M2+M3 migration + SPARQL + Commons fetcher
- Week 3 Day 4-5: M4+M5+M6 R2 + CQRS + scheduler
- Week 4 Day 1: M7+M8 admin endpoint + integration tests
- Week 4 Day 2: M9+M10 FE attribution + staging dry-run

**Output**: 1-2 PR shipped, catalog quality boost (30-40% coverage expected).

### Phase 4 (Backlog) — Outbox concurrency hardening

**Scope**: #1965 FOR UPDATE SKIP LOCKED. **Triggered only by metric breach**.

Out of scope finché >5% duplicate-dispatch rate non osservato in staging/prod su sliding 7-day window. Consumer idempotency (sess.46c) è safety net adeguato.

---

## Risk matrix consolidato

| Issue | Tech Risk | Business Risk | Mitigation |
|---|---|---|---|
| **#1907** | M (legacy 424-line BackgroundService) | L | Integration tests pre-impl, no touch su stale-recovery loops |
| **#1964** | L (test-only) | L | Path A preferred, Path B fallback factory-independent |
| **#1972** | M (40 mock refactor) | M (CVE access, security) | Codemod where possible, canary file first |
| **#1823** | H (external APIs, license edge cases) | M (catalog quality) | Spike + ADR upfront, strict license whitelist, dead-letter pattern |
| **#1965** | H (concurrency primitives) | L (non-goal MVP) | Defer until metric breach, Testcontainers Postgres mandatory |

---

## Anti-patterns to avoid

- ❌ **Bundle all 5 in single mega-PR** — Each issue is independent, parallelizable, must ship separately
- ❌ **Implement #1965 before #1823 priority** — Inverted, #1823 has higher business value pre-metric breach
- ❌ **Skip integration tests on #1907** — Legacy 424-line BackgroundService, regression rate-limit risk
- ❌ **Skip license validation on #1823** — Legal risk, must be machine-validated PD/CC0/CC-BY/CC-BY-SA
- ❌ **Skip ADR on #1823** — External API + license + R2 + FE = cross-stack, requires architecture upfront
- ❌ **Premature optimization on #1965** — Defer until business case observed, avoid scope creep
- ❌ **Path B on #1964 if Path A possible** — #1859 closed, factory should work, prefer simpler path

---

## Effort summary

| Phase | Issues | Effort | Risk | Output |
|---|---|---|---|---|
| Phase 1 | #1907 #1964 | ~1.5gg | L-M | 2 PR shipped |
| Phase 2 | #1972 | ~3gg | M | 1 PR shipped |
| Phase 3 | #1823 | ~5-7gg | H | 1-2 PR shipped + ADR |
| Phase 4 | #1965 | ~2gg (backlog) | H | Triggered impl when needed |
| **Total** | 5 issue | **~10-15gg** | mixed | 4-5 PR + 1 ADR |

---

## Decision points required from user

Prima di iniziare Phase 1, conferma:

1. **Phase 1 vs Phase 3 first?** — Quick wins (Phase 1) vs high value (Phase 3) — raccomandazione: Phase 1 first per momentum
2. **#1972 vitest v4 timing** — Block FE feature work durante 3gg refactor? Coordinate con frontend team?
3. **#1823 spike scope** — Spike-only PR upfront o include in implementation PR? Raccomandazione: separate spike PR
4. **#1965 trigger condition** — Cap stesso 5% threshold o ridurre? Verify current metric baseline staging

---

## References

- Sess.46c memory: `C:/Users/Utente/.claude/projects/D--Repositories-meepleai-monorepo-frontend/memory/sess-46c-3-already-shipped-cluster.md`
- Cluster #1535 closure context: `audits/2026-06-06-issue-1535-consumer-idempotency-audit.md`
- #1859 plan (✅ shipped): `docs/superpowers/plans/2026-06-05-issue-1859-rotate-provider-key.md`
- #1535 outbox plan: `docs/superpowers/plans/2026-06-06-issue-1535-event-outbox.md`
- #1874 spec (parent #1907): TBD discovery during M0
- #1821 L1 placeholder + #1824 L3 user cover: closed via PR #1892
