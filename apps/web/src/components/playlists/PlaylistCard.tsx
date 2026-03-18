'use client';

/**
 * PlaylistCard — Summary card for a game night playlist.
 *
 * Displays name, game count, scheduled date, and shared status.
 * Wraps in a link to the playlist detail page.
 */

import { CalendarDays, Share2, Gamepad2 } from 'lucide-react';
import Link from 'next/link';

import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import type { GameNightPlaylistDto } from '@/lib/api/schemas/playlists.schemas';

// ============================================================================
// Types
// ============================================================================

interface PlaylistCardProps {
  playlist: GameNightPlaylistDto;
}

// ============================================================================
// Helpers
// ============================================================================

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

export function PlaylistCard({ playlist }: PlaylistCardProps) {
  const gameCount = playlist.games.length;

  return (
    <Link href={`/library/playlists/${playlist.id}`} className="block group">
      <Card className="h-full transition-shadow group-hover:shadow-md">
        <CardHeader className="pb-2">
          <div className="flex items-start justify-between gap-2">
            <CardTitle className="text-base font-semibold line-clamp-2 group-hover:text-primary transition-colors">
              {playlist.name}
            </CardTitle>
            {playlist.isShared && (
              <Badge variant="secondary" className="shrink-0 flex items-center gap-1">
                <Share2 className="h-3 w-3" />
                Condivisa
              </Badge>
            )}
          </div>
        </CardHeader>
        <CardContent className="space-y-2">
          <div className="flex items-center gap-1.5 text-sm text-muted-foreground">
            <Gamepad2 className="h-4 w-4 shrink-0" />
            <span>
              {gameCount === 0
                ? 'Nessun gioco'
                : gameCount === 1
                  ? '1 gioco'
                  : `${gameCount} giochi`}
            </span>
          </div>
          {playlist.scheduledDate && (
            <div className="flex items-center gap-1.5 text-sm text-muted-foreground">
              <CalendarDays className="h-4 w-4 shrink-0" />
              <span>{formatScheduledDate(playlist.scheduledDate)}</span>
            </div>
          )}
        </CardContent>
      </Card>
    </Link>
  );
}
