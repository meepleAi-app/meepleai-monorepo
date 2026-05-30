'use client';

import { useCallback } from 'react';

import { useMutation, useQueryClient } from '@tanstack/react-query';

import { useKbDocIngestionLog, kbDocIngestionLogKeys } from '@/hooks/queries/useKbDocIngestionLog';
import { retryIngestionJob } from '@/lib/api/admin-kb-ingestion';

import { IngestionActions } from './IngestionActions';
import { IngestionHero } from './IngestionHero';
import { IngestionLogBlock } from './IngestionLogBlock';
import { IngestionTimeline } from './IngestionTimeline';

interface IngestionPanelProps {
  readonly docId: string;
  readonly chunkCount: number;
  readonly pageCount: number;
}

/**
 * Container of the "Ingestion log" tab inside KbDocDetailPanel. Wires the
 * hook to four presentational children (Hero / Timeline / LogBlock / Actions)
 * and the Re-enqueue mutation (RetryJobCommand). Issue #1650.
 */
export function IngestionPanel({ docId, chunkCount, pageCount }: IngestionPanelProps) {
  const query = useKbDocIngestionLog({ docId });
  const queryClient = useQueryClient();

  const retryMutation = useMutation({
    mutationFn: (jobId: string) => retryIngestionJob(jobId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: kbDocIngestionLogKeys.byId(docId) }),
  });

  const handleRetry = useCallback((jobId: string) => retryMutation.mutate(jobId), [retryMutation]);

  if (query.isLoading) {
    return (
      <div
        data-testid="ingestion-panel-loading"
        className="border border-border/60 rounded-lg bg-card/80 p-6 animate-pulse min-h-[200px]"
      >
        <div className="h-6 w-2/3 bg-muted rounded mb-4" />
        <div className="h-4 w-1/2 bg-muted rounded mb-2" />
        <div className="h-4 w-1/3 bg-muted rounded" />
      </div>
    );
  }

  if (query.isError) {
    return (
      <div className="border border-rose-500/30 rounded-lg bg-rose-500/5 p-6 text-sm text-rose-700 dark:text-rose-300">
        Errore caricamento ingestion log: {query.error.message}
      </div>
    );
  }

  if (query.data === null || query.data === undefined) {
    return (
      <div
        data-testid="ingestion-panel-empty"
        className="border border-border/60 rounded-lg bg-card/80 p-8 text-center text-sm text-muted-foreground min-h-[200px] flex flex-col items-center justify-center gap-2"
      >
        <span aria-hidden="true" className="text-3xl">
          🗂️
        </span>
        <p>Nessun job di ingestion per questo documento.</p>
        <p className="text-xs">
          Il documento potrebbe essere stato indicizzato con una pipeline precedente.
        </p>
      </div>
    );
  }

  const log = query.data;

  return (
    <section className="border border-border/60 rounded-lg bg-card/80 overflow-hidden">
      <IngestionHero log={log} chunkCount={chunkCount} pageCount={pageCount} />
      <div className="p-4">
        <IngestionTimeline steps={log.steps} />
      </div>
      <div className="px-4 pb-4">
        <IngestionLogBlock steps={log.steps} />
      </div>
      <div className="px-4 pb-4">
        <IngestionActions log={log} onRetry={handleRetry} />
      </div>
    </section>
  );
}
