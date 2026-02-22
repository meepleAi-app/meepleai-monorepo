'use client';

/**
 * BusinessRoiCalculator — Interactive ROI slider for Business view
 *
 * Compares TOMAC-RAG vs a naive single-model approach (Claude Sonnet for all
 * queries, no caching, no routing).
 */

import { useMemo, useState } from 'react';

import { DEFAULT_STATS } from './types';

/** Naive: Claude Sonnet for every query, no caching, approx 5K tokens = $0.05 */
const NAIVE_COST_PER_QUERY = 0.05;

/** TOMAC-RAG effective cost derived from DEFAULT_STATS at 100K queries */
const TOMAC_COST_PER_QUERY = DEFAULT_STATS.monthlyCost / 100_000;

const MIN_QUERIES = 5_000;
const MAX_QUERIES = 500_000;
const DEFAULT_QUERIES = 100_000;

function formatCurrency(value: number): string {
  if (value >= 1_000) return `${(value / 1_000).toFixed(1)}K`;
  return `${value.toFixed(0)}`;
}

function formatNumber(n: number): string {
  if (n >= 1_000_000) return `${(n / 1_000_000).toFixed(1)}M`;
  if (n >= 1_000) return `${(n / 1_000).toFixed(0)}K`;
  return `${n}`;
}
export function BusinessRoiCalculator() {
  const [queries, setQueries] = useState(DEFAULT_QUERIES);

  const { naiveCost, tomacCost, savings, savingsPct } = useMemo(() => {
    const naive = queries * NAIVE_COST_PER_QUERY;
    const tomac = queries * TOMAC_COST_PER_QUERY;
    const saved = naive - tomac;
    const pct = naive > 0 ? Math.round((saved / naive) * 100) : 0;
    return { naiveCost: naive, tomacCost: tomac, savings: saved, savingsPct: pct };
  }, [queries]);

  return (
    <div className="space-y-5">
      <div className="space-y-3">
        <div className="flex items-center justify-between text-sm">
          <span className="font-medium text-foreground">Queries per month</span>
          <span className="font-bold text-primary tabular-nums text-lg">
            {formatNumber(queries)}
          </span>
        </div>
        <input
          type="range"
          min={MIN_QUERIES}
          max={MAX_QUERIES}
          step={5_000}
          value={queries}
          onChange={e => setQueries(Number(e.target.value))}
          className="w-full h-2 rounded-full appearance-none cursor-pointer accent-primary"
          aria-label="Monthly query volume"
        />
        <div className="flex justify-between text-xs text-muted-foreground">
          <span>{formatNumber(MIN_QUERIES)}</span>
          <span>{formatNumber(MAX_QUERIES)}</span>
        </div>
      </div>
      <div className="rag-roi-result">
        <div className="rag-roi-box" style={{ borderColor: 'hsla(0,72%,51%,0.3)', backgroundColor: 'hsla(0,72%,51%,0.05)' }}>
          <div className="rag-roi-box-value" style={{ color: 'hsl(0,72%,51%)' }}>{formatCurrency(naiveCost)}</div>
          <div className="rag-roi-box-label">Without TOMAC-RAG</div>
          <p className="text-xs text-muted-foreground mt-1">Sonnet for all queries, no cache</p>
        </div>
        <div className="rag-roi-box" style={{ borderColor: 'hsla(221,83%,53%,0.3)', backgroundColor: 'hsla(221,83%,53%,0.05)' }}>
          <div className="rag-roi-box-value" style={{ color: 'hsl(221,83%,53%)' }}>{formatCurrency(tomacCost)}</div>
          <div className="rag-roi-box-label">With TOMAC-RAG</div>
          <p className="text-xs text-muted-foreground mt-1">Adaptive routing + {DEFAULT_STATS.cacheHitRate}% cache</p>
        </div>
        <div className="rag-roi-box" style={{ borderColor: 'hsla(142,76%,36%,0.3)', backgroundColor: 'hsla(142,76%,36%,0.05)' }}>
          <div className="rag-roi-box-value" style={{ color: 'hsl(142,76%,36%)' }}>{formatCurrency(savings)}</div>
          <div className="rag-roi-box-label">Monthly savings</div>
          <p className="text-xs text-muted-foreground mt-1">{savingsPct}% reduction in LLM spend</p>
        </div>
      </div>
      {savings > 0 && (
        <p className="text-xs text-center text-muted-foreground">
          Projected annual savings:{' '}
          <span className="font-semibold text-green-600 dark:text-green-400">
            {formatCurrency(savings * 12)}
          </span>
          /year at {formatNumber(queries)} queries/month
        </p>
      )}
      <div className="space-y-0.5 border-t border-border/40 pt-2 text-xs text-muted-foreground/60">
        <p>Naive baseline: Claude Sonnet for all queries, approx 5K tokens, no caching ($0.05/query).</p>
        <p>TOMAC-RAG: 65% FAST (free) + 28% BALANCED + 7% premium, {DEFAULT_STATS.cacheHitRate}% cache hit rate.</p>
      </div>
    </div>
  );
}
