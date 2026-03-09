'use client';

/**
 * QuickActions — Grid of action buttons for the live session.
 *
 * Provides fast access to:
 * - Rules Explainer (slide-over)
 * - Ask Arbiter (modal)
 * - Pause / Resume session
 * - Update Scores (quick input)
 *
 * Touch targets >= 44px for mobile.
 *
 * Issue #5587 — Live Game Session UI
 */

import { BookOpen, Scale, Pause, Play, BarChart3 } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

export interface QuickActionsProps {
  /** Whether the session is currently paused */
  isPaused: boolean;
  /** Whether any action is in progress (disables buttons) */
  isLoading?: boolean;
  onOpenRules: () => void;
  onAskArbiter: () => void;
  onTogglePause: () => void;
  onOpenScores: () => void;
  className?: string;
}

export function QuickActions({
  isPaused,
  isLoading = false,
  onOpenRules,
  onAskArbiter,
  onTogglePause,
  onOpenScores,
  className,
}: QuickActionsProps) {
  const actions = [
    {
      id: 'rules',
      label: 'Regole',
      icon: BookOpen,
      onClick: onOpenRules,
      variant: 'outline' as const,
    },
    {
      id: 'arbiter',
      label: 'Arbitro',
      icon: Scale,
      onClick: onAskArbiter,
      variant: 'outline' as const,
    },
    {
      id: 'pause',
      label: isPaused ? 'Riprendi' : 'Pausa',
      icon: isPaused ? Play : Pause,
      onClick: onTogglePause,
      variant: 'outline' as const,
    },
    {
      id: 'scores',
      label: 'Punteggi',
      icon: BarChart3,
      onClick: onOpenScores,
      variant: 'outline' as const,
    },
  ];

  return (
    <div
      className={cn('grid grid-cols-2 gap-2 sm:grid-cols-4', className)}
      data-testid="quick-actions"
    >
      {actions.map(action => {
        const Icon = action.icon;
        return (
          <Button
            key={action.id}
            variant={action.variant}
            disabled={isLoading}
            onClick={action.onClick}
            className={cn(
              'h-11 gap-2 text-sm font-semibold',
              'border-2 border-slate-200 dark:border-slate-700',
              'bg-white dark:bg-slate-800',
              'hover:border-amber-600/50 hover:bg-amber-50 dark:hover:bg-amber-950/20',
              'active:scale-95 transition-all'
            )}
            data-testid={`quick-action-${action.id}`}
          >
            <Icon className="h-4 w-4 flex-shrink-0" aria-hidden="true" />
            <span className="truncate">{action.label}</span>
          </Button>
        );
      })}
    </div>
  );
}
