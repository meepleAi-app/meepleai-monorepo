'use client';

import { useCallback } from 'react';

import type { IngestionLog } from '@/lib/api/schemas/ingestion-log.schemas';

interface IngestionActionsProps {
  readonly log: IngestionLog;
  readonly onRetry: (jobId: string) => void;
}

function buildLogText(log: IngestionLog): string {
  return log.steps
    .flatMap(s => s.logEntries.map(e => ({ ...e, step: s.stepName })))
    .sort((a, b) => a.timestamp.localeCompare(b.timestamp))
    .map(e => `[${e.timestamp}] [${e.level.toUpperCase()}] [${e.step}] ${e.message}`)
    .join('\n');
}

function downloadAsFile(content: string, filename: string): void {
  const blob = new Blob([content], { type: 'text/plain;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.style.display = 'none';
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  setTimeout(() => URL.revokeObjectURL(url), 0);
}

/**
 * Footer with up to 3 actions for the ingestion log tab:
 *   - Download log (always)
 *   - Copy job ID (always)
 *   - Re-enqueue (only when canRetry === true)
 * Issue #1650.
 */
export function IngestionActions({ log, onRetry }: IngestionActionsProps) {
  const handleDownload = useCallback(() => {
    downloadAsFile(buildLogText(log), `ingestion-${log.id}.log`);
  }, [log]);

  const handleCopy = useCallback(() => {
    void navigator.clipboard.writeText(log.id);
  }, [log.id]);

  const handleRetry = useCallback(() => {
    onRetry(log.id);
  }, [log.id, onRetry]);

  return (
    <div className="mt-3 flex flex-wrap gap-1.5">
      <button
        type="button"
        onClick={handleDownload}
        className="px-3 py-1.5 text-xs font-medium border border-border rounded-md hover:bg-muted/70"
      >
        ⤓ Download log
      </button>
      <button
        type="button"
        onClick={handleCopy}
        className="px-3 py-1.5 text-xs font-medium border border-border rounded-md hover:bg-muted/70"
      >
        📋 Copy job ID
      </button>
      {log.canRetry && (
        <button
          type="button"
          onClick={handleRetry}
          className="px-3 py-1.5 text-xs font-medium border border-amber-500/50 text-amber-700 dark:text-amber-300 rounded-md hover:bg-amber-500/10"
        >
          ⟳ Re-enqueue
        </button>
      )}
    </div>
  );
}
