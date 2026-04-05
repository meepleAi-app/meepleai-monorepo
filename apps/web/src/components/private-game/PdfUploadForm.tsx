/**
 * PdfUploadForm — Minimal PDF upload form for private games.
 *
 * Issue #3664: Private game PDF support — upload entry point.
 * POSTs to /api/v1/ingest/pdf (multipart/form-data) with privateGameId.
 */

'use client';

import { useRef, useState } from 'react';

import { Loader2, Upload } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface PdfUploadFormProps {
  /** Private-game UUID */
  privateGameId: string;
  /** Called after a successful upload with the resulting documentId */
  onUploadComplete?: (documentId: string) => void;
  /** Extra CSS classes for the wrapper */
  className?: string;
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export function PdfUploadForm({ privateGameId, onUploadComplete, className }: PdfUploadFormProps) {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [isUploading, setIsUploading] = useState(false);
  const [uploadProgress, setUploadProgress] = useState(0);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const handleSelectFile = () => {
    fileInputRef.current?.click();
  };

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    // Reset input so re-selecting the same file triggers onChange again
    e.target.value = '';

    setIsUploading(true);
    setUploadProgress(0);
    setErrorMessage(null);

    try {
      const result = await api.pdf.uploadPdf(
        privateGameId,
        file,
        percent => setUploadProgress(percent),
        { isPrivateGame: true }
      );

      onUploadComplete?.(result.documentId);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Errore durante il caricamento del PDF.';
      setErrorMessage(message);
    } finally {
      setIsUploading(false);
      setUploadProgress(0);
    }
  };

  return (
    <div className={className} data-testid="pdf-upload-form">
      <div className="rounded-xl border border-dashed border-border bg-muted/30 px-6 py-8 text-center space-y-4">
        <div className="flex flex-col items-center gap-2">
          <Upload className="h-8 w-8 text-muted-foreground" aria-hidden="true" />
          <p className="text-sm font-medium text-foreground">Carica il manuale del gioco</p>
          <p className="text-xs text-muted-foreground">Supporta file PDF fino a 50 MB</p>
        </div>

        {isUploading ? (
          <div className="space-y-2">
            <div className="flex items-center justify-center gap-2 text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin shrink-0" aria-hidden="true" />
              <span>Caricamento in corso... {uploadProgress}%</span>
            </div>
            <div
              className="h-1.5 w-full rounded-full bg-muted overflow-hidden"
              role="progressbar"
              aria-valuenow={uploadProgress}
              aria-valuemin={0}
              aria-valuemax={100}
              aria-label="Progresso caricamento PDF"
            >
              <div
                className="h-full rounded-full bg-primary transition-all duration-300"
                style={{ width: `${uploadProgress}%` }}
              />
            </div>
          </div>
        ) : (
          <Button
            type="button"
            variant="default"
            size="sm"
            onClick={handleSelectFile}
            data-testid="pdf-upload-button"
          >
            <Upload className="h-4 w-4 mr-2" aria-hidden="true" />
            Seleziona PDF
          </Button>
        )}

        {errorMessage && (
          <p
            className="text-sm text-red-600 dark:text-red-400"
            role="alert"
            data-testid="pdf-upload-error"
          >
            {errorMessage}
          </p>
        )}
      </div>

      {/* Hidden file input */}
      <input
        ref={fileInputRef}
        type="file"
        accept=".pdf"
        className="hidden"
        onChange={handleFileChange}
        data-testid="pdf-file-input"
        aria-label="Seleziona file PDF"
      />
    </div>
  );
}
