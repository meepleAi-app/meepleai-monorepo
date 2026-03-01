/**
 * KbStatusPanel — Knowledge Base status panel for the agent config page.
 *
 * Issue #4948: Redesign agent config page with 2-column layout.
 *
 * Left-column panel displaying:
 * - PDF indexing status with progress bar
 * - Chunk count when indexed
 * - Error message when failed
 * - CTA to upload PDF when no document is linked
 */

'use client';

import { useEffect } from 'react';

import Link from 'next/link';

import {
  AlertCircle,
  BrainCircuit,
  CheckCircle2,
  FileText,
  Loader2,
  Plus,
  XCircle,
} from 'lucide-react';

import { usePdfProcessingStatus } from '@/hooks/queries/usePdfProcessingStatus';
import { cn } from '@/lib/utils';

interface KbStatusPanelProps {
  /** UUID of the private game */
  gameId: string;
  /**
   * Called whenever the indexed state changes.
   * Allows parent to derive `hasIndexedKb` without a duplicate hook subscription.
   */
  onStatusChange?: (isIndexed: boolean) => void;
}

function StatusBadge({
  status,
}: {
  status: 'pending' | 'processing' | 'indexed' | 'failed';
}) {
  switch (status) {
    case 'indexed':
      return (
        <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-teal-100 dark:bg-teal-900/40 text-teal-700 dark:text-teal-300 border border-teal-200 dark:border-teal-700/50">
          <CheckCircle2 className="h-3 w-3" />
          Indicizzato
        </span>
      );
    case 'processing':
      return (
        <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-amber-100 dark:bg-amber-900/40 text-amber-700 dark:text-amber-300 border border-amber-200 dark:border-amber-700/50">
          <Loader2 className="h-3 w-3 animate-spin" />
          In elaborazione
        </span>
      );
    case 'pending':
      return (
        <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-slate-100 dark:bg-slate-800/60 text-slate-600 dark:text-slate-400 border border-slate-200 dark:border-slate-700/50">
          <Loader2 className="h-3 w-3" />
          In attesa
        </span>
      );
    case 'failed':
      return (
        <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-red-100 dark:bg-red-900/40 text-red-700 dark:text-red-300 border border-red-200 dark:border-red-700/50">
          <XCircle className="h-3 w-3" />
          Fallito
        </span>
      );
  }
}

export function KbStatusPanel({ gameId, onStatusChange }: KbStatusPanelProps) {
  const { data, isLoading, isError } = usePdfProcessingStatus(gameId);

  useEffect(() => {
    if (onStatusChange) {
      onStatusChange(data?.status === 'indexed');
    }
  }, [data?.status, onStatusChange]);

  return (
    <div
      className="rounded-xl border border-border/50 bg-card p-5 flex flex-col gap-5"
      data-testid="kb-status-panel"
    >
      {/* Header */}
      <div className="flex items-center gap-2">
        <BrainCircuit className="h-5 w-5 text-teal-500" />
        <h2 className="text-base font-semibold">Knowledge Base</h2>
      </div>

      {/* Loading */}
      {isLoading && (
        <div className="flex items-center justify-center py-8 text-muted-foreground">
          <Loader2 className="h-6 w-6 animate-spin mr-2" />
          <span className="text-sm">Controllo stato KB…</span>
        </div>
      )}

      {/* Error fetching status */}
      {isError && !isLoading && (
        <div className="flex items-start gap-3 p-3 rounded-lg bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800/40">
          <AlertCircle className="h-4 w-4 text-red-500 shrink-0 mt-0.5" />
          <div>
            <p className="text-sm font-medium text-red-700 dark:text-red-300">
              Errore nel caricare lo stato KB
            </p>
            <p className="text-xs text-red-600 dark:text-red-400 mt-0.5">
              Riprova tra qualche secondo.
            </p>
          </div>
        </div>
      )}

      {/* No PDF linked — empty state */}
      {!isLoading && !isError && !data && (
        <div
          className="flex flex-col items-center gap-3 py-6 text-center"
          data-testid="kb-status-no-pdf"
        >
          <div className="h-12 w-12 rounded-full bg-muted flex items-center justify-center">
            <FileText className="h-6 w-6 text-muted-foreground" />
          </div>
          <div>
            <p className="text-sm font-medium text-foreground">
              Nessun documento collegato
            </p>
            <p className="text-xs text-muted-foreground mt-1">
              Carica un PDF del regolamento per creare la Knowledge Base.
            </p>
          </div>
          <Link
            href={`/library/private/add?gameId=${gameId}&step=2`}
            className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-md border border-dashed border-primary/50 text-primary hover:bg-primary/5 transition-colors"
            data-testid="kb-status-upload-cta"
          >
            <Plus className="h-3.5 w-3.5" />
            Aggiungi documento
          </Link>
        </div>
      )}

      {/* PDF status card */}
      {!isLoading && !isError && data && (
        <div className="space-y-4">
          {/* Document row */}
          <div
            className={cn(
              'rounded-lg border p-3 flex items-start gap-3',
              data.status === 'indexed'
                ? 'border-teal-200 dark:border-teal-700/50 bg-teal-50/40 dark:bg-teal-950/20'
                : data.status === 'failed'
                  ? 'border-red-200 dark:border-red-700/50 bg-red-50/40 dark:bg-red-950/20'
                  : 'border-amber-200 dark:border-amber-700/50 bg-amber-50/40 dark:bg-amber-950/20'
            )}
            data-testid="kb-status-document"
          >
            <FileText
              className={cn(
                'h-4 w-4 shrink-0 mt-0.5',
                data.status === 'indexed'
                  ? 'text-teal-500'
                  : data.status === 'failed'
                    ? 'text-red-500'
                    : 'text-amber-500'
              )}
            />
            <div className="min-w-0 flex-1">
              <div className="flex items-center gap-2 flex-wrap">
                <span className="text-sm font-medium truncate">
                  Regolamento.pdf
                </span>
                <StatusBadge status={data.status} />
              </div>

              {/* Chunk count when indexed */}
              {data.status === 'indexed' && data.chunkCount != null && (
                <p
                  className="text-xs text-muted-foreground mt-1"
                  data-testid="kb-status-chunk-count"
                >
                  {data.chunkCount.toLocaleString()} chunk indicizzati
                </p>
              )}

              {/* Error message */}
              {data.status === 'failed' && data.errorMessage && (
                <p
                  className="text-xs text-red-600 dark:text-red-400 mt-1"
                  data-testid="kb-status-error"
                >
                  {data.errorMessage}
                </p>
              )}
            </div>
          </div>

          {/* Progress bar for processing/pending */}
          {(data.status === 'processing' || data.status === 'pending') && (
            <div className="space-y-1.5" data-testid="kb-status-progress">
              <div className="flex items-center justify-between text-xs text-muted-foreground">
                <span>Elaborazione in corso…</span>
                {data.progress != null && <span>{data.progress}%</span>}
              </div>
              <div className="h-1.5 rounded-full bg-muted overflow-hidden">
                <div
                  className="h-full rounded-full bg-amber-400 dark:bg-amber-500 transition-all duration-500"
                  style={{ width: `${data.progress ?? 15}%` }}
                  role="progressbar"
                  aria-label="Elaborazione PDF in corso"
                  aria-valuenow={data.progress ?? 15}
                  aria-valuemin={0}
                  aria-valuemax={100}
                />
              </div>
            </div>
          )}

          {/* Add another document — ghost dashed button */}
          <Link
            href={`/library/private/add?gameId=${gameId}&step=2`}
            className="flex w-full items-center justify-center gap-1.5 rounded-lg border border-dashed border-border/60 py-2.5 text-xs font-medium text-muted-foreground hover:border-primary/40 hover:text-primary transition-colors"
            data-testid="kb-status-add-doc"
          >
            <Plus className="h-3.5 w-3.5" />
            Aggiungi documento
          </Link>
        </div>
      )}
    </div>
  );
}
