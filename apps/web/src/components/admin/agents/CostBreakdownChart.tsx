'use client';

/**
 * CostBreakdownChart Component
 * Issue #3382: Agent Metrics Dashboard
 *
 * Displays cost breakdown by model as horizontal bars.
 */

import { motion } from 'framer-motion';
import { useMemo } from 'react';

import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

interface CostBreakdownItem {
  category: string;
  cost: number;
  invocations: number;
  tokens: number;
}

interface CostBreakdownChartProps {
  data: CostBreakdownItem[];
  className?: string;
}

// ============================================================================
// Color Mapping
// ============================================================================

const MODEL_COLORS: Record<string, string> = {
  'claude-3-5-sonnet': 'bg-purple-500',
  'claude-3-5-haiku': 'bg-blue-500',
  'claude-3-opus': 'bg-violet-600',
  'gpt-4o': 'bg-emerald-500',
  'gpt-4o-mini': 'bg-green-500',
  'deepseek': 'bg-amber-500',
  default: 'bg-slate-500',
};

function getModelColor(model: string): string {
  const lowerModel = model.toLowerCase();
  for (const [key, color] of Object.entries(MODEL_COLORS)) {
    if (key !== 'default' && lowerModel.includes(key.toLowerCase())) {
      return color;
    }
  }
  return MODEL_COLORS['default'];
}

// ============================================================================
// Component
// ============================================================================

export function CostBreakdownChart({ data, className }: CostBreakdownChartProps) {
  const maxCost = useMemo(() => Math.max(...data.map((d) => d.cost), 0.01), [data]);
  const totalCost = useMemo(() => data.reduce((sum, d) => sum + d.cost, 0), [data]);

  const formatCost = (cost: number): string => {
    if (cost >= 1) return `$${cost.toFixed(2)}`;
    if (cost >= 0.01) return `$${cost.toFixed(3)}`;
    return `$${cost.toFixed(4)}`;
  };

  const formatTokens = (tokens: number): string => {
    if (tokens >= 1_000_000) return `${(tokens / 1_000_000).toFixed(1)}M`;
    if (tokens >= 1_000) return `${(tokens / 1_000).toFixed(1)}K`;
    return tokens.toString();
  };

  if (data.length === 0) {
    return (
      <div className="flex h-64 items-center justify-center text-muted-foreground">
        No cost data available
      </div>
    );
  }

  return (
    <div className={cn('space-y-3', className)}>
      {data.slice(0, 8).map((item, idx) => {
        const widthPercent = (item.cost / maxCost) * 100;
        const costPercent = totalCost > 0 ? ((item.cost / totalCost) * 100).toFixed(1) : '0';

        return (
          <div key={item.category} className="space-y-1">
            <div className="flex items-center justify-between text-xs">
              <span className="truncate font-medium" title={item.category}>
                {item.category}
              </span>
              <span className="flex items-center gap-2 text-muted-foreground">
                <span>{formatTokens(item.tokens)} tokens</span>
                <span className="font-mono font-medium text-foreground">
                  {formatCost(item.cost)} ({costPercent}%)
                </span>
              </span>
            </div>
            <div className="h-3 w-full overflow-hidden rounded-full bg-muted">
              <motion.div
                initial={{ width: 0 }}
                animate={{ width: `${widthPercent}%` }}
                transition={{ duration: 0.5, delay: idx * 0.05 }}
                className={cn('h-full rounded-full', getModelColor(item.category))}
              />
            </div>
          </div>
        );
      })}

      {/* Total */}
      <div className="mt-4 flex items-center justify-between border-t pt-3 text-sm">
        <span className="font-medium">Total</span>
        <span className="font-mono font-bold">{formatCost(totalCost)}</span>
      </div>
    </div>
  );
}
