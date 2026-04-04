'use client';

/**
 * Shared playlist page — read-only public view accessed via share token.
 *
 * Shows playlist name, scheduled date, and game list with position badges.
 */

import { use } from 'react';

import { CalendarDays, Gamepad2 } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { useSharedPlaylist } from '@/hooks/queries/usePlaylists';

// ============================================================================
// Helpers
// ============================================================================

function formatScheduledDate(dateStr: string): string {
  const date = new Date(dateStr);
  return date.toLocaleDateString('it-IT', {
    weekday: 'long',
    day: '2-digit',
    month: 'long',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

// ============================================================================
// Skeleton
// ============================================================================

function SharedPlaylistSkeleton() {
  return (
    <div className="space-y-4 animate-pulse">
      <div className="h-8 w-56 bg-muted rounded" />
      <div className="h-4 w-40 bg-muted rounded" />
      <div className="space-y-2">
        {[1, 2, 3].map(i => (
          <div key={i} className="h-14 bg-muted rounded-lg" />
        ))}
      </div>
    </div>
  );
}

// ============================================================================
// Page
// ============================================================================

export default function SharedPlaylistPage({ params }: { params: Promise<{ token: string }> }) {
  const { token } = use(params);
  const { data: playlist, isLoading, error } = useSharedPlaylist(token);

  if (isLoading) {
    return (
      <div className="p-4 sm:p-6">
        <SharedPlaylistSkeleton />
      </div>
    );
  }

  if (error || !playlist) {
    return (
      <div className="p-4 sm:p-6">
        <Card>
          <CardContent className="py-16 flex flex-col items-center gap-3 text-center text-muted-foreground">
            <Gamepad2 className="h-10 w-10 opacity-30" />
            <p className="text-lg font-medium">Playlist non trovata</p>
            <p className="text-sm">
              Il link potrebbe essere scaduto o la condivisione è stata revocata.
            </p>
          </CardContent>
        </Card>
      </div>
    );
  }

  const sortedGames = [...playlist.games].sort((a, b) => a.position - b.position);

  return (
    <div className="space-y-6 p-4 sm:p-6">
      {/* Header */}
      <div className="space-y-1">
        <h1 className="text-2xl font-bold tracking-tight">{playlist.name}</h1>
        {playlist.scheduledDate && (
          <div className="flex items-center gap-1.5 text-sm text-muted-foreground">
            <CalendarDays className="h-4 w-4 shrink-0" />
            <span>{formatScheduledDate(playlist.scheduledDate)}</span>
          </div>
        )}
      </div>

      {/* Game list */}
      {sortedGames.length === 0 ? (
        <Card>
          <CardContent className="py-12 flex flex-col items-center gap-3 text-center text-muted-foreground">
            <Gamepad2 className="h-10 w-10 opacity-30" />
            <p>Nessun gioco in questa playlist.</p>
          </CardContent>
        </Card>
      ) : (
        <div className="space-y-2">
          {sortedGames.map((game, index) => (
            <div
              key={game.sharedGameId}
              className="flex items-center gap-3 p-3 rounded-lg border border-border/50 bg-card"
            >
              <Badge variant="outline" className="shrink-0 w-7 justify-center text-xs">
                {index + 1}
              </Badge>
              <div className="flex items-center gap-2 flex-1 min-w-0">
                <Gamepad2 className="h-4 w-4 text-muted-foreground shrink-0" />
                <span className="text-sm truncate font-mono text-muted-foreground">
                  {game.sharedGameId.slice(0, 8)}…
                </span>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
