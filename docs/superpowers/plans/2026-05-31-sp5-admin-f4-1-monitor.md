# SP5 F4.1 — A8 Monitor re-skin + LiveEventLog Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Re-skin `/admin/monitor` and add a new "events" tab hosting the `LiveEventLog` component (realtime SSE stream over the existing `DomainEventLog` outbox). 4 phases = 4 PRs stacked on the same branch.

**Architecture:** Backend gets a SSE broadcast endpoint over the outbox (Phase 1). Frontend Monitor hub is re-skinned with Tailwind tokens + shared section band (Phase 2). New `LiveEventLog` component with `useLiveEvents` hook is built (Phase 3). New `events` tab integrates the component into the hub, with reuse-ready exports (Phase 4).

**Tech Stack:** .NET 9 (BE), Next.js 16 App Router + React 19 + Tailwind v4 + `react-window ^2.2.7` (FE), Vitest + Playwright (test), Testcontainers Postgres (BE integration).

**Design doc:** `docs/superpowers/specs/2026-05-31-sp5-admin-f4-1-monitor-design.md` (acceptance criteria panel-grade in §7, risk register in §8).

**Mockup reference (committed `9fa1a624e`):** `admin-mockups/design_handoff_admin/admin/sp5-admin-monitor.html` (header band + 13-tab sub-nav + KPI strip + filter bar + LiveEventLog panel).

**Issue:** [#1718](https://github.com/meepleAi-app/meepleai-monorepo/issues/1718) (P1, area/backend + area/frontend + admin).

**Branch:** `feature/issue-1718-f4-1-monitor` (already created, parent `main-dev`, pushed `9fa1a624e`).

---

## Ground rules (read before any task)

1. **Stack di PR** — 1 PR per fase, rebase-merged su `main-dev` in ordine. Ogni PR è autonomamente reviewable e mergeable. Pattern S1/S2/S3 sicurezza (PR #1532 + supplement, PR #1555, PR #1597).
2. **Re-skin Fase 2 = className/CSS-only.** Hook, `useQuery`, SSE attaching, `refetchInterval`, state, handler stay byte-identical. Test esistenti sono il gate.
3. **No new primitives ex-novo** per Fase 2 — usa Tailwind utility + token semantici esistenti. `LiveEventLog` (Fase 3) è l'unica eccezione (componente ex-novo).
4. **Endpoint contract stability.** Fase 1 aggiunge endpoint nuovi (`/api/v1/admin/events`, `/api/v1/admin/events/stream`, `/api/v1/admin/events/types`); NON modifica `/api/v1/users/me/activity-feed` esistente.
5. **CQRS strict.** Endpoint Fase 1 usa ONLY `IMediator.Send()` — zero direct service injection (regola progetto CLAUDE.md).
6. **Exception types.** Usa `NotFoundException` (404), `ConflictException` (409); MAI `InvalidOperationException` (500).
7. **Token mapping** (pattern consolidato F3-FU-3):
   - `font-quicksand` (display) · `font-mono` (labels/values/IDs) · body = no class
   - `text-foreground`, `text-muted-foreground`, `bg-background`, `bg-card`, `bg-muted`
   - `border-border/60` (NON esiste `border-border-light`)
   - entity accent: `text-entity-event` / `bg-entity-event/12` / `border-l-4 border-l-entity-event` (rosso, semantica operations/incidents)
   - status chip: success → `bg-entity-toolkit/12 text-entity-toolkit`; danger → `bg-destructive text-destructive-foreground`; warning → `bg-amber-500/14 text-amber-600` (file-level eslint-disable amber/zinc/emerald/rose)
8. **ESLint** `local/no-hardcoded-color-utility` mode `error` — file con neutral palette hardcoded vietati. File-level disable per palette amber/zinc/emerald/rose (admin pattern).
9. **Commands** run from `apps/web/` for FE (tests/lint/typecheck), `apps/api/src/Api/` for BE (`dotnet test`); git from repo root.
10. **One commit per task.** Message pattern: `feat(admin-monitor): #1718 <subject>` (file ≤72 char totale).
11. **Pre-push hook = full Next.js build** (~1-2min). Tollerare.
12. **Pre-flight branch hygiene** (#806): `git branch --show-current` MUST print `feature/issue-1718-f4-1-monitor` before each commit; `git status` clean before each Edit batch.

---

## File Structure overview

### Phase 1 BE — Create

```
apps/api/src/Api/BoundedContexts/Administration/Application/Queries/AdminEvents/
  GetAdminEventsQuery.cs
  GetAdminEventsResult.cs
  GetAdminEventsQueryHandler.cs
  GetAdminEventsQueryHandler.Test.cs            # (in tests/)
  GetEventTypeStatsQuery.cs
  GetEventTypeStatsResult.cs
  GetEventTypeStatsQueryHandler.cs

apps/api/src/Api/Infrastructure/EventBroadcasting/
  IEventBroadcaster.cs
  ChannelEventBroadcaster.cs                    # singleton, Channel<T> bounded
  DomainEventBroadcastInterceptor.cs            # SaveChanges hook
  EventBroadcastingExtensions.cs                # AddEventBroadcasting() in DI

apps/api/src/Api/Routing/
  AdminEventsEndpoints.cs                       # GET /events, GET /events/stream, GET /events/types

tests/Api.Tests/Administration/Application/Queries/AdminEvents/
  GetAdminEventsQueryHandlerTests.cs            # unit
  GetEventTypeStatsQueryHandlerTests.cs         # unit

tests/Api.Tests/Administration/Integration/
  AdminEventsEndpointsIntegrationTests.cs       # Testcontainers
  EventBroadcasterIntegrationTests.cs           # Testcontainers
```

### Phase 1 BE — Modify

```
apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs    # invoke broadcaster.Publish after SaveChanges commit
apps/api/src/Api/Program.cs                              # services.AddEventBroadcasting()
```

### Phase 2 FE re-skin — Create

```
apps/web/src/app/admin/(dashboard)/monitor/layout.tsx                   # NEW (banda condivisa)
apps/web/src/components/admin/monitor/MonitorTopBand.tsx                # h1 + crumbs + actions
apps/web/src/components/admin/monitor/MonitorCrumbs.tsx                 # usePathname → label
apps/web/src/components/admin/monitor/MonitorTopActions.tsx             # search ⌘K + Pause/Export
apps/web/src/components/admin/monitor/__tests__/MonitorTopBand.test.tsx
apps/web/src/components/admin/monitor/__tests__/MonitorCrumbs.test.tsx
```

### Phase 2 FE re-skin — Modify (12 tab + page + sub-routes)

```
apps/web/src/app/admin/(dashboard)/monitor/page.tsx                     # remove h1, re-skin TABS scaffolding
apps/web/src/app/admin/(dashboard)/monitor/{AlertsTab,CacheTab,CommandCenterTab,
  ContainersTab,GrafanaTab,InfrastructureTab,LogsTab,MauTab,TestingTab,
  AlertHistoryTab,BulkExportTab,EmailManagementTab}.tsx                 # 12 tab re-skin
apps/web/src/app/admin/(dashboard)/monitor/{containers,grafana,logs,mau,
  operations,service-calls,services}/page.tsx                           # 7 sub-routes re-skin (light pass)
```

### Phase 3 LiveEventLog — Create

```
apps/web/src/components/admin/monitor/LiveEventLog.tsx                  # main component
apps/web/src/components/admin/monitor/use-live-events.ts                # hook
apps/web/src/components/admin/monitor/live-event-types.ts               # DTOs matching BE
apps/web/src/components/admin/monitor/parse-event-message.ts            # pure helper
apps/web/src/components/admin/monitor/event-level-mapper.ts             # eventType → info/ok/warn/err
apps/web/src/components/admin/monitor/__tests__/LiveEventLog.test.tsx
apps/web/src/components/admin/monitor/__tests__/use-live-events.test.ts
apps/web/src/components/admin/monitor/__tests__/parse-event-message.test.ts
apps/web/src/components/admin/monitor/__tests__/event-level-mapper.test.ts
apps/web/src/components/admin/monitor/index.ts                          # public exports
```

### Phase 4 Integrazione — Modify

```
apps/web/src/app/admin/(dashboard)/monitor/page.tsx                     # add 'events' tab + renderTabContent case
apps/web/e2e/admin/monitor-events-tab.spec.ts                           # E2E smoke
admin-mockups/design_handoff_admin/ADMIN_AUDIT.md                       # A8 status → ✅ PR #...
admin-mockups/design_handoff_admin/SCREENS.md                           # A8 entry → completed
```

---

# PHASE 1 — Backend SSE + admin-scoped query

**PR title:** `feat(admin-monitor): #1718 Phase 1/4 — SSE broadcast + admin events query`
**Goal:** Backend infrastructure for streaming domain events to admin clients.

## Task 1.1: GetAdminEventsQuery + Handler (admin-scoped polling)

**Files:**
- Create: `Application/Queries/AdminEvents/GetAdminEventsQuery.cs`
- Create: `Application/Queries/AdminEvents/GetAdminEventsResult.cs`
- Create: `Application/Queries/AdminEvents/GetAdminEventsQueryHandler.cs`
- Create: `tests/Api.Tests/Administration/Application/Queries/AdminEvents/GetAdminEventsQueryHandlerTests.cs`

- [ ] **Step 1: Write failing tests first** (TDD). Cover:
  - `Handle_ReturnsEvents_OrderedByLoggedAtDesc`
  - `Handle_AppliesRetention_90Days` (events older than 90d excluded)
  - `Handle_WithSinceCursor_FiltersOlderThanSince`
  - `Handle_WithEventTypesFilter_AppliesIn`
  - `Handle_WithAggregateTypesFilter_AppliesIn`
  - `Handle_WithUserIdFilter_AppliesEquality`
  - `Handle_WithAggregateIdFilter_AppliesEquality`
  - `Handle_AppliesLimit` (default 100, clamp 1-1000)
  - `Handle_EmptyResult_ReturnsEmptyList`

- [ ] **Step 2: Implement** `GetAdminEventsQuery` record + `GetAdminEventsResult` (matches `DomainEventDto` in design §3.2).

- [ ] **Step 3: Implement** `GetAdminEventsQueryHandler` (LINQ on `_db.DomainEventLogs`, AsNoTracking, retention filter, optional filters, Take(limit clamped)).

- [ ] **Step 4: Run tests** — all pass. Coverage ≥ 90% del handler.

- [ ] **Step 5: Commit** `feat(admin-monitor): #1718 add GetAdminEventsQueryHandler` (admin-scoped query)

## Task 1.2: GetEventTypeStatsQuery + Handler (metadata endpoint)

**Files:**
- Create: `Application/Queries/AdminEvents/GetEventTypeStatsQuery.cs`
- Create: `Application/Queries/AdminEvents/GetEventTypeStatsResult.cs`
- Create: `Application/Queries/AdminEvents/GetEventTypeStatsQueryHandler.cs`
- Create: `tests/.../GetEventTypeStatsQueryHandlerTests.cs`

- [ ] **Step 1: TDD tests** for:
  - `Handle_GroupsByEventType_LastDay`
  - `Handle_ReturnsCountAndLastSeenAt`
  - `Handle_EmptyDb_ReturnsKnownTypesWithZeroCount` (uses `EventTypeRegistry.Known`)

- [ ] **Step 2: Implement** query + handler. SQL: `GROUP BY EventType WHERE LoggedAt >= UtcNow - 24h`.

- [ ] **Step 3: Tests pass.**

- [ ] **Step 4: Commit** `feat(admin-monitor): #1718 add GetEventTypeStatsQueryHandler` (chips metadata)

## Task 1.3: IEventBroadcaster + ChannelEventBroadcaster

**Files:**
- Create: `Infrastructure/EventBroadcasting/IEventBroadcaster.cs`
- Create: `Infrastructure/EventBroadcasting/ChannelEventBroadcaster.cs`
- Create: `tests/Api.Tests/Infrastructure/EventBroadcasting/ChannelEventBroadcasterTests.cs`

- [ ] **Step 1: TDD tests** for:
  - `Publish_QueuesEvent_AvailableToSubscribers`
  - `Subscribe_ReturnsAsyncEnumerable`
  - `Multiple_Subscribers_AllReceiveEvents` (fan-out)
  - `Backpressure_DropOldest` (channel bounded, overflow drops oldest)
  - `Subscribe_AfterUnsubscribe_NoDelivery`
  - `Dispose_ClosesAllChannels`

- [ ] **Step 2: Implement** `IEventBroadcaster` interface (`Publish(DomainEventDto)`, `Subscribe(filter, CancellationToken) → IAsyncEnumerable<DomainEventDto>`).

- [ ] **Step 3: Implement** `ChannelEventBroadcaster` using `System.Threading.Channels.Channel<T>` bounded (capacity 1000, `BoundedChannelFullMode.DropOldest`). Singleton-safe (concurrent subscribers via `ConcurrentDictionary<Guid, Channel>`).

- [ ] **Step 4: Tests pass + add metric** `meepleai_admin_sse_events_dropped_total` counter (Prometheus, riusa `MeepleAiMetrics`).

- [ ] **Step 5: Commit** `feat(admin-monitor): #1718 add ChannelEventBroadcaster (bounded fan-out)`

## Task 1.4: DomainEventBroadcastInterceptor (SaveChanges hook)

**Files:**
- Create: `Infrastructure/EventBroadcasting/DomainEventBroadcastInterceptor.cs`
- Modify: `Infrastructure/MeepleAiDbContext.cs` (invoke interceptor on `SaveChangesAsync` overload)
- Create: `tests/Api.Tests/Infrastructure/EventBroadcasting/DomainEventBroadcastInterceptorTests.cs`

- [ ] **Step 1: TDD tests** for:
  - `SaveChanges_PublishesEachNewDomainEventLog`
  - `SaveChanges_RollbackOnError_NoPublish` (transactional)
  - `Publishes_AfterCommit_NotBefore` (atomicity)

- [ ] **Step 2: Implement** interceptor that captures `DomainEventLogEntity` instances added in `SavingChanges`, then post-commit invokes `broadcaster.Publish(dto)` for each.

- [ ] **Step 3: Wire in** `MeepleAiDbContext.SaveChangesAsync` — interceptor invoked via DI (avoid adding interceptor manually, prefer EF `IInterceptor` registration).

- [ ] **Step 4: Tests pass** (use Testcontainers Postgres for transactional verification).

- [ ] **Step 5: Commit** `feat(admin-monitor): #1718 wire DomainEventBroadcastInterceptor to SaveChanges`

## Task 1.5: AdminEventsEndpoints (HTTP layer)

**Files:**
- Create: `Routing/AdminEventsEndpoints.cs`
- Modify: `Program.cs` (`app.MapAdminEventsEndpoints()`)
- Create: `tests/Api.Tests/Administration/Integration/AdminEventsEndpointsIntegrationTests.cs`

- [ ] **Step 1: Integration tests** (Testcontainers) for:
  - `GET /api/v1/admin/events` returns paginated events
  - `GET /api/v1/admin/events?since=<cursor>` applies cursor filter
  - `GET /api/v1/admin/events?eventTypes=agent.created` applies filter
  - `GET /api/v1/admin/events/types` returns stats with counts
  - `GET /api/v1/admin/events/stream` emits SSE on new event commit within 1s (latency assertion)
  - `GET /api/v1/admin/events/stream` heartbeat `:hb\n\n` every 15s
  - `GET /api/v1/admin/events/stream` with `Last-Event-ID` header backfills then attaches stream
  - 401 without session, 403 with non-admin role, 200 with admin/superadmin (gate via `RequireAdminSession()`)

- [ ] **Step 2: Implement** endpoints using minimal API + `IMediator.Send()` for queries (CQRS strict). SSE endpoint uses `IAsyncEnumerable<DomainEventDto>` from broadcaster.

- [ ] **Step 3: Add filter parameter parsing** (comma-separated `eventTypes`, etc.).

- [ ] **Step 4: Apply gate** `RequireAdminSession()` filter to all 3 endpoints.

- [ ] **Step 5: Tests pass + Traefik SSE timeout check** (D-5 §9 design doc): leggi `infra/traefik/dynamic.yml`, verifica timeout default, aggiungi middleware `responseTimeout=0` se necessario.

- [ ] **Step 6: Commit** `feat(admin-monitor): #1718 add AdminEventsEndpoints (GET, stream, types)`

## Task 1.6: DI wiring + Program.cs

**Files:**
- Modify: `Infrastructure/EventBroadcasting/EventBroadcastingExtensions.cs` (create) — `AddEventBroadcasting()` extension
- Modify: `Program.cs` — call `builder.Services.AddEventBroadcasting()` + `app.MapAdminEventsEndpoints()`

- [ ] **Step 1: Implement** extension method that registers `IEventBroadcaster` singleton + `DomainEventBroadcastInterceptor` scoped.

- [ ] **Step 2: Wire** in `Program.cs` (near other `AddXxx` calls).

- [ ] **Step 3: Run full suite** `dotnet test --filter "BoundedContext=Administration"` → 0 regression + new tests verdi.

- [ ] **Step 4: Manual smoke** via Bruno or curl: start dev server, trigger an event (e.g. create an agent), `curl -N http://localhost:8080/api/v1/admin/events/stream` with admin session cookie → verify event arrives within 1s.

- [ ] **Step 5: Commit** `feat(admin-monitor): #1718 wire event broadcasting in DI`

## Task 1.7: Phase 1 PR + review + merge

- [ ] **Step 1: Push branch** + `gh pr create --base main-dev --title "feat(admin-monitor): #1718 Phase 1/4 — SSE broadcast + admin events query"`
- [ ] **Step 2: Run code-review skill** `/code-review:code-review <PR-URL>` (per CLAUDE.md workflow rule)
- [ ] **Step 3: Address review findings** (only score ≥80)
- [ ] **Step 4: Wait for CI green** (typecheck, lint, full BE test suite)
- [ ] **Step 5: Squash merge** to `main-dev` (parent branch auto-delete on merge enabled)
- [ ] **Step 6: Rebase feature branch on main-dev** after merge: `git rebase main-dev` (clean continuation for Phase 2)

---

# PHASE 2 — FE re-skin Monitor 12 tab

**PR title:** `feat(admin-monitor): #1718 Phase 2/4 — Monitor hub re-skin + section band`
**Goal:** Re-skin the 12 existing tabs with Tailwind tokens + introduce shared section band. Zero behavioral changes.

## Task 2.1: MonitorTopBand + MonitorCrumbs + MonitorTopActions

**Files:**
- Create: 3 components + 2 test files (vedi File Structure §Phase 2 Create)

- [ ] **Step 1: TDD** `MonitorTopBand.test.tsx` — single h1 "Monitor", crumbs slot, actions slot rendered.
- [ ] **Step 2: TDD** `MonitorCrumbs.test.tsx` — pathname/tab → label mapping (`/admin/monitor?tab=events` → `Admin · Monitor · Events`).
- [ ] **Step 3: Implement** components using Tailwind tokens (NO new CSS files). Sticky `top-0 z-50` for the band.
- [ ] **Step 4: Tests pass.**
- [ ] **Step 5: Commit** `feat(admin-monitor): #1718 add MonitorTopBand + Crumbs + TopActions`

## Task 2.2: monitor/layout.tsx (banda wrapping)

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/monitor/layout.tsx`
- Modify: `apps/web/src/app/admin/(dashboard)/monitor/page.tsx` — remove local h1 (if present), let band handle it

- [ ] **Step 1: Check** if `monitor/layout.tsx` already exists — likely not (most admin sections have only page.tsx).
- [ ] **Step 2: Create layout** wrapping `<MonitorTopBand /> {children}` with proper padding/spacing.
- [ ] **Step 3: Remove** local "Monitor" h1 from `page.tsx` (header section, before TABS).
- [ ] **Step 4: Run** `pnpm test:admin-dashboard` — verify 0 regression.
- [ ] **Step 5: Commit** `feat(admin-monitor): #1718 add monitor layout band wrapping`

## Task 2.3: Re-skin AlertsTab + CacheTab + InfrastructureTab (3 tab batch)

**Files:**
- Modify: `AlertsTab.tsx`, `CacheTab.tsx`, `InfrastructureTab.tsx`

- [ ] **Step 1: Baseline** `pnpm test apps/web/src/app/admin/\(dashboard\)/monitor/__tests__/AlertsTab.test.tsx CacheTab.test.tsx` etc. — confirm green.
- [ ] **Step 2: Re-skin** each tab: convert `bg-slate-*`/`text-gray-*`/`border-zinc-*` → semantic tokens, KPI tiles → `border-l-4 border-l-entity-*`, panels → `rounded-[10px] border border-border/60 bg-card`.
- [ ] **Step 3: Add file-level ESLint disable** if `amber`/`zinc`/`emerald`/`rose` palette used.
- [ ] **Step 4: Tests pass** (no DOM structure change, only className).
- [ ] **Step 5: Commit** `refactor(admin-monitor): #1718 re-skin Alerts/Cache/Infrastructure tabs`

## Task 2.4: Re-skin Containers + Logs + Grafana + MAU (4 tab batch)

Idem pattern Task 2.3. Commit: `refactor(admin-monitor): #1718 re-skin Containers/Logs/Grafana/MAU tabs`

## Task 2.5: Re-skin Command + Testing + BulkExport + Email + AlertHistory (5 tab batch)

Idem pattern Task 2.3. Commit: `refactor(admin-monitor): #1718 re-skin Command/Testing/BulkExport/Email/AlertHistory tabs`

## Task 2.6: Re-skin sub-routes (7 page.tsx light pass)

**Files:**
- `monitor/{containers,grafana,logs,mau,operations,service-calls,services}/page.tsx` (+ operations sub-tabs)

- [ ] **Step 1: Light re-skin** — sub-routes hanno componenti complessi (ContainerDashboard, GrafanaDashboard, LogViewer, MauDashboard, AuditTab/EmergencyTab/QueueTab/ResourcesTab, ServiceCallHistory, ServicesDashboard). Limita re-skin a chrome (header, container, spacing). NO refactor logica.
- [ ] **Step 2: Tests pass** — gate test esistenti.
- [ ] **Step 3: Commit** `refactor(admin-monitor): #1718 re-skin monitor sub-routes`

## Task 2.7: Phase 2 PR + review + merge

Stessa procedura Task 1.7.

---

# PHASE 3 — LiveEventLog component

**PR title:** `feat(admin-monitor): #1718 Phase 3/4 — LiveEventLog component + useLiveEvents hook`
**Goal:** New cross-cutting component consuming the BE SSE stream.

## Task 3.1: live-event-types.ts + parse-event-message.ts (pure helpers)

**Files:**
- Create: `live-event-types.ts` (TS types matching BE `DomainEventDto`)
- Create: `parse-event-message.ts` (event → display fragments with entity color extraction)
- Create: tests for both

- [ ] **Step 1: TDD** `parse-event-message.test.ts` — mapping eventType to displayable parts.
- [ ] **Step 2: Implement** parse helper (pure function, no React).
- [ ] **Step 3: Tests pass.**
- [ ] **Step 4: Commit** `feat(admin-monitor): #1718 add live-event types + message parser`

## Task 3.2: event-level-mapper.ts

**Files:**
- Create: `event-level-mapper.ts` + test

- [ ] **Step 1: TDD** mapping table:
  - `*.failed` / `*.error` → `err`
  - `*.created` / `*.indexed` / `*.finalized` → `ok`
  - `*.removed` → `warn`
  - default → `info`
- [ ] **Step 2: Implement.**
- [ ] **Step 3: Tests pass.**
- [ ] **Step 4: Commit** `feat(admin-monitor): #1718 add event-level mapper`

## Task 3.3: useLiveEvents hook (backfill + SSE attach)

**Files:**
- Create: `use-live-events.ts`
- Create: `use-live-events.test.ts`

- [ ] **Step 1: TDD** with mock fetch + mock EventSource for:
  - Initial backfill populates `events[]`
  - SSE event appends to top
  - `MAX_BUFFER=1000` bounded
  - `Last-Event-ID` set on reconnect
  - Exponential backoff `[1s, 2s, 4s, 8s, max 30s]` on error
  - Cleanup on unmount (close EventSource)
  - Pause/Resume toggles attach
  - Refetch re-runs backfill
- [ ] **Step 2: Implement** hook (useState + useEffect + EventSource + AbortController for backfill).
- [ ] **Step 3: Tests pass** ≥ 85% coverage.
- [ ] **Step 4: Commit** `feat(admin-monitor): #1718 add useLiveEvents hook (backfill + SSE)`

## Task 3.4: LiveEventLog component (virtualized list + filters)

**Files:**
- Create: `LiveEventLog.tsx`
- Create: `LiveEventLog.test.tsx`

- [ ] **Step 1: TDD** for:
  - Render with backfill mock → exact row count
  - Append from SSE mock → top row visible
  - Pause button toggles `isStreaming=false`
  - Empty state when 0 events
  - Error state with retry
  - Filter chip click filters
  - Virtualization: 1000 events → only ~30 DOM rows
- [ ] **Step 2: Implement** component using `react-window` `FixedSizeList`, row height 32px, overscanCount 10.
- [ ] **Step 3: Style rows** matching mockup `.event-log .row` pattern (grid 96px·60px·1fr, mono font, entity colors via `text-entity-*` classes).
- [ ] **Step 4: Implement filter chips** querying `/api/v1/admin/events/types` for counts.
- [ ] **Step 5: Tests pass.**
- [ ] **Step 6: Commit** `feat(admin-monitor): #1718 add LiveEventLog component (virtualized + filters)`

## Task 3.5: Public index.ts exports

**Files:**
- Create: `components/admin/monitor/index.ts`

- [ ] **Step 1: Export** `LiveEventLog`, `LiveEventLogProps`, `DomainEventDto` for reuse.
- [ ] **Step 2: Commit** `feat(admin-monitor): #1718 export LiveEventLog public API`

## Task 3.6: Phase 3 PR + review + merge

Stessa procedura Task 1.7.

---

# PHASE 4 — Integrazione tab "events"

**PR title:** `feat(admin-monitor): #1718 Phase 4/4 — events tab integration + E2E smoke`
**Goal:** Wire LiveEventLog into the Monitor hub as the 13th tab.

## Task 4.1: Add 'events' tab to TABS array

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/monitor/page.tsx`

- [ ] **Step 1: Add** entry `{ id: 'events', label: 'Events', href: '/admin/monitor?tab=events', icon: <Radio />, badge: 'live' }` to `TABS` array.
- [ ] **Step 2: Add** `case 'events'` to `renderTabContent` switch importing `<LiveEventLog />` (from `components/admin/monitor`).
- [ ] **Step 3: Verify** badge rendering — `live` shown as dot pulsante via Tailwind `animate-pulse`.
- [ ] **Step 4: Run** `pnpm test:admin-dashboard` + `pnpm test apps/web/src/app/admin/\(dashboard\)/monitor/__tests__/page.test.tsx` — verify tab integration.
- [ ] **Step 5: Commit** `feat(admin-monitor): #1718 add events tab to Monitor hub`

## Task 4.2: E2E smoke test

**Files:**
- Create: `apps/web/e2e/admin/monitor-events-tab.spec.ts`

- [ ] **Step 1: Write** E2E spec following design §6.3:
  - Login as admin
  - Navigate `/admin/monitor?tab=events`
  - Verify tab `aria-selected="true"`
  - Verify `LiveEventLog` panel renders
  - Verify at least 1 row entro 5s (DB seed con evento outbox)
  - Verify filter chip click filters
  - Verify pause/resume
- [ ] **Step 2: Run locally** `pnpm test:e2e -- monitor-events-tab.spec.ts`.
- [ ] **Step 3: Commit** `test(admin-monitor): #1718 add E2E smoke for events tab`

## Task 4.3: Docs + memory update

**Files:**
- Modify: `admin-mockups/design_handoff_admin/ADMIN_AUDIT.md` (riga 32 + 176: `🚧 in corso #1718` → `✅ PR #...`)
- Modify: `admin-mockups/design_handoff_admin/SCREENS.md` (A8 entry: PR mergiata)
- Rename: `~/.claude/.../memory/project_sp5_admin_f4_1_monitor_wip.md` → `project_sp5_admin_f4_1_monitor_done.md`
- Modify: `~/.claude/.../memory/MEMORY.md` (sposta F4.1 da WIP a Executed Plans, 1 riga)

- [ ] **Step 1: Update** ADMIN_AUDIT.md con il PR number reale dopo merge.
- [ ] **Step 2: Update** SCREENS.md.
- [ ] **Step 3: Rename + sintesi finale** del memory file.
- [ ] **Step 4: Update** MEMORY.md.
- [ ] **Step 5: Commit** `docs(admin-monitor): #1718 update audit + screens + memory post-merge`

## Task 4.4: Phase 4 PR + review + merge

Stessa procedura Task 1.7.

---

## Post-merge checklist

- [ ] Issue #1718 chiusa automaticamente dal merge dell'ultima PR (linking `Closes #1718`).
- [ ] Branch `feature/issue-1718-f4-1-monitor` cancellata automaticamente (repo setting auto-delete).
- [ ] CI green su `main-dev` post-merge.
- [ ] Manual smoke staging (se F4.1 promossa a staging immediato): `/admin/monitor?tab=events` → verifica stream attivo.
- [ ] Apertura **F3-FU-7** (A4 `/admin/ai` re-skin) come backlog parallelo deciso (multi-track sequenziale).

## Definition of Done

- ✅ Tutte e 4 le PR mergiate su `main-dev`
- ✅ Tutti i 25+ task del plan completati
- ✅ Acceptance criteria panel-grade del design doc §7 verificati per ciascuna fase
- ✅ Test coverage backend ≥ 90% per handler nuovi, frontend ≥ 85% per LiveEventLog
- ✅ 0 regression test esistenti (BE: `Administration` BC; FE: `admin-dashboard` 558+ suite)
- ✅ E2E smoke verde su CI
- ✅ Docs aggiornate (ADMIN_AUDIT.md, SCREENS.md, memory)
- ✅ Issue #1718 chiusa
