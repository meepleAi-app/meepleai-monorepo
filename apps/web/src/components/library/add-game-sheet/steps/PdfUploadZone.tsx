'use client';

/**
 * PdfUploadZone - Drag-and-drop PDF upload with progress
 * Issue #4820: Step 2 Knowledge Base & PDF Management
 * Epic #4817: User Collection Wizard
 */

import { useState, useCallback, useRef } from 'react';
import { Upload, FileText, X, AlertCircle } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';

export interface PdfUploadZoneProps {
  /** Called when upload completes successfully */
  onUploadComplete: (documentId: string, fileName: string, fileSizeBytes: number) => void;
  /** Called to perform the upload */
  onUpload: (file: File, onProgress: (percent: number) => void) => Promise<{ documentId: string; fileName: string }>;
  /** Whether upload is disabled */
  disabled?: boolean;
  /** Additional CSS classes */
  className?: string;
}

const MAX_FILE_SIZE = 50 * 1024 * 1024; // 50MB

export function PdfUploadZone({
  onUploadComplete,
  onUpload,
  disabled,
  className,
}: PdfUploadZoneProps) {
  const [file, setFile] = useState<File | null>(null);
  const [dragActive, setDragActive] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [uploadProgress, setUploadProgress] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const validateFile = useCallback((f: File): string | null => {
    if (f.type !== 'application/pdf') {
      return 'Seleziona un file PDF valido.';
    }
    if (f.size > MAX_FILE_SIZE) {
      return 'Il file non deve superare i 50 MB.';
    }
    return null;
  }, []);

  const handleFileSelect = useCallback(
    (selectedFile: File) => {
      const validationError = validateFile(selectedFile);
      if (validationError) {
        setError(validationError);
        return;
      }
      setError(null);
      setFile(selectedFile);
    },
    [validateFile],
  );

  const handleDrop = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault();
      e.stopPropagation();
      setDragActive(false);
      const droppedFile = e.dataTransfer.files[0];
      if (droppedFile) handleFileSelect(droppedFile);
    },
    [handleFileSelect],
  );

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setDragActive(true);
  }, []);

  const handleDragLeave = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setDragActive(false);
  }, []);

  const handleUpload = useCallback(async () => {
    if (!file) return;
    setUploading(true);
    setUploadProgress(0);
    setError(null);

    try {
      const result = await onUpload(file, setUploadProgress);
      onUploadComplete(result.documentId, result.fileName, file.size);
      setFile(null);
      setUploadProgress(0);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Errore durante il caricamento';
      setError(message);
    } finally {
      setUploading(false);
    }
  }, [file, onUpload, onUploadComplete]);

  const handleClearFile = useCallback(() => {
    setFile(null);
    setError(null);
    setUploadProgress(0);
    if (inputRef.current) inputRef.current.value = '';
  }, []);

  const formatSize = (bytes: number) => `${(bytes / (1024 * 1024)).toFixed(1)} MB`;

  return (
    <div className={cn('space-y-3', className)} data-testid="pdf-upload-zone">
      {/* Drop zone */}
      <div
        className={cn(
          'relative rounded-xl border-2 border-dashed p-6 text-center transition-colors cursor-pointer',
          dragActive && 'border-teal-400 bg-teal-400/5',
          file && !dragActive && 'border-teal-500/50 bg-teal-500/5',
          !file && !dragActive && 'border-slate-700 hover:border-slate-500 hover:bg-slate-800/30',
          disabled && 'pointer-events-none opacity-50',
        )}
        onDrop={handleDrop}
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onClick={() => !uploading && inputRef.current?.click()}
        data-testid="pdf-drop-area"
        role="button"
        aria-label="Carica un file PDF"
        tabIndex={0}
        onKeyDown={(e) => {
          if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            inputRef.current?.click();
          }
        }}
      >
        <input
          ref={inputRef}
          type="file"
          accept="application/pdf"
          className="hidden"
          onChange={(e) => {
            const selected = e.target.files?.[0];
            if (selected) handleFileSelect(selected);
          }}
          data-testid="pdf-file-input"
        />

        {file ? (
          <div className="flex items-center justify-center gap-3">
            <FileText className="h-8 w-8 text-teal-400" />
            <div className="text-left">
              <p className="text-sm font-medium text-slate-200">{file.name}</p>
              <p className="text-xs text-slate-500">{formatSize(file.size)}</p>
            </div>
            {!uploading && (
              <Button
                variant="ghost"
                size="icon"
                className="h-6 w-6 text-slate-500 hover:text-slate-300"
                onClick={(e) => {
                  e.stopPropagation();
                  handleClearFile();
                }}
                aria-label="Rimuovi file"
              >
                <X className="h-3.5 w-3.5" />
              </Button>
            )}
          </div>
        ) : (
          <div className="space-y-1.5">
            <Upload className="mx-auto h-8 w-8 text-slate-500" />
            <p className="text-sm font-medium text-slate-300">
              Trascina un PDF o clicca per selezionare
            </p>
            <p className="text-xs text-slate-500">PDF fino a 50 MB</p>
          </div>
        )}
      </div>

      {/* Progress bar */}
      {uploading && (
        <div className="space-y-1.5" data-testid="pdf-upload-progress">
          <div className="flex justify-between text-xs text-slate-400">
            <span>Caricamento in corso...</span>
            <span>{uploadProgress}%</span>
          </div>
          <div className="h-1.5 overflow-hidden rounded-full bg-slate-700">
            <div
              className="h-full rounded-full bg-teal-500 transition-all duration-300"
              style={{ width: `${uploadProgress}%` }}
              role="progressbar"
              aria-valuenow={uploadProgress}
              aria-valuemin={0}
              aria-valuemax={100}
            />
          </div>
        </div>
      )}

      {/* Error message */}
      {error && (
        <div
          className="flex items-center gap-2 rounded-lg border border-red-500/30 bg-red-500/10 px-3 py-2 text-xs text-red-400"
          role="alert"
          data-testid="pdf-upload-error"
        >
          <AlertCircle className="h-3.5 w-3.5 shrink-0" />
          {error}
        </div>
      )}

      {/* Upload button */}
      {file && !uploading && (
        <Button
          onClick={(e) => {
            e.stopPropagation();
            void handleUpload();
          }}
          disabled={uploading || disabled}
          className="w-full bg-teal-600 hover:bg-teal-700"
          data-testid="pdf-upload-button"
        >
          <Upload className="mr-1.5 h-4 w-4" />
          Carica PDF
        </Button>
      )}
    </div>
  );
}
