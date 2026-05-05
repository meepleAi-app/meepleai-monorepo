'use client';

import { useMemo } from 'react';

import { Users, Gamepad2 } from 'lucide-react';
import Link from 'next/link';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import type { MeepleCardMetadata } from '@/components/ui/data-display/meeple-card';
import { buildEventConnections } from '@/components/ui/data-display/meeple-card/nav-items';
import { cn } from '@/lib/utils';
import type { GameNightSummary, GameNightStatus } from '@/stores/game-night';

const STATUS_BADGE: Record<GameNightStatus, string> = {
  upcoming: 'Prossima',
  draft: 'Bozza',
  completed: 'Completata',
};

interface MeepleGameNightCardProps {
  night: GameNightSummary;
}

export function MeepleGameNightCard({ night }: MeepleGameNightCardProps) {
  const dateStr = new Date(night.date).toLocaleDateString('it-IT', {
    weekday: 'short',
    day: 'numeric',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
  });

  const subtitle = [dateStr, night.location].filter(Boolean).join(' \u00B7 ');

  const metadata: MeepleCardMetadata[] = [
    { icon: <Users className="h-4 w-4" />, label: String(night.playerCount) },
    { icon: <Gamepad2 className="h-4 w-4" />, label: String(night.gameCount) },
  ];

  const connections = useMemo(
    () =>
      buildEventConnections(
        { participantCount: night.playerCount, gameCount: night.gameCount },
        {
          // Link-style: entire card navigates via <Link> wrapper, so inner clicks
          // are intentionally no-ops to avoid double navigation.
          onParticipantsClick: () => {},
          onGamesClick: () => {},
        }
      ),
    [night.playerCount, night.gameCount]
  );

  return (
    <Link
      href={`/game-nights/${night.id}`}
      className={cn('block', night.status === 'completed' && 'opacity-60')}
    >
      <MeepleCard
        entity="event"
        variant="grid"
        title={night.title}
        subtitle={subtitle}
        metadata={metadata}
        badge={STATUS_BADGE[night.status] ?? 'Sconosciuto'}
        connections={connections}
        className="h-full"
        data-testid="game-night-card"
      />
    </Link>
  );
}
