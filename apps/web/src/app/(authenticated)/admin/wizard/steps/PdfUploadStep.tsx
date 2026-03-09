'use client';

/**
 * Step 1: PDF Upload
 *
 * Upload a PDF rulebook and optionally mark it as public (visible to registered users).
 */

import { useState, useCallback } from 'react';

import { ArrowRight, FileText, SkipForward, Upload, X } from 'lucide-react';

import { toast } from '@/components/layout';
import { Spinner } from '@/components/loading';
import { Button } from '@/components/ui/primitives/button';
import { Checkbox } from '@/components/ui/primitives/checkbox';
import { Label } from '@/components/ui/primitives/label';
import { cn } from '@/lib/utils';

interface PdfUploadStepProps {
  onComplete: (pdfId: string, fileName: string, isPublic: boolean) => void;
  /** Shared catalog game ID — sends 'gameId' form field to the backend. */
  gameId?: string;
  /**
   * Manually-created private game ID — sends 'privateGameId' form field.
   * Use this (instead of gameId) when the game was created via the manual
   * private-game flow (PrivateGame entity). Never combine with gameId.
   */
  privateGameId?: string;
  /**
   * When true, hides the "Add to Public Library" checkbox.
   * Use for user wizard where PDFs are always private.
   */
  isPrivate?: boolean;
  /**
   * When provided, shows a "Skip" button allowing the user to complete
   * without uploading a PDF.
   */
  onSkip?: () => void;
}

export function PdfUploadStep({
  onComplete,
  gameId,
  privateGameId,
  isPrivate = false,
  onSkip,
}: PdfUploadStepProps) {
  const [file, setFile] = useState<File | null>(null);
  const [isPublic, setIsPublic] = useState(!isPrivate);
  const [uploading, setUploading] = useState(false);
  const [uploadProgress, setUploadProgress] = useState(0);
  const [dragActive, setDragActive] = useState(false);

  const handleFileSelect = useCallback((selectedFile: File) => {
    if (selectedFile.type !== 'application/pdf') {
      toast.error('Seleziona un file PDF');
      return;
    }
    if (selectedFile.size > 50 * 1024 * 1024) {
      // 50MB limit
      toast.error('Il file deve essere inferiore a 50MB');
      return;
    }
    setFile(selectedFile);
  }, []);

  const handleDrop = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault();
      setDragActive(false);
      const droppedFile = e.dataTransfer.files[0];
      if (droppedFile) {
        handleFileSelect(droppedFile);
      }
    },
    [handleFileSelect]
  );

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setDragActive(true);
  }, []);

  const handleDragLeave = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setDragActive(false);
  }, []);

  const handleUpload = useCallback(async () => {
    if (!file) {
      toast.error('Seleziona un file PDF');
      return;
    }

    setUploading(true);
    setUploadProgress(0);

    try {
      const formData = new FormData();
      formData.append('file', file);
      // privateGameId → manually-created private game (routes to HandlePrivateGamePdfUploadAsync)
      // gameId        → shared catalog game or admin upload (standard path)
      if (privateGameId) {
        formData.append('privateGameId', privateGameId);
      } else if (gameId) {
        formData.append('gameId', gameId);
      }
      formData.append('language', 'it'); // Default to Italian

      // Use XHR for progress tracking
      const xhr = new XMLHttpRequest();

      const uploadPromise = new Promise<{ documentId: string; fileName: string }>(
        (resolve, reject) => {
          xhr.upload.addEventListener('progress', e => {
            if (e.lengthComputable) {
              setUploadProgress(Math.round((e.loaded / e.total) * 100));
            }
          });

          xhr.addEventListener('load', () => {
            if (xhr.status >= 200 && xhr.status < 300) {
              try {
                const response = JSON.parse(xhr.responseText);
                resolve(response);
              } catch {
                reject(new Error('Risposta non valida dal server'));
              }
            } else {
              try {
                const error = JSON.parse(xhr.responseText);
                reject(new Error(error.error || 'Upload fallito'));
              } catch {
                reject(new Error(`Upload fallito: ${xhr.statusText}`));
              }
            }
          });

          xhr.addEventListener('error', () => {
            reject(new Error('Errore di rete durante upload'));
          });

          xhr.open('POST', '/api/v1/ingest/pdf');
          xhr.withCredentials = true;
          xhr.send(formData);
        }
      );

      const result = await uploadPromise;

      toast.success('PDF caricato con successo!');
      onComplete(result.documentId, file.name, isPublic);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Errore sconosciuto';
      toast.error(`Upload fallito: ${message}`);
    } finally {
      setUploading(false);
    }
  }, [file, isPublic, onComplete, gameId, privateGameId]);

  return (
    <div className="space-y-5">
      <div>
        <h2 className="text-lg font-semibold text-foreground mb-1">Carica il Regolamento PDF</h2>
        <p className="text-sm text-muted-foreground">
          Carica il file PDF del regolamento del gioco. Verrà processato per estrarre le regole.
        </p>
      </div>

      {/* Drop Zone */}
      <div
        className={cn(
          'relative border-2 border-dashed rounded-xl p-6 text-center transition-all cursor-pointer',
          dragActive && 'border-amber-500 bg-amber-500/5 scale-[1.01]',
          file && !dragActive && 'border-emerald-500/50 bg-emerald-500/5',
          !file && !dragActive && 'border-border hover:border-amber-500/50 hover:bg-muted/30',
          uploading && 'pointer-events-none opacity-70'
        )}
        onDrop={handleDrop}
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onClick={() => document.getElementById('pdf-file-input')?.click()}
      >
        <input
          id="pdf-file-input"
          type="file"
          accept="application/pdf"
          className="hidden"
          onChange={e => {
            const selectedFile = e.target.files?.[0];
            if (selectedFile) handleFileSelect(selectedFile);
          }}
        />

        {file ? (
          <div className="flex items-center justify-center gap-3">
            <div className="flex items-center justify-center w-10 h-10 rounded-lg bg-emerald-500/10">
              <FileText className="h-5 w-5 text-emerald-500" />
            </div>
            <div className="text-left min-w-0">
              <p className="font-medium text-foreground text-sm truncate">{file.name}</p>
              <p className="text-xs text-muted-foreground">
                {(file.size / 1024 / 1024).toFixed(1)} MB
              </p>
            </div>
            <Button
              variant="ghost"
              size="icon"
              className="h-7 w-7 text-muted-foreground hover:text-foreground shrink-0"
              onClick={e => {
                e.stopPropagation();
                setFile(null);
              }}
              aria-label="Rimuovi file"
            >
              <X className="h-3.5 w-3.5" />
            </Button>
          </div>
        ) : (
          <div className="space-y-2 py-2">
            <div className="mx-auto w-10 h-10 rounded-lg bg-muted flex items-center justify-center">
              <Upload className="h-5 w-5 text-muted-foreground" />
            </div>
            <p className="font-medium text-foreground text-sm">
              Trascina qui il PDF o clicca per selezionare
            </p>
            <p className="text-xs text-muted-foreground">PDF fino a 50MB</p>
          </div>
        )}
      </div>

      {/* Public checkbox — hidden for private game uploads */}
      {!isPrivate && (
        <div className="flex items-start space-x-3">
          <Checkbox
            id="is-public"
            checked={isPublic}
            onCheckedChange={checked => setIsPublic(checked === true)}
          />
          <div className="space-y-1">
            <Label htmlFor="is-public" className="font-medium cursor-pointer">
              Aggiungi alla Libreria Pubblica
            </Label>
            <p className="text-sm text-muted-foreground">
              Il PDF sarà visibile a tutti gli utenti registrati nella libreria pubblica.
            </p>
          </div>
        </div>
      )}

      {/* Progress bar */}
      {uploading && (
        <div className="space-y-2">
          <div className="flex justify-between text-sm">
            <span className="text-muted-foreground">Caricamento in corso...</span>
            <span className="font-semibold text-amber-600 dark:text-amber-400 tabular-nums">
              {uploadProgress}%
            </span>
          </div>
          <div className="h-1.5 bg-muted/60 rounded-full overflow-hidden">
            <div
              className="h-full rounded-full bg-gradient-to-r from-amber-500 to-amber-400 transition-all duration-300 ease-out"
              style={{ width: `${uploadProgress}%` }}
            />
          </div>
        </div>
      )}

      {/* Actions */}
      <div className="flex justify-between gap-3 pt-1">
        {onSkip && (
          <Button
            variant="ghost"
            onClick={onSkip}
            disabled={uploading}
            className="text-muted-foreground"
          >
            <SkipForward className="mr-1.5 h-3.5 w-3.5" />
            Salta
          </Button>
        )}
        <Button
          onClick={handleUpload}
          disabled={!file || uploading}
          className="min-w-32 ml-auto bg-amber-600 hover:bg-amber-700 text-white"
        >
          {uploading ? (
            <>
              <Spinner size="sm" className="mr-2" />
              Caricamento...
            </>
          ) : (
            <>
              Carica PDF
              <ArrowRight className="ml-1.5 h-3.5 w-3.5" />
            </>
          )}
        </Button>
      </div>
    </div>
  );
}
