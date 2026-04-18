'use client';

/**
 * SnapshotUploadDialog — Modal for uploading snapshot images.
 *
 * Session Vision AI — Task 15.3
 */

import { useState, useRef, useCallback } from 'react';

import { Upload, X, ImagePlus } from 'lucide-react';

import { cn } from '@/lib/utils';

interface SnapshotUploadDialogProps {
  open: boolean;
  onClose: () => void;
  onUpload: (images: File[], caption: string, turnNumber: number) => void;
  defaultTurnNumber?: number;
  isUploading?: boolean;
}

interface PreviewImage {
  file: File;
  url: string;
}

export function SnapshotUploadDialog({
  open,
  onClose,
  onUpload,
  defaultTurnNumber = 1,
  isUploading = false,
}: SnapshotUploadDialogProps) {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [previews, setPreviews] = useState<PreviewImage[]>([]);
  const [caption, setCaption] = useState('');
  const [turnNumber, setTurnNumber] = useState(defaultTurnNumber);

  const handleFiles = useCallback((files: FileList | null) => {
    if (!files) return;
    const newPreviews: PreviewImage[] = [];
    for (let i = 0; i < files.length; i++) {
      const file = files[i];
      if (file.type.startsWith('image/')) {
        newPreviews.push({ file, url: URL.createObjectURL(file) });
      }
    }
    setPreviews(prev => [...prev, ...newPreviews]);
  }, []);

  const removePreview = useCallback((index: number) => {
    setPreviews(prev => {
      const removed = prev[index];
      if (removed) URL.revokeObjectURL(removed.url);
      return prev.filter((_, i) => i !== index);
    });
  }, []);

  const handleSubmit = () => {
    if (previews.length === 0) return;
    onUpload(
      previews.map(p => p.file),
      caption.trim(),
      turnNumber
    );
    // Cleanup
    previews.forEach(p => URL.revokeObjectURL(p.url));
    setPreviews([]);
    setCaption('');
  };

  const handleClose = () => {
    previews.forEach(p => URL.revokeObjectURL(p.url));
    setPreviews([]);
    setCaption('');
    onClose();
  };

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
      <div className="mx-4 w-full max-w-md rounded-2xl border border-[var(--nh-border-default)] bg-white p-5 shadow-xl">
        {/* Header */}
        <div className="mb-4 flex items-center justify-between">
          <h3 className="font-quicksand text-lg font-bold text-[var(--nh-text-primary)]">
            Nuovo Snapshot
          </h3>
          <button
            type="button"
            onClick={handleClose}
            className="flex h-8 w-8 items-center justify-center rounded-lg text-[var(--nh-text-muted)] hover:bg-stone-100"
            aria-label="Chiudi"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        {/* File drop zone */}
        <button
          type="button"
          onClick={() => fileInputRef.current?.click()}
          className={cn(
            'mb-4 flex w-full flex-col items-center gap-2 rounded-xl border-2 border-dashed p-6 transition-colors',
            'border-stone-300 hover:border-amber-400 hover:bg-amber-50/50'
          )}
        >
          <ImagePlus className="h-8 w-8 text-stone-400" />
          <span className="font-nunito text-sm text-[var(--nh-text-muted)]">
            Tocca per selezionare immagini
          </span>
        </button>

        <input
          ref={fileInputRef}
          type="file"
          accept="image/jpeg,image/png,image/webp"
          multiple
          className="hidden"
          onChange={e => {
            handleFiles(e.target.files);
            e.target.value = '';
          }}
        />

        {/* Previews */}
        {previews.length > 0 && (
          <div className="mb-4 flex flex-wrap gap-2">
            {previews.map((p, idx) => (
              <div key={p.url} className="group relative">
                <img
                  src={p.url}
                  alt={`Anteprima ${idx + 1}`}
                  className="h-16 w-20 rounded-lg border border-stone-200 object-cover"
                />
                <button
                  type="button"
                  onClick={() => removePreview(idx)}
                  aria-label={`Rimuovi immagine ${idx + 1}`}
                  className="absolute -right-1.5 -top-1.5 flex h-5 w-5 items-center justify-center rounded-full bg-red-500 text-white opacity-0 shadow-sm transition-opacity group-hover:opacity-100"
                >
                  <X className="h-3 w-3" />
                </button>
              </div>
            ))}
          </div>
        )}

        {/* Turn number */}
        <div className="mb-3">
          <label className="mb-1 block font-nunito text-xs font-semibold text-[var(--nh-text-muted)]">
            Numero turno
          </label>
          <input
            type="number"
            min={1}
            value={turnNumber}
            onChange={e => setTurnNumber(Math.max(1, parseInt(e.target.value) || 1))}
            className="w-full rounded-lg border border-[var(--nh-border-default)] bg-[var(--nh-bg-base)] px-3 py-2 font-nunito text-sm text-[var(--nh-text-primary)] outline-none focus:border-amber-400 focus:ring-1 focus:ring-amber-200"
          />
        </div>

        {/* Caption */}
        <div className="mb-4">
          <label className="mb-1 block font-nunito text-xs font-semibold text-[var(--nh-text-muted)]">
            Descrizione (opzionale)
          </label>
          <input
            type="text"
            value={caption}
            onChange={e => setCaption(e.target.value)}
            placeholder="Es. Fine del round 3, Alice in vantaggio"
            className="w-full rounded-lg border border-[var(--nh-border-default)] bg-[var(--nh-bg-base)] px-3 py-2 font-nunito text-sm text-[var(--nh-text-primary)] outline-none placeholder:text-[var(--nh-text-muted)] focus:border-amber-400 focus:ring-1 focus:ring-amber-200"
          />
        </div>

        {/* Actions */}
        <div className="flex gap-3">
          <button
            type="button"
            onClick={handleClose}
            className="flex-1 rounded-xl border border-[var(--nh-border-default)] bg-white py-2.5 font-nunito text-sm font-medium text-[var(--nh-text-primary)] transition-colors hover:bg-stone-50"
          >
            Annulla
          </button>
          <button
            type="button"
            onClick={handleSubmit}
            disabled={previews.length === 0 || isUploading}
            className="flex flex-1 items-center justify-center gap-2 rounded-xl bg-amber-500 py-2.5 font-nunito text-sm font-semibold text-white transition-colors hover:bg-amber-600 disabled:opacity-50"
          >
            <Upload className="h-4 w-4" />
            {isUploading ? 'Caricamento...' : 'Carica'}
          </button>
        </div>
      </div>
    </div>
  );
}
