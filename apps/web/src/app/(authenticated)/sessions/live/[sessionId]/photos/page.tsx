/**
 * Photos Child Card — /sessions/live/[sessionId]/photos
 *
 * Camera capture for board-state photos. Photos are persisted in IndexedDB
 * via lib/storage/photo-store. Local-only (no cross-device sync).
 */

'use client';

import { use, useRef, useState, useCallback, useEffect } from 'react';

import { Camera, Image as ImageIcon, Trash2 } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { addPhoto, listPhotos, deletePhoto, type StoredPhoto } from '@/lib/storage/photo-store';

// ─── Types ────────────────────────────────────────────────────────────────────

interface DisplayPhoto {
  id: string;
  objectUrl: string;
  timestamp: number;
}

// ─── Sub-components ───────────────────────────────────────────────────────────

interface PhotoCardProps {
  photo: DisplayPhoto;
  onDelete: (id: string) => void;
}

function PhotoCard({ photo, onDelete }: PhotoCardProps) {
  const date = new Date(photo.timestamp);
  const timeLabel = date.toLocaleTimeString('it-IT', {
    hour: '2-digit',
    minute: '2-digit',
  });

  return (
    <div className="group relative rounded-xl overflow-hidden bg-gray-100 aspect-square shadow-sm border border-white/60">
      {/* eslint-disable-next-line @next/next/no-img-element */}
      <img
        src={photo.objectUrl}
        alt={`Foto partita ${timeLabel}`}
        className="w-full h-full object-cover"
      />
      {/* Overlay */}
      <div className="absolute inset-0 bg-black/0 group-hover:bg-black/30 transition-colors" />
      {/* Timestamp */}
      <div className="absolute bottom-0 inset-x-0 bg-gradient-to-t from-black/60 to-transparent p-2">
        <p className="text-white text-xs font-mono">{timeLabel}</p>
      </div>
      {/* Delete button */}
      <button
        className="absolute top-1.5 right-1.5 h-7 w-7 rounded-full bg-black/40 hover:bg-red-500/80 flex items-center justify-center opacity-0 group-hover:opacity-100 transition-all"
        onClick={() => onDelete(photo.id)}
        aria-label={`Elimina foto ${timeLabel}`}
      >
        <Trash2 className="h-3.5 w-3.5 text-white" />
      </button>
    </div>
  );
}

// ─── Main Component ────────────────────────────────────────────────────────────

interface PhotosPageProps {
  params: Promise<{ sessionId: string }>;
}

function toDisplay(stored: StoredPhoto): DisplayPhoto {
  return {
    id: stored.id,
    objectUrl: URL.createObjectURL(stored.blob),
    timestamp: stored.timestamp,
  };
}

export default function PhotosPage({ params }: PhotosPageProps) {
  const { sessionId } = use(params);

  const fileInputRef = useRef<HTMLInputElement>(null);
  const [photos, setPhotos] = useState<DisplayPhoto[]>([]);
  const photosRef = useRef<DisplayPhoto[]>([]);

  // Keep ref in sync with state so unmount cleanup sees latest photos
  useEffect(() => {
    photosRef.current = photos;
  }, [photos]);

  // Load existing photos from IndexedDB on mount
  useEffect(() => {
    let cancelled = false;
    listPhotos(sessionId).then(stored => {
      if (cancelled) return;
      setPhotos(stored.map(toDisplay));
    });
    return () => {
      cancelled = true;
    };
  }, [sessionId]);

  // Revoke object URLs on unmount to avoid memory leaks
  useEffect(() => {
    return () => {
      photosRef.current.forEach(p => URL.revokeObjectURL(p.objectUrl));
    };
  }, []);

  const handleCapture = useCallback(
    async (e: React.ChangeEvent<HTMLInputElement>) => {
      const file = e.target.files?.[0];
      if (!file) return;

      // Reset input so same file can be re-selected
      e.target.value = '';

      const stored = await addPhoto(sessionId, file, file.name);
      // Compute display object ONCE outside the state updater to avoid
      // double invocation (and double createObjectURL) in StrictMode.
      const display = toDisplay(stored);
      setPhotos(prev => [...prev, display]);
    },
    [sessionId]
  );

  const handleDelete = useCallback(async (id: string) => {
    await deletePhoto(id);
    // Look up via ref and revoke OUTSIDE the updater to avoid double-revoke
    // in StrictMode (keeps side effects out of the functional updater).
    const target = photosRef.current.find(p => p.id === id);
    if (target) URL.revokeObjectURL(target.objectUrl);
    setPhotos(prev => prev.filter(p => p.id !== id));
  }, []);

  return (
    <div className="space-y-4 p-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-xl font-quicksand font-bold text-gray-900">Foto partita</h2>
          <p className="text-xs text-gray-500 font-nunito mt-0.5">
            {photos.length === 0 ? 'Nessuna foto ancora' : `${photos.length} foto`}
          </p>
        </div>

        <Button
          size="sm"
          className="gap-2 bg-amber-500 hover:bg-amber-600 text-white font-nunito"
          onClick={() => fileInputRef.current?.click()}
          data-testid="capture-button"
        >
          <Camera className="h-4 w-4" />
          Scatta
        </Button>
      </div>

      {/* Hidden file input (camera capture on mobile) */}
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
        <div className="flex flex-col items-center gap-3 py-12 text-gray-400">
          <ImageIcon className="h-12 w-12 opacity-30" />
          <p className="text-sm font-nunito text-center">
            Scatta foto per documentare lo stato della partita
          </p>
          <Button
            variant="outline"
            className="gap-2 font-nunito"
            onClick={() => fileInputRef.current?.click()}
          >
            <Camera className="h-4 w-4" />
            Prima foto
          </Button>
        </div>
      )}

      {/* Photo grid */}
      {photos.length > 0 && (
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
          {photos.map(photo => (
            <PhotoCard key={photo.id} photo={photo} onDelete={handleDelete} />
          ))}
        </div>
      )}
    </div>
  );
}
