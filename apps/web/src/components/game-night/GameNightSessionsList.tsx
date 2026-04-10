/**
 * GameNightSessionsList — Displays sessions played within a game night
 *
 * Shows each session as a card with game title, status badge, play order,
 * and a link to the live session page.
 *
 * Plan 2 Task 5 — Session Flow v2.1
 */

'use client';

import {
  Gamepad2,
  Play,
  Pause,
  CheckCircle2,
  Clock,
} from 'lucide-react';
import Link from 'next/link';
import type { LucideIcon } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import type { GameNightActiveSession } from '@/stores/game-night/types';

// ─── Status metadata ────────────────────────────────────────────────────────

interface StatusMeta {
  icon: LucideIcon;
  label: string;
  badgeClass: string;
}

const STATUS_MAP: Record<string, StatusMeta> = {
  in_progress: {
    icon: Play,
    label: 'In corso',
    badgeClass: 'bg-emerald-100 text-emerald-800 border-emerald-200',
  },
  pending: {
    icon: Clock,
    label: 'In attesa',
    badgeClass: 'bg-slate-100 text-slate-600 border-slate-200',
  },
  completed: {
    icon: CheckCircle2,
    label: 'Completata',
    badgeClass: 'bg-blue-100 text-blue-800 border-blue-200',
  },
  skipped: {
    icon: Pause,
    label: 'Saltata',
    badgeClass: 'bg-amber-100 text-amber-800 border-amber-200',
  },
};

const FALLBACK_STATUS: StatusMeta = {
  icon: Gamepad2,
  label: 'Sconosciuto',
  badgeClass: 'bg-gray-100 text-gray-600 border-gray-200',
};

// ─── Component ──────────────────────────────────────────────────────────────

export interface GameNightSessionsListProps {
  sessions: GameNightActiveSession[];
  gameNightId: string;
}

export function GameNightSessionsList({ sessions, gameNightId }: GameNightSessionsListProps) {
  if (sessions.length === 0) {
    return (
      <Card>
        <CardContent className="pt-6">
          <div className="text-center py-4 text-muted-foreground text-sm font-nunito">
            <Gamepad2 className="h-8 w-8 mx-auto mb-2 opacity-40" />
            Nessuna partita ancora — aggiungi un gioco per iniziare!
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base font-quicksand flex items-center gap-2">
          <Gamepad2 className="h-4 w-4" />
          Partite ({sessions.length})
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-2">
        {sessions.map(session => {
          const meta = STATUS_MAP[session.status] ?? FALLBACK_STATUS;
          const Icon = meta.icon;

          return (
            <Link
              key={session.id}
              href={`/sessions/${session.id}`}
              className="flex items-center justify-between gap-3 rounded-lg border p-3 transition-colors hover:bg-muted/50"
            >
              <div className="flex items-center gap-3 min-w-0">
                <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-primary/10 text-xs font-bold text-primary">
                  {session.playOrder}
                </span>
                <div className="min-w-0">
                  <p className="text-sm font-medium truncate font-nunito">
                    {session.gameTitle}
                  </p>
                  {session.startedAt && (
                    <p className="text-[11px] text-muted-foreground">
                      {new Date(session.startedAt).toLocaleTimeString('it-IT', {
                        hour: '2-digit',
                        minute: '2-digit',
                      })}
                    </p>
                  )}
                </div>
              </div>

              <Badge className={meta.badgeClass}>
                <Icon className="h-3 w-3 mr-1" />
                {meta.label}
              </Badge>
            </Link>
          );
        })}
      </CardContent>
    </Card>
  );
}
