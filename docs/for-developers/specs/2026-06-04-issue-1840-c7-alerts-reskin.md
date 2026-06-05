# SP5 F4-C7 — `/admin/monitor?tab=alerts` re-skin + completion (AlertRulesTable + Activity Feed + TestAlert + Canali drawer)

**Issue**: [#1840](https://github.com/meepleAi-app/meepleai-monorepo/issues/1840) · **Parent epic**: [#1833](https://github.com/meepleAi-app/meepleai-monorepo/issues/1833) F4 Ondata Ops · **Date**: 2026-06-04 · **Branch**: `feature/issue-1840-c7-alerts`

## Goal

Completare il modulo Alerting del tab `/admin/monitor?tab=alerts` allineandolo al mockup `admin-mockups/design_handoff_admin/admin/sp5-admin-alerts.html` (680 righe). Il modulo BE+FE esiste già in forma embrionale (TODO esplicito in `AlertsTab.tsx:121`) e va portato a feature-complete.

Sei componenti chiave dal mockup:

1. **KPI strip** — 3 KPI (Regole attive · Alert oggi · Canali configurati) con sparkline SVG
2. **AlertRulesTable** — tabella 7 colonne (Regola · Metrica · Condizione · Finestra · Severità · Canale · Attiva · Azioni)
3. **MetricSelector** — dropdown dinamico popolato da Prometheus `/api/v1/label/__name__/values`
4. **ChannelChip** — multi-select Email + Slack (PagerDuty rimosso da scope)
5. **AlertActivityFeed** — feed live SSE filtrato per `eventTypes=alert.fired,alert.resolved`
6. **TestAlert** — azione per regola: POST `/alert-rules/{id}/test?mode=dryRun|live`
7. **Canali drawer** — slide-over per config Email + Slack (token + test-connection)

## Decisioni utente confermate (2026-06-04)

| ID | Decisione | Impatto |
|---|---|---|
| D1 | Channels in scope = **Email + Slack full**, PagerDuty rimosso | -2h vs full3, +3h vs stub |
| D2 | MetricSelector = **dynamic Prometheus** (`/api/v1/label/__name__/values`) | +3h vs hardcoded |
| D3 | TestAlert = **dryRun default + `?mode=live` opt-in** | safe-by-default semantics |
| D4 | Canali config = **drawer in-scope minimal** | +3h vs out-of-scope |

**Effort rivisto**: ~27h (vs 16h iniziali) — giustificato dalle 4 decisioni.

## Context — AS-IS

### Backend già esistente

- `AlertRule` aggregate (`Domain/Aggregates/AlertRule/`) — value objects `AlertDuration`, `AlertThreshold`, `AlertSeverity`
- `AlertConfiguration` aggregate (`Domain/Aggregates/AlertConfiguration/`)
- `Alert` entity (`Domain/Entities/Alert.cs`)
- Repositories: `AlertRepository`, `AlertConfigurationRepository`, `AlertRuleRepository`
- Endpoints: `AlertConfigEndpoints.cs` — CRUD `/api/v1/admin/alert-rules` (GET all, GET by id, POST, PUT, DELETE, toggle)
- `AdminEventsEndpoints.cs` — SSE `/api/v1/admin/events/stream` con filter `?eventTypes=a,b` (riusabile)
- `EventTypeRegistry` — opt-in alias registry (NO alert aliases attualmente)
- Prometheus config in `appsettings.json` (`BaseUrl=http://prometheus:9090`)

### Backend mancante

- `AlertFiredEvent` + `AlertResolvedEvent` domain events
- EventTypeRegistry aliases `alert.fired` / `alert.resolved`
- `POST /api/v1/admin/alert-rules/{id}/test?mode=dryRun|live` endpoint
- `ISlackWebhookClient` + DI + secrets infrastructure
- `GET /api/v1/admin/alert-channels` + `PUT /api/v1/admin/alert-channels/{type}` endpoints
- `POST /api/v1/admin/alert-channels/{type}/test-connection`
- `GET /api/v1/admin/metrics/labels` (Prometheus passthrough con cache 60s)
- Alert evaluation logic che pubblica `AlertFiredEvent`/`AlertResolvedEvent` (decisione: estendere logica esistente se presente, altrimenti stub per now con manual test trigger)

### Frontend già esistente

- `/admin/monitor?tab=alerts` wired in `monitor/page.tsx:51`
- `AlertsTab.tsx` con `AlertsBanner` + `AlertRuleList` + `CreateAlertRuleDialog`
- `alertRulesApi` client (`lib/api/alert-rules.api.ts`) — `getAll`/`toggle`/`delete`/`create`
- `AlertHistoryTab.tsx` (separato in tab `?tab=history`, no SSE)
- TODO esplicito linea 121: `// TODO #1840 C7: wire onTestAlert once POST /alert-rules/{id}/test is implemented`

### Frontend mancante

- KPI strip 3 card mockup-style con sparkline
- Tabella mockup 6 colonne (attualmente solo 2-3 colonne semplici)
- `MetricSelector` dropdown con search + Prometheus fetch
- `ChannelChip` multi-select con status indicator
- `AlertActivityFeed` componente SSE-driven
- `TestAlertButton` row action
- `CanaliDrawer` slide-over
- `useLiveEvents` filter `eventTypes=alert.fired,alert.resolved`

## Scope — TO-BE

### Componenti BE da creare

```
apps/api/src/Api/
├── BoundedContexts/Administration/
│   ├── Domain/
│   │   ├── Events/
│   │   │   ├── AlertFiredEvent.cs                    [NEW]
│   │   │   └── AlertResolvedEvent.cs                 [NEW]
│   │   ├── Aggregates/AlertChannel/
│   │   │   ├── AlertChannel.cs                       [NEW]
│   │   │   └── AlertChannelType.cs                   [NEW enum: Email, Slack]
│   ├── Application/
│   │   ├── Commands/AlertRules/
│   │   │   └── TestAlertRuleCommand.cs               [NEW]
│   │   ├── Commands/AlertChannels/
│   │   │   ├── UpsertAlertChannelCommand.cs          [NEW]
│   │   │   └── TestAlertChannelConnectionCommand.cs  [NEW]
│   │   └── Queries/AlertChannels/
│   │       └── GetAllAlertChannelsQuery.cs           [NEW]
│   ├── Application/Queries/Metrics/
│   │   └── GetPrometheusMetricLabelsQuery.cs         [NEW]
│   └── Infrastructure/
│       ├── External/
│       │   ├── ISlackWebhookClient.cs                [NEW]
│       │   ├── SlackWebhookClient.cs                 [NEW]
│       │   └── PrometheusLabelsClient.cs             [NEW]
│       └── Persistence/
│           └── AlertChannelRepository.cs             [NEW]
└── Routing/
    ├── AlertConfigEndpoints.cs                       [EXTEND: + /{id}/test]
    ├── AlertChannelsEndpoints.cs                     [NEW]
    └── AdminMetricsEndpoints.cs                      [NEW: /metrics/labels]

apps/api/src/Api/Infrastructure/DomainEventLog/
└── EventTypeRegistry.cs                              [EXTEND: + alert.fired/resolved]

apps/api/src/Api/Infrastructure/Migrations/
└── 20260605_AddAlertChannels.cs                      [NEW migration]

infra/secrets/
├── slack.secret.example                              [NEW template]
└── slack.secret                                      [NEW placeholder, .gitignored]
```

### Componenti FE da creare/modificare

```
apps/web/src/
├── app/admin/(dashboard)/monitor/
│   ├── AlertsTab.tsx                                 [MODIFY: KPI strip + drawer trigger + TestAlert wiring]
│   └── CreateAlertRuleDialog.tsx                     [REBUILD: MetricSelector + operator + ChannelChip]
├── components/admin/alert-rules/
│   ├── AlertRuleList.tsx                             [REBUILD: 7-column mockup table]
│   ├── AlertKpiStrip.tsx                             [NEW]
│   ├── AlertActivityFeed.tsx                         [NEW]
│   ├── MetricSelector.tsx                            [NEW]
│   ├── ChannelChip.tsx                               [NEW]
│   ├── TestAlertButton.tsx                           [NEW]
│   └── CanaliDrawer.tsx                              [NEW]
├── hooks/
│   ├── useAlertKpis.ts                               [NEW]
│   ├── usePrometheusMetricLabels.ts                  [NEW: cache 5min]
│   └── useAlertChannels.ts                           [NEW]
└── lib/api/
    ├── alert-rules.api.ts                            [EXTEND: test()]
    ├── alert-channels.api.ts                         [NEW]
    └── prometheus-metrics.api.ts                     [NEW]
```

## Acceptance Criteria (Given/When/Then)

### Scenario A — KPI strip popolata

```
Given 6 regole alert attive, 3 alert oggi, 4 canali configurati
When admin apre /admin/monitor?tab=alerts
Then KPI strip mostra:
  - "Regole attive: 6/7" + sparkline (history 24h)
  - "Alert oggi: 3 · 2 risolti" + trend ▲ vs media 7g
  - "Canali configurati: 4" + breakdown "2 slack · 2 email"
And i valori sono refresh ogni 30s
```

### Scenario B — Tabella regole con 7 colonne

```
Given 6 regole alert configurate
When admin scrolla la tabella
Then ogni riga mostra:
  - Regola: rule-mark icon + nome + id mono
  - Metrica: chip mono con namespace (es. "meepleai_chat_p95_ms")
  - Condizione: "value op threshold" (es. "> 5%")
  - Finestra: durata mono (es. "5m")
  - Severità: status-chip (danger/warning/info)
  - Canale: chip stack (Slack #alerts + Email ops@)
  - Attiva: toggle on/off
  - Azioni: Test · Edit · Delete buttons
And table header sticky on scroll
```

### Scenario C — Crea regola con MetricSelector dinamico

```
Given admin click "+ Nuova regola"
When modale apre
Then form mostra:
  - Input nome (required)
  - MetricSelector dropdown popolato da GET /api/v1/admin/metrics/labels (Prometheus)
  - Operator select (>, ≥, <, ≤, ==, ≠)
  - Threshold value + unit
  - Duration window (mockup default "5m")
  - Severity radio (Info/Warning/Critical)
  - ChannelChip multi-select Email + Slack
  - Description textarea
And preview pill mostra: "WHEN {metric} {op} {value}{unit} FOR {duration} → {channels}"
And submit → POST /alert-rules con body validato + table refresh
```

### Scenario D — Test alert dryRun

```
Given regola "high_error_rate > 5% → Slack #alerts + email ops@"
When admin click "Test" su quella row (dryRun default)
Then POST /api/v1/admin/alert-rules/{id}/test (no ?mode param)
And BE emette AlertFiredEvent con metadata "isDryRun=true"
And AlertActivityFeed mostra row "🧪 TEST · high_error_rate · DRY RUN" entro 2s (via SSE)
And nessuna notifica reale inviata a Slack/Email
And toast success "Test eseguito · canali simulati"
```

### Scenario E — Test alert live (esplicito)

```
Given regola "high_error_rate > 5% → Slack + email"
When admin click "Test live" (CTA secondario con confirm dialog)
Then POST /api/v1/admin/alert-rules/{id}/test?mode=live (con confirm step-up se richiesto)
And BE emette AlertFiredEvent con metadata "isDryRun=false"
And Slack webhook + Email reali inviati
And AlertActivityFeed mostra "🔥 FIRED · LIVE TEST" badge
And toast confirm con link a Slack channel
```

### Scenario F — Activity feed SSE live

```
Given SSE attiva su /admin/events/stream?eventTypes=alert.fired,alert.resolved
When un alert fires (metric supera threshold)
Then AlertActivityFeed aggiunge row in cima con badge "FIRED":
  - timestamp HH:mm:ss
  - rule name
  - metric value vs threshold
And quando alert si risolve: badge "RESOLVED" + delta time "after 12m 34s"
And feed scrolla limitato a 50 row più recenti
And role="log" aria-live="polite"
```

### Scenario G — Canali drawer Slack config

```
Given admin click button "⚙ Canali" header
When drawer slide-over apre da destra
Then mostra 2 tab: Email | Slack
And tab Slack: input webhook URL + channel name + test-connection button
And submit form → PUT /api/v1/admin/alert-channels/slack
And button "Test Connection" → POST /test-connection
  Returns: { status: 'ok' | 'error', message: string }
And UI mostra status pill verde/rosso + last-tested-at timestamp
```

### Scenario H — Channel error fallback

```
Given regola "X → Slack" ma Slack webhook URL invalid/expired
When alert fires (real, non-test)
Then AlertActivityFeed mostra row "FIRED · Channel error: Slack 401"
And ChannelChip in AlertRuleList mostra status rosso + tooltip "Slack disconnected · check /admin/monitor?tab=alerts (Canali)"
And email fallback se configurato come secondario
```

### Scenario I — RowVersion concurrency

```
Given admin A apre edit regola "high_error_rate" (RowVersion=v1)
When admin B salva modifica concorrente (RowVersion bumped to v2)
Then admin A submit fallisce con 409 ConflictException
And UI mostra toast "Conflict: regola modificata da altro admin · ricarica"
And button "Refresh" disponibile
```

## Architecture

### BE — Alert event flow

```
[Existing Alert evaluation logic]
       ↓ raises
AlertFiredEvent / AlertResolvedEvent (IDomainEvent)
       ↓ MediatR Publish
   ├──> DomainEventLogPersistenceHandler (existing) → DB log
   │     (via EventTypeRegistry alias resolution)
   ├──> ChannelDispatchHandler (NEW)
   │     - Reads AlertRule.Channels
   │     - For each channel: IEmailClient / ISlackWebhookClient.SendAsync()
   │     - Skip if event.IsDryRun=true (only logs to feed)
   └──> EventBroadcaster (existing) → SSE clients filtered by eventTypes
```

### BE — TestAlert command flow

```
POST /api/v1/admin/alert-rules/{id}/test?mode=dryRun|live
       ↓
TestAlertRuleCommand(ruleId, mode) → MediatR
       ↓
TestAlertRuleCommandHandler:
  1. Load AlertRule by id (NotFoundException if missing)
  2. Build synthetic metric payload (mock value triggering threshold)
  3. Raise AlertFiredEvent { isDryRun = (mode != "live"), isTest = true }
  4. Wait for ChannelDispatchHandler completion (Task.WhenAll)
  5. Return { dispatchedChannels: [...], errors: [...] }
       ↓
Response: 200 OK { results: [{ channel, status, message }] }
```

### FE — Component tree

```
AlertsTab (existing, modified)
├── AlertsBanner (existing, kept)
├── AlertKpiStrip (NEW)
│   └── 3× KpiCard with SVG sparkline
├── AlertRuleList (REBUILT)
│   └── per row:
│       ├── RuleMark + rule meta
│       ├── MetricChip
│       ├── ConditionCell
│       ├── WindowCell
│       ├── SeverityChip
│       ├── ChannelChipStack
│       ├── ToggleSwitch
│       └── RowActions (TestAlertButton + Edit + Delete)
├── AlertActivityFeed (NEW)
│   └── usesLiveEvents({ eventTypes: ['alert.fired', 'alert.resolved'] })
├── CreateAlertRuleDialog (REBUILT)
│   ├── MetricSelector (NEW, uses usePrometheusMetricLabels)
│   ├── OperatorSelect
│   ├── ThresholdInput + UnitInput
│   ├── DurationInput
│   ├── SeverityRadio
│   ├── ChannelChip (NEW, multi-select)
│   └── PreviewPill
└── CanaliDrawer (NEW, slide-over)
    ├── EmailConfigTab
    └── SlackConfigTab
```

## Edge cases

1. **Prometheus down**: `/metrics/labels` fallback a cache stale o lista hardcoded minima (5 metric note). MetricSelector mostra warning "Prometheus offline · cached labels".
2. **Slack webhook 401**: ChannelDispatchHandler logga `AlertChannelErrorEvent`, ChannelChip aggiorna status rosso. Retry policy: 3 attempts con exp backoff.
3. **Test live senza step-up**: PR si limita a header `X-StepUp-Token` opzionale; se assente, backend procede con audit log severity=Warning. (Strict 2FA fuori scope di #1840, già coperto da S3 #1597.)
4. **Mockup ha 1 disattiva su 7**: scenario "regola disabilitata" deve restare visibile in table con toggle off, ma escluso da KPI "regole attive" count.
5. **AlertActivityFeed durante reconnect**: usa `Last-Event-ID` header per backfill (pattern già implementato in `AdminEventsEndpoints.cs:179`).
6. **Empty state**: 0 regole → empty state CTA "Crea la prima regola" centrato.
7. **Concurrent toggle**: optimistic update con rollback su errore (già implementato in `AlertsTab.tsx:60`).

## Effort estimate

| Phase | Component | Effort |
|---|---|---|
| BE | Slack webhook client + secrets | ~2h |
| BE | Prometheus labels passthrough + cache | ~1.5h |
| BE | AlertFiredEvent/Resolved + EventTypeRegistry + ChannelDispatchHandler | ~3h |
| BE | TestAlertRuleCommand + endpoint | ~2h |
| BE | AlertChannel aggregate + CRUD endpoints + migration | ~3h |
| BE | Unit + integration tests | ~3h |
| FE | AlertKpiStrip + useAlertKpis | ~2h |
| FE | AlertRuleList rebuild (7-col table) | ~3h |
| FE | CreateAlertRuleDialog rebuild (MetricSelector + operator + ChannelChip + preview) | ~3h |
| FE | AlertActivityFeed SSE-wired | ~1.5h |
| FE | TestAlertButton + dryRun/live confirm | ~1h |
| FE | CanaliDrawer (Email + Slack tabs) | ~2h |
| FE | Unit (Vitest) + E2E (Playwright) tests | ~2h |
| Misc | DoD compliance (ESLint tokens + a11y + cleanup) | ~1h |
| **Total** | | **~30h** |

> Nota: stima leggermente sopra ~27h previsti per buffer su Prometheus integration + Slack test-connection complexity.

## DoD checklist (#1833 epic)

- [ ] 6 nuovi/aggiornati componenti FE rispecchiano mockup `sp5-admin-alerts.html`
- [ ] `/admin/monitor?tab=alerts` accessibile senza 404
- [ ] Token semantici only (no `bg-white`, `text-gray-*`, ecc.) — `pnpm lint` = 0 errori
- [ ] Entity utilities dove applicabile (`text-entity-event`, `bg-entity-event/10`)
- [ ] Dark default scoped admin (mockup `data-theme="dark"`)
- [ ] A11y axe: 0 violazioni (manual run + Playwright a11y suite)
- [ ] BE: unit + integration tests verdi (`dotnet test`)
- [ ] FE: unit + E2E tests verdi (`pnpm test && pnpm test:e2e`)
- [ ] Mockup → preview side-by-side review manuale OK
- [ ] PR linked a #1840 + epic #1833
- [ ] Code review subagent passed (no BLOCKERS)

## References

- Mockup: `admin-mockups/design_handoff_admin/admin/sp5-admin-alerts.html` (680 righe)
- Spec consolidamento SP5: `docs/superpowers/specs/2026-05-24-sp5-admin-console-consolidation-design.md` §5 Gruppo C (riga C7), §8 SSE `events/live` gap
- Pattern SSE reference: `apps/api/src/Api/Routing/AdminEventsEndpoints.cs` (F4.1 #1718)
- Pattern KPI sparkline reference: `apps/web/src/components/admin/monitor/KPISparklineStrip.tsx` (#1837 C1 Infra, PR #1872)
- BE foundation reference: `apps/api/src/Api/BoundedContexts/Administration/Domain/Aggregates/AlertRule/` (esistente)
- Track ⊥ Sicurezza S3 strict 2FA: PR #1597 (header `X-StepUp-Token`, fuori scope ma compatibile)
- Epic F4 Ondata Ops: [#1833](https://github.com/meepleAi-app/meepleai-monorepo/issues/1833)

🤖 Spec auto-generated 2026-06-04 per branch `feature/issue-1840-c7-alerts` (monolitico).
