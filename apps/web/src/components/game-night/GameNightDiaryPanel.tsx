/**
 * GameNightDiaryPanel — Cross-session diary timeline for a game night
 *
 * Fetches diary entries via the Session Flow v2.1 `getGameNightDiary` endpoint
 * (UNION of all sessions in the night) and renders them using the same vertical
 * timeline pattern as SessionDiaryTimeline.
 *
 * Plan 2 Task 5 — Session Flow v2.1
 */

'use client';

import { useMemo } from 'react';

import {
  BookOpen,
  Dice5,
  Flag,
  Pause,
  Play,
  RefreshCw,
  Shuffle,
  Trophy,
  Users,
} from 'lucide-react';
import type { LucideIcon } from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ScrollArea } from '@/components/ui/primitives/scroll-area';
import { useGameNightDiaryQuery } from '@/hooks/queries/useSessionFlow';
import { cn } from '@/lib/utils';

// ─── Event type meta ────────────────────────────────────────────────────────

interface EventMeta {
  icon: LucideIcon;
  color: string;
  label: string;
}

const EVENT_META: Record<string, EventMeta> = {
  session_started: { icon: Play, color: 'text-emerald-500', label: 'Sessione avviata' },
  session_paused: { icon: Pause, color: 'text-amber-500', label: 'Sessione in pausa' },
  session_resumed: { icon: Play, color: 'text-emerald-500', label: 'Sessione ripresa' },
  session_finalized: { icon: Flag, color: 'text-slate-500', label: 'Sessione finalizzata' },
  turn_advanced: { icon: RefreshCw, color: 'text-blue-500', label: 'Turno avanzato' },
  turn_order_set: { icon: Shuffle, color: 'text-indigo-500', label: 'Ordine turni impostato' },
  dice_rolled: { icon: Dice5, color: 'text-orange-500', label: 'Lancio dadi' },
  score_updated: { icon: Trophy, color: 'text-green-500', label: 'Punteggio aggiornato' },
  participant_joined: { icon: Users, color: 'text-purple-500', label: 'Partecipante unito' },
  game_night_completed: { icon: Flag, color: 'text-primary', label: 'Serata completata' },
};

const FALLBACK_META: EventMeta = {
  icon: BookOpen,
  color: 'text-muted-foreground',
  label: 'Evento',
};

function getEventMeta(eventType: string): EventMeta {
  return EVENT_META[eventType] ?? FALLBACK_META;
}

// ─── Payload parser ─────────────────────────────────────────────────────────

function parseSummary(eventType: string, payload: string | null): string | null {
  if (!payload) return null;
  try {
    const data = JSON.parse(payload);
    switch (eventType) {
      case 'dice_rolled':
        return data.formula ? `${data.formula} → ${data.total ?? ''}` : null;
      case 'score_updated':
        return data.newValue !== undefined ? `Nuovo punteggio: ${data.newValue}` : null;
      case 'turn_advanced':
        return data.toParticipantId ? 'Prossimo giocatore' : null;
      default:
        return null;
    }
  } catch {
    return null;
  }
}

// ─── Component ──────────────────────────────────────────────────────────────

export interface GameNightDiaryPanelProps {
  gameNightId: string;
  /** Maximum entries to display (default: 50). */
  limit?: number;
}

export function GameNightDiaryPanel({ gameNightId, limit = 50 }: GameNightDiaryPanelProps) {
  const { data: entries, isLoading } = useGameNightDiaryQuery(gameNightId);

  const sortedEntries = useMemo(() => {
    if (!entries) return [];
    return [...entries]
      .sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime())
      .slice(0, limit);
  }, [entries, limit]);

  if (isLoading && sortedEntries.length === 0) {
    return (
      <Card>
        <CardHeader>
          <CardTitle className="text-base font-quicksand flex items-center gap-2">
            <BookOpen className="h-4 w-4" />
            Diario serata
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="flex items-center justify-center py-8">
            <div className="h-5 w-5 animate-spin rounded-full border-2 border-muted-foreground border-t-transparent" />
          </div>
        </CardContent>
      </Card>
    );
  }

  if (sortedEntries.length === 0) {
    return (
      <Card>
        <CardHeader>
          <CardTitle className="text-base font-quicksand flex items-center gap-2">
            <BookOpen className="h-4 w-4" />
            Diario serata
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="text-center py-4 text-muted-foreground text-sm font-nunito">
            Nessun evento registrato — le attivita appariranno qui
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base font-quicksand flex items-center gap-2">
          <BookOpen className="h-4 w-4" />
          Diario serata ({sortedEntries.length})
        </CardTitle>
      </CardHeader>
      <CardContent>
        <ScrollArea className="h-[350px]">
          <div className="relative space-y-0">
            {/* Vertical line */}
            <div className="absolute left-[15px] top-2 bottom-2 w-px bg-border" />

            {sortedEntries.map(entry => {
              const meta = getEventMeta(entry.eventType);
              const Icon = meta.icon;
              const time = new Date(entry.timestamp).toLocaleTimeString('it-IT', {
                hour: '2-digit',
                minute: '2-digit',
                second: '2-digit',
              });
              const summary = parseSummary(entry.eventType, entry.payload);

              return (
                <div
                  key={entry.id}
                  className="relative flex items-start gap-3 py-2 pl-0"
                  data-testid="diary-entry"
                >
                  <div
                    className={cn(
                      'relative z-10 flex h-[30px] w-[30px] shrink-0 items-center justify-center rounded-full border bg-background',
                      meta.color
                    )}
                  >
                    <Icon className="h-3.5 w-3.5" />
                  </div>

                  <div className="flex-1 min-w-0 pt-0.5">
                    <div className="flex items-baseline gap-2">
                      <span className="text-sm font-medium text-foreground">{meta.label}</span>
                      <span className="text-[10px] text-muted-foreground tabular-nums">{time}</span>
                    </div>
                    {summary && (
                      <p className="text-xs text-muted-foreground mt-0.5 truncate">{summary}</p>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        </ScrollArea>
      </CardContent>
    </Card>
  );
}
