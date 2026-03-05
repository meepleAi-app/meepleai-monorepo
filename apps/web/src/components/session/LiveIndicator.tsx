/**
 * LiveIndicator — real-time session status pill
 *
 * Shows: green pulse dot + "Live"/"Disconnesso" + "Round N · duration"
 *
 * Issue #5041 — Sessions Redesign Phase 2
 */

'use client';

import { formatDistanceToNow } from 'date-fns';
import { it } from 'date-fns/locale';
import { Wifi, WifiOff } from 'lucide-react';

interface LiveIndicatorProps {
  /** ISO string of session start time */
  startedAt: string | null;
  /** Current round number (1-based) */
  currentRound: number;
  /** Whether SSE connection is active */
  isConnected: boolean;
}

export function LiveIndicator({ startedAt, currentRound, isConnected }: LiveIndicatorProps) {
  const duration = startedAt
    ? formatDistanceToNow(new Date(startedAt), { locale: it, addSuffix: false })
    : null;

  return (
    <div className="flex items-center justify-center gap-3 py-3">
      {/* Connection status badge */}
      {isConnected ? (
        <span className="inline-flex items-center gap-1.5 rounded-full bg-emerald-100 px-3 py-1 text-xs font-semibold text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-400">
          <span className="relative flex h-2 w-2">
            <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-emerald-400 opacity-75" />
            <span className="relative inline-flex h-2 w-2 rounded-full bg-emerald-500" />
          </span>
          <Wifi className="h-3 w-3" />
          Live
        </span>
      ) : (
        <span className="inline-flex items-center gap-1.5 rounded-full bg-red-100 px-3 py-1 text-xs font-semibold text-red-700 dark:bg-red-900/30 dark:text-red-400">
          <span className="h-2 w-2 rounded-full bg-red-500" />
          <WifiOff className="h-3 w-3" />
          Disconnesso
        </span>
      )}

      {/* Round + duration info */}
      <span className="text-xs text-muted-foreground">
        Round {currentRound}
        {duration && <> &middot; {duration}</>}
      </span>
    </div>
  );
}
