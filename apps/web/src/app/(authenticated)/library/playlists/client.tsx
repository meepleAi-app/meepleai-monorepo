'use client';

/**
 * Playlists page — list of all game night playlists.
 *
 * Shows a grid of PlaylistCard components with a create button.
 * Handles loading (skeleton) and empty states.
 */

import { MeeplePlaylistCard } from '@/components/playlists/MeeplePlaylistCard';
import { PlaylistForm } from '@/components/playlists/PlaylistForm';
import { usePlaylists } from '@/hooks/queries/usePlaylists';

// ============================================================================
// Skeleton
// ============================================================================

function PlaylistGridSkeleton() {
  return (
    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
      {[1, 2, 3].map(i => (
        <div key={i} className="h-36 animate-pulse rounded-lg bg-muted" />
      ))}
    </div>
  );
}

// ============================================================================
// Page
// ============================================================================

export default function PlaylistsPage() {
  const { data, isLoading } = usePlaylists();

  const playlists = data?.playlists ?? [];

  return (
    <div className="space-y-6 p-4 sm:p-6">
      {/* Header */}
      <div className="flex items-center justify-between gap-4">
        <h1 className="text-2xl font-bold tracking-tight">Playlist</h1>
        <PlaylistForm />
      </div>

      {/* Content */}
      {isLoading ? (
        <PlaylistGridSkeleton />
      ) : playlists.length === 0 ? (
        <div className="flex flex-col items-center justify-center gap-3 py-20 text-center text-muted-foreground">
          <p className="text-lg font-medium">Nessuna playlist ancora</p>
          <p className="text-sm">Crea la tua prima playlist per organizzare le serate da gioco.</p>
          <PlaylistForm />
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {playlists.map(playlist => (
            <MeeplePlaylistCard key={playlist.id} playlist={playlist} />
          ))}
        </div>
      )}
    </div>
  );
}
