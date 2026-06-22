/**
 * Issue #1823 Phase E F4 — EventSource hook for live attempt-recorded events
 * on the admin Wikidata dead-letter visibility page.
 *
 * Native browser EventSource auto-reconnects with a UA-default backoff
 * (typically 3s) on transient network errors. We layer a slim wrapper that
 *   - normalises connection state for UI badges (`connecting | open | closed`)
 *   - exposes a `lastEvent` payload for inline row patches
 *   - hard-closes the connection on unmount so React 19 strict-mode dev
 *     re-mounts don't leak persistent SSE sockets
 */

'use client';

import { useEffect, useRef, useState } from 'react';

export type AttemptTimelineOutcome = 'Success' | 'Skipped' | 'Failed' | 'DeadLetter';

export interface WikidataEnrichmentEventPayload {
  attemptId: string;
  sharedGameId: string;
  outcome: AttemptTimelineOutcome;
  reason: string | null;
  attemptedAt: string;
  retryCount: number;
  nextRetryAt: string | null;
  deadLetteredAt: string | null;
}

export type SseConnectionState = 'connecting' | 'open' | 'closed';

export interface UseWikidataEnrichmentEventsResult {
  readonly state: SseConnectionState;
  readonly lastEvent: WikidataEnrichmentEventPayload | null;
}

const SSE_ENDPOINT = '/api/v1/admin/wikidata/enrichment/events';

/**
 * Subscribes to the BE SSE stream and exposes the most-recent event for
 * call-site consumption. Reconnection is delegated to the browser's native
 * EventSource implementation; the only manual close is the unmount cleanup.
 */
export function useWikidataEnrichmentEvents(): UseWikidataEnrichmentEventsResult {
  const [state, setState] = useState<SseConnectionState>('connecting');
  const [lastEvent, setLastEvent] = useState<WikidataEnrichmentEventPayload | null>(null);
  const sourceRef = useRef<EventSource | null>(null);

  useEffect(() => {
    const source = new EventSource(SSE_ENDPOINT, { withCredentials: true });
    sourceRef.current = source;

    source.onopen = () => setState('open');
    source.onerror = () => {
      // EventSource silently reconnects on error; surface 'connecting' so the
      // UI badge reflects the transient state.
      setState('connecting');
    };
    source.addEventListener('attempt-recorded', e => {
      try {
        const data = JSON.parse((e as MessageEvent).data) as WikidataEnrichmentEventPayload;
        setLastEvent(data);
      } catch {
        // Ignore malformed payloads — the BE serializer guarantees well-formed
        // JSON, so a parse error indicates an out-of-band proxy / heartbeat
        // collision and is safe to skip.
      }
    });

    return () => {
      source.close();
      sourceRef.current = null;
      setState('closed');
    };
  }, []);

  return { state, lastEvent };
}
