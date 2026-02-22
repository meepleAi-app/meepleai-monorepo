'use client';

/**
 * BusinessStrategyComparison — Interactive strategy table for Business view
 *
 * Shows all 5 primary strategies side-by-side with:
 * - Accuracy visual bars
 * - Cost per query
 * - Latency
 * - Typical usage distribution
 * - Primary use case
 */

import { useState } from 'react';

import { cn } from '@/lib/utils';

import { STRATEGY_CONFIGS } from './types';

// =============================================================================
// Data
// =============================================================================

const STRATEGIES_TO_SHOW = ['FAST', 'BALANCED', 'PRECISE', 'EXPERT', 'CONSENSUS'] as const;

// Parse "78-85%" → average 81.5 for bar width
function parseAccuracyMidpoint(range: string): number {
  const match = range.match(/(\d+)-(\d+)/);
  if (!match) return 80;
  return (parseInt(match[1]) + parseInt(match[2])) / 2;
}

// Strategy accent colors (matching hero pipeline hues)
const STRATEGY_COLORS: Record<string, string> = {
  FAST: 'hsl(142,76%,36%)',
  BALANCED: 'hsl(221,83%,53%)',
  PRECISE: 'hsl(25,95%,53%)',
  EXPERT: 'hsl(262,83%,62%)',
  CONSENSUS: 'hsl(0,72%,51%)',
};

const STRATEGY_BG: Record<string, string> = {
  FAST: 'hsla(142,76%,36%,0.12)',
  BALANCED: 'hsla(221,83%,53%,0.12)',
  PRECISE: 'hsla(25,95%,53%,0.12)',
  EXPERT: 'hsla(262,83%,62%,0.12)',
  CONSENSUS: 'hsla(0,72%,51%,0.12)',
};
// =============================================================================
// Component
// =============================================================================

export function BusinessStrategyComparison() {
  const [highlighted, setHighlighted] = useState<string | null>(null);

  return (
    <div className="rounded-xl border border-border/60 overflow-hidden">
      <div className="overflow-x-auto">
        <table className="rag-strategy-table">
          <thead>
            <tr>
              <th>Strategy</th>
              <th>Accuracy</th>
              <th>Cost / Query</th>
              <th>Latency</th>
              <th className="hidden md:table-cell">Usage</th>
              <th className="hidden lg:table-cell">Use Case</th>
            </tr>
          </thead>
          <tbody>
            {STRATEGIES_TO_SHOW.map(key => {
              const cfg = STRATEGY_CONFIGS[key];
              const color = STRATEGY_COLORS[key];
              const bg = STRATEGY_BG[key];
              const midpoint = parseAccuracyMidpoint(cfg.accuracyRange);
              const isHighlighted = highlighted === key;

              return (
                <tr
                  key={key}
                  onClick={() => setHighlighted(isHighlighted ? null : key)}
                  className={cn('cursor-pointer', isHighlighted && 'bg-muted/30')}
                  style={isHighlighted ? { backgroundColor: bg } : undefined}
                >
                  {/* Strategy badge */}
                  <td>
                    <span
                      className="inline-flex items-center px-2.5 py-1 rounded-full text-xs font-bold tracking-wide"
                      style={{ backgroundColor: bg, color }}
                    >
                      {key}
                    </span>
                  </td>

                  {/* Accuracy bar */}
                  <td>
                    <div className="rag-accuracy-bar">
                      <div className="rag-accuracy-bar-track">
                        <div
                          className="rag-accuracy-bar-fill"
                          style={{
                            width: `${midpoint}%`,
                            backgroundColor: color,
                          }}
                        />
                      </div>
                      <span className="text-xs text-muted-foreground whitespace-nowrap">
                        {cfg.accuracyRange}
                      </span>
                    </div>
                  </td>

                  {/* Cost */}
                  <td>
                    <span className="text-sm font-semibold" style={{ color }}>
                      $
                      {cfg.estimatedCost.toFixed(4) === '0.0000'
                        ? '0.0001'
                        : cfg.estimatedCost.toFixed(4)}
                    </span>
                  </td>

                  {/* Latency */}
                  <td>
                    <span className="text-xs text-muted-foreground">{cfg.latencyMs}</span>
                  </td>

                  {/* Usage % */}
                  <td className="hidden md:table-cell">
                    <span className="text-xs text-muted-foreground">{cfg.usagePercent}</span>
                  </td>

                  {/* Use Case */}
                  <td className="hidden lg:table-cell max-w-xs">
                    <span className="text-xs text-muted-foreground leading-snug">
                      {cfg.useCase}
                    </span>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {/* Legend */}
      <div className="px-4 py-2.5 border-t border-border/50 bg-muted/20">
        <p className="text-xs text-muted-foreground">
          Click a row to highlight. Costs are per-query estimates at 2026 model pricing.
        </p>
      </div>
    </div>
  );
}
