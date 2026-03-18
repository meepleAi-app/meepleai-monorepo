'use client';

import { useEffect, useRef, useCallback } from 'react';

import { useRouter } from 'next/navigation';
import { toast } from 'sonner';

import { api } from '@/lib/api';

/**
 * SSE event payload for PDF processing job completion/failure.
 * Matches the backend enriched JobCompleted/JobFailed events from
 * GET /api/v1/admin/queue/stream
 */
interface PdfJobEvent {
  type?: string;
  jobId: string;
  fileName?: string;
  sharedGameId?: string;
  gameName?: string;
  error?: string;
}

/**
 * Invisible component that listens to SSE events from the admin queue stream
 * and shows sonner toast notifications for PDF processing outcomes.
 *
 * Mounted in the admin dashboard layout so it's active on all admin pages.
 * Returns null — no DOM output.
 */
export function PdfProcessingNotifier() {
  const router = useRouter();
  const eventSourceRef = useRef<EventSource | null>(null);
  const reconnectTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const reconnectDelayRef = useRef(1000);

  const buildTitle = useCallback((fileName?: string, gameName?: string): string => {
    const name = fileName ?? 'File';
    if (gameName) {
      return `${name} per ${gameName}`;
    }
    return name;
  }, []);

  const handleJobCompleted = useCallback(
    (event: PdfJobEvent) => {
      const title = buildTitle(event.fileName, event.gameName);
      toast.success(title, {
        description: 'Indicizzazione completata',
        duration: 8000,
        action: event.sharedGameId
          ? {
              label: 'Vai al gioco',
              onClick: () => router.push(`/admin/shared-games/${event.sharedGameId}`),
            }
          : undefined,
      });
    },
    [buildTitle, router]
  );

  const handleJobFailed = useCallback(
    (event: PdfJobEvent) => {
      const title = buildTitle(event.fileName, event.gameName);
      const errorMessage = event.error
        ? `Elaborazione fallita: ${event.error}`
        : 'Elaborazione fallita';
      toast.error(title, {
        description: errorMessage,
        duration: 8000,
        action: event.jobId
          ? {
              label: 'Riprova',
              onClick: () => {
                api.admin.retryJob(event.jobId).catch(() => {
                  toast.error('Impossibile riprovare il job');
                });
              },
            }
          : undefined,
      });
    },
    [buildTitle]
  );

  const parseEvent = useCallback((data: string) => {
    try {
      return JSON.parse(data) as PdfJobEvent;
    } catch {
      return null;
    }
  }, []);

  const connect = useCallback(() => {
    // Use relative URL so the browser routes through the Next.js proxy (no CORS)
    const es = new EventSource('/api/v1/admin/queue/stream');
    eventSourceRef.current = es;

    // Named event listeners (backend sends `event: JobCompleted` / `event: JobFailed`)
    es.addEventListener('JobCompleted', (e: MessageEvent) => {
      const parsed = parseEvent(e.data);
      if (parsed) handleJobCompleted(parsed);
    });

    es.addEventListener('JobFailed', (e: MessageEvent) => {
      const parsed = parseEvent(e.data);
      if (parsed) handleJobFailed(parsed);
    });

    // Fallback: unnamed events where type is inside the JSON payload
    es.onmessage = (e: MessageEvent) => {
      const parsed = parseEvent(e.data);
      if (!parsed) return;

      if (parsed.type === 'JobCompleted') {
        handleJobCompleted(parsed);
      } else if (parsed.type === 'JobFailed') {
        handleJobFailed(parsed);
      }
    };

    es.onopen = () => {
      // Reset backoff on successful connection
      reconnectDelayRef.current = 1000;
    };

    es.onerror = () => {
      es.close();
      eventSourceRef.current = null;

      // Exponential backoff reconnect: 1s, 2s, 4s, ... max 30s
      const delay = reconnectDelayRef.current;
      reconnectTimeoutRef.current = setTimeout(() => {
        reconnectTimeoutRef.current = null;
        connect();
      }, delay);
      reconnectDelayRef.current = Math.min(delay * 2, 30000);
    };
  }, [parseEvent, handleJobCompleted, handleJobFailed]);

  useEffect(() => {
    connect();

    return () => {
      if (eventSourceRef.current) {
        eventSourceRef.current.close();
        eventSourceRef.current = null;
      }
      if (reconnectTimeoutRef.current) {
        clearTimeout(reconnectTimeoutRef.current);
        reconnectTimeoutRef.current = null;
      }
    };
  }, [connect]);

  return null;
}
