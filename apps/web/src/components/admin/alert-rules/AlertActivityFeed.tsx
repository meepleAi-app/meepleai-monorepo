'use client';

/**
 * AlertActivityFeed (#1840 SP5 F4-C7) — feed live SSE filtrato per
 * `eventTypes=alert.fired,alert.resolved` (vedi EventTypeRegistry BE 1.3).
 *
 * Mockup riga 580-660: header "Activity feed" + lista `.activity-item` con
 * timestamp · badge livello · rule + metric value. Riusa pattern `useLiveEvents`
 * già esistente dal F4.1 #1718 (C1 Monitor).
 *
 * Acceptance scenario F:
 *   Given SSE attiva su /admin/events/stream?eventTypes=alert.fired,alert.resolved
 *   When un alert fires (metric supera threshold)
 *   Then AlertActivityFeed aggiunge row in cima con badge "FIRED"
 *   And quando alert si risolve: badge "RESOLVED" + delta time
 *   And feed scrolla limitato a 50 row più recenti
 *   And role="log" aria-live="polite"
 */

import { useMemo } from 'react';

import { Bell, CheckCircle2, FlaskConical, RadioTower } from 'lucide-react';

import type { DomainEventDto } from '@/components/admin/monitor/live-event-types';
import { useLiveEvents } from '@/components/admin/monitor/use-live-events';
import { cn } from '@/lib/utils';

const ALERT_EVENT_TYPES = ['alert.fired', 'alert.resolved'] as const;
const MAX_VISIBLE = 50;

interface AlertEventPayload {
  ruleName?: string;
  metric?: string;
  value?: number;
  threshold?: number;
  thresholdUnit?: string;
  channels?: string[];
  isDryRun?: boolean;
  isTest?: boolean;
  duration?: string; // ISO duration string for resolved events
}

function parsePayload(json: string): AlertEventPayload {
  try {
    const parsed = JSON.parse(json) as Partial<AlertEventPayload> & Record<string, unknown>;
    // Wire shape is camelCase; copy known keys defensively.
    return {
      ruleName: typeof parsed.ruleName === 'string' ? parsed.ruleName : undefined,
      metric: typeof parsed.metric === 'string' ? parsed.metric : undefined,
      value: typeof parsed.value === 'number' ? parsed.value : undefined,
      threshold: typeof parsed.threshold === 'number' ? parsed.threshold : undefined,
      thresholdUnit: typeof parsed.thresholdUnit === 'string' ? parsed.thresholdUnit : undefined,
      channels: Array.isArray(parsed.channels)
        ? parsed.channels.filter((c): c is string => typeof c === 'string')
        : undefined,
      isDryRun: typeof parsed.isDryRun === 'boolean' ? parsed.isDryRun : undefined,
      isTest: typeof parsed.isTest === 'boolean' ? parsed.isTest : undefined,
      duration: typeof parsed.duration === 'string' ? parsed.duration : undefined,
    };
  } catch {
    return {};
  }
}

function formatTime(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleTimeString('it-IT', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
}

function AlertActivityRow({ event }: { event: DomainEventDto }) {
  const payload = useMemo(() => parsePayload(event.payloadJson), [event.payloadJson]);
  const isResolved = event.eventType === 'alert.resolved';
  const isTest = payload.isTest === true;
  const isDryRun = payload.isDryRun === true;

  const Icon = isResolved ? CheckCircle2 : isTest ? FlaskConical : Bell;
  const badgeLabel = isResolved
    ? 'RESOLVED'
    : isTest && isDryRun
      ? 'TEST · DRY RUN'
      : isTest
        ? 'TEST · LIVE'
        : 'FIRED';
  const badgeColorClass = isResolved
    ? 'border-emerald-400/40 bg-emerald-50 text-emerald-700 dark:border-emerald-500/30 dark:bg-emerald-950/30 dark:text-emerald-300'
    : isTest
      ? 'border-blue-400/40 bg-blue-50 text-blue-700 dark:border-blue-500/30 dark:bg-blue-950/30 dark:text-blue-300'
      : 'border-rose-400/40 bg-rose-50 text-rose-700 dark:border-rose-500/30 dark:bg-rose-950/30 dark:text-rose-300';

  return (
    <li
      data-testid={`alert-activity-row-${event.id}`}
      className="flex items-start gap-3 border-b border-border/40 px-4 py-2.5 last:border-b-0"
    >
      <span
        className={cn(
          'inline-grid h-7 w-7 shrink-0 place-items-center rounded-full',
          isResolved
            ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-300'
            : isTest
              ? 'bg-blue-100 text-blue-700 dark:bg-blue-950/40 dark:text-blue-300'
              : 'bg-rose-100 text-rose-700 dark:bg-rose-950/40 dark:text-rose-300'
        )}
      >
        <Icon className="h-3.5 w-3.5" strokeWidth={2.5} aria-hidden />
      </span>

      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-2">
          <span
            className={cn(
              'inline-flex items-center rounded-full border px-2 py-0.5 font-mono text-[10px] font-bold uppercase tracking-wider',
              badgeColorClass
            )}
          >
            {badgeLabel}
          </span>
          <span className="font-quicksand text-sm font-bold text-foreground">
            {payload.ruleName ?? 'Regola sconosciuta'}
          </span>
          {payload.channels && payload.channels.length > 0 && (
            <span className="font-mono text-[10px] text-muted-foreground">
              → {payload.channels.join(' + ')}
            </span>
          )}
        </div>
        {payload.metric && payload.value !== undefined && payload.threshold !== undefined && (
          <p className="mt-0.5 font-mono text-[11.5px] text-muted-foreground">
            {payload.metric}{' '}
            <span className="rounded bg-muted/50 px-1 font-bold text-foreground">
              {payload.value}
              {payload.thresholdUnit ?? ''}
            </span>{' '}
            vs threshold{' '}
            <span className="rounded bg-muted/50 px-1 font-bold text-foreground">
              {payload.threshold}
              {payload.thresholdUnit ?? ''}
            </span>
          </p>
        )}
      </div>

      <time
        dateTime={event.occurredAt}
        className="shrink-0 font-mono text-[10px] text-muted-foreground"
      >
        {formatTime(event.occurredAt)}
      </time>
    </li>
  );
}

interface AlertActivityFeedProps {
  className?: string;
}

export function AlertActivityFeed({ className }: AlertActivityFeedProps) {
  const { events, isLoading, isStreaming, error } = useLiveEvents({
    eventTypes: [...ALERT_EVENT_TYPES],
    initialLimit: MAX_VISIBLE,
    maxBuffer: MAX_VISIBLE,
  });

  return (
    <section
      className={cn('overflow-hidden rounded-xl border border-border bg-card/60', className)}
      data-testid="alert-activity-feed"
    >
      <header className="flex items-center justify-between gap-3 border-b border-border/60 bg-muted/30 px-4 py-2.5">
        <div className="flex items-center gap-2">
          <RadioTower
            aria-hidden
            className={cn(
              'h-4 w-4',
              isStreaming ? 'text-emerald-600 dark:text-emerald-400' : 'text-muted-foreground'
            )}
            strokeWidth={2.5}
          />
          <h3 className="font-quicksand text-sm font-bold text-foreground">Activity feed</h3>
          <span
            aria-live="polite"
            className="font-mono text-[10px] uppercase tracking-wider text-muted-foreground"
          >
            {isStreaming ? '· live' : isLoading ? '· caricamento' : '· offline'}
          </span>
        </div>
        <span className="font-mono text-[10px] text-muted-foreground">
          ultimi {Math.min(events.length, MAX_VISIBLE)} eventi
        </span>
      </header>

      <ul
        role="log"
        aria-live="polite"
        aria-relevant="additions"
        aria-label="Feed eventi alert"
        className="max-h-96 overflow-y-auto"
      >
        {error ? (
          <li className="px-4 py-6 text-center font-mono text-[11px] text-rose-600 dark:text-rose-400">
            Errore connessione SSE: {error.message}
          </li>
        ) : events.length === 0 ? (
          <li className="px-4 py-6 text-center font-mono text-[11px] text-muted-foreground">
            {isLoading
              ? 'Caricamento backfill…'
              : 'Nessun alert recente · gli eventi appariranno qui in tempo reale'}
          </li>
        ) : (
          events
            .slice(0, MAX_VISIBLE)
            .map(event => <AlertActivityRow key={event.id} event={event} />)
        )}
      </ul>
    </section>
  );
}
