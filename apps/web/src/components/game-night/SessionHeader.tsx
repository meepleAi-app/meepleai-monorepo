'use client';

/**
 * SessionHeader — Game name, active expansions, current phase indicator.
 *
 * Renders game info at the top of the live session play view.
 * Mobile-first with sticky positioning.
 *
 * Issue #5587 — Live Game Session UI
 */

import { Gamepad2 } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { cn } from '@/lib/utils';

export interface SessionHeaderProps {
  /** Game name (e.g. "Catan") */
  gameName: string;
  /** Active expansion names */
  expansions?: string[];
  /** Current turn number (1-based) */
  turnNumber: number;
  /** Current phase label (e.g. "Scambi", "Costruzione") */
  currentPhase?: string | null;
  /** Session status */
  status: 'Active' | 'Paused' | 'Finalized';
  className?: string;
}

const STATUS_STYLES: Record<SessionHeaderProps['status'], string> = {
  Active: 'bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border-emerald-500/20',
  Paused: 'bg-amber-500/10 text-amber-600 dark:text-amber-400 border-amber-500/20',
  Finalized: 'bg-slate-500/10 text-slate-600 dark:text-slate-400 border-slate-500/20',
};

export function SessionHeader({
  gameName,
  expansions = [],
  turnNumber,
  currentPhase,
  status,
  className,
}: SessionHeaderProps) {
  return (
    <header
      className={cn(
        'sticky top-0 z-40 border-b border-amber-900/20',
        'bg-gradient-to-br from-amber-50 via-orange-50 to-amber-100',
        'dark:from-slate-900 dark:via-slate-800 dark:to-slate-900',
        'backdrop-blur-xl px-4 py-3',
        className
      )}
      data-testid="live-session-header"
    >
      {/* Row 1: Game name + status */}
      <div className="flex items-center gap-2 flex-wrap">
        <Gamepad2
          className="h-5 w-5 text-amber-600 dark:text-amber-400 flex-shrink-0"
          aria-hidden="true"
        />
        <h1 className="font-bold text-lg text-slate-900 dark:text-amber-50 truncate tracking-tight">
          {gameName}
        </h1>

        {expansions.length > 0 && (
          <span className="text-sm text-slate-500 dark:text-slate-400 truncate">
            + {expansions.join(', ')}
          </span>
        )}

        <Badge
          variant="outline"
          className={cn('text-xs font-medium border ml-auto', STATUS_STYLES[status])}
        >
          {status}
        </Badge>
      </div>

      {/* Row 2: Turn + Phase */}
      <div className="mt-1 flex items-center gap-2 text-sm text-slate-600 dark:text-slate-400">
        <span className="font-semibold tabular-nums">Turno {turnNumber}</span>
        {currentPhase && (
          <>
            <span className="text-slate-300 dark:text-slate-600">&middot;</span>
            <span>Fase: {currentPhase}</span>
          </>
        )}
      </div>
    </header>
  );
}
