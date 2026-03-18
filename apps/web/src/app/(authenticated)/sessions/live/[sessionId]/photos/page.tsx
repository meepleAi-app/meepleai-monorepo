/**
 * Photos Child Card — /sessions/live/[sessionId]/photos
 *
 * Game Night Improvvisata — Task 21
 *
 * Camera capture for board-state photos. Grid of captured photos with
 * timestamps. Photos are held locally in component state; if the
 * session attachment endpoint exists they can be uploaded there.
 */

'use client';

import { use, useRef, useState, useCallback } from 'react';

import { Camera, Image as ImageIcon, Trash2, Upload } from 'lucide-react';

import { Button } from '@/components/ui/button';

// ─── Types ────────────────────────────────────────────────────────────────────

interface CapturedPhoto {
  id: string;
  dataUrl: string;
  timestamp: number;
  filename: string;
}

// ─── Sub-components ───────────────────────────────────────────────────────────

interface PhotoCardProps {
  photo: CapturedPhoto;
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
        src={photo.dataUrl}
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

export default function PhotosPage({ params }: PhotosPageProps) {
  const { sessionId: _sessionId } = use(params);

  const fileInputRef = useRef<HTMLInputElement>(null);
  const [photos, setPhotos] = useState<CapturedPhoto[]>([]);
  const [isUploading, setIsUploading] = useState(false);

  const handleCapture = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    // Reset input so same file can be re-selected
    e.target.value = '';

    const reader = new FileReader();
    reader.onload = ev => {
      const dataUrl = ev.target?.result as string;
      if (!dataUrl) return;
      setPhotos(prev => [
        ...prev,
        {
          id: `photo-${Date.now()}-${Math.random().toString(36).slice(2, 6)}`,
          dataUrl,
          timestamp: Date.now(),
          filename: file.name,
        },
      ]);
    };
    reader.readAsDataURL(file);
  }, []);

  function handleDelete(id: string) {
    setPhotos(prev => prev.filter(p => p.id !== id));
  }

  async function handleUploadAll() {
    if (photos.length === 0) return;
    setIsUploading(true);
    try {
      // Placeholder: session attachment upload endpoint
      // await api.sessions.uploadAttachment(sessionId, photoBlob)
      // For now: simulate upload delay
      await new Promise(r => setTimeout(r, 1000));
    } finally {
      setIsUploading(false);
    }
  }

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

        <div className="flex gap-2">
          {photos.length > 0 && (
            <Button
              size="sm"
              variant="outline"
              className="gap-1 font-nunito text-xs"
              onClick={handleUploadAll}
              disabled={isUploading}
              data-testid="upload-button"
            >
              <Upload className="h-3.5 w-3.5" />
              {isUploading ? 'Caricamento...' : 'Carica'}
            </Button>
          )}

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
