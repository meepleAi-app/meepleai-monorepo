'use client';
import { X } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';

import { useCatalogSyncRunLogs } from '../hooks/use-catalog-sync-run-logs';

interface LogStreamProps {
  runId: string | null;
  onClose: () => void;
}

export function LogStream({ runId, onClose }: LogStreamProps) {
  const { data, isLoading } = useCatalogSyncRunLogs(runId);

  if (runId === null) return null;

  return (
    <section
      role="region"
      aria-label="Sync run logs"
      className="overflow-hidden rounded-xl border border-border bg-card"
    >
      <header className="flex items-center gap-2.5 border-b border-border bg-muted/30 px-3.5 py-2.5">
        <div>
          <h3 className="font-quicksand text-[13px] font-extrabold text-foreground">Run logs</h3>
          <p className="font-mono text-[10px] text-muted-foreground">{runId}</p>
        </div>
        <Button
          variant="ghost"
          size="icon"
          onClick={onClose}
          aria-label="Close logs"
          className="ml-auto h-7 w-7"
        >
          <X className="h-4 w-4" />
        </Button>
      </header>
      <div className="p-3.5">
        {isLoading && <p className="text-sm text-muted-foreground">Loading logs…</p>}

        {!isLoading && data === null && (
          <p className="text-sm text-muted-foreground">Run not found.</p>
        )}

        {!isLoading && data && !data.logsAvailable && (
          <p className="text-sm text-muted-foreground">
            Logs not available (file mancante o non leggibile).
          </p>
        )}

        {!isLoading && data && data.logsAvailable && (
          <>
            {data.status !== 'Success' && data.errorCode && (
              <div className="mb-3 rounded-md border-l-4 border-entity-event bg-entity-event/[0.04] px-3 py-2">
                <div className="font-mono text-xs font-bold text-entity-event">
                  {data.errorCode}
                </div>
                {data.errorDetail && (
                  <div className="mt-1 font-mono text-[11px] text-muted-foreground">
                    {data.errorDetail}
                  </div>
                )}
              </div>
            )}
            <pre className="max-h-96 overflow-y-auto rounded bg-muted/40 p-3 font-mono text-[11px] text-foreground">
              {data.logs.join('\n')}
            </pre>
          </>
        )}
      </div>
    </section>
  );
}
