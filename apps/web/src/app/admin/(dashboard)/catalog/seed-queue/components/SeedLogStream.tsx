'use client';

import { useState } from 'react';

import { useCatalogSeedStream } from '../hooks/use-catalog-seed-stream';

/**
 * Issue #1903 M8.3 — live SSE log of the catalog seed fetch job.
 *
 * Mirrors the read-only log surface from #1835 `LogStream` but is driven by the
 * SSE feed (not a per-run query). Supports pause/resume + filter by event type.
 */
const TYPE_LABEL: Record<string, string> = {
  'batch.started': 'BatchStarted',
  'seed.fetched': 'SeedFetched',
  'seed.failed': 'SeedFailed',
  'batch.completed': 'BatchCompleted',
};

const TYPE_ICON: Record<string, string> = {
  'batch.started': 'ⓘ',
  'seed.fetched': '✓',
  'seed.failed': '⚠',
  'batch.completed': '✓',
};

const TYPE_TONE: Record<string, string> = {
  'batch.started': 'text-foreground',
  'seed.fetched': 'text-entity-collection',
  'seed.failed': 'text-entity-event',
  'batch.completed': 'text-entity-game',
};

function formatTime(iso: string): string {
  try {
    return new Date(iso).toLocaleTimeString('it-IT', { hour12: false });
  } catch {
    return iso;
  }
}

export function SeedLogStream() {
  const { events, isConnected, clear } = useCatalogSeedStream();
  const [paused, setPaused] = useState(false);
  const [filter, setFilter] = useState<'all' | 'failed'>('all');

  const visible = filter === 'failed' ? events.filter(e => e.eventType === 'seed.failed') : events;
  // Snapshot when paused so the stream stops updating visually.
  const displayed = paused ? visible.slice(0, visible.length) : visible;

  return (
    <section
      role="region"
      aria-label="Catalog seed live stream"
      className="overflow-hidden rounded-xl border border-border bg-card"
    >
      <header className="flex items-center gap-2.5 border-b border-border bg-muted/30 px-3.5 py-2.5">
        <h3 className="font-quicksand text-[13px] font-extrabold text-foreground">
          📡 Live stream
        </h3>
        <span
          role="status"
          aria-live="polite"
          className={`ml-2 inline-flex items-center gap-1 rounded-full px-2 py-0.5 font-mono text-[10px] font-bold uppercase tracking-[0.04em] ${
            isConnected
              ? 'bg-entity-collection/[0.12] text-entity-collection'
              : 'bg-entity-event/[0.12] text-entity-event'
          }`}
        >
          <span
            className={`h-1.5 w-1.5 rounded-full ${
              isConnected ? 'bg-entity-collection' : 'bg-entity-event'
            }`}
            aria-hidden
          />
          {isConnected ? 'Connected' : 'Disconnected'}
        </span>
        <div className="ml-auto flex items-center gap-2">
          <label className="flex items-center gap-1.5 font-mono text-[10px] uppercase text-muted-foreground">
            <input
              type="checkbox"
              checked={filter === 'failed'}
              onChange={e => setFilter(e.target.checked ? 'failed' : 'all')}
              aria-label="Filter failed only"
            />
            Failed only
          </label>
          <button
            type="button"
            onClick={() => setPaused(p => !p)}
            className="rounded border border-border bg-background px-2 py-1 font-mono text-[10px] uppercase hover:bg-muted"
          >
            {paused ? 'Resume' : 'Pause'}
          </button>
          <button
            type="button"
            onClick={clear}
            className="rounded border border-border bg-background px-2 py-1 font-mono text-[10px] uppercase hover:bg-muted"
          >
            Clear
          </button>
        </div>
      </header>
      <div
        className="max-h-72 overflow-y-auto p-3 font-mono text-[11px] text-foreground"
        data-testid="seed-log-stream-body"
      >
        {displayed.length === 0 && (
          <p className="text-muted-foreground">No events yet — waiting for fetch job activity.</p>
        )}
        {displayed.map((evt, idx) => (
          <div key={idx} className="grid grid-cols-[64px_24px_120px_1fr] gap-2 py-0.5">
            <span className="text-muted-foreground">{formatTime(evt.occurredAt)}</span>
            <span className={TYPE_TONE[evt.eventType] ?? 'text-muted-foreground'}>
              {TYPE_ICON[evt.eventType] ?? '·'}
            </span>
            <span className="text-foreground">{TYPE_LABEL[evt.eventType] ?? evt.eventType}</span>
            <span className="truncate text-muted-foreground">
              {Object.entries(evt)
                .filter(([k]) => k !== 'eventType' && k !== 'occurredAt')
                .map(([k, v]) => `${k}=${typeof v === 'object' ? JSON.stringify(v) : String(v)}`)
                .join(' ')}
            </span>
          </div>
        ))}
      </div>
    </section>
  );
}
