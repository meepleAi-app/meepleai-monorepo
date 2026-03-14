'use client';

/**
 * PlaylistShareDialog — Manage sharing for a playlist.
 *
 * If the playlist is already shared: shows the share URL with a copy button
 * and an option to revoke the share link.
 * If not shared: shows a button to generate a share link.
 */

import * as React from 'react';

import { Check, Copy, Link2, Link2Off, Share2 } from 'lucide-react';

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
import { useSharePlaylist, useRevokePlaylistShare } from '@/hooks/queries/usePlaylists';
import type { GameNightPlaylistDto } from '@/lib/api/schemas/playlists.schemas';

// ============================================================================
// Types
// ============================================================================

interface PlaylistShareDialogProps {
  playlist: GameNightPlaylistDto;
}

// ============================================================================
// Component
// ============================================================================

export function PlaylistShareDialog({ playlist }: PlaylistShareDialogProps) {
  const [open, setOpen] = React.useState(false);
  const [copied, setCopied] = React.useState(false);
  const [shareUrl, setShareUrl] = React.useState<string | null>(null);

  const sharePlaylist = useSharePlaylist();
  const revokeShare = useRevokePlaylistShare();

  // Derive current share URL:
  // 1. From the mutation result (fresh link just generated)
  // 2. From shareToken stored on the playlist (after page reload)
  const resolvedShareUrl =
    shareUrl ??
    (playlist.shareToken
      ? `${typeof window !== 'undefined' ? window.location.origin : ''}/playlists/shared/${playlist.shareToken}`
      : null);

  const isShared = playlist.isShared || resolvedShareUrl !== null;

  async function handleGenerateLink() {
    try {
      const result = await sharePlaylist.mutateAsync(playlist.id);
      setShareUrl(result.shareUrl);
    } catch {
      // Keep dialog open on error
    }
  }

  async function handleRevoke() {
    try {
      await revokeShare.mutateAsync(playlist.id);
      setShareUrl(null);
    } catch {
      // Keep dialog open on error
    }
  }

  async function handleCopy() {
    if (!resolvedShareUrl) return;
    try {
      await navigator.clipboard.writeText(resolvedShareUrl);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Clipboard not available
    }
  }

  function handleOpenChange(nextOpen: boolean) {
    setOpen(nextOpen);
    if (!nextOpen) {
      setCopied(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogTrigger asChild>
        <Button variant="outline" size="sm">
          <Share2 className="h-4 w-4 mr-1.5" />
          Condividi
        </Button>
      </DialogTrigger>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Condividi playlist</DialogTitle>
        </DialogHeader>

        <div className="space-y-4 pt-2">
          {isShared && resolvedShareUrl ? (
            <>
              <p className="text-sm text-muted-foreground">
                Chiunque abbia il link può visualizzare questa playlist.
              </p>

              <div className="space-y-1.5">
                <Label htmlFor="share-url">Link di condivisione</Label>
                <div className="flex gap-2">
                  <Input
                    id="share-url"
                    readOnly
                    value={resolvedShareUrl}
                    className="text-sm"
                    onClick={e => (e.target as HTMLInputElement).select()}
                  />
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={handleCopy}
                    className="shrink-0"
                    aria-label="Copia link"
                  >
                    {copied ? (
                      <Check className="h-4 w-4 text-green-600" />
                    ) : (
                      <Copy className="h-4 w-4" />
                    )}
                  </Button>
                </div>
              </div>

              <div className="flex justify-between items-center pt-1">
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  className="text-destructive hover:text-destructive hover:bg-destructive/10"
                  onClick={handleRevoke}
                  disabled={revokeShare.isPending}
                >
                  <Link2Off className="h-4 w-4 mr-1.5" />
                  {revokeShare.isPending ? 'Revoca in corso…' : 'Revoca link'}
                </Button>
                <Button type="button" onClick={() => setOpen(false)}>
                  Chiudi
                </Button>
              </div>
            </>
          ) : (
            <>
              <p className="text-sm text-muted-foreground">
                Genera un link per condividere questa playlist con altri utenti.
              </p>
              <div className="flex justify-end gap-2 pt-1">
                <Button type="button" variant="outline" onClick={() => setOpen(false)}>
                  Annulla
                </Button>
                <Button
                  type="button"
                  onClick={handleGenerateLink}
                  disabled={sharePlaylist.isPending}
                >
                  <Link2 className="h-4 w-4 mr-1.5" />
                  {sharePlaylist.isPending ? 'Generazione…' : 'Genera link'}
                </Button>
              </div>
            </>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}
