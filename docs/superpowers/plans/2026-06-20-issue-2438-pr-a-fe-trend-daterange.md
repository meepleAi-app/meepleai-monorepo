# #2438 PR-A — Stats Trend Chart + Date-Range Filter (FE) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a recharts win-rate trend chart and a date-range preset filter to the Play Records statistics view, consuming BE data that is already shipped.

**Architecture:** `StatisticsView` gains (1) a preset segmented control (`Tutto · 30g · 90g · 12 mesi`) held in local state that feeds an optional `{startDate?, endDate?}` range into `usePlayerStatistics`, and (2) a full-width `TrendChart` section below the existing 2-col grid that renders `stats.winRateTrend` via a recharts `AreaChart`. The BE query handler already filters `StartDate`/`EndDate` and already populates `winRateTrend`, so this is FE-only.

**Tech Stack:** Next.js 16 · React 19 · recharts ^3.8.1 · React Query · Vitest + Testing Library · Tailwind semantic tokens

**Reference patterns (read before implementing):**
- recharts area chart + tokens + tooltip + loading/error/empty: `apps/web/src/components/admin/business/CostStackedArea.tsx`
- stats section style (card/border/header/empty/`data-testid`/`aria-label`/i18n): `apps/web/src/components/play-records/stats/MostPlayedBar.tsx`
- the schema: `MonthlyWinRate = {month: "YYYY-MM", winRate: 0..1}`, `PlayerStatistics.winRateTrend?: MonthlyWinRate[]` in `apps/web/src/lib/api/schemas/play-records.schemas.ts:108-132`

---

## File Structure

| File | Responsibility | Action |
|---|---|---|
| `apps/web/src/lib/api/play-records.api.ts` | `getPlayerStatistics` accepts optional range → query params | Modify (lines 164-171) |
| `apps/web/src/lib/domain-hooks/usePlayRecords.ts` | `usePlayerStatistics(range?)` + range in query key | Modify (lines 26-28 keys, 98-105 hook) |
| `apps/web/src/components/play-records/stats/TrendChart.tsx` | recharts win-rate trend section | Create |
| `apps/web/src/components/play-records/stats/__tests__/TrendChart.test.tsx` | TrendChart unit + a11y | Create |
| `apps/web/src/components/play-records/StatisticsView.tsx` | preset filter state + mount TrendChart | Modify |
| `apps/web/src/components/play-records/__tests__/StatisticsView.test.tsx` | preset re-query + TrendChart mount | Modify/Create (check existing) |
| `apps/web/src/locales/{it,en}.json` | i18n keys (verify path via existing `playRecords.stats.*`) | Modify |

**Test command:** `pnpm -C apps/web test -- play-records` · **Typecheck:** `pnpm -C apps/web typecheck`

---

### Task 1: Extend API client with optional date range

**Files:**
- Modify: `apps/web/src/lib/api/play-records.api.ts:164-171`

- [ ] **Step 1: Replace `getPlayerStatistics` with a range-aware version**

```typescript
  /**
   * Get player statistics across all games. Optional date range narrows the
   * window; the BE binds startDate/endDate case-insensitively (#2438).
   */
  async getPlayerStatistics(
    params: { startDate?: string; endDate?: string } = {}
  ): Promise<PlayerStatistics> {
    const search = new URLSearchParams();
    if (params.startDate) search.set('startDate', params.startDate);
    if (params.endDate) search.set('endDate', params.endDate);
    const qs = search.toString();
    const res = await fetch(`${BASE_URL}/statistics${qs ? `?${qs}` : ''}`);
    if (!res.ok) {
      const error = await res.json().catch(() => ({ message: 'Failed to get statistics' }));
      throw new Error(error.message || 'Failed to get statistics');
    }
    return res.json();
  },
```

- [ ] **Step 2: Typecheck**

Run: `pnpm -C apps/web typecheck`
Expected: PASS (no callers break — the param is optional with a default).

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/lib/api/play-records.api.ts
git commit -m "feat(play-records): #2438 getPlayerStatistics accepts date range"
```

---

### Task 2: Range-aware `usePlayerStatistics` hook

**Files:**
- Modify: `apps/web/src/lib/domain-hooks/usePlayRecords.ts` (keys ~line 28, hook ~lines 98-105)

- [ ] **Step 1: Define the range type + extend the query key**

At the top of the file (near the other type imports) add:

```typescript
export type StatsRange = { startDate?: string; endDate?: string };
```

Change the `statistics` key (line 28) from:
```typescript
  statistics: () => [...playRecordsKeys.all, 'statistics'] as const,
```
to:
```typescript
  statistics: (range?: StatsRange) =>
    [...playRecordsKeys.all, 'statistics', range?.startDate ?? null, range?.endDate ?? null] as const,
```

- [ ] **Step 2: Extend the hook (lines 98-105)**

```typescript
export function usePlayerStatistics(range?: StatsRange) {
  return useQuery({
    queryKey: playRecordsKeys.statistics(range),
    queryFn: () => playRecordsApi.getPlayerStatistics(range ?? {}),
    staleTime: 5 * 60 * 1000, // 5 minutes
    retry: false,
  });
}
```

- [ ] **Step 3: Typecheck**

Run: `pnpm -C apps/web typecheck`
Expected: PASS — `usePlayerStatistics()` with no arg still valid; `statistics()` key callers unaffected (optional param). If any other call site references `playRecordsKeys.statistics()` for invalidation, it still resolves (range optional).

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/lib/domain-hooks/usePlayRecords.ts
git commit -m "feat(play-records): #2438 usePlayerStatistics accepts date range"
```

---

### Task 3: `TrendChart` component (recharts area)

**Files:**
- Create: `apps/web/src/components/play-records/stats/TrendChart.tsx`

Follow `CostStackedArea.tsx` for the recharts wiring and `MostPlayedBar.tsx` for the section shell (card/border/header/empty/`data-testid`/`aria-label`/i18n).

- [ ] **Step 1: Write the component**

```tsx
'use client';

/**
 * TrendChart — win-rate trend over the last 6 months (#2438).
 * Consumes stats.winRateTrend ({month: "YYYY-MM", winRate: 0..1}), already
 * populated by the BE. recharts AreaChart; win rate shown as 0–100%.
 */

import {
  Area,
  AreaChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';

import { useTranslation } from '@/hooks/useTranslation';
import type { PlayerStatistics } from '@/lib/api/schemas/play-records.schemas';

interface TrendChartProps {
  stats: PlayerStatistics;
}

// "YYYY-MM" → localized short month (e.g. "giu").
function formatMonthShort(month: string): string {
  const [y, m] = month.split('-').map(Number);
  if (!y || !m) return month;
  return new Date(y, m - 1, 1).toLocaleDateString('it-IT', { month: 'short' });
}

interface TrendTooltipProps {
  active?: boolean;
  payload?: { value: number }[];
  label?: string;
}

function TrendTooltip({ active, payload, label }: TrendTooltipProps) {
  if (!active || !payload || payload.length === 0) return null;
  const pct = Math.round((payload[0].value ?? 0) * 100);
  return (
    <div className="rounded-md border border-border bg-card/95 px-3 py-2 font-mono text-[11px] shadow-lg backdrop-blur-sm">
      <p className="font-display text-[12px] font-bold text-foreground">
        {label ? formatMonthShort(label) : ''}
      </p>
      <p className="mt-1 text-muted-foreground">
        win rate <span className="font-bold text-foreground">{pct}%</span>
      </p>
    </div>
  );
}

export function TrendChart({ stats }: TrendChartProps) {
  const { t } = useTranslation();
  const trend = stats.winRateTrend ?? [];
  const isEmpty = trend.length === 0;

  if (isEmpty) {
    return (
      <section
        className="rounded-lg border border-border bg-card p-4 md:p-5"
        data-testid="trend-empty"
        aria-label={t('playRecords.stats.trend.title')}
      >
        <header className="mb-4 flex items-center gap-3">
          <div className="flex h-8 w-8 flex-shrink-0 items-center justify-center rounded-md bg-entity-game/12 text-entity-game">
            📈
          </div>
          <h2 className="font-display text-base font-black text-foreground md:text-lg">
            {t('playRecords.stats.trend.title')}
          </h2>
        </header>
        <div className="flex flex-col items-center gap-2 rounded-md border border-dashed border-entity-game/30 bg-muted/30 px-4 py-6 text-center">
          <p className="text-xs text-muted-foreground">{t('playRecords.stats.trend.empty')}</p>
        </div>
      </section>
    );
  }

  // Screen-reader summary: list each month's win rate as a percentage.
  const srSummary = trend
    .map(m => `${formatMonthShort(m.month)} ${Math.round(m.winRate * 100)}%`)
    .join(', ');

  return (
    <section
      className="rounded-lg border border-border bg-card p-4 md:p-5"
      data-testid="trend-section"
      aria-label={t('playRecords.stats.trend.title')}
    >
      <header className="mb-4 flex items-center gap-3">
        <div className="flex h-8 w-8 flex-shrink-0 items-center justify-center rounded-md bg-entity-game/12 text-entity-game">
          📈
        </div>
        <h2 className="font-display text-base font-black text-foreground md:text-lg">
          {t('playRecords.stats.trend.title')}
        </h2>
      </header>

      <div role="img" aria-label={`${t('playRecords.stats.trend.title')}: ${srSummary}`}>
        <ResponsiveContainer width="100%" height={220}>
          <AreaChart data={trend} margin={{ top: 12, right: 16, left: 0, bottom: 8 }}>
            <CartesianGrid stroke="hsl(var(--border))" strokeDasharray="3 3" />
            <XAxis
              dataKey="month"
              tickFormatter={formatMonthShort}
              stroke="hsl(var(--muted-foreground))"
              fontSize={10}
              tickLine={false}
              axisLine={{ stroke: 'hsl(var(--border))' }}
            />
            <YAxis
              domain={[0, 1]}
              tickFormatter={v => `${Math.round(v * 100)}%`}
              stroke="hsl(var(--muted-foreground))"
              fontSize={10}
              tickLine={false}
              axisLine={false}
              width={40}
            />
            <Tooltip content={<TrendTooltip />} />
            <Area
              type="monotone"
              dataKey="winRate"
              stroke="hsl(var(--entity-game))"
              fill="hsl(var(--entity-game))"
              fillOpacity={0.25}
              strokeWidth={2}
            />
          </AreaChart>
        </ResponsiveContainer>
      </div>

      {/* Visually-hidden data table for screen readers / non-visual axe checks */}
      <table className="sr-only">
        <caption>{t('playRecords.stats.trend.title')}</caption>
        <thead>
          <tr>
            <th>{t('playRecords.stats.trend.month')}</th>
            <th>{t('playRecords.stats.trend.winRate')}</th>
          </tr>
        </thead>
        <tbody>
          {trend.map(m => (
            <tr key={m.month}>
              <td>{formatMonthShort(m.month)}</td>
              <td>{Math.round(m.winRate * 100)}%</td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}
```

> **Verify during implementation:** the token `--entity-game` exists as a CSS var consumable via `hsl(var(--entity-game))`. `MostPlayedBar` uses the Tailwind utility `text-entity-game`/`bg-entity-game`. If `hsl(var(--entity-game))` does not resolve (entity tokens may be defined as full colors, not H S L triplets), fall back to `var(--entity-game)` (no `hsl()` wrapper) — check `apps/web/src/styles/design-tokens-canonical.css` for the token's format and match it. This mirrors how `CostStackedArea` uses `hsl(var(--c-*))` for the dedicated chart palette.

- [ ] **Step 2: Typecheck**

Run: `pnpm -C apps/web typecheck`
Expected: PASS

- [ ] **Step 3: Commit** (with Task 4 tests)

---

### Task 4: `TrendChart` tests

**Files:**
- Create: `apps/web/src/components/play-records/stats/__tests__/TrendChart.test.tsx`

Mirror `MostPlayedBar.test.tsx` for the render/i18n harness (read it for the exact `render` wrapper + i18n mock pattern used by stats tests). recharts needs a sized container in jsdom — assert on the `sr-only` table + section testid, not on SVG geometry.

- [ ] **Step 1: Write the tests**

```tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { TrendChart } from '@/components/play-records/stats/TrendChart';
import type { PlayerStatistics } from '@/lib/api/schemas/play-records.schemas';

const base: PlayerStatistics = {
  totalSessions: 3,
  totalWins: 2,
  gamePlayCounts: {},
  averageScoresByGame: {},
};

describe('TrendChart', () => {
  it('renders the empty state when winRateTrend is missing/empty', () => {
    render(<TrendChart stats={base} />);
    expect(screen.getByTestId('trend-empty')).toBeInTheDocument();
  });

  it('renders the chart section + sr-only data table when trend has data', () => {
    const stats: PlayerStatistics = {
      ...base,
      winRateTrend: [
        { month: '2026-04', winRate: 0.5 },
        { month: '2026-05', winRate: 1 },
      ],
    };
    render(<TrendChart stats={stats} />);
    expect(screen.getByTestId('trend-section')).toBeInTheDocument();
    // sr-only table mirrors the data points as percentages
    expect(screen.getByText('50%')).toBeInTheDocument();
    expect(screen.getByText('100%')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run tests to verify they pass**

Run: `pnpm -C apps/web test -- TrendChart`
Expected: PASS (2 tests). If the i18n mock differs (keys render as raw strings), adjust assertions to match what `MostPlayedBar.test.tsx` does (it may assert testids only).

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/play-records/stats/TrendChart.tsx apps/web/src/components/play-records/stats/__tests__/TrendChart.test.tsx
git commit -m "feat(play-records): #2438 TrendChart win-rate recharts component"
```

---

### Task 5: Date-range preset filter + mount TrendChart in `StatisticsView`

**Files:**
- Modify: `apps/web/src/components/play-records/StatisticsView.tsx`

- [ ] **Step 1: Add preset state + range derivation + filter UI + TrendChart**

Replace the component body (currently lines 26-94) so it: holds a `preset` state, derives a `range`, passes it to `usePlayerStatistics`, renders a segmented control in the header area, and mounts `<TrendChart>` below the grid.

```tsx
'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';

import { MostPlayedBar } from '@/components/play-records/stats/MostPlayedBar';
import { StatsHero } from '@/components/play-records/stats/StatsHero';
import { TrendChart } from '@/components/play-records/stats/TrendChart';
import { WinByGameBar } from '@/components/play-records/stats/WinByGameBar';
import { MobileHeader } from '@/components/ui/navigation/MobileHeader';
import { useTranslation } from '@/hooks/useTranslation';
import { usePlayerStatistics, type StatsRange } from '@/lib/domain-hooks/usePlayRecords';

type RangePreset = 'all' | '30d' | '90d' | '12m';

const PRESETS: RangePreset[] = ['all', '30d', '90d', '12m'];

function rangeForPreset(preset: RangePreset): StatsRange | undefined {
  if (preset === 'all') return undefined;
  const now = new Date();
  const from = new Date(now);
  if (preset === '30d') from.setDate(now.getDate() - 30);
  else if (preset === '90d') from.setDate(now.getDate() - 90);
  else if (preset === '12m') from.setMonth(now.getMonth() - 12);
  return { startDate: from.toISOString() };
}

export function StatisticsView() {
  const router = useRouter();
  const { t } = useTranslation();
  const [preset, setPreset] = useState<RangePreset>('all');
  const { data: stats, isLoading, error } = usePlayerStatistics(rangeForPreset(preset));

  return (
    <div className="flex flex-col min-h-full bg-background" data-testid="stats-page">
      <MobileHeader
        title={t('playRecords.stats.headerTitle')}
        onBack={() => router.push('/play-records')}
      />

      {/* Date-range preset filter */}
      <div className="px-4 pt-4" data-testid="stats-range-filter">
        <div
          className="inline-flex rounded-lg border border-border bg-card p-1"
          role="group"
          aria-label={t('playRecords.stats.range.label')}
        >
          {PRESETS.map(p => (
            <button
              key={p}
              type="button"
              onClick={() => setPreset(p)}
              aria-pressed={preset === p}
              data-testid={`range-${p}`}
              className={
                preset === p
                  ? 'rounded-md bg-entity-game px-3 py-1.5 text-[11px] font-bold text-white'
                  : 'rounded-md px-3 py-1.5 text-[11px] font-bold text-muted-foreground hover:text-foreground'
              }
            >
              {t(`playRecords.stats.range.${p}`)}
            </button>
          ))}
        </div>
      </div>

      {isLoading && (
        <div className="flex-1 px-4 pt-6 pb-12 flex flex-col gap-6" data-testid="stats-loading">
          <div className="h-40 animate-pulse rounded-lg bg-muted" />
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4 md:gap-6" data-testid="stats-grid">
            <div className="h-56 animate-pulse rounded-lg bg-muted" data-testid="section-skeleton" />
            <div className="h-56 animate-pulse rounded-lg bg-muted" data-testid="section-skeleton" />
          </div>
        </div>
      )}

      {!isLoading && error && (
        <>
          {stats && <StatsHero stats={stats} />}
          <div className="flex-1 px-4 pt-6 pb-12">
            <div
              className="rounded-lg border border-danger/30 bg-danger/10 px-6 py-6 text-center"
              data-testid="stats-error"
            >
              <div className="text-4xl mb-3" aria-hidden="true">
                ⚠️
              </div>
              <h3 className="font-bold text-foreground">{t('playRecords.stats.error.title')}</h3>
              <p className="mt-2 text-sm text-muted-foreground">
                {t('playRecords.stats.error.description')}
              </p>
            </div>
          </div>
        </>
      )}

      {!isLoading && !error && stats && (
        <>
          <StatsHero stats={stats} />
          <div className="flex-1 px-4 pt-6 pb-12 flex flex-col gap-4 md:gap-6">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4 md:gap-6" data-testid="stats-grid">
              <MostPlayedBar stats={stats} />
              <WinByGameBar stats={stats} />
            </div>
            <TrendChart stats={stats} />
          </div>
        </>
      )}
    </div>
  );
}
```

- [ ] **Step 2: Typecheck**

Run: `pnpm -C apps/web typecheck`
Expected: PASS

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/play-records/StatisticsView.tsx
git commit -m "feat(play-records): #2438 date-range preset filter + mount TrendChart"
```

---

### Task 6: i18n keys

**Files:**
- Modify: `apps/web/src/locales/it.json` + `apps/web/src/locales/en.json` (verify path: grep for an existing key like `"headerTitle"` under `playRecords.stats` to find the file and nesting)

- [ ] **Step 1: Locate the stats namespace**

Run: `grep -rln "playRecords" apps/web/src/locales apps/web/src/**/messages* 2>/dev/null | head`
Then open the file(s) and find the `playRecords.stats` object.

- [ ] **Step 2: Add keys under `playRecords.stats`** (both it + en)

it.json:
```json
"trend": { "title": "Andamento win rate", "empty": "Dati insufficienti per l'andamento", "month": "Mese", "winRate": "Win rate" },
"range": { "label": "Filtra periodo", "all": "Tutto", "30d": "30g", "90d": "90g", "12m": "12 mesi" }
```
en.json:
```json
"trend": { "title": "Win rate trend", "empty": "Not enough data for a trend", "month": "Month", "winRate": "Win rate" },
"range": { "label": "Filter period", "all": "All", "30d": "30d", "90d": "90d", "12m": "12mo" }
```

- [ ] **Step 3: Run the i18n consistency test + the stats tests**

Run: `pnpm -C apps/web test -- play-records`
Expected: PASS. If the repo has a MESSAGES/locale-parity test, it confirms it+en have identical key sets.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/locales/
git commit -m "feat(play-records): #2438 i18n keys for trend + range filter"
```

---

### Task 7: StatisticsView integration test (preset re-query + TrendChart mount)

**Files:**
- Modify/Create: `apps/web/src/components/play-records/__tests__/StatisticsView.test.tsx` (check if it exists first)

- [ ] **Step 1: Check for an existing test + its harness**

Run: `ls apps/web/src/components/play-records/__tests__/ | grep -i statistics`
If it exists, read it for the React Query + MSW harness. Add the cases below to it; otherwise create it mirroring that harness.

- [ ] **Step 2: Add the cases**

```tsx
it('renders the range filter and the trend section on success', async () => {
  // ... render StatisticsView within the existing RQ+MSW provider wrapper ...
  expect(await screen.findByTestId('stats-range-filter')).toBeInTheDocument();
  expect(screen.getByTestId('range-all')).toHaveAttribute('aria-pressed', 'true');
  // TrendChart renders (section or empty depending on MSW fixture)
  expect(
    screen.getByTestId('trend-section') ?? screen.getByTestId('trend-empty')
  ).toBeInTheDocument();
});

it('switching preset updates aria-pressed', async () => {
  // ... render ...
  const btn30 = await screen.findByTestId('range-30d');
  await userEvent.click(btn30);
  expect(btn30).toHaveAttribute('aria-pressed', 'true');
});
```

> Fill the render wrapper from the existing stats test harness (Step 1). If the MSW `statistics` handler ignores query params, the preset switch still re-queries the same fixture — the test asserts UI state (`aria-pressed`), not network, so it is deterministic.

- [ ] **Step 3: Run the tests**

Run: `pnpm -C apps/web test -- play-records`
Expected: PASS

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/play-records/__tests__/StatisticsView.test.tsx
git commit -m "test(play-records): #2438 StatisticsView range filter + trend"
```

---

## Self-Review

**1. Spec coverage (PR-A scope = trend chart + date-range):**
- Trend chart (recharts, consumes winRateTrend, default+empty, a11y) → Tasks 3, 4. ✅
- Date-range preset filter (api+hook+UI) → Tasks 1, 2, 5. ✅
- i18n → Task 6. ✅
- Integration → Task 7. ✅
- Redis cache / CSV / leaderboard → correctly NOT here (PR-B / out of scope). ✅

**2. Placeholder scan:** The "verify during implementation" notes (token format in Task 3; i18n path in Task 6; test harness in Tasks 4/7) are explicit read-then-match instructions with named fallbacks, not vague TODOs. Test render wrappers in Tasks 4/7 reference the real sibling harness (`MostPlayedBar.test.tsx` / existing StatisticsView test) to copy — flagged as a read step.

**3. Type consistency:** `StatsRange` defined in Task 2, consumed in Tasks 1 (shape `{startDate?, endDate?}`), 2, 5. `getPlayerStatistics(params)` signature consistent Task 1 ↔ Task 2 call. `winRateTrend`/`MonthlyWinRate` match the schema. `rangeForPreset`/`RangePreset` defined+used in Task 5 only.

**Risk:** recharts in jsdom renders no real SVG geometry — tests assert on the `sr-only` table + testids (Task 4), not chart pixels. The `--entity-game` token format (`hsl(var())` vs `var()`) is flagged for verification against `design-tokens-canonical.css` in Task 3.
