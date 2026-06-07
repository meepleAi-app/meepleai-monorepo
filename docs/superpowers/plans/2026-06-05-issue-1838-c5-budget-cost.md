# Plan: Issue #1838 C5 Budget & Cost (monolithic — closes epic F4)

**Spec**: [`docs/for-developers/specs/2026-06-05-issue-1838-c5-budget-cost.md`](../../for-developers/specs/2026-06-05-issue-1838-c5-budget-cost.md) · **Branch**: `feature/issue-1838-c5-budget-cost` · **Date**: 2026-06-05

## Strategy

Monolithic single-PR (chiude epic F4). 4 fasi: BE → FE → Test → PR. Pattern già consolidato da #1840.

**Race-safe merge** (lezione #1840 incorporata):
```bash
LOCAL_HEAD=$(git rev-parse HEAD)
REMOTE_HEAD=$(gh pr view <num> --json headRefOid --jq '.headRefOid')
[ "$LOCAL_HEAD" = "$REMOTE_HEAD" ] && gh pr merge <num> --squash --delete-branch \
  || { echo "MISMATCH"; exit 1; }
```

## Phase 1 — BE Foundation (~8h)

### 1.1 AppBudget aggregate + migration (~2h)

**Files**:
- `apps/api/src/Api/BoundedContexts/BusinessSimulations/Domain/Aggregates/AppBudget/AppBudget.cs`
- `apps/api/src/Api/BoundedContexts/BusinessSimulations/Domain/Repositories/IAppBudgetRepository.cs`
- `apps/api/src/Api/BoundedContexts/BusinessSimulations/Infrastructure/Persistence/AppBudgetRepository.cs`
- `apps/api/src/Api/Infrastructure/Entities/BusinessSimulations/AppBudgetEntity.cs`
- `apps/api/src/Api/Infrastructure/EntityConfigurations/BusinessSimulations/AppBudgetEntityConfiguration.cs`
- Migration: `dotnet ef migrations add AddAppBudget`

**Schema**:
```sql
CREATE TABLE app_budgets (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  monthly_limit_amount NUMERIC(12,4) NOT NULL,
  monthly_limit_currency VARCHAR(3) NOT NULL DEFAULT 'USD',
  alert_threshold_pct INT NOT NULL DEFAULT 80,         -- 80% warning
  critical_threshold_pct INT NOT NULL DEFAULT 95,      -- 95% critical
  is_enabled BOOLEAN NOT NULL DEFAULT TRUE,
  row_version BYTEA NOT NULL,
  created_at TIMESTAMP NOT NULL,
  updated_at TIMESTAMP NOT NULL,
  created_by VARCHAR(200) NULL,
  updated_by VARCHAR(200) NULL,
  -- Single-row constraint: at most 1 row per environment
  CONSTRAINT app_budgets_singleton CHECK (id = id) -- application-level enforced
);
```

Note: enforcing singleton at app layer (repository checks `GetAsync()` only ever returns at most 1). Pattern simile a `AlertChannel` ma con `Id` Guid invece di string discriminator perché non c'è categoria multipla.

### 1.2 Budget endpoints (~1.5h)

**Files**:
- `apps/api/src/Api/BoundedContexts/BusinessSimulations/Application/Queries/AppBudget/GetAppBudgetQuery.cs + Handler`
- `apps/api/src/Api/BoundedContexts/BusinessSimulations/Application/Commands/AppBudget/UpsertAppBudgetCommand.cs + Handler + Validator`
- `apps/api/src/Api/Routing/AdminBudgetEndpoints.cs`

**Endpoint shape**:
- `GET /api/v1/admin/budget` → `{ limit, currency, alertThresholdPct, criticalThresholdPct, isEnabled, spent: { today, thisMonth, projectedMonthEnd }, daysRemaining, rowVersion }`
- `PUT /api/v1/admin/budget` body `{ limit, currency, alertThresholdPct, criticalThresholdPct, rowVersion? }` → upsert con concurrency check

**Auth**: `.RequireAdminSession()` per-endpoint. **Routing**: invocato su `v1Api` con `MapGroup("/admin/budget")` (relative, NO double prefix — lezione #1840 incorporata).

### 1.3 Cost breakdown endpoints (~2.5h)

**Files**:
- `apps/api/src/Api/BoundedContexts/BusinessSimulations/Application/Queries/CostBreakdown/GetCostBreakdownByProviderQuery.cs + Handler`
- `apps/api/src/Api/BoundedContexts/BusinessSimulations/Application/Queries/CostBreakdown/GetCostBreakdownByFeatureQuery.cs + Handler`
- Extend `AdminBusinessStatsEndpoints.cs` con 2 nuovi endpoint

**Queries**:
- `GetCostBreakdownByProviderQuery(range: '7d'|'30d'|'90d'|'1y')` → aggregato `LedgerEntry` `GROUP BY provider, date_trunc('day', loggedAt)` con totali per provider
- `GetCostBreakdownByFeatureQuery(range)` → `GROUP BY LedgerCategory` con drill per provider via secondary query

**Endpoint**:
- `GET /api/v1/admin/business/breakdown?range=30d` → array `[{ date, providers: [{name, cost}], total }]`
- `GET /api/v1/admin/business/per-feature?range=30d` → array `[{ feature, totalCost, providers: [{name, cost}] }]`

**Cache HybridCache 5min** (`business:cost-breakdown:{range}`).

### 1.4 BE tests (~2h)

- Unit: AppBudgetTests (Create + UpdateLimit + RowVersion preservation)
- Integration Testcontainers: AppBudgetRepository + endpoints E2E
- Acceptance per scenarios A, B, C, D, F (handler-driven, no fixture-only)

**Gate Phase 1**: `dotnet test --filter "BoundedContext=BusinessSimulations"` verde.

## Phase 2 — FE Components (~12h)

### 2.1 API clients + hooks (~1.5h)

- `budget.api.ts` (get + upsert)
- `business-cost.api.ts` (breakdown + per-feature)
- Zod schemas (`budget.schemas.ts`, `business-cost.schemas.ts`)
- Hooks: `useBudget` (mutation upsert + invalidate), `useCostBreakdown` (5min stale), `useFeatureCosts` (5min stale)

### 2.2 BudgetKpiStrip rebuild (~2h)

4 KPI mockup riga 263-300:
- Spesa oggi (sparkline 30d valori da useCostBreakdown)
- Spesa mese (progress bar + spent/limit)
- Budget residuo (USD + days remaining label)
- Proiezione fine mese (under/over budget delta)

### 2.3 CostStackedArea Recharts (~3h)

`<AreaChart>` con `<Area stackId="cost">` per provider. Cap line via `<ReferenceLine>`. Tooltip custom con breakdown. Mockup riga 302-370.

### 2.4 FeatureCostTable + drill (~2.5h)

DataTable primitive + collapsible row expand. Per row: feature name · cost · % · trend sparkline mini. Drill: secondary fetch per `provider` breakdown (cached).

### 2.5 CostSimulator (~2h)

Form: RPM% slider + model select (riusa lista da existing CostCalculator endpoints). Readout: `<EstimateAgentCostQuery>` call con debounce 500ms. Output card stile mockup riga 205-217 (44px display + alt-stack + impact dot).

### 2.6 BudgetGauge + SetBudgetDialog (~2h)

Custom SVG radial 180° arc (no Recharts piccolezza). Colore conditional via threshold. SetBudgetDialog: Form + Zod + `useBudget.upsert` con conflict retry.

### 2.7 Page wiring + Export CSV + range select (~1h)

`business/page.tsx` rewrite: sostituisci 4 placeholders con componenti reali. Header actions: range select (7d/30d/90d/1y URL persisted) + Export CSV (riusa `/admin/ledger/export` esistente) + "Imposta budget" CTA → modal.

### 2.8 FE tests (~2h)

- Vitest unit per ogni componente (snapshot + interaction)
- 1 E2E Playwright smoke: page loads + KPI populated + click "Imposta budget" → modal opens

**Gate Phase 2**: `pnpm typecheck && pnpm lint:tokens && pnpm vitest run src/components/admin/business` tutti verdi.

## Phase 3 — DoD (~1h)

- A11y axe: 0 violations
- Mockup conformity side-by-side
- ESLint `local/no-hardcoded-color-utility` = 0
- `git add` + commit incrementali (1 per sezione)
- Push

## Phase 4 — PR + race-safe merge + epic close

```bash
# 1. Create PR
gh pr create --base main-dev --title "..." --body "..."

# 2. Code review skill
/code-review:code-review <PR-URL>

# 3. Address findings if any

# 4. RACE-SAFE MERGE (lezione #1840):
LOCAL_HEAD=$(git rev-parse HEAD)
REMOTE_HEAD=$(gh pr view <num> --json headRefOid --jq '.headRefOid')
if [ "$LOCAL_HEAD" = "$REMOTE_HEAD" ]; then
  gh pr merge <num> --squash --delete-branch
else
  echo "MISMATCH local=$LOCAL_HEAD remote=$REMOTE_HEAD — wait + retry"
fi

# 5. Verify post-merge state on main-dev (catch any race anyway)
git checkout main-dev && git pull
grep -n "AppBudget\|MapGroup.*business" apps/api/src/Api/...

# 6. Cleanup branch
git branch -D feature/issue-1838-c5-budget-cost
git remote prune origin

# 7. Close epic #1833 if all sub-tasks done
gh issue close 1833 --comment "All 6 F4 sub-tasks merged: ..."
```

## Risk register

| Risk | Mitigation |
|---|---|
| Recharts bundle size | Tree-shake imports; dynamic import per CostStackedArea/Gauge |
| LedgerEntry query slow on 90d | HybridCache 5min + DB index su `(provider, logged_at)` |
| AppBudget singleton race | Application-level lock + `WHERE id IN (...)` upsert via repository transaction |
| Squash merge race (#1840 incident) | `headRefOid` pre-merge check in Phase 4 |
| Provider name normalization | Use enum constants matching `LedgerEntry.Provider` field exactly |

## Out of scope (follow-up potenziali)

- Per-provider budget allocation
- Forecast ML model (linear regression OK per MVP)
- Multi-currency support (USD only per MVP)
- Webhook on budget threshold reached (riusa AlertChannel pattern in future)
- Historical budget editing (only current month editable)
