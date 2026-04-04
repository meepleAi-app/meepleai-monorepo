'use client';

/**
 * MeeplePlaylistCard — MeepleCard-based summary card for a game night playlist.
 *
 * Displays name, game count, scheduled date, and shared status
 * using the MeepleCard design system with entity="collection".
 */

import { CalendarDays, Gamepad2 } from 'lucide-react';
import Link from 'next/link';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import type { MeepleCardMetadata } from '@/components/ui/data-display/meeple-card';
import type { GameNightPlaylistDto } from '@/lib/api/schemas/playlists.schemas';

// ============================================================================
// Types
// ============================================================================

interface MeeplePlaylistCardProps {
  playlist: GameNightPlaylistDto;
}

// ============================================================================
// Helpers
// ============================================================================

function formatGameCount(count: number): string {
  if (count === 0) return 'Nessun gioco';
  if (count === 1) return '1 gioco';
  return `${count} giochi`;
}

function formatScheduledDate(dateStr: string): string {
  const date = new Date(dateStr);
  return date.toLocaleDateString('it-IT', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  });
}

// ============================================================================
// Component
// ============================================================================

export function MeeplePlaylistCard({ playlist }: MeeplePlaylistCardProps) {
  const gameCount = playlist.games.length;

  const metadata: MeepleCardMetadata[] = [
    { icon: Gamepad2, label: formatGameCount(gameCount) },
    ...(playlist.scheduledDate
      ? [{ icon: CalendarDays, label: formatScheduledDate(playlist.scheduledDate) }]
      : []),
  ];

  return (
    <Link href={`/library/playlists/${playlist.id}`} className="block">
      <MeepleCard
        entity="collection"
        variant="list"
        title={playlist.name}
        subtitle={playlist.isShared ? 'Condivisa' : undefined}
        metadata={metadata}
        badge={playlist.isShared ? 'Condivisa' : undefined}
        data-testid="playlist-card"
      />
    </Link>
  );
}
