'use client';

import { AlertCircle, FileQuestion, Loader2 } from 'lucide-react';

import { GradientButton } from '@/components/ui/buttons/GradientButton';

export type AiState = 'loading' | 'timeout' | 'no-results' | 'no-pdf';

interface AiLoadingStateProps {
  state: AiState;
  onRetry?: () => void;
  onUploadPdf?: () => void;
  statusMessage?: string;
}

/**
 * Displays different UI states for the AI response lifecycle:
 * - loading: spinner with status message
 * - timeout: error with retry button
 * - no-results: fallback to general knowledge notice
 * - no-pdf: prompt to upload the game rulebook PDF
 */
export function AiLoadingState({
  state,
  onRetry,
  onUploadPdf,
  statusMessage,
}: AiLoadingStateProps) {
  if (state === 'loading') {
    return (
      <div className="flex items-center gap-3 text-sm text-[var(--gaming-text-secondary,rgba(255,255,255,0.6))]">
        <Loader2 className="h-4 w-4 animate-spin text-amber-400 flex-shrink-0" />
        <span>{statusMessage ?? 'Sto cercando nelle regole...'}</span>
      </div>
    );
  }

  if (state === 'timeout') {
    return (
      <div className="flex flex-col gap-3">
        <div className="flex items-center gap-2 text-sm text-red-400">
          <AlertCircle className="h-4 w-4 flex-shrink-0" />
          <span>{statusMessage ?? 'La ricerca ha impiegato troppo tempo. Riprova.'}</span>
        </div>
        {onRetry && (
          <button
            type="button"
            onClick={onRetry}
            className="self-start rounded-md bg-white/10 px-3 py-1.5 text-sm text-white hover:bg-white/20 transition-colors"
          >
            Riprova
          </button>
        )}
      </div>
    );
  }

  if (state === 'no-results') {
    return (
      <p className="text-sm text-[var(--gaming-text-secondary,rgba(255,255,255,0.6))]">
        {statusMessage ??
          'Nessun risultato trovato nelle regole. Rispondo usando la mia conoscenza generale.'}
      </p>
    );
  }

  // no-pdf
  return (
    <div className="flex flex-col items-start gap-3">
      <div className="flex items-center gap-2 text-sm text-[var(--gaming-text-secondary,rgba(255,255,255,0.6))]">
        <FileQuestion className="h-4 w-4 flex-shrink-0 text-amber-400" />
        <span>{statusMessage ?? 'Carica il regolamento per risposte più precise.'}</span>
      </div>
      {onUploadPdf && (
        <GradientButton size="sm" onClick={onUploadPdf}>
          Carica PDF
        </GradientButton>
      )}
    </div>
  );
}
