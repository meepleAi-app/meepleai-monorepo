'use client';

import Link from 'next/link';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { useUpcomingGameNights } from '@/hooks/queries/useGameNights';
import { IS_ALPHA_MODE } from '@/lib/alpha-mode';
import type { GameNightDto } from '@/lib/api/schemas/game-nights.schemas';

function formatScheduled(scheduledAt: string): string {
  const date = new Date(scheduledAt);
  return date.toLocaleDateString('it-IT', { weekday: 'short', day: 'numeric', month: 'short' });
}

export function GameNightZone() {
  const { data: nights, isLoading } = useUpcomingGameNights();
  const upcoming: GameNightDto[] = nights ?? [];

  if (IS_ALPHA_MODE) return null;

  if (isLoading) {
    return <div className="animate-pulse h-16 rounded-xl bg-muted/50" />;
  }

  return (
    <div data-testid="game-night-zone">
      <h3 className="text-sm font-medium text-foreground/80 mb-2">Prossima Serata</h3>
      {upcoming.length === 0 ? (
        <Link href="/game-nights/new">
          <MeepleCard
            entity="event"
            variant="compact"
            title="Pianifica una serata di gioco"
            subtitle="Invita amici e scegli i giochi"
            data-testid="game-night-suggestion"
          />
        </Link>
      ) : (
        <Link href={`/game-nights/${upcoming[0].id}`}>
          <MeepleCard
            id={upcoming[0].id}
            entity="event"
            variant="compact"
            title={upcoming[0].title}
            subtitle={`${formatScheduled(upcoming[0].scheduledAt)} · ${upcoming[0].acceptedCount} partecipanti`}
            data-testid="game-night-upcoming"
          />
        </Link>
      )}
    </div>
  );
}
