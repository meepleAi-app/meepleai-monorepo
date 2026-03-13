'use client';

/**
 * UploadRulesStep — Step 2 of GameNightWizard.
 *
 * Shows CopyrightDisclaimerModal → then file upload via XHR → processing status.
 * Has "Salta" (skip) button to proceed without uploading a PDF.
 *
 * Issue #123 — Game Night Quick Start Wizard
 */

import { useCallback, useRef, useState } from 'react';

import { FileText, Loader2, SkipForward } from 'lucide-react';

import { PdfProcessingStatus } from '@/components/library/PdfProcessingStatus';
import { CopyrightDisclaimerModal } from '@/components/pdf/CopyrightDisclaimerModal';
import { Button } from '@/components/ui/primitives/button';

// ============================================================================
// Types
// ============================================================================

interface UploadRulesStepProps {
  gameId?: string;
  privateGameId?: string;
  gameTitle: string;
  onComplete: (pdfId?: string) => void;
  onSkip: () => void;
}

type UploadPhase = 'prompt' | 'disclaimer' | 'upload' | 'uploading' | 'processing';

const MAX_FILE_SIZE = 50 * 1024 * 1024; // 50MB

// ============================================================================
// Component
// ============================================================================

export function UploadRulesStep({
  gameId,
  privateGameId,
  gameTitle,
  onComplete,
  onSkip,
}: UploadRulesStepProps) {
  const [phase, setPhase] = useState<UploadPhase>('prompt');
  const [uploadProgress, setUploadProgress] = useState(0);
  const [pdfDocumentId, setPdfDocumentId] = useState<string | null>(null);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const xhrRef = useRef<XMLHttpRequest | null>(null);

  const effectiveGameId = gameId ?? privateGameId ?? null;

  const handleDisclaimerAccept = useCallback(() => {
    setPhase('upload');
  }, []);

  const handleDisclaimerCancel = useCallback(() => {
    setPhase('prompt');
  }, []);

  const handleFileSelected = useCallback(
    async (e: React.ChangeEvent<HTMLInputElement>) => {
      const file = e.target.files?.[0];
      if (!file) return;

      // Validate PDF
      if (file.type !== 'application/pdf') {
        setUploadError('Solo file PDF sono supportati.');
        return;
      }
      if (file.size > MAX_FILE_SIZE) {
        setUploadError('Il file supera il limite di 50MB.');
        return;
      }

      setUploadError(null);
      setPhase('uploading');
      setUploadProgress(0);

      const formData = new FormData();
      formData.append('file', file);
      if (effectiveGameId) {
        formData.append('gameId', effectiveGameId);
      }

      const xhr = new XMLHttpRequest();
      xhrRef.current = xhr;

      xhr.upload.addEventListener('progress', ev => {
        if (ev.lengthComputable) {
          setUploadProgress(Math.round((ev.loaded / ev.total) * 100));
        }
      });

      xhr.addEventListener('load', () => {
        if (xhr.status >= 200 && xhr.status < 300) {
          try {
            const response = JSON.parse(xhr.responseText);
            setPdfDocumentId(response.documentId ?? response.id ?? null);
            setPhase('processing');
          } catch {
            setUploadError('Risposta server non valida.');
            setPhase('upload');
          }
        } else {
          setUploadError(`Upload fallito (${xhr.status}). Riprova.`);
          setPhase('upload');
        }
      });

      xhr.addEventListener('error', () => {
        setUploadError("Errore di rete durante l'upload.");
        setPhase('upload');
      });

      // Use relative URL to route through Next.js proxy (avoids CORS)
      xhr.open('POST', '/api/v1/ingest/pdf');
      xhr.withCredentials = true;
      xhr.send(formData);
    },
    [effectiveGameId]
  );

  return (
    <div className="space-y-4" data-testid="upload-rules-step">
      <div>
        <h3 className="font-quicksand font-bold text-lg text-slate-900 dark:text-slate-100">
          Regolamento (opzionale)
        </h3>
        <p className="text-sm text-muted-foreground mt-1">
          Carica il PDF del regolamento di <strong>{gameTitle}</strong> per attivare
          l&apos;assistente AI.
        </p>
      </div>

      {uploadError && (
        <p className="text-sm text-destructive" data-testid="upload-error">
          {uploadError}
        </p>
      )}

      {phase === 'prompt' && (
        <div className="space-y-3">
          <Button
            onClick={() => setPhase('disclaimer')}
            className="w-full"
            variant="outline"
            data-testid="upload-rules-button"
          >
            <FileText className="h-4 w-4 mr-2" aria-hidden="true" />
            Carica Regolamento PDF
          </Button>
          <Button
            onClick={onSkip}
            variant="ghost"
            className="w-full text-muted-foreground"
            data-testid="skip-rules-button"
          >
            <SkipForward className="h-4 w-4 mr-2" aria-hidden="true" />
            Salta — gioca senza assistente AI
          </Button>
        </div>
      )}

      {phase === 'upload' && (
        <div className="space-y-3">
          <label
            htmlFor="pdf-upload-input"
            className="flex flex-col items-center justify-center rounded-lg border-2 border-dashed border-slate-300 dark:border-slate-600 p-8 cursor-pointer hover:border-amber-500/50 transition-colors"
          >
            <FileText className="h-8 w-8 text-muted-foreground mb-2" aria-hidden="true" />
            <span className="text-sm font-medium">Seleziona il PDF del regolamento</span>
            <span className="text-xs text-muted-foreground mt-1">Max 50MB</span>
            <input
              id="pdf-upload-input"
              type="file"
              accept=".pdf,application/pdf"
              onChange={handleFileSelected}
              className="hidden"
              data-testid="pdf-file-input"
            />
          </label>
          <Button onClick={() => setPhase('prompt')} variant="ghost" className="w-full">
            Indietro
          </Button>
        </div>
      )}

      {phase === 'uploading' && (
        <div className="space-y-3" data-testid="upload-progress">
          <div className="flex items-center gap-3 p-4 rounded-lg border">
            <Loader2 className="h-5 w-5 animate-spin text-amber-600" aria-hidden="true" />
            <div className="flex-1">
              <p className="text-sm font-medium">Upload in corso... {uploadProgress}%</p>
              <div className="h-2 mt-2 rounded-full bg-slate-200 dark:bg-slate-700 overflow-hidden">
                <div
                  className="h-full bg-amber-500 transition-all duration-300"
                  style={{ width: `${uploadProgress}%` }}
                />
              </div>
            </div>
          </div>
        </div>
      )}

      {phase === 'processing' && (
        <div className="space-y-3">
          <PdfProcessingStatus
            gameId={effectiveGameId}
            pdfFileName={null}
            onContinue={() => onComplete(pdfDocumentId ?? undefined)}
          />
        </div>
      )}

      <CopyrightDisclaimerModal
        open={phase === 'disclaimer'}
        onAccept={handleDisclaimerAccept}
        onCancel={handleDisclaimerCancel}
      />
    </div>
  );
}
