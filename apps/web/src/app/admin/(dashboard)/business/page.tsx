'use client';

/**
 * #1838 SP5 F4-C5 — `/admin/business` Budget & Cost
 *
 * Page allineata al mockup `sp5-admin-budget.html` (619 righe). Chiude l'epic
 * F4 Ondata Ops (#1833).
 *
 * Layout:
 *   1. Hero header (titolo + crumbs + descrizione + range select + actions)
 *   2. BudgetKpiStrip (4 KPI)
 *   3. CostStackedArea (stacked area 30gg/range)
 *   4. Grid 2-col: BudgetGauge + CostSimulator
 *   5. FeatureCostTable (drill provider)
 *
 * Range select persiste via URL `?range=30d|7d|90d|1y`. SetBudgetDialog è
 * un modal controllato da state locale.
 */

import { useState } from 'react';

import { Plus, Settings2 } from 'lucide-react';
import { useRouter, useSearchParams } from 'next/navigation';

import { BudgetGauge } from '@/components/admin/business/BudgetGauge';
import { BudgetKpiStrip } from '@/components/admin/business/BudgetKpiStrip';
import { CostSimulator } from '@/components/admin/business/CostSimulator';
import { CostStackedArea } from '@/components/admin/business/CostStackedArea';
import { FeatureCostTable } from '@/components/admin/business/FeatureCostTable';
import { SetBudgetDialog } from '@/components/admin/business/SetBudgetDialog';
import { Button } from '@/components/ui/primitives/button';
import { costBreakdownRangeSchema } from '@/lib/api/schemas/business-cost.schemas';
import type { CostBreakdownRange } from '@/lib/api/schemas/business-cost.schemas';

const RANGE_LABELS: Record<CostBreakdownRange, string> = {
  '7d': '7 giorni',
  '30d': '30 giorni',
  '90d': '90 giorni',
  '1y': 'Anno',
};

function parseRange(raw: string | null): CostBreakdownRange {
  const parsed = costBreakdownRangeSchema.safeParse(raw);
  return parsed.success ? parsed.data : '30d';
}

export default function BudgetPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const range = parseRange(searchParams.get('range'));
  const [setBudgetOpen, setSetBudgetOpen] = useState(false);

  const handleRangeChange = (next: CostBreakdownRange) => {
    const params = new URLSearchParams(searchParams.toString());
    params.set('range', next);
    router.replace(`/admin/business?${params.toString()}`);
  };

  return (
    <div className="space-y-5" data-testid="business-page">
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <nav
            aria-label="breadcrumb"
            className="font-mono text-[10.5px] uppercase tracking-wider text-muted-foreground mb-1"
          >
            Admin &middot; Platform &amp; Operations &middot; Budget
          </nav>
          <h1 className="font-quicksand text-xl sm:text-2xl font-bold tracking-tight text-foreground">
            Budget &amp; Cost
          </h1>
          <p className="mt-1 text-sm text-muted-foreground">
            KPI di spesa per provider/feature, simulator di costo, e configurazione budget mensile.
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <label className="sr-only" htmlFor="business-range">
            Intervallo di tempo
          </label>
          <select
            id="business-range"
            value={range}
            onChange={e => handleRangeChange(e.target.value as CostBreakdownRange)}
            className="rounded border border-border bg-background px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
            data-testid="business-range-select"
          >
            {(Object.keys(RANGE_LABELS) as CostBreakdownRange[]).map(r => (
              <option key={r} value={r}>
                {RANGE_LABELS[r]}
              </option>
            ))}
          </select>
          <Button
            size="sm"
            onClick={() => setSetBudgetOpen(true)}
            className="gap-1.5"
            data-testid="business-set-budget-button"
          >
            <Plus className="h-4 w-4" />
            Imposta budget
          </Button>
          <a
            href="/api/v1/admin/business/usage?period=30"
            className="inline-flex items-center gap-1.5 rounded border border-border bg-background px-3 py-1.5 text-sm hover:bg-muted"
            title="Usage statistics endpoint (legacy)"
          >
            <Settings2 className="h-4 w-4" aria-hidden />
            Usage
          </a>
        </div>
      </header>

      <BudgetKpiStrip />

      <CostStackedArea range={range} />

      <div className="grid grid-cols-1 gap-3 lg:grid-cols-2">
        <BudgetGauge />
        <CostSimulator />
      </div>

      <FeatureCostTable range={range} />

      <SetBudgetDialog open={setBudgetOpen} onClose={() => setSetBudgetOpen(false)} />
    </div>
  );
}
