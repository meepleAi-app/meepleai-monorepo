'use client';

import { Users, Gamepad2 } from 'lucide-react';
import Link from 'next/link';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import type { MeepleCardMetadata } from '@/components/ui/data-display/meeple-card';
import { cn } from '@/lib/utils';
import type { GameNightSummary, GameNightStatus } from '@/store/game-night';

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
    { icon: Users, value: String(night.playerCount) },
    { icon: Gamepad2, value: String(night.gameCount) },
  ];

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
        className="h-full"
        data-testid="game-night-card"
      />
    </Link>
  );
}
