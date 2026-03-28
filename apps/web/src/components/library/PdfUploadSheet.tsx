/**
 * PdfUploadSheet Component
 *
 * Bottom sheet variant of PDF upload for mobile game detail page.
 * Validates PDF only, max 50MB, calls actual upload API.
 */

'use client';

import { useState, useRef, useCallback } from 'react';

import { FileText, Upload, Loader2 } from 'lucide-react';

import { toast } from '@/components/layout/Toast';
import { GradientButton } from '@/components/ui/buttons/GradientButton';
import { BottomSheet } from '@/components/ui/overlays/BottomSheet';
import { api } from '@/lib/api';

const MAX_FILE_SIZE_MB = 50;
const MAX_FILE_SIZE_BYTES = MAX_FILE_SIZE_MB * 1024 * 1024;

export interface PdfUploadSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  gameId: string;
  onUploaded?: () => void;
}

export function PdfUploadSheet({ open, onOpenChange, gameId, onUploaded }: PdfUploadSheetProps) {
  const [file, setFile] = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);
  const [progress, setProgress] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const reset = useCallback(() => {
    setFile(null);
    setUploading(false);
    setProgress(0);
    setError(null);
  }, []);

  const handleClose = useCallback(
    (nextOpen: boolean) => {
      if (!nextOpen) reset();
      onOpenChange(nextOpen);
    },
    [onOpenChange, reset]
  );

  const handleFileChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const selected = e.target.files?.[0];
    if (!selected) return;

    setError(null);

    if (selected.type !== 'application/pdf') {
      setError('Il file deve essere in formato PDF');
      setFile(null);
      return;
    }

    if (selected.size > MAX_FILE_SIZE_BYTES) {
      setError(`Il file è troppo grande (max ${MAX_FILE_SIZE_MB}MB)`);
      setFile(null);
      return;
    }

    if (selected.size === 0) {
      setError('Il file è vuoto');
      setFile(null);
      return;
    }

    setFile(selected);
  }, []);

  const handleUpload = useCallback(async () => {
    if (!file) return;

    setUploading(true);
    setProgress(0);
    setError(null);

    try {
      await api.pdf.uploadPdf(gameId, file, percent => {
        setProgress(percent);
      });

      toast.success('PDF caricato con successo!');
      onUploaded?.();
      handleClose(false);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Errore durante il caricamento';
      setError(message);
      toast.error(message);
      setUploading(false);
      setProgress(0);
    }
  }, [file, gameId, onUploaded, handleClose]);

  const formatSize = (bytes: number): string => {
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
    return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
  };

  return (
    <BottomSheet open={open} onOpenChange={handleClose} title="Carica Regolamento">
      <div className="flex flex-col gap-4">
        {/* Hidden file input */}
        <input
          ref={inputRef}
          type="file"
          accept="application/pdf"
          className="hidden"
          onChange={handleFileChange}
          data-testid="pdf-sheet-file-input"
        />

        {/* Choose file button */}
        {!file && !uploading && (
          <GradientButton
            fullWidth
            size="lg"
            onClick={() => inputRef.current?.click()}
            data-testid="choose-file-button"
          >
            <Upload className="h-4 w-4" />
            Scegli File PDF
          </GradientButton>
        )}

        {/* File selected - show info and upload */}
        {file && !uploading && (
          <div className="space-y-3">
            <div className="flex items-center gap-3 rounded-lg bg-white/5 p-3">
              <FileText className="h-5 w-5 shrink-0 text-purple-400" />
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-medium text-[var(--gaming-text-primary)]">
                  {file.name}
                </p>
                <p className="text-xs text-[var(--gaming-text-secondary)]">
                  {formatSize(file.size)}
                </p>
              </div>
            </div>

            <div className="flex gap-2">
              <button
                onClick={() => {
                  setFile(null);
                  setError(null);
                  if (inputRef.current) inputRef.current.value = '';
                }}
                className="flex-1 rounded-lg border border-[var(--gaming-border-glass)] px-4 py-2.5 text-sm text-[var(--gaming-text-secondary)] transition-colors hover:bg-white/5"
              >
                Cambia file
              </button>
              <GradientButton className="flex-1" onClick={handleUpload} data-testid="upload-button">
                <Upload className="h-4 w-4" />
                Carica
              </GradientButton>
            </div>
          </div>
        )}

        {/* Uploading state */}
        {uploading && (
          <div className="flex flex-col items-center gap-3 py-4">
            <Loader2 className="h-8 w-8 animate-spin text-purple-400" />
            <p className="text-sm text-[var(--gaming-text-secondary)]">Caricamento in corso...</p>
            <div className="h-2 w-full overflow-hidden rounded-full bg-white/10">
              <div
                className="h-full rounded-full bg-purple-500 transition-all duration-300"
                style={{ width: `${progress}%` }}
              />
            </div>
            <p className="text-xs text-[var(--gaming-text-tertiary)]">{progress}%</p>
          </div>
        )}

        {/* Error state */}
        {error && (
          <p className="text-center text-sm text-red-400" data-testid="upload-error">
            {error}
          </p>
        )}

        {/* Info text */}
        <p className="text-center text-xs text-[var(--gaming-text-tertiary)]">
          Formato: PDF &bull; Max {MAX_FILE_SIZE_MB}MB
        </p>
      </div>
    </BottomSheet>
  );
}
