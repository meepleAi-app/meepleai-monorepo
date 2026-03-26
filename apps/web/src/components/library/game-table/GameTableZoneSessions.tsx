/**
 * GameTableZoneSessions — Sessions & play stats zone for the Game Table
 *
 * Renders recent session info, play statistics row, and new session CTA.
 * Stats display as horizontal cards on desktop, vertical on mobile.
 *
 * Issue #3513 — Game Table Detail
 */

'use client';

import React from 'react';

import { formatDistanceToNow } from 'date-fns';
import { it as itLocale } from 'date-fns/locale';
import { Calendar, Clock, Trophy, Hash, Plus } from 'lucide-react';
import Link from 'next/link';

import { Button } from '@/components/ui/primitives/button';
import type { LibraryGameDetail } from '@/hooks/queries/useLibrary';

// ============================================================================
// Types
// ============================================================================

export interface GameTableZoneSessionsProps {
  gameDetail: LibraryGameDetail;
  gameId: string;
}

// ============================================================================
// Styling constants
// ============================================================================

const CARD_ROW = 'bg-[#21262d] rounded-lg p-3 border border-[#30363d]';

// ============================================================================
// Helpers
// ============================================================================

function formatLastPlayed(dateStr: string | null): string {
  if (!dateStr) return 'Mai';
  try {
    return formatDistanceToNow(new Date(dateStr), { addSuffix: true, locale: itLocale });
  } catch {
    return 'N/A';
  }
}

// ============================================================================
// Sub-components
// ============================================================================

interface StatCardProps {
  icon: React.ReactNode;
  label: string;
  value: string | number;
  testId: string;
}

function StatCard({ icon, label, value, testId }: StatCardProps): React.ReactNode {
  return (
    <div className={`${CARD_ROW} flex-1 min-w-[120px]`} data-testid={testId}>
      <div className="flex items-center gap-2 mb-1">
        {icon}
        <span className="text-xs text-[#8b949e] font-nunito">{label}</span>
      </div>
      <p className="text-lg font-quicksand font-bold text-[#e6edf3]">{value}</p>
    </div>
  );
}

// ============================================================================
// Component
// ============================================================================

export function GameTableZoneSessions({
  gameDetail,
  gameId,
}: GameTableZoneSessionsProps): React.ReactNode {
  const recentSession =
    gameDetail.recentSessions && gameDetail.recentSessions.length > 0
      ? gameDetail.recentSessions[0]
      : null;

  return (
    <div className="space-y-3">
      {/* Stats row — horizontal on desktop, wraps on mobile */}
      <div className="flex flex-col sm:flex-row gap-3" data-testid="stats-row">
        <StatCard
          icon={<Hash className="h-4 w-4 text-amber-400" />}
          label="Partite"
          value={gameDetail.timesPlayed}
          testId="stat-times-played"
        />
        <StatCard
          icon={<Trophy className="h-4 w-4 text-amber-400" />}
          label="Vittorie"
          value={gameDetail.winRate ?? 'N/A'}
          testId="stat-win-rate"
        />
        <StatCard
          icon={<Calendar className="h-4 w-4 text-amber-400" />}
          label="Ultima partita"
          value={formatLastPlayed(gameDetail.lastPlayed)}
          testId="stat-last-played"
        />
        <StatCard
          icon={<Clock className="h-4 w-4 text-amber-400" />}
          label="Durata media"
          value={gameDetail.avgDuration ?? 'N/A'}
          testId="stat-avg-duration"
        />
      </div>

      {/* Recent session */}
      <div className={CARD_ROW} data-testid="recent-session">
        <span className="text-sm font-quicksand font-semibold text-[#e6edf3] block mb-2">
          Ultima sessione
        </span>
        {recentSession ? (
          <div className="space-y-1 text-sm font-nunito">
            <div className="flex items-center justify-between">
              <span className="text-[#8b949e]">
                {new Date(recentSession.playedAt).toLocaleDateString('it-IT')}
              </span>
              <span className="text-[#8b949e]" data-testid="session-duration">
                {recentSession.durationFormatted}
              </span>
            </div>
            {recentSession.didWin !== null && (
              <span
                className={`inline-block text-xs px-2 py-0.5 rounded-full ${
                  recentSession.didWin
                    ? 'bg-green-900/40 text-green-400'
                    : 'bg-red-900/40 text-red-400'
                }`}
                data-testid="session-result"
              >
                {recentSession.didWin ? 'Vittoria' : 'Sconfitta'}
              </span>
            )}
            {recentSession.players && (
              <p className="text-xs text-[#8b949e]" data-testid="session-players">
                {recentSession.players} giocatori
              </p>
            )}
          </div>
        ) : (
          <p className="text-sm text-[#8b949e] font-nunito" data-testid="no-sessions-message">
            Nessuna partita registrata
          </p>
        )}
      </div>

      {/* New session CTA */}
      <Link href={`/library/games/${gameId}/sessions/new`} className="block">
        <Button
          className="w-full bg-amber-600 hover:bg-amber-500 text-white font-quicksand"
          data-testid="new-session-btn"
        >
          <Plus className="h-4 w-4 mr-2" />
          Nuova sessione
        </Button>
      </Link>
    </div>
  );
}
