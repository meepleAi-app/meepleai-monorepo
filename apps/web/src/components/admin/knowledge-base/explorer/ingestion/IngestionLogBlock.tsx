'use client';

import type { IngestionLogEntry, IngestionStep } from '@/lib/api/schemas/ingestion-log.schemas';

interface IngestionLogBlockProps {
  readonly steps: ReadonlyArray<IngestionStep>;
}

const LEVEL_CLASS: Record<IngestionLogEntry['level'], string> = {
  Info: 'text-muted-foreground',
  Warning: 'text-amber-600 dark:text-amber-300',
  Error: 'text-rose-700 dark:text-rose-300 font-semibold',
};

function pad2(n: number): string {
  return n.toString().padStart(2, '0');
}

function pad3(n: number): string {
  return n.toString().padStart(3, '0');
}

function formatTimestamp(iso: string): string {
  const d = new Date(iso);
  return `${pad2(d.getHours())}:${pad2(d.getMinutes())}:${pad2(d.getSeconds())}.${pad3(d.getMilliseconds())}`;
}

/**
 * Flat log block — concatenates all log entries across the 5 pipeline steps,
 * ordered by timestamp ascending. Color coding per level (Info muted, Warning
 * amber, Error rose). No live cursor (polling instead of SSE per design).
 * Issue #1650.
 */
export function IngestionLogBlock({ steps }: IngestionLogBlockProps) {
  const all = steps
    .flatMap(s => s.logEntries.map(e => ({ ...e, step: s.stepName })))
    .sort((a, b) => a.timestamp.localeCompare(b.timestamp));

  if (all.length === 0) {
    return (
      <div className="font-mono text-[11px] bg-muted/40 text-muted-foreground rounded-md px-3.5 py-3 italic">
        Nessun log da mostrare.
      </div>
    );
  }

  return (
    <pre
      data-testid="ingestion-log-block"
      className="font-mono text-[11px] bg-muted/40 text-foreground rounded-md px-3.5 py-3 leading-relaxed max-h-[280px] overflow-auto whitespace-pre-wrap break-words m-0"
    >
      {all.map(e => (
        <span key={e.id} data-log-level={e.level} className={LEVEL_CLASS[e.level]}>
          <span className="text-muted-foreground">[{formatTimestamp(e.timestamp)}]</span> [
          {e.level.toUpperCase()}] {e.message}
          {'\n'}
        </span>
      ))}
    </pre>
  );
}
