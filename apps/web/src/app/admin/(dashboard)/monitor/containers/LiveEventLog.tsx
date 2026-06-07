'use client';

/**
 * LiveEventLog (#1837 SP5 F4-C1) — feed visuale dei domain events
 * Container/Infrastructure consumati dalla subscription useLiveEvents
 * sollevata a livello di `page.tsx`. NO subscription propria (props-driven).
 *
 * Layout mockup `.event-log .row`:
 *   [ts: 96px mono] [lvl: 60px uppercase color] [msg: 1fr ellipsis]
 *
 * Fade-out: eventi loggedAt > staleThresholdMs fa → opacity 50.
 * Riconciliazione: setInterval ogni 5s aggiorna `now` (re-render rows
 * con/senza opacità senza richiedere update dello state esterno).
 */

import { useEffect, useState } from 'react';

import { Radio } from 'lucide-react';

import type { DomainEventDto } from '@/components/admin/monitor/live-event-types';
import { cn } from '@/lib/utils';

import {
  buildLogMessage,
  deriveLogLevel,
  formatLogTimestamp,
  isStaleEvent,
  type LogLevel,
} from './_utils/event-log-mapper';

const LEVEL_CLASSES: Record<LogLevel, string> = {
  INFO: 'text-sky-600 dark:text-sky-400',
  OK: 'text-emerald-600 dark:text-emerald-400',
  WARN: 'text-amber-600 dark:text-amber-400',
  ERR: 'text-rose-600 dark:text-rose-400',
};

const DEFAULT_MAX_VISIBLE = 20;
const DEFAULT_STALE_THRESHOLD_MS = 30_000;
const STALE_RECONCILE_INTERVAL_MS = 5_000;

interface LiveEventLogProps {
  events: ReadonlyArray<DomainEventDto>;
  isStreaming: boolean;
  isLoading: boolean;
  maxVisible?: number;
  staleThresholdMs?: number;
}

export function LiveEventLog({
  events,
  isStreaming,
  isLoading,
  maxVisible = DEFAULT_MAX_VISIBLE,
  staleThresholdMs = DEFAULT_STALE_THRESHOLD_MS,
}: LiveEventLogProps) {
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    const id = setInterval(() => setNow(Date.now()), STALE_RECONCILE_INTERVAL_MS);
    return () => clearInterval(id);
  }, []);

  const visible = events.slice(0, maxVisible);

  if (isLoading && visible.length === 0) {
    return (
      <div
        role="log"
        aria-live="polite"
        data-testid="live-event-log"
        className="rounded-lg border border-border bg-card/60 p-6 text-center text-sm text-muted-foreground"
      >
        Caricamento eventi…
      </div>
    );
  }

  if (visible.length === 0) {
    return (
      <div
        role="log"
        aria-live="polite"
        data-testid="live-event-log"
        className="rounded-lg border border-border bg-card/60 p-6 text-center text-sm text-muted-foreground"
      >
        {isStreaming
          ? 'Nessun evento ricevuto. SSE in attesa.'
          : 'Stream offline. Tentativo di riconnessione in corso…'}
      </div>
    );
  }

  return (
    <div
      role="log"
      aria-live="polite"
      data-testid="live-event-log"
      className="rounded-lg border border-border bg-card/60 font-mono text-xs"
    >
      <div className="flex items-center gap-2 border-b border-border px-3 py-2 text-[10px] uppercase tracking-wider text-muted-foreground">
        <Radio
          aria-hidden="true"
          className={cn('h-3 w-3', isStreaming ? 'text-emerald-500' : 'text-muted-foreground')}
        />
        <span>{isStreaming ? 'streaming · SSE' : 'offline · SSE down'}</span>
        <span className="ml-auto text-muted-foreground/70">
          {visible.length} / {events.length}
        </span>
      </div>

      <ul className="max-h-[340px] overflow-y-auto divide-y divide-border">
        {visible.map(event => {
          const stale = isStaleEvent(event.loggedAt, now, staleThresholdMs);
          const level = deriveLogLevel(event.eventType);
          return (
            <li
              key={event.id}
              data-testid="live-event-row"
              className={cn(
                'grid grid-cols-[96px_60px_1fr] items-center gap-3 px-3 py-1.5 transition-opacity',
                stale ? 'opacity-50' : 'opacity-100'
              )}
            >
              <span className="text-muted-foreground">{formatLogTimestamp(event.loggedAt)}</span>
              <span
                className={cn(
                  'font-bold uppercase tracking-wider text-[10px]',
                  LEVEL_CLASSES[level]
                )}
              >
                {level}
              </span>
              <span className="truncate text-foreground">{buildLogMessage(event)}</span>
            </li>
          );
        })}
      </ul>
    </div>
  );
}
