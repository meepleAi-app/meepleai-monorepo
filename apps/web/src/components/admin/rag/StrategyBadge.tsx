import { Badge } from '@/components/ui/data-display/badge';
import { cn } from '@/lib/utils';

export interface StrategyBadgeProps {
  /** RAG strategy name */
  strategy: string;
  /** Additional className */
  className?: string;
}

const STRATEGY_COLORS = {
  POC: 'bg-amber-100/80 text-amber-900 dark:bg-amber-900/30 dark:text-amber-200',
  SingleModel:
    'bg-blue-100/80 text-blue-900 dark:bg-blue-900/30 dark:text-blue-200',
  MultiModelConsensus:
    'bg-purple-100/80 text-purple-900 dark:bg-purple-900/30 dark:text-purple-200',
  HybridRAG:
    'bg-green-100/80 text-green-900 dark:bg-green-900/30 dark:text-green-200',
  RetrievalOnly:
    'bg-slate-100/80 text-slate-900 dark:bg-slate-800/30 dark:text-slate-200',
} as const;

/**
 * Color-coded badge for RAG strategy types
 */
export function StrategyBadge({ strategy, className }: StrategyBadgeProps) {
  const colorClass =
    STRATEGY_COLORS[strategy as keyof typeof STRATEGY_COLORS] ||
    'bg-slate-100 text-slate-900';

  return (
    <Badge className={cn('font-mono text-xs', colorClass, className)}>
      {strategy}
    </Badge>
  );
}
