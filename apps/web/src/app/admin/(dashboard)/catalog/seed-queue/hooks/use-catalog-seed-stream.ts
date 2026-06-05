'use client';

import { useEffect, useRef, useState } from 'react';

import type { SeedStreamEvent } from '../lib/catalog-seed-types';

/**
 * Issue #1903 M8.1 — EventSource subscription to the admin catalog seed SSE stream.
 *
 * The endpoint emits four named events: `batch.started`, `seed.fetched`,
 * `seed.failed`, `batch.completed`. We attach a listener per event type so the
 * UI can filter without inspecting the parsed JSON, and we also subscribe to the
 * default `message` event so a generic test fixture can drive the hook without
 * having to emit named events.
 *
 * Lifecycle: opens on mount, closes on unmount. Buffer caps at 200 events
 * (matching the server-side replay buffer documented in §6 of the design spec).
 */
const STREAM_URL = '/api/v1/admin/catalog/seeds/stream';
const EVENT_TYPES = ['batch.started', 'seed.fetched', 'seed.failed', 'batch.completed'] as const;
const MAX_BUFFER = 200;

export interface UseCatalogSeedStreamResult {
  events: SeedStreamEvent[];
  isConnected: boolean;
  clear: () => void;
}

export function useCatalogSeedStream(): UseCatalogSeedStreamResult {
  const [events, setEvents] = useState<SeedStreamEvent[]>([]);
  const [isConnected, setIsConnected] = useState(false);
  const esRef = useRef<EventSource | null>(null);

  useEffect(() => {
    // Guard for non-browser environments (SSR / tests without EventSource).
    if (typeof EventSource === 'undefined') return;

    const es = new EventSource(STREAM_URL, { withCredentials: true });
    esRef.current = es;
    es.onopen = () => setIsConnected(true);
    es.onerror = () => setIsConnected(false);

    const appendEvent = (eventType: string, raw: string) => {
      try {
        const parsed = JSON.parse(raw) as Record<string, unknown>;
        const evt: SeedStreamEvent = {
          eventType,
          occurredAt: (parsed.occurredAt as string) ?? new Date().toISOString(),
          ...parsed,
        };
        setEvents(prev => {
          const next = [...prev, evt];
          return next.length > MAX_BUFFER ? next.slice(-MAX_BUFFER) : next;
        });
      } catch {
        // Malformed JSON — drop silently so a corrupt frame can't crash the UI.
      }
    };

    const listeners = EVENT_TYPES.map(type => {
      const handler = (e: MessageEvent) => appendEvent(type, e.data);
      es.addEventListener(type, handler as EventListener);
      return { type, handler };
    });

    // Default `message` event for generic emitters (tests, future event types).
    const defaultHandler = (e: MessageEvent) => appendEvent('message', e.data);
    es.addEventListener('message', defaultHandler as EventListener);

    return () => {
      listeners.forEach(({ type, handler }) =>
        es.removeEventListener(type, handler as EventListener)
      );
      es.removeEventListener('message', defaultHandler as EventListener);
      es.close();
      esRef.current = null;
    };
  }, []);

  return {
    events,
    isConnected,
    clear: () => setEvents([]),
  };
}
