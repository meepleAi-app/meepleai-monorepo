'use client';

import { useEffect, useRef, useState } from 'react';

import { getApiBase } from '@/lib/api/core/httpClient';

export interface QueueStreamEvent {
  timestamp: string;
  queued: number;
  processing: number;
  completed: number;
  failed: number;
  eta: number;
  items: Array<{ bggId: number; gameName?: string; status: number }>;
}

interface UseSseQueueOptions {
  enabled: boolean;
  onUpdate?: (event: QueueStreamEvent) => void;
}

export function useSseQueue({ enabled, onUpdate }: UseSseQueueOptions) {
  const [isConnected, setIsConnected] = useState(false);
  const [lastEvent, setLastEvent] = useState<QueueStreamEvent | null>(null);
  const eventSourceRef = useRef<EventSource | null>(null);
  const onUpdateRef = useRef(onUpdate);
  const enabledRef = useRef(enabled);

  onUpdateRef.current = onUpdate;
  enabledRef.current = enabled;

  useEffect(() => {
    if (!enabled) {
      eventSourceRef.current?.close();
      eventSourceRef.current = null;
      setIsConnected(false);
      return;
    }

    let reconnectTimer: ReturnType<typeof setTimeout> | null = null;

    function connect() {
      if (eventSourceRef.current) {
        eventSourceRef.current.close();
      }

      // #4059: come in `guest-session-view.tsx` — `NEXT_PUBLIC_API_URL` non esiste nei build
      // deployabili, quindi si ricadeva su `''` e lo stream passava dal proxy Next. Giusto
      // per caso; ora dichiarato.
      const baseUrl = getApiBase();
      const es = new EventSource(`${baseUrl}/api/v1/admin/bgg-queue/stream`);

      es.onopen = () => setIsConnected(true);

      es.onmessage = event => {
        try {
          const data: QueueStreamEvent = JSON.parse(event.data);
          setLastEvent(data);
          onUpdateRef.current?.(data);
        } catch {
          // Ignore parse errors
        }
      };

      es.onerror = () => {
        setIsConnected(false);
        es.close();
        reconnectTimer = setTimeout(() => {
          if (enabledRef.current) connect();
        }, 5000);
      };

      eventSourceRef.current = es;
    }

    connect();

    return () => {
      if (reconnectTimer) clearTimeout(reconnectTimer);
      eventSourceRef.current?.close();
      eventSourceRef.current = null;
      setIsConnected(false);
    };
  }, [enabled]);

  return { isConnected, lastEvent };
}
