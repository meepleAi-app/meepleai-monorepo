/**
 * useLiveEvents — Task 3.3 (Issue #1718, F4.1 Monitor)
 *
 * Combines an HTTP backfill request with an SSE stream for real-time
 * domain event monitoring in the admin Monitor panel.
 *
 * Lifecycle:
 *  1. Mount   → GET /api/v1/admin/events (backfill) → EventSource attach
 *  2. Message → prepend to events[], bounded by maxBuffer (drops oldest)
 *  3. Pause   → close EventSource, stop accepting messages
 *  4. Resume  → reopen EventSource from last seen id
 *  5. Refetch → clear state, redo backfill + attach
 *  6. Error   → exponential backoff after BACKOFF_THRESHOLD consecutive failures
 *  7. Unmount → EventSource.close(), clear pending timers (no memory leak)
 */

import { useCallback, useEffect, useRef, useState } from 'react';

import type { DomainEventDto, LiveEventFilters } from './live-event-types';

// ============================================================================
// Public types
// ============================================================================

export interface UseLiveEventsResult {
  /** Domain events, newest-first (DESC by loggedAt). */
  events: DomainEventDto[];
  /** true while the initial HTTP backfill is in-flight. */
  isLoading: boolean;
  /** true while the SSE connection is open (after simulateOpen / onopen). */
  isStreaming: boolean;
  /** Last error from backfill or SSE. */
  error: Error | null;
  /** Close the EventSource; future SSE messages are discarded. */
  pause: () => void;
  /** Reopen the EventSource from the last seen event id. */
  resume: () => void;
  /** Reset all state, re-run backfill and re-attach SSE. */
  refetch: () => void;
}

export interface UseLiveEventsOptions extends LiveEventFilters {
  /** Number of events to fetch from the backfill endpoint. Default: 100. */
  initialLimit?: number;
  /** Maximum events kept in memory. Oldest events are dropped on overflow. Default: 1000. */
  maxBuffer?: number;
}

// ============================================================================
// Constants
// ============================================================================

const DEFAULT_MAX_BUFFER = 1_000;
const DEFAULT_INITIAL_LIMIT = 100;

/**
 * Backoff delays in ms.  After BACKOFF_THRESHOLD consecutive failures
 * the hook waits BACKOFF_SEQUENCE[attempt - BACKOFF_THRESHOLD] before
 * reconnecting.  Capped at the last value (30 s).
 */
const BACKOFF_SEQUENCE = [1_000, 2_000, 4_000, 8_000, 16_000, 30_000] as const;
const BACKOFF_THRESHOLD = 3;

// ============================================================================
// Helpers
// ============================================================================

/**
 * Build a URLSearchParams from filter options (shared between backfill URL
 * and SSE URL to keep them in sync).
 */
function buildFilterParams(filters: LiveEventFilters): URLSearchParams {
  const p = new URLSearchParams();
  if (filters.eventTypes?.length) p.set('eventTypes', filters.eventTypes.join(','));
  if (filters.aggregateTypes?.length) p.set('aggregateTypes', filters.aggregateTypes.join(','));
  if (filters.userId) p.set('userId', filters.userId);
  if (filters.aggregateId) p.set('aggregateId', filters.aggregateId);
  return p;
}

function backoffDelay(consecutiveFailures: number): number {
  const idx = Math.max(0, consecutiveFailures - BACKOFF_THRESHOLD);
  return BACKOFF_SEQUENCE[Math.min(idx, BACKOFF_SEQUENCE.length - 1)];
}

// ============================================================================
// Hook
// ============================================================================

export function useLiveEvents(options: UseLiveEventsOptions = {}): UseLiveEventsResult {
  const {
    initialLimit = DEFAULT_INITIAL_LIMIT,
    maxBuffer = DEFAULT_MAX_BUFFER,
    eventTypes,
    aggregateTypes,
    userId,
    aggregateId,
  } = options;

  // --- Derived filter object (stable ref guard handled via JSON-key deps) ---
  const filters: LiveEventFilters = { eventTypes, aggregateTypes, userId, aggregateId };

  // --- State ---
  const [events, setEvents] = useState<DomainEventDto[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [isStreaming, setIsStreaming] = useState<boolean>(false);
  const [error, setError] = useState<Error | null>(null);

  // --- Mutable refs (never trigger re-renders) ---
  const eventSourceRef = useRef<EventSource | null>(null);
  const consecutiveFailuresRef = useRef<number>(0);
  const backoffTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const pausedRef = useRef<boolean>(false);
  /** Mirror of the events state for access inside SSE callbacks without closure stale reads. */
  const eventsRef = useRef<DomainEventDto[]>([]);
  /** Generation counter incremented on each refetch to abort stale async callbacks. */
  const generationRef = useRef<number>(0);

  // Keep eventsRef in sync with state
  const updateEvents = useCallback((updater: (prev: DomainEventDto[]) => DomainEventDto[]) => {
    setEvents(prev => {
      const next = updater(prev);
      eventsRef.current = next;
      return next;
    });
  }, []);

  // -------------------------------------------------------------------------
  // SSE management
  // -------------------------------------------------------------------------

  const closeEventSource = useCallback(() => {
    if (eventSourceRef.current) {
      eventSourceRef.current.close();
      eventSourceRef.current = null;
    }
    setIsStreaming(false);
  }, []);

  const clearBackoffTimer = useCallback(() => {
    if (backoffTimerRef.current !== null) {
      clearTimeout(backoffTimerRef.current);
      backoffTimerRef.current = null;
    }
  }, []);

  /** Open (or reopen) an EventSource.  Pass `lastId` to tell the server
   *  which event to resume from; omit to start from the current tail. */
  const attachEventSource = useCallback(
    (lastId?: string) => {
      if (pausedRef.current) return;

      closeEventSource();
      clearBackoffTimer();

      const filterParams = buildFilterParams(filters);
      if (lastId) {
        filterParams.set('lastEventId', lastId);
      }

      const qs = filterParams.toString();
      const url = `/api/v1/admin/events/stream${qs ? `?${qs}` : ''}`;

      const es = new EventSource(url, { withCredentials: true });
      eventSourceRef.current = es;

      es.onopen = () => {
        consecutiveFailuresRef.current = 0;
        setIsStreaming(true);
      };

      es.onmessage = (evt: MessageEvent<string>) => {
        if (pausedRef.current) return;
        try {
          const incoming = JSON.parse(evt.data) as DomainEventDto;
          updateEvents(prev => [incoming, ...prev].slice(0, maxBuffer));
        } catch {
          // Malformed JSON — silently discard
        }
      };

      es.onerror = () => {
        es.close();
        if (eventSourceRef.current === es) {
          eventSourceRef.current = null;
        }
        setIsStreaming(false);
        consecutiveFailuresRef.current += 1;

        if (consecutiveFailuresRef.current >= BACKOFF_THRESHOLD) {
          const delay = backoffDelay(consecutiveFailuresRef.current);
          backoffTimerRef.current = setTimeout(() => {
            if (!pausedRef.current) {
              attachEventSource(eventsRef.current[0]?.id);
            }
          }, delay);
        } else {
          // Immediate reconnect below threshold
          if (!pausedRef.current) {
            attachEventSource(eventsRef.current[0]?.id);
          }
        }
      };
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [
      maxBuffer,
      closeEventSource,
      clearBackoffTimer,
      updateEvents,
      eventTypes?.join(','),
      aggregateTypes?.join(','),
      userId,
      aggregateId,
    ]
  );

  // -------------------------------------------------------------------------
  // Backfill
  // -------------------------------------------------------------------------

  const runBackfill = useCallback(
    async (generation: number) => {
      setIsLoading(true);
      setError(null);

      try {
        const filterParams = buildFilterParams(filters);
        filterParams.set('limit', String(initialLimit));
        const qs = filterParams.toString();
        const url = `/api/v1/admin/events${qs ? `?${qs}` : ''}`;

        const resp = await fetch(url);
        if (!resp.ok) {
          throw new Error(`Backfill failed: ${resp.status}`);
        }

        // Guard against stale callbacks from a previous refetch
        if (generationRef.current !== generation) return;

        const data = (await resp.json()) as { items: DomainEventDto[] };
        const fetched: DomainEventDto[] = data.items ?? [];

        eventsRef.current = fetched;
        setEvents(fetched);
        setIsLoading(false);

        // Attach SSE — pass the most recent event id as Last-Event-ID hint
        attachEventSource(fetched[0]?.id);
      } catch (err) {
        if (generationRef.current !== generation) return;
        setError(err instanceof Error ? err : new Error(String(err)));
        setIsLoading(false);
      }
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [
      initialLimit,
      attachEventSource,
      eventTypes?.join(','),
      aggregateTypes?.join(','),
      userId,
      aggregateId,
    ]
  );

  // -------------------------------------------------------------------------
  // Mount / filter-change effect
  // -------------------------------------------------------------------------

  useEffect(() => {
    pausedRef.current = false;
    generationRef.current += 1;
    const gen = generationRef.current;

    // Reset state for fresh start
    eventsRef.current = [];
    setEvents([]);
    setIsLoading(true);
    setError(null);
    closeEventSource();
    clearBackoffTimer();

    void runBackfill(gen);

    return () => {
      // Cleanup on unmount or dep change
      closeEventSource();
      clearBackoffTimer();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [initialLimit, eventTypes?.join(','), aggregateTypes?.join(','), userId, aggregateId]);

  // -------------------------------------------------------------------------
  // Public controls
  // -------------------------------------------------------------------------

  const pause = useCallback(() => {
    pausedRef.current = true;
    closeEventSource();
    clearBackoffTimer();
  }, [closeEventSource, clearBackoffTimer]);

  const resume = useCallback(() => {
    pausedRef.current = false;
    attachEventSource(eventsRef.current[0]?.id);
  }, [attachEventSource]);

  const refetch = useCallback(() => {
    pausedRef.current = false;
    closeEventSource();
    clearBackoffTimer();
    consecutiveFailuresRef.current = 0;

    generationRef.current += 1;
    const gen = generationRef.current;

    eventsRef.current = [];
    setEvents([]);
    setIsLoading(true);
    setError(null);

    void runBackfill(gen);
  }, [closeEventSource, clearBackoffTimer, runBackfill]);

  // -------------------------------------------------------------------------

  return { events, isLoading, isStreaming, error, pause, resume, refetch };
}
