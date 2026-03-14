'use client';

/**
 * PlaylistForm — Dialog form for creating or editing a playlist.
 *
 * In create mode (no playlist prop): uses useCreatePlaylist().
 * In edit mode (playlist prop provided): uses useUpdatePlaylist().
 */

import * as React from 'react';

import { Pencil, Plus } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/overlays/dialog';
import { useCreatePlaylist, useUpdatePlaylist } from '@/hooks/queries/usePlaylists';
import type { GameNightPlaylistDto } from '@/lib/api/schemas/playlists.schemas';

// ============================================================================
// Types
// ============================================================================

interface PlaylistFormProps {
  playlist?: GameNightPlaylistDto;
  trigger?: React.ReactNode;
  onSuccess?: () => void;
}

// ============================================================================
// Helpers
// ============================================================================

/** Convert ISO string to datetime-local input value (YYYY-MM-DDTHH:mm) */
function toDatetimeLocal(isoString: string): string {
  return isoString.slice(0, 16);
}

/** Convert datetime-local input value to ISO string */
function fromDatetimeLocal(value: string): string {
  return value ? new Date(value).toISOString() : '';
}

// ============================================================================
// Component
// ============================================================================

export function PlaylistForm({ playlist, trigger, onSuccess }: PlaylistFormProps) {
  const isEditMode = Boolean(playlist);
  const [open, setOpen] = React.useState(false);
  const [name, setName] = React.useState(playlist?.name ?? '');
  const [scheduledDate, setScheduledDate] = React.useState(
    playlist?.scheduledDate ? toDatetimeLocal(playlist.scheduledDate) : ''
  );

  const createPlaylist = useCreatePlaylist();
  const updatePlaylist = useUpdatePlaylist();

  const isPending = createPlaylist.isPending || updatePlaylist.isPending;

  // Reset form when dialog opens (for re-use after close)
  function handleOpenChange(nextOpen: boolean) {
    setOpen(nextOpen);
    if (nextOpen) {
      setName(playlist?.name ?? '');
      setScheduledDate(playlist?.scheduledDate ? toDatetimeLocal(playlist.scheduledDate) : '');
    }
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!name.trim()) return;

    const data = {
      name: name.trim(),
      ...(scheduledDate ? { scheduledDate: fromDatetimeLocal(scheduledDate) } : {}),
    };

    try {
      if (isEditMode && playlist) {
        await updatePlaylist.mutateAsync({ id: playlist.id, data });
      } else {
        await createPlaylist.mutateAsync(data);
      }
      setOpen(false);
      onSuccess?.();
    } catch {
      // Errors handled by mutation; keep dialog open
    }
  }

  const defaultTrigger = isEditMode ? (
    <Button variant="outline" size="sm">
      <Pencil className="h-4 w-4 mr-1.5" />
      Modifica
    </Button>
  ) : (
    <Button size="sm">
      <Plus className="h-4 w-4 mr-1.5" />
      Nuova playlist
    </Button>
  );

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogTrigger asChild>{trigger ?? defaultTrigger}</DialogTrigger>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{isEditMode ? 'Modifica playlist' : 'Nuova playlist'}</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4 pt-2">
          <div className="space-y-1.5">
            <Label htmlFor="playlist-name">
              Nome <span className="text-destructive">*</span>
            </Label>
            <Input
              id="playlist-name"
              value={name}
              onChange={e => setName(e.target.value)}
              maxLength={100}
              placeholder="Es. Serata con amici"
              required
              disabled={isPending}
            />
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="playlist-date">Data programmata</Label>
            <Input
              id="playlist-date"
              type="datetime-local"
              value={scheduledDate}
              onChange={e => setScheduledDate(e.target.value)}
              disabled={isPending}
            />
          </div>

          <div className="flex justify-end gap-2 pt-2">
            <Button
              type="button"
              variant="outline"
              onClick={() => setOpen(false)}
              disabled={isPending}
            >
              Annulla
            </Button>
            <Button type="submit" disabled={isPending || !name.trim()}>
              {isPending ? 'Salvataggio…' : isEditMode ? 'Salva modifiche' : 'Crea playlist'}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}
