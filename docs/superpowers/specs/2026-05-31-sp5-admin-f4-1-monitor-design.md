# SP5 F4.1 — A8 Monitor re-skin + LiveEventLog Design

**Issue**: [#1718](https://github.com/meepleAi-app/meepleai-monorepo/issues/1718) (P1, area/backend + area/frontend + admin)
**Parent epic**: SP5 Admin Console integration (spec `docs/superpowers/specs/2026-05-24-sp5-admin-console-consolidation-design.md` §9 F4 Ondata Ops)
**Branch**: `feature/issue-1718-f4-1-monitor` (parent `main-dev`)
**WIP context**: `~/.claude/.../memory/project_sp5_admin_f4_1_monitor_wip.md`
**Mockup riferimento**: `admin-mockups/design_handoff_admin/admin/sp5-admin-monitor.html`

## 1. Cosa fa F4.1 (e cosa non fa)

Prima ondata della **F4 Ondata Ops**: re-skin di `/admin/monitor` (A8) + introduzione del componente `LiveEventLog` realtime, alimentato dall'outbox `DomainEventLog` già esistente (Issue #661, Track S1 PR #1532). Implementato come **1 epic + stack di 4 PR** sullo stesso branch (pattern S1/S2/S3 sicurezza).

**In scope** (4 PR stacked):
1. **PR 1/4 BE** — `GET /api/v1/admin/events/stream` SSE broadcast sopra commit outbox + `GET /api/v1/admin/events` admin-scoped query (cross-utente, no filtro `UserId`).
2. **PR 2/4 FE re-skin** — Tailwind tokens cosmetic sulle 12 tab esistenti del Monitor hub + banda di sezione condivisa (pattern F3-FU-3 KB).
3. **PR 3/4 LiveEventLog component** — `components/admin/monitor/LiveEventLog.tsx` con hook `useLiveEvents()` (backfill polling + SSE attach), lista virtualized `react-window`, filter chips EventType/EntityType/UserId.
4. **PR 4/4 integrazione** — tab `events` (13°) aggiunto a `monitor/page.tsx` con `<LiveEventLog />` + reuse predisposto per C1 Infra; E2E smoke; update memory + audit docs.

**Out of scope** (tracked separatamente):
- ❌ Refactor logica nelle 12 tab esistenti (re-skin = className/Tailwind only, Nygard)
- ❌ Backend nuove API oltre quelle Fase 1 (no `force-logout`, no `flag-hallucination` — ondate diverse)
- ❌ Content moderation A3 (gruppo 0% backend, backlog dedicato)
- ❌ Sub-route migration to tab (cleanup IA `/admin/monitor/{containers,grafana,logs,mau,operations,service-calls,services}` → tab) — valutare in F4.4
- ❌ Retention policy editor, RBAC granulare per `eventType`, settings per-tipo — non in scope F4.1
- ❌ Visual-regression gate (rimosso 2026-05-20). Sostituto: manual designer review + ESLint `local/no-hardcoded-color-utility` verde + behavioral test verdi

## 2. Decisione headline — LiveEventLog è componente nuovo, NON `LogsTab`

**Problema**: lo spec consolidamento `ADMIN_AUDIT.md` riga 176 originale dichiarava `LiveEventLog ≈ LogsTab` — premessa errata.

**Realtà verificata** (audit 2026-05-30):
- `apps/web/src/app/admin/(dashboard)/monitor/LogsTab.tsx` è un wrapper di `LogViewer` che consuma **Loki application logs** (stack trace, request logs, ecc).
- `LiveEventLog` deve consumare **domain events** dell'outbox `DomainEventLog` (eventi di business: `agent.created`, `kb.doc.indexed`, `session.*`, `chat.session.created`, `library.*`).
- Loki application logs ≠ domain events. Sono due concern distinti, due fonti dati distinte, due UI distinte.

**Soluzione**: LiveEventLog è **componente ex-novo** sotto `components/admin/monitor/LiveEventLog.tsx`, ospitato in un **nuovo tab "events"** (13° tab) della Monitor hub. `LogsTab` (Loki) resta separato e invariato.

**Implicazione cross-cutting** (sp5-admin-infra.html C1):
- `sp5-admin-infra.html` riga 9 cita `LiveEventLog` come componente di reuse anche per C1.
- Il componente deve quindi essere **trasversale**: API stabile + props minime (`?eventType`, `?entityType`, `?userId`, `?aggregateId`) che permettono il riuso in C1 Infra (focus batch jobs) e altrove senza fork.
- In F4.1 il componente vive in `components/admin/monitor/` (primo consumer). Se in F4.4 o oltre emerge un secondo consumer (C1 Infra), si valuterà la promozione a `components/ui/live-event-log/`.

## 3. Architettura Fase 1 BE — SSE broadcast + admin-scoped query

### 3.1 Stato attuale verificato

Già esistente (Issue #661, S1 PR #1532, 2026-05-27):

| Componente | Path |
|---|---|
| Entity `DomainEventLogEntity` (Outbox append-only) | `apps/api/src/Api/Infrastructure/Entities/DomainEventLog/DomainEventLog.cs` |
| Entity configuration | `apps/api/src/Api/Infrastructure/EntityConfigurations/DomainEventLogEntityConfiguration.cs` |
| `EventTypeRegistry` (alias stabile vs CLR type) | `apps/api/src/Api/Infrastructure/DomainEventLog/EventTypeRegistry.cs` |
| `DomainEventLogMapper` | `apps/api/src/Api/Infrastructure/DomainEventLog/DomainEventLogMapper.cs` |
| `GetActivityFeedQueryHandler` (polling, 90gg retention, **user-scoped**) | `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/ActivityFeed/GetActivityFeedQueryHandler.cs` |
| HTTP endpoint `/api/v1/users/me/activity-feed` (presunto) | `apps/api/src/Api/Routing/ActivityFeedEndpoints.cs` |
| Migration `AddPayloadVersionToDomainEventLogs` | `apps/api/src/Api/Infrastructure/Migrations/20260527192206_*` |

Eventi già emessi:
- `agent.created`
- `chat.session.created`
- `kb.doc.indexed`
- `session.created`, `session.finalized`
- `library.entry.removed`, `library.session.recorded`

### 3.2 Cosa serve aggiungere

**A. Admin-scoped query handler** — nuovo `GetAdminEventsQueryHandler` (NON modificare `GetActivityFeedQueryHandler` per non rompere le viste user):

```csharp
namespace Api.BoundedContexts.Administration.Application.Queries.AdminEvents;

public record GetAdminEventsQuery(
    DateTime? Since = null,          // cursor: LoggedAt < Since (backward in time)
    int Limit = 100,
    IReadOnlyList<string>? EventTypes = null,
    IReadOnlyList<string>? AggregateTypes = null,
    Guid? UserId = null,             // optional filter by user
    Guid? AggregateId = null         // optional filter by aggregate
) : IQuery<GetAdminEventsResult>;
```

Filtro:
- Nessun `WHERE UserId = @x` di default (cross-user, admin-scoped).
- Retention `LoggedAt >= UtcNow - 90 days` invariata (allineata a `GetActivityFeedQueryHandler`).
- Paginazione cursor (`Since` = ultimo `LoggedAt` ricevuto, ORDER BY `LoggedAt` DESC).
- Filtri opzionali combinabili (EventTypes IN, AggregateTypes IN, UserId =, AggregateId =).

Gate: `RequireAdminSession()` (riuso pattern Administration BC).

**B. SSE broadcast endpoint** — `/api/v1/admin/events/stream`:

```
GET /api/v1/admin/events/stream
  ?eventTypes=agent.created,kb.doc.indexed
  &aggregateTypes=Agent,PdfDocument
  &userId=<guid>
  &aggregateId=<guid>

Content-Type: text/event-stream

data: {"id":"...","eventId":"...","eventType":"agent.created","aggregateType":"Agent",...}\n\n
data: {...}\n\n
```

Architettura:
- **In-process broadcaster**: `IEventBroadcaster` singleton con `Channel<DomainEventLogDto>` (bounded, drop-oldest se backpressure).
- **Source**: hook in `MeepleAiDbContext.SaveChangesAsync` overload: dopo commit successful, per ogni `DomainEventLogEntity` salvato → `broadcaster.Publish(dto)`.
- **Per-connection filter**: ogni SSE connection consuma il channel via `IAsyncEnumerable<DomainEventLogDto>` con `.Where(filter)` matching i query params.
- **Heartbeat**: comment line `:hb\n\n` ogni 15s per evitare timeout proxy/LB.
- **Reconnect**: client manda `Last-Event-ID` header → server backfill da `Since=LoggedAt(lastEventId)` poi attacca stream.

Gate: `RequireAdminSession()` + filtro eventi sensibili (NB: in F4.1 NESSUN filtro, tutti gli eventi sono visibili agli admin; promotion a superadmin-only per security events tracciata in §6 D-3).

**C. Endpoint metadata** — `/api/v1/admin/events/types`:

```
GET /api/v1/admin/events/types
→ 200 [{ "eventType": "agent.created", "count": 12, "lastSeenAt": "..." }, ...]
```

Risolto dal `EventTypeRegistry.Known` + COUNT(*) GROUP BY ultimi 24h, per popolare le filter chips senza hardcode.

### 3.3 Test

| Tipo | Path | Coverage target |
|---|---|---|
| Unit | `tests/Api.Tests/Administration/Application/Queries/AdminEvents/GetAdminEventsQueryHandlerTests.cs` | Cursor pagination, filtri opzionali combinati, retention 90gg, edge `Limit=0`/`>1000` |
| Integration | `tests/Api.Tests/Administration/Integration/AdminEventsEndpointsIntegrationTests.cs` (Testcontainers Postgres) | SSE broadcast: emette eventi dopo commit, heartbeat, `Last-Event-ID` reconnect, gate `RequireAdminSession` 401 senza session |
| Integration | `tests/Api.Tests/Administration/Integration/EventBroadcasterIntegrationTests.cs` | Hook su `SaveChangesAsync`: ogni `DomainEventLogEntity` salvato genera pubblicazione, backpressure drop-oldest |

## 4. Architettura Fase 2 FE re-skin — Monitor 12 tab

### 4.1 Scope

Re-skin **Tailwind/CSS-only** delle 12 tab esistenti + del wrapper `monitor/page.tsx` + del layout `monitor/layout.tsx` (creare se manca, per la banda condivisa). Zero refactor di hook, SSE attaching, refetch logic.

### 4.2 Banda di sezione condivisa

Stesso pattern di F3-FU-3 KB layout band. Crea `monitor/layout.tsx` se non esiste; aggiunge componenti dedicati in `components/admin/monitor/`:

- `MonitorTopBand.tsx` — h1 "Monitor", crumbs dinamiche (pathname → "Admin · Monitor · Events"), search ⌘K, actions `⏸ Pause stream` + `⤓ Export ndjson` (per ora solo visivi se non in tab events).
- `MonitorCrumbs.tsx` — derivazione crumbs da `usePathname` e dal `?tab=` corrente.

Le 12 tab perdono l'eventuale h1 locale "Monitor" (assorbita dalla banda). Identità della pagina = tab attivo nella sub-nav (`AdminHubTabBar` esistente).

### 4.3 Re-skin tab body

Per ognuna delle 12 tab esistenti: convertire utility hardcoded (es. `bg-slate-900`, `text-gray-400`, `border-zinc-700`) in token semantici (`bg-background`, `bg-card`, `bg-muted`, `text-foreground`, `text-muted-foreground`, `border-border`, `border-border/60`) e entity utility (`text-entity-event`, `bg-entity-event/12`, `ring-entity-event/30`) dove appropriato.

Pattern da rispettare (consolidato in F3-FU-3 PR #1664):
- KPI tile: `border-l-4 border-l-entity-* min-h-[88px]`
- admin-panel: `rounded-[10px] border border-border/60 bg-card overflow-hidden`
- admin-panel head: `flex items-center gap-2.5 border-b border-border/60 bg-background px-3.5 py-2.5`
- h3 panel head: `<h3 font-quicksand text-[13px] font-extrabold>`

Bare `<button>` net-new: aggiungere `type="button" focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2`.

### 4.4 Gate

| Check | Target |
|---|---|
| TypeScript | `pnpm typecheck` → 0 errors |
| Token lint | `pnpm lint:tokens` → 0 violations |
| ESLint `local/no-hardcoded-color-utility` | mode `error`, 0 fail |
| Admin dashboard test | `pnpm test:admin-dashboard` → 558+/558 verdi (gate F3-FU-3 stesso) |
| Test specifici Monitor | 0 regression in `apps/web/src/app/admin/(dashboard)/monitor/__tests__/*.test.tsx` (≥ 60 file di test) |
| Behavior SSE/refetch | Byte-identici (Nygard) — confermato dai test esistenti |

## 5. Architettura Fase 3 LiveEventLog component

### 5.1 Collocazione

```
apps/web/src/components/admin/monitor/
  LiveEventLog.tsx               # main component
  LiveEventLog.test.tsx          # unit
  use-live-events.ts             # hook
  use-live-events.test.ts        # unit
  live-event-types.ts            # TypeScript types matching BE DTO
  parse-event-message.ts         # pure helper (event → display row)
```

### 5.2 API

```typescript
interface LiveEventLogProps {
  eventTypes?: string[];        // initial filter
  aggregateTypes?: string[];    // initial filter
  userId?: string;              // restrict to single user
  aggregateId?: string;         // restrict to single aggregate
  initialLimit?: number;        // backfill page size, default 100
  height?: number | string;     // virtualized list height, default '70vh'
  onEventClick?: (event: DomainEventDto) => void;
}

interface DomainEventDto {
  id: string;                   // surrogate PK
  eventId: string;              // domain event id
  eventType: string;            // 'agent.created' etc.
  aggregateType: string | null; // 'Agent', 'ChatSession', etc.
  aggregateId: string | null;
  userId: string | null;
  payloadJson: string;
  payloadVersion: number;
  occurredAt: string;           // ISO
  loggedAt: string;             // ISO
}
```

### 5.3 Hook `useLiveEvents`

```typescript
function useLiveEvents(filters: LiveEventFilters): {
  events: DomainEventDto[];
  isLoading: boolean;       // initial backfill in progress
  isStreaming: boolean;     // SSE connected
  error: Error | null;
  pause: () => void;
  resume: () => void;
  refetch: () => void;
}
```

Lifecycle:
1. **Backfill**: `GET /api/v1/admin/events?limit=initialLimit&...filters` → popola `events[]`.
2. **SSE attach**: `EventSource('/api/v1/admin/events/stream?...filters', { withCredentials: true })` con `Last-Event-ID` = `events[0].id` se backfill ha risultati.
3. **Append**: ogni `data:` evento → `setEvents(prev => [event, ...prev].slice(0, MAX_BUFFER))` (default `MAX_BUFFER = 1000` per evitare unbounded growth).
4. **Pause/Resume**: chiude/riapre `EventSource` (server invia `Last-Event-ID` on reconnect).
5. **Reconnect strategy**: `EventSource` reconnect nativo + exponential backoff manuale a 3 fallimenti consecutivi (1s, 2s, 4s, 8s, max 30s).

### 5.4 UI

- **Virtualized list** via `react-window` (`^2.2.7`, già in dependency tree per `PdfTableRow`). `FixedSizeList`, row height fissa 32px, `overscanCount={10}`.
- **Riga LiveEventLog** (replica `.event-log .row` mockup):
  - `grid-cols-[96px_60px_1fr]` (timestamp · level · message)
  - timestamp `font-mono text-xs text-text-muted` formato `HH:mm:ss.SSS`
  - level (info/ok/warn/err) → derivato da `eventType` mapping (es. `*.failed` → err, `*.created`/`*.indexed` → ok, `*.removed` → warn, default → info)
  - message: `eventType <span class="k">aggregateType=</span><span class="v-{entity}">value</span> <span class="k">aggregateId=</span>{uuid8}` (parser puro in `parse-event-message.ts`)
- **Detail drawer** (opzionale 350px sidebar, behind `?detailEventId=` URL param): hero EventType badge + key/value mono + `<pre>` payload truncated 300 char + "Show full payload" modal.
- **Filter chips bar**: derivata da `useEventTypes()` query (`/api/v1/admin/events/types`) con count.
- **Empty state**: dot `.live-pill` ramato + "In ascolto… (0 eventi in 90 giorni — verifica i filtri)".
- **Error state**: banner inline + retry button (sotto admin-panel head).

### 5.5 Test

| Test | Coverage |
|---|---|
| `LiveEventLog.test.tsx` | render con backfill mock, append da SSE mock, pause/resume, filter applicato, empty state, error state, virtualization (10/1000 visible) |
| `use-live-events.test.ts` | backfill+attach flow, MAX_BUFFER bounded, Last-Event-ID set su reconnect, exponential backoff, cleanup on unmount |
| `parse-event-message.test.ts` | mapping eventType → level, entity color extraction per `aggregateType`, fallback default |

## 6. Architettura Fase 4 — Integrazione tab "events"

### 6.1 `monitor/page.tsx` update

Aggiungere voce `events` al `TABS` array (linea ~48-66 di `page.tsx`):

```tsx
{ id: 'events', label: 'Events', href: '/admin/monitor?tab=events', icon: <Radio />, badge: 'live' }
```

(Icon `Radio` da `lucide-react`, badge `live` mostrato come dot ramato pulsante via Tailwind `animate-pulse`.)

Aggiungere `case 'events'` al `renderTabContent` switch:

```tsx
case 'events':
  return (
    <Suspense fallback={<TabSkeleton />}>
      <LiveEventLog height="70vh" />
    </Suspense>
  );
```

### 6.2 Banda di sezione

La banda viene gestita in `monitor/layout.tsx` (creata in Fase 2) — il tab events non aggiunge nulla di custom alla banda.

### 6.3 E2E smoke test

`apps/web/e2e/admin/monitor-events-tab.spec.ts`:

1. Login come admin → navigate `/admin/monitor?tab=events`
2. Verify tab `events` `aria-selected="true"`
3. Verify badge `live` visible
4. Verify `LiveEventLog` panel renders
5. Verify at least 1 row appears entro 5s (richiede seed DB con almeno 1 evento outbox negli ultimi 90gg — usa fixture `kb.doc.indexed`)
6. Verify filter chip `agent.created` click filtra la lista
7. Verify pause/resume toggle funziona

### 6.4 Reuse-ready

In Fase 4 aggiungiamo un'export pubblico da `components/admin/monitor/index.ts`:

```typescript
export { LiveEventLog } from './LiveEventLog';
export type { LiveEventLogProps, DomainEventDto } from './live-event-types';
```

Così quando F4.2/F4.3/F4.4 vorranno il componente in C1 Infra (o altri), import diretto senza fork.

### 6.5 Docs update

- `admin-mockups/design_handoff_admin/ADMIN_AUDIT.md`: A8 stato `🚧 in corso #1718` → `✅ PR #...` (riga 176).
- `admin-mockups/design_handoff_admin/SCREENS.md`: A8 entry tracciato con PR mergiata.
- Memory: `project_sp5_admin_f4_1_monitor_wip.md` → `project_sp5_admin_f4_1_monitor_done.md` (rename + sintesi finale).
- MEMORY.md: spostare F4.1 da WIP a Executed Plans.

## 7. Acceptance criteria panel-grade (Wiegers)

Ogni fase ha criteri verificabili (osservabili) — non opinabili.

### Fase 1 BE

- ✅ `GetAdminEventsQueryHandler` ritorna `DomainEventDto[]` ordinato `LoggedAt DESC` con paginazione cursor.
- ✅ Filtri `EventTypes`, `AggregateTypes`, `UserId`, `AggregateId` applicabili in qualsiasi combinazione (incluso vuoto).
- ✅ Retention 90gg applicata (`LoggedAt >= UtcNow - 90 days`).
- ✅ SSE `/api/v1/admin/events/stream` emette evento entro **1 secondo** dal `SaveChangesAsync` commit dell'outbox (test integration con misurazione latency).
- ✅ Heartbeat `:hb\n\n` emesso ogni 15s.
- ✅ `Last-Event-ID` header su reconnect → backfill da quel cursore + attach stream, NO duplicati (test integration).
- ✅ Gate `RequireAdminSession()`: 401 senza session, 403 con session ruolo `user`/`editor`/`creator`, 200 con `admin`/`superadmin`.
- ✅ Unit test coverage ≥ 90% del nuovo handler, integration test verde su Testcontainers Postgres.
- ✅ 0 regression test `Administration` BC esistenti.

### Fase 2 FE re-skin

- ✅ `pnpm typecheck` 0 errori.
- ✅ `pnpm lint:tokens` 0 violazioni.
- ✅ ESLint `local/no-hardcoded-color-utility` 0 errori sui file modificati.
- ✅ `pnpm test:admin-dashboard` 558+/558 verdi (gate F3-FU-3 stesso).
- ✅ 0 file con neutral palette hardcoded (`bg-slate-*`, `bg-gray-*`, `bg-zinc-*`, `bg-neutral-*`, `bg-stone-*`, `bg-white`, `bg-black`) nelle 12 tab.
- ✅ Banda `MonitorTopBand` presente in `monitor/layout.tsx` con h1 unico "Monitor" + crumbs.
- ✅ Tutte le tab perdono l'h1 locale "Monitor" (se presente) — assorbito dalla banda.
- ✅ Behavior SSE/refetch byte-identico — test esistenti verdi.
- ✅ Manual designer review approvata (visual gate sostituto).

### Fase 3 LiveEventLog component

- ✅ Componente render con `initialLimit=100` mostra esattamente 100 row da backfill.
- ✅ SSE event ricevuto → row appare in cima entro 100ms (test con mock EventSource).
- ✅ `MAX_BUFFER=1000` rispettato — row 1001+ scartate.
- ✅ Pause: SSE chiuso, append fermo. Resume: SSE riaperto, append riprende con `Last-Event-ID` corretto.
- ✅ Reconnect dopo `error`: backoff `[1s, 2s, 4s, 8s, max 30s]`.
- ✅ Cleanup on unmount: `EventSource.close()` chiamato, no memory leak.
- ✅ Filter chips: click su `agent.created` filtra in-memory + invia query updated alla rotta SSE.
- ✅ Empty state ramata + "in ascolto" se 0 eventi.
- ✅ Error state con retry funzionante.
- ✅ Virtualization: 1000 eventi in `events[]` render solo ~30 row visible (resto unmounted).
- ✅ Unit test coverage ≥ 85% (`LiveEventLog.test.tsx`, `use-live-events.test.ts`, `parse-event-message.test.ts`).
- ✅ a11y: `role="log" aria-live="polite"`, focus management corretto su filter chips.

### Fase 4 Integrazione

- ✅ Tab `events` (13°) visibile in `monitor/page.tsx` `TABS` array.
- ✅ Badge `live` con dot pulsante visibile.
- ✅ Navigate `/admin/monitor?tab=events` rende `<LiveEventLog />`.
- ✅ E2E smoke test verde su CI (`admin/monitor-events-tab.spec.ts`).
- ✅ Export pubblico `components/admin/monitor/index.ts` esporta `LiveEventLog`.
- ✅ Docs updated: ADMIN_AUDIT.md, SCREENS.md, memory.

## 8. Rischi e mitigation (Nygard)

| # | Rischio | Probabilità | Impatto | Mitigation |
|---|---------|-------------|---------|------------|
| R1 | Backpressure SSE: 1000 eventi/s in burst saturano il channel | bassa (dev/staging volume), media in prod | events drop visibili nel mockup come "lost events" | `Channel<T>` bounded con `drop-oldest`; metric `meepleai_admin_sse_events_dropped_total`; log warning ogni 1000 drop |
| R2 | EventSource Last-Event-ID non rispettato dal browser (proxy stripping) | media (Cloudflare/staging Traefik) | gap eventi on reconnect | fallback query param `?lastEventId=` se header missing; client tracking ultimo `id` ricevuto + manual reconcile |
| R3 | Security: eventi sensibili (es. `user.password_changed` ipotetico futuro) leakati ad admin generico | bassa (eventi attuali non sensibili), futura crescita | privacy/compliance | filtro `RestrictedEventTypes` config-driven; default empty in F4.1; promotion a superadmin-only tracked separata D-3 §10 |
| R4 | LiveEventLog component cresce >2000 LOC con tutti i fix | bassa | reviewability PR 3/4 | split tasks granulari (max 100 LOC per task); spec-review per ogni task via subagent |
| R5 | Drift mockup → impl (sidebar inventata già nota) | confermata (sidebar mockup ≠ real) | nessuno (sidebar non integrata) | nota in §1 mockup riferimento; integrazione FE usa `AdminSideDrawer` data-driven F0a, NON mockup |
| R6 | Re-skin Fase 2 rompe SSE behavior in qualche tab (Nygard) | bassa (gate test esistenti) | regression in `containers`/`queue`/`logs` | regola "className/CSS-only" rigida; 60+ test esistenti gate; subagent-driven con quality-review per ogni task |

## 9. Decisioni residue (D-residue)

| # | Decisione | Stato | Da risolvere in |
|---|-----------|-------|-----------------|
| D-1 | LiveEventLog vive in `components/admin/monitor/` (primo consumer) o subito `components/ui/live-event-log/` (trasversale anticipato)? | **`components/admin/monitor/` (YAGNI)** — promote a `ui/` solo quando emerge il 2° consumer (es. C1 Infra ondata F4.4) | Fase 3 implementation |
| D-2 | `RestrictedEventTypes` lista nera per security events: empty in F4.1, ma config-driven o hardcoded? | **config-driven** via `IConfiguration["AdminEvents:RestrictedEventTypes"]` (default empty array) — permette hotfix senza redeploy | Fase 1 implementation |
| D-3 | Promotion `RestrictedEventTypes` a superadmin-only (vs nascosti) | **deferred** a track sicurezza separato post F4.1; in F4.1 lista vuota = nessun restriction | Track sicurezza |
| D-4 | `MAX_BUFFER` client-side: 1000 events sufficient? | **sì** (≈10MB DOM weight max); valutare reducer su volume reali post F4.1 launch | Post-F4.1 osservazione |
| D-5 | SSE timeout/heartbeat compatibile con Traefik staging (`compose.traefik.yml`)? | **verificare in Fase 1**: leggere `infra/traefik/dynamic.yml` per timeout default; potrebbe servire `traefik.http.middlewares.sse-timeout.headers.responseTimeout=0` | Fase 1 acceptance |
| D-6 | Permessi `RestrictedEventTypes` per ruolo `editor`/`creator` (oggi gate `RequireAdminSession()` esclude editor/creator)? | **fuori scope F4.1** — gate esistente è binario admin/non-admin; granularità per ruolo è D-residue track sicurezza | Track sicurezza |

## 10. Riferimenti

- Spec consolidamento: `docs/superpowers/specs/2026-05-24-sp5-admin-console-consolidation-design.md` §9 (F4 Ondata Ops)
- Audit gap analysis: `admin-mockups/design_handoff_admin/ADMIN_AUDIT.md` §5-7
- Mockup riferimento: `admin-mockups/design_handoff_admin/admin/sp5-admin-monitor.html` (KPI strip + filter bar + LiveEventLog panel)
- Pattern LiveEventLog CSS: `admin-mockups/design_handoff_admin/admin/sp5-admin-infra.html:197-215` (classes) + `:457-489` (sample rows)
- Pattern band + admin-top: `admin-mockups/design_handoff_admin/admin/sp5-admin-alerts.html`
- Pattern hub tabs: `admin-mockups/design_handoff_admin/admin/sp5-admin-kb-subnav.html`
- BE Outbox entity: `apps/api/src/Api/Infrastructure/Entities/DomainEventLog/DomainEventLog.cs`
- BE Event registry: `apps/api/src/Api/Infrastructure/DomainEventLog/EventTypeRegistry.cs`
- BE User-scoped handler: `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/ActivityFeed/GetActivityFeedQueryHandler.cs`
- BE HTTP endpoint: `apps/api/src/Api/Routing/ActivityFeedEndpoints.cs`
- FE hub monitor: `apps/web/src/app/admin/(dashboard)/monitor/page.tsx`
- FE LogsTab (Loki, NON LiveEventLog): `apps/web/src/app/admin/(dashboard)/monitor/LogsTab.tsx`
- Predecessor pattern re-skin: PR #1664 (F3-FU-3 KB tool-pages)
- Predecessor pattern sub-nav: PR #1649 (F3 KB Explorer)
- Track sicurezza S1 outbox: PR #1532 (Issue #661)
- F0a nav config + filter ruolo: PR #1496
- Tracking memory: `~/.claude/.../memory/project_sp5_admin_f4_1_monitor_wip.md`
