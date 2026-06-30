'use client';

/**
 * PhotosTabContent — #2588 A1 + A2 photos tab for canonical session-live view.
 *
 * Two coexisting sections within the "Foto" tab:
 *  1. In-session photo gallery backed by the client-local IndexedDB photo-store
 *     (lib/storage/photo-store). Zero backend — local-only, session-scoped (A1).
 *  2. Vision-AI snapshots via SessionSnapshotPanel (A2) — server-backed
 *     (/live-sessions/{id}/vision-snapshots) board-state capture + game-state
 *     extraction. Backported from legacy /sessions/live/[sessionId]/photos.
 *
 * Ported from legacy /sessions/live/[sessionId]/photos/page.tsx.
 * Agent/dispute tab is a later sub-slice — NOT here.
 */

import { useRef, useState, useCallback, useEffect, type ReactElement } from 'react';

import { Camera, Image as ImageIcon, Trash2 } from 'lucide-react';

import { SessionSnapshotPanel } from '@/components/session/SessionSnapshotPanel';
import { Button } from '@/components/ui/primitives/button';
import { useTranslation } from '@/hooks/useTranslation';
import { addPhoto, listPhotos, deletePhoto, type StoredPhoto } from '@/lib/storage/photo-store';

// ─── Internal types ────────────────────────────────────────────────────────────

interface DisplayPhoto {
  id: string;
  objectUrl: string;
  timestamp: number;
}

function toDisplay(stored: StoredPhoto): DisplayPhoto {
  return {
    id: stored.id,
    objectUrl: URL.createObjectURL(stored.blob),
    timestamp: stored.timestamp,
  };
}

// ─── PhotoCard sub-component ───────────────────────────────────────────────────

interface PhotoCardProps {
  photo: DisplayPhoto;
  onDelete: (id: string) => void;
}

function PhotoCard({ photo, onDelete }: PhotoCardProps): ReactElement {
  const date = new Date(photo.timestamp);
  const timeLabel = date.toLocaleTimeString('it-IT', {
    hour: '2-digit',
    minute: '2-digit',
  });

  return (
    <div className="group relative rounded-xl overflow-hidden bg-muted aspect-square shadow-sm border border-border">
      <img
        src={photo.objectUrl}
        alt={`Foto partita ${timeLabel}`}
        className="w-full h-full object-cover"
      />
      {/* Hover overlay */}
      <div className="absolute inset-0 bg-foreground/0 group-hover:bg-foreground/30 transition-colors" />
      {/* Timestamp badge */}
      <div className="absolute bottom-0 inset-x-0 bg-gradient-to-t from-black/60 to-transparent p-2">
        <p className="text-white text-xs font-mono">{timeLabel}</p>
      </div>
      {/* Delete button */}
      <button
        type="button"
        className="absolute top-1.5 right-1.5 h-7 w-7 rounded-full bg-foreground/40 hover:bg-destructive/80 flex items-center justify-center opacity-0 group-hover:opacity-100 transition-all"
        onClick={() => onDelete(photo.id)}
        aria-label={`Elimina foto ${timeLabel}`}
        data-testid={`delete-photo-${photo.id}`}
      >
        <Trash2 className="h-3.5 w-3.5 text-white" />
      </button>
    </div>
  );
}

// ─── PhotosTabContent ──────────────────────────────────────────────────────────

export interface PhotosTabContentProps {
  readonly sessionId: string;
  /**
   * Current user id — required by SessionSnapshotPanel to attribute Vision-AI
   * snapshot uploads. Threaded from SessionLiveView (currentUser?.id ?? '').
   */
  readonly userId: string;
  /**
   * Current turn number — seeds the snapshot upload dialog's default turn.
   * Defaults to 1 (mirrors the legacy /photos page hardcode) when omitted.
   */
  readonly currentTurn?: number;
}

export function PhotosTabContent({
  sessionId,
  userId,
  currentTurn = 1,
}: PhotosTabContentProps): ReactElement {
  const { t } = useTranslation();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [photos, setPhotos] = useState<DisplayPhoto[]>([]);
  const photosRef = useRef<DisplayPhoto[]>([]);

  // Keep ref in sync so unmount cleanup sees latest object URLs
  useEffect(() => {
    photosRef.current = photos;
  }, [photos]);

  // Load session photos from IndexedDB on mount / sessionId change
  useEffect(() => {
    let cancelled = false;
    listPhotos(sessionId).then(stored => {
      if (cancelled) return;
      photosRef.current.forEach(p => URL.revokeObjectURL(p.objectUrl));
      setPhotos(stored.map(toDisplay));
    });
    return () => {
      cancelled = true;
    };
  }, [sessionId]);

  // Revoke object URLs on unmount to prevent memory leaks
  useEffect(() => {
    return () => {
      photosRef.current.forEach(p => URL.revokeObjectURL(p.objectUrl));
    };
  }, []);

  const handleCapture = useCallback(
    async (e: React.ChangeEvent<HTMLInputElement>) => {
      const file = e.target.files?.[0];
      if (!file) return;
      e.target.value = '';
      const stored = await addPhoto(sessionId, file, file.name);
      const display = toDisplay(stored);
      setPhotos(prev => [...prev, display]);
    },
    [sessionId]
  );

  const handleDelete = useCallback(async (id: string) => {
    await deletePhoto(id);
    const target = photosRef.current.find(p => p.id === id);
    if (target) URL.revokeObjectURL(target.objectUrl);
    setPhotos(prev => prev.filter(p => p.id !== id));
  }, []);

  return (
    <div className="space-y-6" data-testid="photos-tab-content">
      {/* ─── Section 1: in-session local gallery (A1) ─────────────────────── */}
      <section aria-label={t('pages.sessionLive.photosTab.galleryHeading')} className="space-y-4">
        {/* Header */}
        <div className="flex items-center justify-between">
          <div>
            <h3 className="text-base font-semibold text-foreground">Foto partita</h3>
            <p className="text-xs text-muted-foreground mt-0.5">
              {photos.length === 0 ? 'Nessuna foto ancora' : `${photos.length} foto`}
            </p>
          </div>

          <Button
            size="sm"
            className="gap-2 bg-entity-session text-white hover:opacity-90 font-nunito"
            onClick={() => fileInputRef.current?.click()}
            data-testid="capture-button"
          >
            <Camera className="h-4 w-4" />
            Scatta
          </Button>
        </div>

        {/* Hidden file input — camera capture on mobile, file picker on desktop */}
        <input
          ref={fileInputRef}
          type="file"
          accept="image/*"
          capture="environment"
          className="hidden"
          onChange={handleCapture}
          data-testid="photo-input"
        />

        {/* Empty state */}
        {photos.length === 0 && (
          <div className="flex flex-col items-center gap-3 py-12 text-muted-foreground">
            <ImageIcon className="h-12 w-12 opacity-30" />
            <p className="text-sm text-center">
              Scatta foto per documentare lo stato della partita
            </p>
            <Button
              variant="outline"
              className="gap-2"
              onClick={() => fileInputRef.current?.click()}
            >
              <Camera className="h-4 w-4" />
              Prima foto
            </Button>
          </div>
        )}

        {/* Photo grid */}
        {photos.length > 0 && (
          <div className="grid grid-cols-2 gap-3">
            {photos.map(photo => (
              <PhotoCard key={photo.id} photo={photo} onDelete={handleDelete} />
            ))}
          </div>
        )}
      </section>

      {/* ─── Section 2: Vision-AI snapshots (A2) ──────────────────────────── */}
      <section
        aria-label={t('pages.sessionLive.photosTab.snapshotsHeading')}
        className="border-t border-border pt-6"
        data-testid="photos-tab-snapshots"
      >
        <SessionSnapshotPanel sessionId={sessionId} userId={userId} currentTurn={currentTurn} />
      </section>
    </div>
  );
}
