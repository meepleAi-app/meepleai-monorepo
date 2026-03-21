'use client';

import Link from 'next/link';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { useActiveSessions } from '@/hooks/queries/useActiveSessions';
import type { GameSessionDto } from '@/lib/api/schemas';

function formatDuration(minutes: number): string {
  if (minutes < 60) return `${minutes} min`;
  const h = Math.floor(minutes / 60);
  const m = minutes % 60;
  return m > 0 ? `${h}h ${m}m` : `${h}h`;
}

export function ActiveSessionZone() {
  const { data: activeData, isLoading } = useActiveSessions(5);
  const sessions: GameSessionDto[] = activeData?.sessions ?? [];

  if (isLoading) {
    return (
      <div className="space-y-2">
        {Array.from({ length: 2 }, (_, i) => (
          <div key={i} className="animate-pulse h-20 rounded-xl bg-muted/50" />
        ))}
      </div>
    );
  }

  if (sessions.length === 0) return null;

  return (
    <div className="space-y-2" data-testid="active-session-zone">
      <h3 className="text-sm font-medium text-foreground/80 mb-2">Sessioni Attive</h3>
      {sessions.length === 1 ? (
        <Link href={`/sessions/live/${sessions[0].id}`}>
          <MeepleCard
            id={sessions[0].id}
            entity="session"
            variant="featured"
            title="Partita in corso"
            subtitle={`${sessions[0].playerCount} giocatori · ${formatDuration(sessions[0].durationMinutes)}`}
            className="border-l-4 border-green-500"
            data-testid="active-session-featured"
          />
        </Link>
      ) : (
        sessions.map(session => (
          <Link key={session.id} href={`/sessions/live/${session.id}`}>
            <MeepleCard
              id={session.id}
              entity="session"
              variant="list"
              title="Partita in corso"
              subtitle={`${session.playerCount} giocatori · ${formatDuration(session.durationMinutes)}`}
              className="border-l-4 border-green-500"
              data-testid={`active-session-${session.id}`}
            />
          </Link>
        ))
      )}
    </div>
  );
}
