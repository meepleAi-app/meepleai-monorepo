# SP5 F4-C5 — `/admin/business` Budget & Cost (NEW page completion)

**Issue**: [#1838](https://github.com/meepleAi-app/meepleai-monorepo/issues/1838) · **Parent epic**: [#1833](https://github.com/meepleAi-app/meepleai-monorepo/issues/1833) F4 Ondata Ops (closes the epic) · **Date**: 2026-06-05 · **Branch**: `feature/issue-1838-c5-budget-cost`

## Goal

Sostituire i 4 `BudgetPlaceholderPanel` della pagina `/admin/business` (già scaffolded) con componenti reali allineati al mockup `admin-mockups/design_handoff_admin/admin/sp5-admin-budget.html` (619 righe). Chiude l'epic F4 (6/6 sub-task).

4 componenti chiave dal mockup:

1. **BudgetKpiStrip** (rebuild) — 4 KPI: Spesa oggi · Spesa mese (con progress bar) · Budget residuo · Proiezione fine mese
2. **CostStackedArea** — stacked area chart 30gg cost per provider con cap line
3. **FeatureCostTable** — tabella per-feature con drill provider
4. **CostSimulator** — what-if calculator (RPM% + model switch)
5. **BudgetGauge** — gauge spent vs limit mensile + ETA exhaust + "Imposta budget" modal

## Decisioni utente confermate (2026-06-05)

| ID | Decisione | Impatto |
|---|---|---|
| D1 | CostStackedArea = **LedgerEntry aggregati SQL** (provider+date group-by) | BC nativo, persistente, già popolato da `TokenUsageLedgerEventHandler` |
| D2 | Budget = **AppBudget nuova entity globale** (1 row per environment) | Pattern simile a `AlertChannel`; nuova migration; non riusa `UserBudget` per-user |

## Context — AS-IS

### Backend già esistente

- **BC `BusinessSimulations`** completo:
  - Entities: `LedgerEntry`, `CostScenario`, `ResourceForecast`, `UserBudget`
  - Value Object: `Money`
  - Repositories: `LedgerEntryRepository`, `CostScenarioRepository`, `ResourceForecastRepository`, `UserBudgetRepository`
- **Queries esistenti**: `GetLedgerSummaryQuery`, `EstimateAgentCostQuery`, `GetAppUsageStatsQuery`, `ExportLedgerQuery`, `GetLedgerEntriesQuery`
- **Commands esistenti**: ledger CRUD, save/delete scenario, save/delete forecast
- **Services**: `LedgerTrackingService`, `TokenUsageLedgerEventHandler`, `InfrastructureCostTrackingJob`, `MonthlyLedgerReportJob`
- **Endpoints esistenti**:
  - `GET /api/v1/admin/business/usage` (`AdminBusinessStatsEndpoints`) — app usage DAU/MAU
  - `POST /api/v1/admin/cost-calculator/estimate` (`CostCalculatorEndpoints`) — agent cost
  - `POST /api/v1/admin/cost-calculator/scenarios` (CRUD scenari)

### Backend mancante

- `AppBudget` aggregate (entity + repo + migration) — 1 row globale
- `GetAppBudgetQuery` + `UpsertAppBudgetCommand` (admin upserts)
- `GET /api/v1/admin/budget` (current limit + spent)
- `PUT /api/v1/admin/budget` (upsert limit + alert thresholds)
- `GET /api/v1/admin/business/breakdown?range=30d` (cost per provider+date aggregated from `LedgerEntry`)
- `GET /api/v1/admin/business/per-feature` (cost per `LedgerCategory` aggregated)

### Frontend già esistente

- `business/page.tsx` con hero header + `BudgetKpiStrip` (esistente — da rebuild) + 4 `BudgetPlaceholderPanel`
- Nav config già wired (no work)

### Frontend mancante

- BudgetKpiStrip rebuild 4-KPI mockup (con sparkline svg)
- 4 componenti reali (`CostStackedArea` + `FeatureCostTable` + `CostSimulator` + `BudgetGauge`)
- Set-budget modal + Export CSV button + range select (7/30/90/year)
- Hook `useBudget` + `useCostBreakdown` + `useFeatureCosts`
- API clients (`budget.api.ts` + `business-cost.api.ts`)
- Zod schemas

## Scope — TO-BE

### Componenti BE da creare

```
apps/api/src/Api/
├── BoundedContexts/BusinessSimulations/
│   ├── Domain/
│   │   ├── Aggregates/AppBudget/
│   │   │   └── AppBudget.cs                          [NEW aggregate]
│   │   └── Repositories/IAppBudgetRepository.cs      [NEW]
│   ├── Application/
│   │   ├── Commands/AppBudget/UpsertAppBudgetCommand.cs + Handler + Validator
│   │   └── Queries/AppBudget/GetAppBudgetQuery.cs + Handler
│   ├── Application/Queries/CostBreakdown/
│   │   ├── GetCostBreakdownByProviderQuery.cs + Handler (30d/7d/90d ranges)
│   │   └── GetCostBreakdownByFeatureQuery.cs + Handler (per LedgerCategory)
│   └── Infrastructure/
│       ├── Persistence/AppBudgetRepository.cs        [NEW]
│       └── Entities/AppBudgetEntity.cs               [NEW]
└── Routing/
    ├── AdminBudgetEndpoints.cs                       [NEW: /admin/budget GET+PUT]
    └── AdminBusinessStatsEndpoints.cs                [EXTEND: + /breakdown + /per-feature]

apps/api/src/Api/Infrastructure/Migrations/
└── 2026MMDD_AddAppBudget.cs                          [NEW migration]
```

### Componenti FE da creare/modificare

```
apps/web/src/
├── app/admin/(dashboard)/business/
│   └── page.tsx                                      [REWRITE: 4 placeholders → real]
├── components/admin/business/
│   ├── BudgetKpiStrip.tsx                            [REBUILD: 4 KPI mockup]
│   ├── BudgetPlaceholderPanel.tsx                    [DELETE]
│   ├── CostStackedArea.tsx                           [NEW: Recharts AreaChart]
│   ├── FeatureCostTable.tsx                          [NEW: DataTable + drill]
│   ├── CostSimulator.tsx                             [NEW: form + readout]
│   ├── BudgetGauge.tsx                               [NEW: SVG radial]
│   └── SetBudgetDialog.tsx                           [NEW: modal]
├── hooks/
│   ├── useBudget.ts                                  [NEW]
│   ├── useCostBreakdown.ts                           [NEW]
│   └── useFeatureCosts.ts                            [NEW]
└── lib/api/
    ├── budget.api.ts                                 [NEW]
    ├── business-cost.api.ts                          [NEW]
    └── schemas/
        ├── budget.schemas.ts                         [NEW]
        └── business-cost.schemas.ts                  [NEW]
```

## Acceptance Criteria (Given/When/Then)

### Scenario A — KPI strip 4 card

```
Given AppBudget limit = $1200, spesa mese = $284, spesa oggi = $12.40
When admin apre /admin/business
Then KPI strip mostra 4 card:
  - "Spesa oggi": $12.40 con sparkline 30gg + trend "▲ 8.6% vs ieri"
  - "Spesa mese": $284 / $1.200 con progress bar 23.6% + label "giorno 22/31"
  - "Budget residuo": $916.00 + label "9 giorni residui · $101.7/d capacità"
  - "Proiezione fine mese": $890.00 + delta "▼ -$310 sotto budget · 74.2%"
And la card "Spesa mese" border-left colorata entity-event (rose)
```

### Scenario B — Set budget mensile

```
Given admin click "+ Imposta budget"
When modal apre
Then form mostra: amount input (USD) + threshold alert (80/95/100%)
And submit → PUT /api/v1/admin/budget con RowVersion (409 ConflictException su concurrent edit)
And KPI strip refresh con nuovo limite
```

### Scenario C — CostStackedArea 30gg

```
Given LedgerEntry con costi 30gg per 4 provider (DeepSeek, OpenRouter, OpenAI, Anthropic)
When admin guarda CostStackedArea
Then chart mostra stacked area 30gg con 4 stack ordinati per costo totale
And Y axis $0-$16 (max scaled), X axis 30 date label sample
And cap line orizzontale a $40/d (daily budget / 30)
And tooltip per giorno mostra breakdown per provider con totale
```

### Scenario D — FeatureCostTable drill

```
Given LedgerEntry con LedgerCategory in {rag-query, embedding, image-gen, chat, pdf-ingest, ...}
When admin guarda FeatureCostTable
Then tabella mostra ogni feature con: nome · cost totale 30gg · % del totale · trend
And click row expand → breakdown per provider (DeepSeek $80, OpenRouter $50, ...)
And sortable per cost desc
```

### Scenario E — CostSimulator what-if

```
Given default: 1000 RPM, modello "DeepSeek V3"
When admin imposta "2000 RPM" + cambia a "Claude Sonnet 4.6"
Then readout mostra:
  - Stimato $X/mese (font 44px)
  - 3 alt rows: vs baseline (delta colorato), vs budget mensile (warning se >100%), impatto giornaliero
And se >budget mostra impact dot rosso + warning text
```

### Scenario F — BudgetGauge ETA exhaust

```
Given limit $1.200, spent $284 (giorno 22/31)
When admin guarda BudgetGauge
Then gauge SVG mostra arc riempito 23.6%
And label centrale "$284 / $1.200"
And sub-label "ETA exhaust: 28/06" (calcolata da spent_rate * giorni_residui)
And colore arc: verde <80% · arancione 80-95% · rosso >95%
```

### Scenario G — Range select 7/30/90/year

```
Given default range 30 giorni
When admin cambia select a "90 giorni"
Then CostStackedArea + FeatureCostTable + KPI "Spesa mese" si refresha con dati 90gg
And URL query persiste ?range=90d
```

### Scenario H — Export CSV ledger

```
Given range 30 giorni
When admin click "⤓ Export CSV"
Then download CSV ledger_2026-05-06_to_2026-06-05.csv con tutte le LedgerEntry
And riusa esistente ExportLedgerQuery / endpoint
```

### Scenario I — Empty state (no data)

```
Given AppBudget non configurato (prima visita)
When admin guarda page
Then KPI mostra placeholder "—" con tooltip "Imposta budget per vedere KPI"
And CostStackedArea mostra empty state SVG illustrativo
And CTA hero "Imposta budget" highlighted
```

## Effort estimate

| Phase | Component | Effort |
|---|---|---|
| BE | AppBudget aggregate + entity + migration | ~2h |
| BE | Budget endpoints (GET + PUT) | ~1.5h |
| BE | Cost breakdown endpoints (by provider + by feature) | ~2.5h |
| BE | Unit + integration tests | ~2.5h |
| FE | API clients + 3 hooks foundation | ~1.5h |
| FE | BudgetKpiStrip rebuild | ~2h |
| FE | CostStackedArea (Recharts) | ~3h |
| FE | FeatureCostTable + drill | ~2.5h |
| FE | CostSimulator | ~2h |
| FE | BudgetGauge + SetBudgetDialog | ~2h |
| FE | page wiring + Export CSV + range select | ~1h |
| FE | Unit + E2E tests | ~2h |
| Misc | DoD compliance | ~1h |
| **Total** | | **~25.5h** |

## DoD checklist (#1833 epic — chiude la wave)

- [ ] 5 componenti FE (KPI strip + 4 panels + modal) sostituiscono i placeholder
- [ ] `/admin/business` accessibile senza 404
- [ ] Token semantici only — `pnpm lint` 0 errori
- [ ] Entity utilities applicate (border-l-entity-agent/event/toolkit/chat per i KPI)
- [ ] A11y: 0 axe violations (manual + Playwright)
- [ ] BE unit + integration tests verdi
- [ ] FE unit (Vitest) + smoke E2E (Playwright)
- [ ] Mockup → preview side-by-side review manuale OK
- [ ] PR linked a #1838 + epic #1833
- [ ] Code review subagent passed
- [ ] **Race-safe merge**: verificare `headRefOid` GitHub == local HEAD prima di `gh pr merge --squash` (lezione #1840)

## References

- Mockup: `admin-mockups/design_handoff_admin/admin/sp5-admin-budget.html` (619 righe)
- Spec consolidamento SP5: `docs/superpowers/specs/2026-05-24-sp5-admin-console-consolidation-design.md` §5 (C5 row)
- BC esistente: `apps/api/src/Api/BoundedContexts/BusinessSimulations/`
- Pattern KPI sparkline: `apps/web/src/app/admin/(dashboard)/monitor/containers/KPISparklineStrip.tsx` (#1837)
- Pattern AppBudget: `AlertChannel` aggregate (#1840) — 1 row per environment + RowVersion concurrency
- Recharts già disponibile in `package.json` (`recharts: ^2.x`)
- Race lesson: `memory/feedback_gh_pr_merge_squash_race.md`
- Epic F4: [#1833](https://github.com/meepleAi-app/meepleai-monorepo/issues/1833) — chiusura epic dopo questo merge

Auto-generated 2026-06-05 per branch `feature/issue-1838-c5-budget-cost`.
