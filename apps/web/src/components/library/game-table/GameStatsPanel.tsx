/**
 * GameStatsPanel — Play statistics panel for the GameTableDrawer
 *
 * Displays a 2x2 stat card grid (timesPlayed, winRate, lastPlayed, avgDuration)
 * and a scrollable session history list. Shows an empty state when no sessions
 * have been recorded and a skeleton while data loads.
 *
 * Issue #3513 — Game Table Detail
 */

'use client';

import React from 'react';

import { format } from 'date-fns';
import { it as itLocale } from 'date-fns/locale';
import { BarChart2 } from 'lucide-react';

import { useLibraryGameDetail } from '@/hooks/queries/useLibrary';

// ============================================================================
// Types
// ============================================================================

export interface GameStatsPanelProps {
  gameId: string;
}

// ============================================================================
// Styling constants
// ============================================================================

const STAT_CARD = 'bg-[#21262d] rounded-lg p-4 border border-[#30363d]';

// ============================================================================
// Helpers
// ============================================================================

function formatPlayedAt(dateStr: string | null): string {
  if (!dateStr) return '—';
  try {
    return format(new Date(dateStr), 'dd MMM yyyy', { locale: itLocale });
  } catch {
    return '—';
  }
}

// ============================================================================
// Sub-components
// ============================================================================

interface StatCardProps {
  label: string;
  value: string | number;
  testId?: string;
}

function StatCard({ label, value, testId }: StatCardProps): React.ReactNode {
  return (
    <div className={STAT_CARD} data-testid={testId}>
      <p className="text-xs text-[#8b949e] uppercase tracking-wider font-nunito mb-1">{label}</p>
      <p className="text-2xl font-bold text-[#e6edf3] font-quicksand">{value}</p>
    </div>
  );
}

interface WinBadgeProps {
  didWin: boolean | null;
}

function WinBadge({ didWin }: WinBadgeProps): React.ReactNode {
  if (didWin === null) {
    return <span className="text-xs px-2 py-0.5 rounded-full bg-[#30363d] text-[#8b949e]">—</span>;
  }
  return didWin ? (
    <span className="text-xs px-2 py-0.5 rounded-full bg-green-900/40 text-green-400">
      Vittoria
    </span>
  ) : (
    <span className="text-xs px-2 py-0.5 rounded-full bg-red-900/40 text-red-400">Sconfitta</span>
  );
}

// ============================================================================
// Skeleton
// ============================================================================

function GameStatsPanelSkeleton(): React.ReactNode {
  return (
    <div className="space-y-4" data-testid="stats-skeleton">
      <div className="grid grid-cols-2 gap-3">
        {[...Array(4)].map((_, i) => (
          <div key={i} className={`${STAT_CARD} animate-pulse`}>
            <div className="h-3 bg-[#30363d] rounded w-2/3 mb-3" />
            <div className="h-7 bg-[#30363d] rounded w-1/2" />
          </div>
        ))}
      </div>
      <div className="space-y-2">
        {[...Array(3)].map((_, i) => (
          <div key={i} className={`${STAT_CARD} h-14 animate-pulse`} />
        ))}
      </div>
    </div>
  );
}

// ============================================================================
// Empty state
// ============================================================================

function EmptyState(): React.ReactNode {
  return (
    <div
      className="flex flex-col items-center justify-center py-12 text-center"
      data-testid="empty-state"
    >
      <BarChart2 className="h-10 w-10 text-[#30363d] mb-3" aria-hidden="true" />
      <p className="text-sm text-[#8b949e] font-nunito">Nessuna partita registrata</p>
    </div>
  );
}

// ============================================================================
// Main component
// ============================================================================

export function GameStatsPanel({ gameId }: GameStatsPanelProps): React.ReactNode {
  const { data, isLoading } = useLibraryGameDetail(gameId);

  if (isLoading) {
    return <GameStatsPanelSkeleton />;
  }

  if (!data || data.timesPlayed === 0) {
    return <EmptyState />;
  }

  const sessions = data.recentSessions ?? [];

  return (
    <div className="space-y-4">
      {/* 2x2 stat cards grid */}
      <div className="grid grid-cols-2 gap-3" data-testid="stats-grid">
        <StatCard label="Partite giocate" value={data.timesPlayed} testId="stat-times-played" />
        <StatCard label="Win Rate" value={data.winRate ?? '—'} testId="stat-win-rate" />
        <StatCard
          label="Ultima partita"
          value={formatPlayedAt(data.lastPlayed)}
          testId="stat-last-played"
        />
        <StatCard label="Durata media" value={data.avgDuration ?? '—'} testId="stat-avg-duration" />
      </div>

      {/* Session history list */}
      {sessions.length > 0 && (
        <div className="space-y-2" data-testid="session-history">
          <h3 className="text-xs text-[#8b949e] uppercase tracking-wider font-nunito">
            Sessioni recenti
          </h3>
          {sessions.map(session => (
            <div
              key={session.id}
              className={`${STAT_CARD} flex items-center justify-between gap-2`}
              data-testid={`session-row-${session.id}`}
            >
              <div className="flex flex-col gap-0.5">
                <span className="text-sm text-[#e6edf3] font-nunito">
                  {formatPlayedAt(session.playedAt)}
                </span>
                {session.players && (
                  <span className="text-xs text-[#8b949e]">{session.players} giocatori</span>
                )}
              </div>
              <div className="flex items-center gap-2 flex-shrink-0">
                <span className="text-xs text-[#8b949e]">{session.durationFormatted}</span>
                <WinBadge didWin={session.didWin} />
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
