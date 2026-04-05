/**
 * PdfProcessingStatus — KB/RAG indexing status panel for private games.
 *
 * Issue #3664: Private game PDF support — KB readiness polling.
 * Polls /api/v1/private-games/{id}/kb-status and renders four stage indicators
 * (Pending → Extracting → Chunking → Embedding) with a progress bar.
 *
 * Terminal states: Completed (success) | Failed (error)
 */

'use client';

import { CheckCircle2, Circle, Info, Loader2, XCircle } from 'lucide-react';

import { usePrivateGameKbStatus } from '@/hooks/queries/usePrivateGameKbStatus';
import { cn } from '@/lib/utils';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface PdfProcessingStatusProps {
  /** Private-game UUID — drives the polling query */
  privateGameId: string;
  /** Extra CSS classes for the wrapper */
  className?: string;
}

type StageState = 'completed' | 'active' | 'pending' | 'failed';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

type KbStatus = 'Pending' | 'Extracting' | 'Chunking' | 'Embedding' | 'Completed' | 'Failed';

const STAGE_LABELS: Record<string, string> = {
  pending: 'Caricamento',
  extracting: 'Estrazione testo',
  chunking: 'Suddivisione in chunk',
  embedding: 'Indicizzazione',
};

function getStageStates(status: KbStatus | undefined): StageState[] {
  switch (status) {
    case 'Pending':
      return ['active', 'pending', 'pending', 'pending'];
    case 'Extracting':
      return ['completed', 'active', 'pending', 'pending'];
    case 'Chunking':
      return ['completed', 'completed', 'active', 'pending'];
    case 'Embedding':
      return ['completed', 'completed', 'completed', 'active'];
    case 'Completed':
      return ['completed', 'completed', 'completed', 'completed'];
    case 'Failed':
      return ['completed', 'completed', 'failed', 'failed'];
    default:
      return ['pending', 'pending', 'pending', 'pending'];
  }
}

function stageStateLabel(state: StageState): string {
  switch (state) {
    case 'completed':
      return 'Completato';
    case 'active':
      return 'In corso...';
    case 'failed':
      return 'Errore';
    default:
      return 'In attesa';
  }
}

// ---------------------------------------------------------------------------
// Stage row sub-component
// ---------------------------------------------------------------------------

function StageRow({ label, state }: { label: string; state: StageState }) {
  const icon = (() => {
    switch (state) {
      case 'completed':
        return <CheckCircle2 className="h-4 w-4 text-emerald-500 shrink-0" aria-hidden="true" />;
      case 'active':
        return (
          <Loader2 className="h-4 w-4 animate-spin text-amber-500 shrink-0" aria-hidden="true" />
        );
      case 'failed':
        return <XCircle className="h-4 w-4 text-red-500 shrink-0" aria-hidden="true" />;
      default:
        return <Circle className="h-4 w-4 text-muted-foreground/30 shrink-0" aria-hidden="true" />;
    }
  })();

  return (
    <div
      className={cn(
        'flex items-center gap-3 rounded-lg px-3 py-2 transition-colors',
        state === 'active' && 'bg-amber-500/8 dark:bg-amber-500/5',
        state === 'completed' && 'opacity-60'
      )}
    >
      {icon}
      <span
        className={cn(
          'flex-1 text-sm',
          state === 'completed' && 'text-muted-foreground line-through',
          state === 'active' && 'text-foreground font-medium',
          state === 'failed' && 'text-red-600 dark:text-red-400',
          state === 'pending' && 'text-muted-foreground/50'
        )}
      >
        {label}
      </span>
      <span
        className={cn(
          'text-xs tabular-nums font-medium',
          state === 'active' && 'text-amber-600 dark:text-amber-400',
          state === 'completed' && 'text-emerald-600 dark:text-emerald-400',
          state === 'failed' && 'text-red-500',
          state === 'pending' && 'text-muted-foreground/40'
        )}
      >
        {stageStateLabel(state)}
      </span>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Progress bar sub-component
// ---------------------------------------------------------------------------

function ProgressBar({ value }: { value: number }) {
  return (
    <div
      className="h-1.5 bg-muted/60 rounded-full overflow-hidden"
      role="progressbar"
      aria-valuenow={value}
      aria-valuemin={0}
      aria-valuemax={100}
      aria-label="Progresso indicizzazione PDF"
    >
      <div
        className="h-full rounded-full bg-gradient-to-r from-amber-500 to-amber-400 transition-all duration-500 ease-out"
        style={{ width: `${value}%` }}
      />
    </div>
  );
}

// ---------------------------------------------------------------------------
// Main component
// ---------------------------------------------------------------------------

export function PdfProcessingStatus({ privateGameId, className }: PdfProcessingStatusProps) {
  const { data, isLoading, isError } = usePrivateGameKbStatus(privateGameId);

  const status = data?.status as KbStatus | undefined;
  const progress = data?.progress ?? (status === 'Completed' ? 100 : 0);
  const stageStates = getStageStates(status);
  const isTerminal = status === 'Completed' || status === 'Failed';

  // Loading skeleton
  if (isLoading && !data) {
    return (
      <div
        className={cn(
          'rounded-xl border border-amber-500/20 bg-amber-500/5 dark:bg-amber-900/10 px-5 py-4 space-y-4',
          className
        )}
        data-testid="pdf-processing-status-loading"
        aria-label="Caricamento stato indicizzazione..."
      >
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin text-amber-500 shrink-0" aria-hidden="true" />
          <span>Verifica stato indicizzazione...</span>
        </div>
      </div>
    );
  }

  // Error state
  if (isError && !data) {
    return (
      <div
        className={cn(
          'rounded-xl border border-red-500/20 bg-red-500/5 dark:bg-red-900/10 px-5 py-4',
          className
        )}
        data-testid="pdf-processing-status-error"
        role="alert"
      >
        <p className="text-sm text-red-700 dark:text-red-300">
          Impossibile recuperare lo stato dell&apos;indicizzazione.
        </p>
      </div>
    );
  }

  // No data yet (pending first poll)
  if (!data) {
    return (
      <div
        className={cn(
          'rounded-xl border border-amber-500/20 bg-amber-500/5 dark:bg-amber-900/10 px-5 py-4',
          className
        )}
        data-testid="pdf-processing-status-waiting"
        aria-live="polite"
      >
        <p className="text-sm text-muted-foreground">In attesa dell&apos;indicizzazione...</p>
      </div>
    );
  }

  const headerTitle = (() => {
    if (status === 'Completed') return 'Indicizzazione completata';
    if (status === 'Failed') return 'Indicizzazione fallita';
    return 'Indicizzazione in corso';
  })();

  return (
    <div
      className={cn(
        'rounded-xl border px-5 py-4 space-y-4',
        status === 'Completed'
          ? 'border-emerald-500/20 bg-emerald-500/5 dark:bg-emerald-900/10'
          : status === 'Failed'
            ? 'border-red-500/20 bg-red-500/5 dark:bg-red-900/10'
            : 'border-amber-500/20 bg-amber-500/5 dark:bg-amber-900/10',
        className
      )}
      data-testid="pdf-processing-status"
      aria-live="polite"
    >
      {/* Header */}
      <div className="flex items-center justify-between gap-3">
        <p
          className={cn(
            'text-sm font-semibold',
            status === 'Completed' && 'text-emerald-700 dark:text-emerald-300',
            status === 'Failed' && 'text-red-700 dark:text-red-300',
            !isTerminal && 'text-foreground'
          )}
        >
          {headerTitle}
        </p>
        {!isTerminal && (
          <span className="text-sm font-semibold text-amber-600 dark:text-amber-400 tabular-nums shrink-0">
            {progress}%
          </span>
        )}
        {status === 'Completed' && data.processedChunks > 0 && (
          <span className="text-xs text-emerald-600 dark:text-emerald-400 tabular-nums shrink-0">
            {data.processedChunks} chunk indicizzati
          </span>
        )}
      </div>

      {/* Progress bar (hidden once terminal) */}
      {!isTerminal && <ProgressBar value={progress} />}

      {/* Stage rows */}
      <div className="space-y-0.5">
        {Object.entries(STAGE_LABELS).map(([key, label], i) => (
          <StageRow key={key} label={label} state={stageStates[i]} />
        ))}
      </div>

      {/* Error message */}
      {status === 'Failed' && data.errorMessage && (
        <p className="text-xs text-red-600 dark:text-red-400" role="alert">
          {data.errorMessage}
        </p>
      )}

      {/* Background note for non-terminal states */}
      {!isTerminal && (
        <p className="flex items-start gap-2 text-xs text-muted-foreground leading-relaxed">
          <Info className="h-3.5 w-3.5 mt-0.5 shrink-0 text-amber-400" aria-hidden="true" />
          L&apos;indicizzazione continua in background. Puoi continuare ad usare l&apos;app.
        </p>
      )}
    </div>
  );
}
