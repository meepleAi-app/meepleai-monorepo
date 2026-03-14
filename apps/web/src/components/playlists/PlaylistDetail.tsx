'use client';

/**
 * PlaylistDetail — Full detail view for a single playlist.
 *
 * Shows playlist name, scheduled date, edit/share controls, and a
 * reorderable list of games with remove buttons.
 * Drag-reorder uses the HTML5 drag-and-drop API.
 */

import * as React from 'react';

import { GripVertical, Trash2, Gamepad2 } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  usePlaylist,
  useRemoveGameFromPlaylist,
  useReorderPlaylistGames,
} from '@/hooks/queries/usePlaylists';
import type { PlaylistGameDto } from '@/lib/api/schemas/playlists.schemas';

import { PlaylistForm } from './PlaylistForm';
import { PlaylistShareDialog } from './PlaylistShareDialog';

// ============================================================================
// Types
// ============================================================================

interface PlaylistDetailProps {
  playlistId: string;
}

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
// GameRow sub-component
// ============================================================================

interface GameRowProps {
  game: PlaylistGameDto;
  index: number;
  isDragging: boolean;
  isDragOver: boolean;
  onDragStart: (index: number) => void;
  onDragOver: (e: React.DragEvent, index: number) => void;
  onDrop: (e: React.DragEvent, index: number) => void;
  onDragEnd: () => void;
  onRemove: (gameId: string) => void;
  isRemoving: boolean;
}

function GameRow({
  game,
  index,
  isDragging,
  isDragOver,
  onDragStart,
  onDragOver,
  onDrop,
  onDragEnd,
  onRemove,
  isRemoving,
}: GameRowProps) {
  return (
    <div
      draggable
      onDragStart={() => onDragStart(index)}
      onDragOver={e => onDragOver(e, index)}
      onDrop={e => onDrop(e, index)}
      onDragEnd={onDragEnd}
      className={[
        'flex items-center gap-3 p-3 rounded-lg border bg-card transition-all',
        isDragging ? 'opacity-50 cursor-grabbing' : 'cursor-grab',
        isDragOver ? 'border-primary bg-primary/5' : 'border-border/50',
      ].join(' ')}
    >
      <GripVertical className="h-4 w-4 text-muted-foreground shrink-0" />
      <Badge variant="outline" className="shrink-0 w-7 justify-center text-xs">
        {index + 1}
      </Badge>
      <div className="flex items-center gap-2 flex-1 min-w-0">
        <Gamepad2 className="h-4 w-4 text-muted-foreground shrink-0" />
        <span className="text-sm truncate font-mono text-muted-foreground">
          {game.sharedGameId.slice(0, 8)}…
        </span>
      </div>
      <Button
        variant="ghost"
        size="sm"
        className="shrink-0 text-destructive hover:text-destructive hover:bg-destructive/10"
        onClick={() => onRemove(game.sharedGameId)}
        disabled={isRemoving}
        aria-label="Rimuovi gioco"
      >
        <Trash2 className="h-4 w-4" />
      </Button>
    </div>
  );
}

// ============================================================================
// Component
// ============================================================================

export function PlaylistDetail({ playlistId }: PlaylistDetailProps) {
  const { data: playlist, isLoading, error } = usePlaylist(playlistId);
  const removeGame = useRemoveGameFromPlaylist();
  const reorderGames = useReorderPlaylistGames();

  const [dragSourceIndex, setDragSourceIndex] = React.useState<number | null>(null);
  const [dragOverIndex, setDragOverIndex] = React.useState<number | null>(null);
  // Local optimistic order for smoother drag UX
  const [localGames, setLocalGames] = React.useState<PlaylistGameDto[]>([]);

  // Sync local games when server data arrives
  React.useEffect(() => {
    if (playlist?.games) {
      const sorted = [...playlist.games].sort((a, b) => a.position - b.position);
      setLocalGames(sorted);
    }
  }, [playlist?.games]);

  function handleDragStart(index: number) {
    setDragSourceIndex(index);
  }

  function handleDragOver(e: React.DragEvent, index: number) {
    e.preventDefault();
    setDragOverIndex(index);
  }

  function handleDrop(e: React.DragEvent, dropIndex: number) {
    e.preventDefault();
    if (dragSourceIndex === null || dragSourceIndex === dropIndex) {
      setDragOverIndex(null);
      return;
    }
    const reordered = [...localGames];
    const [moved] = reordered.splice(dragSourceIndex, 1);
    reordered.splice(dropIndex, 0, moved);
    setLocalGames(reordered);
    setDragSourceIndex(null);
    setDragOverIndex(null);
    reorderGames.mutate({
      playlistId,
      data: { orderedGameIds: reordered.map(g => g.sharedGameId) },
    });
  }

  function handleDragEnd() {
    setDragSourceIndex(null);
    setDragOverIndex(null);
  }

  function handleRemove(gameId: string) {
    removeGame.mutate({ playlistId, gameId });
  }

  // ── Loading ───────────────────────────────────────────────────────────────
  if (isLoading) {
    return (
      <div className="space-y-4 animate-pulse">
        <div className="h-8 w-48 bg-muted rounded" />
        <div className="h-4 w-32 bg-muted rounded" />
        <div className="space-y-2">
          {[1, 2, 3].map(i => (
            <div key={i} className="h-14 bg-muted rounded-lg" />
          ))}
        </div>
      </div>
    );
  }

  // ── Error ─────────────────────────────────────────────────────────────────
  if (error || !playlist) {
    return (
      <Card>
        <CardContent className="py-10 text-center text-muted-foreground">
          Impossibile caricare la playlist.
        </CardContent>
      </Card>
    );
  }

  // ── Render ────────────────────────────────────────────────────────────────
  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div className="space-y-1">
          <h1 className="text-2xl font-bold tracking-tight">{playlist.name}</h1>
          {playlist.scheduledDate && (
            <p className="text-sm text-muted-foreground">
              {formatScheduledDate(playlist.scheduledDate)}
            </p>
          )}
        </div>
        <div className="flex items-center gap-2 shrink-0">
          <PlaylistForm playlist={playlist} />
          <PlaylistShareDialog playlist={playlist} />
        </div>
      </div>

      {/* Game list */}
      {localGames.length === 0 ? (
        <Card>
          <CardContent className="py-12 flex flex-col items-center gap-3 text-center text-muted-foreground">
            <Gamepad2 className="h-10 w-10 opacity-30" />
            <p>Nessun gioco in questa playlist.</p>
            <p className="text-sm">Aggiungili dalla pagina di catalogo.</p>
          </CardContent>
        </Card>
      ) : (
        <div className="space-y-2">
          {localGames.map((game, index) => (
            <GameRow
              key={game.sharedGameId}
              game={game}
              index={index}
              isDragging={dragSourceIndex === index}
              isDragOver={dragOverIndex === index}
              onDragStart={handleDragStart}
              onDragOver={handleDragOver}
              onDrop={handleDrop}
              onDragEnd={handleDragEnd}
              onRemove={handleRemove}
              isRemoving={removeGame.isPending}
            />
          ))}
        </div>
      )}
    </div>
  );
}
