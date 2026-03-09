'use client';

import { cn } from '@/lib/utils';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip';

/** Strategy tier badge configuration */
const STRATEGY_BADGES = {
  Fast: {
    icon: '\u26A1',
    label: 'Risposta rapida',
    description: 'Risposta generata con modello veloce e leggero',
    colorClasses: 'bg-amber-500/15 text-amber-700 dark:text-amber-300 border-amber-500/30',
  },
  Balanced: {
    icon: '\uD83C\uDFAF',
    label: 'Risposta bilanciata',
    description: 'Risposta generata con modello bilanciato tra qualita e velocita',
    colorClasses: 'bg-blue-500/15 text-blue-700 dark:text-blue-300 border-blue-500/30',
  },
  Premium: {
    icon: '\uD83D\uDC8E',
    label: 'Risposta premium',
    description: 'Risposta generata con modello ad alte prestazioni',
    colorClasses: 'bg-violet-500/15 text-violet-700 dark:text-violet-300 border-violet-500/30',
  },
  HybridRAG: {
    icon: '\uD83E\uDDE0',
    label: 'Analisi approfondita',
    description: 'Risposta generata con analisi multi-livello e verifica incrociata',
    colorClasses: 'bg-emerald-500/15 text-emerald-700 dark:text-emerald-300 border-emerald-500/30',
  },
} as const;

export type StrategyTier = keyof typeof STRATEGY_BADGES;

interface ResponseMetaBadgeProps {
  /** Strategy tier from backend StreamingComplete event */
  strategyTier: string;
  /** Additional class names */
  className?: string;
}

/**
 * Soft quality badge shown on AI responses.
 * Issue #5481: ResponseMetaBadge component.
 *
 * Shows a colored badge with icon indicating the quality tier of the AI response.
 * Tooltip provides a brief description. No technical details for non-editor users.
 */
export function ResponseMetaBadge({ strategyTier, className }: ResponseMetaBadgeProps) {
  const badge = STRATEGY_BADGES[strategyTier as StrategyTier];

  if (!badge) return null;

  return (
    <TooltipProvider delayDuration={300}>
      <Tooltip>
        <TooltipTrigger asChild>
          <span
            role="status"
            aria-label={badge.label}
            className={cn(
              'inline-flex items-center gap-1 text-[10px] px-1.5 py-0.5 rounded-full border',
              'font-medium select-none transition-colors',
              badge.colorClasses,
              className,
            )}
            data-testid="response-meta-badge"
          >
            <span aria-hidden="true">{badge.icon}</span>
            {badge.label}
          </span>
        </TooltipTrigger>
        <TooltipContent side="top" className="max-w-[200px] text-center">
          <p>{badge.description}</p>
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
}
