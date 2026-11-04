'use client';

/**
 * #1838 SP5 F4-C5 — `/admin/business` Budget & Cost
 *
 * Page allineata al mockup `sp5-admin-budget.html` (619 righe). Chiude l'epic
 * F4 Ondata Ops (#1833).
 *
 * Layout:
 *   1. Hero header (titolo + crumbs + descrizione + range select + actions)
 *   2. CostKpiStrip (4 KPI)
 *   3. CostStackedArea (stacked area 30gg/range)
 *   4. CostSimulator
 *   5. FeatureCostTable (drill provider)
 *
 * Range select persiste via URL `?range=30d|7d|90d|1y`.
 *
 * #4169 (ADR-095 D6/D8): via «Imposta budget», `SetBudgetDialog` e `BudgetGauge`. `AppBudget` era un
 * secondo budget che il gateway LLM non applicava; l'unico budget è quello del gateway, nel tab
 * Budget dell'AI, e questa pagina mostra i costi.
 */

import { Settings2 } from 'lucide-react';
import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';

import { CostKpiStrip } from '@/components/admin/business/CostKpiStrip';
import { CostSimulator } from '@/components/admin/business/CostSimulator';
import { CostStackedArea } from '@/components/admin/business/CostStackedArea';
import { FeatureCostTable } from '@/components/admin/business/FeatureCostTable';
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
            Admin &middot; Platform &amp; Operations &middot; Costs
          </nav>
          <h1 className="font-quicksand text-xl sm:text-2xl font-bold tracking-tight text-foreground">
            Costs
          </h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Spesa per provider e per feature dal registro dei costi, e simulatore di costo. I tetti
            di spesa dell&apos;AI li applica il gateway: si gestiscono in{' '}
            <Link
              href="/admin/ai?tab=config"
              className="underline underline-offset-2 text-foreground"
            >
              AI → Budget
            </Link>
            .
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

      <CostKpiStrip />

      <CostStackedArea range={range} />

      <CostSimulator />

      <FeatureCostTable range={range} />
    </div>
  );
}
