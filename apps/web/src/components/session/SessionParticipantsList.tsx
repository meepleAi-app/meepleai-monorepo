/**
 * SessionParticipantsList — Players list with turn-order highlight
 *
 * Shows all session participants in join order, highlighting the player
 * whose turn it currently is. Uses the LiveSessionDto player array and
 * the currentTurnPlayerId field.
 *
 * Plan 2 Task 4 — Session Flow v2.1
 */

'use client';

import { Crown, ArrowRight } from 'lucide-react';

import type { PlayerColor } from '@/lib/api/schemas/live-sessions.schemas';
import { cn } from '@/lib/utils';

import { playerColorToHex } from './adapters';

interface ParticipantInfo {
  id: string;
  displayName: string;
  color: PlayerColor;
  role: string;
  totalScore: number;
  currentRank: number;
  isActive: boolean;
}

interface SessionParticipantsListProps {
  participants: ParticipantInfo[];
  /** UUID of the player whose turn it currently is (null if no turn order). */
  currentTurnPlayerId: string | null;
}

export function SessionParticipantsList({
  participants,
  currentTurnPlayerId,
}: SessionParticipantsListProps) {
  if (participants.length === 0) {
    return (
      <div className="rounded-lg border border-border bg-muted/30 p-4 text-center">
        <p className="text-sm text-muted-foreground">Nessun partecipante</p>
      </div>
    );
  }

  return (
    <section className="space-y-2" data-testid="session-participants-list">
      <h2 className="text-sm font-semibold text-muted-foreground uppercase tracking-wider">
        Partecipanti
      </h2>
      <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
        {participants.map(p => {
          const isCurrent = currentTurnPlayerId === p.id;
          const colorHex = playerColorToHex(p.color);

          return (
            <div
              key={p.id}
              data-testid={`participant-${p.id}`}
              className={cn(
                'flex items-center gap-3 rounded-lg border px-3 py-2 transition-all',
                isCurrent
                  ? 'border-amber-500/50 bg-amber-50 dark:bg-amber-900/20 shadow-sm ring-1 ring-amber-500/30'
                  : 'border-border bg-card hover:bg-muted/40'
              )}
            >
              {/* Color dot + turn indicator */}
              <div className="relative shrink-0">
                <div
                  className="h-8 w-8 rounded-full flex items-center justify-center text-white text-xs font-bold"
                  style={{ backgroundColor: colorHex }}
                >
                  {p.displayName.charAt(0).toUpperCase()}
                </div>
                {isCurrent && (
                  <span className="absolute -bottom-0.5 -right-0.5 flex h-3.5 w-3.5 items-center justify-center rounded-full bg-amber-500 text-white">
                    <ArrowRight className="h-2.5 w-2.5" />
                  </span>
                )}
              </div>

              {/* Name + rank */}
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-1.5">
                  <span
                    className={cn(
                      'text-sm font-medium truncate',
                      isCurrent ? 'text-amber-900 dark:text-amber-200' : 'text-foreground'
                    )}
                  >
                    {p.displayName}
                  </span>
                  {p.role === 'Owner' && (
                    <Crown className="h-3 w-3 shrink-0 text-amber-600 dark:text-amber-400" />
                  )}
                </div>
                {isCurrent && (
                  <span className="text-[10px] font-medium uppercase tracking-widest text-amber-600 dark:text-amber-400">
                    Turno attuale
                  </span>
                )}
              </div>

              {/* Score */}
              <div className="text-right shrink-0">
                <span className="text-sm font-bold tabular-nums text-foreground">
                  {p.totalScore}
                </span>
                {p.currentRank > 0 && (
                  <p className="text-[10px] text-muted-foreground">#{p.currentRank}</p>
                )}
              </div>
            </div>
          );
        })}
      </div>
    </section>
  );
}

export type { SessionParticipantsListProps };
