'use client';

/**
 * GameDetailSessionsTab — Sessions list for game detail page
 *
 * Uses useGameSessions(gameId) to fetch and display play sessions.
 */

import { AlertCircle, Calendar, Clock, Gamepad2, Plus, Trophy, Users } from 'lucide-react';
import Link from 'next/link';

import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { useGameSessions } from '@/hooks/queries/useGames';
import type { GameSessionDto } from '@/lib/api/schemas/games.schemas';
import { cn } from '@/lib/utils';

export interface GameDetailSessionsTabProps {
  gameId: string;
}

function formatDate(dateString: string) {
  return new Date(dateString).toLocaleDateString('it-IT', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  });
}

function formatDuration(minutes: number) {
  if (minutes < 60) return `${minutes} min`;
  const h = Math.floor(minutes / 60);
  const m = minutes % 60;
  return m > 0 ? `${h}h ${m}min` : `${h}h`;
}

function SessionRow({ session }: { session: GameSessionDto }) {
  return (
    <Link
      href={`/sessions/${session.id}`}
      className={cn(
        'flex items-center gap-4 rounded-xl p-4 transition-colors',
        'border border-border/40 bg-card hover:bg-muted/50'
      )}
    >
      {/* Date */}
      <div className="flex flex-shrink-0 flex-col items-center text-center">
        <Calendar className="mb-1 h-4 w-4 text-muted-foreground" />
        <span className="font-nunito text-xs text-muted-foreground">
          {formatDate(session.startedAt)}
        </span>
      </div>

      {/* Details */}
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-2">
          {/* Players */}
          <span className="flex items-center gap-1 text-sm text-muted-foreground">
            <Users className="h-3.5 w-3.5" />
            {session.playerCount}
          </span>

          {/* Duration */}
          {session.durationMinutes > 0 && (
            <span className="flex items-center gap-1 text-sm text-muted-foreground">
              <Clock className="h-3.5 w-3.5" />
              {formatDuration(session.durationMinutes)}
            </span>
          )}

          {/* Winner */}
          {session.winnerName && (
            <span className="flex items-center gap-1 text-sm font-medium text-emerald-600 dark:text-emerald-400">
              <Trophy className="h-3.5 w-3.5" />
              {session.winnerName}
            </span>
          )}
        </div>

        {/* Notes */}
        {session.notes && (
          <p className="mt-1 truncate font-nunito text-xs text-muted-foreground">{session.notes}</p>
        )}
      </div>

      {/* Status badge */}
      <span
        className={cn(
          'flex-shrink-0 rounded-full px-2 py-0.5 text-xs font-medium',
          session.status === 'Completed'
            ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-400'
            : 'bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-400'
        )}
      >
        {session.status === 'Completed' ? 'Completata' : 'In corso'}
      </span>
    </Link>
  );
}

function SessionsSkeleton() {
  return (
    <div className="space-y-3">
      {[1, 2, 3].map(i => (
        <div
          key={i}
          className="flex items-center gap-4 rounded-xl border border-border/40 bg-card p-4"
        >
          <Skeleton className="h-10 w-10 rounded-lg" />
          <div className="flex-1 space-y-2">
            <Skeleton className="h-4 w-1/3" />
            <Skeleton className="h-3 w-1/2" />
          </div>
          <Skeleton className="h-6 w-16 rounded-full" />
        </div>
      ))}
    </div>
  );
}

export function GameDetailSessionsTab({ gameId }: GameDetailSessionsTabProps) {
  const { data: sessions, isLoading, error } = useGameSessions(gameId);

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h2 className="font-quicksand text-lg font-bold text-foreground">Sessioni di Gioco</h2>
        <Button variant="outline" size="sm" asChild>
          <Link href={`/sessions/new?gameId=${gameId}`}>
            <Plus className="mr-1.5 h-4 w-4" />
            Nuova Sessione
          </Link>
        </Button>
      </div>

      {/* Content */}
      {error ? (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>Errore nel caricamento delle sessioni.</AlertDescription>
        </Alert>
      ) : isLoading ? (
        <SessionsSkeleton />
      ) : !sessions || sessions.length === 0 ? (
        <div className="rounded-2xl border border-border/40 bg-card p-8 text-center">
          <Gamepad2 className="mx-auto mb-3 h-10 w-10 text-muted-foreground/40" />
          <p className="mb-1 font-quicksand text-base font-semibold text-foreground">
            Nessuna sessione registrata
          </p>
          <p className="font-nunito text-sm text-muted-foreground">
            Registra la tua prima partita per iniziare a tenere traccia delle tue sessioni.
          </p>
        </div>
      ) : (
        <div className="space-y-3">
          {sessions.map((session: GameSessionDto) => (
            <SessionRow key={session.id} session={session} />
          ))}
        </div>
      )}
    </div>
  );
}
