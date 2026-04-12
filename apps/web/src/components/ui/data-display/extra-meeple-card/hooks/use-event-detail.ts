'use client';

import React from 'react';

import type { EventDetailData } from '../types';

// ============================================================================
// Data Fetching Hook — Event
// ============================================================================

interface UseEventDetailResult {
  data: EventDetailData | null;
  loading: boolean;
  error: string | null;
  retry: () => void;
}

export function useEventDetail(eventId: string): UseEventDetailResult {
  const [data, setData] = React.useState<EventDetailData | null>(null);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);

  const fetchData = React.useCallback(
    async (signal: AbortSignal) => {
      setLoading(true);
      setError(null);
      try {
        const res = await fetch(`/api/v1/events/${eventId}`, { signal });
        if (!res.ok) throw new Error(`Errore ${res.status}: evento non trovato`);
        const json = (await res.json()) as Record<string, unknown>;
        setData(mapToEventDetailData(json));
      } catch (err) {
        if (err instanceof Error && err.name === 'AbortError') return;
        setError(err instanceof Error ? err.message : "Impossibile caricare i dati dell'evento");
      } finally {
        setLoading(false);
      }
    },
    [eventId]
  );

  React.useEffect(() => {
    if (!eventId) return;
    const controller = new AbortController();
    void fetchData(controller.signal);
    return () => {
      controller.abort();
    };
  }, [eventId, fetchData]);

  const retry = React.useCallback(() => {
    const controller = new AbortController();
    void fetchData(controller.signal);
  }, [fetchData]);

  return { data, loading, error, retry };
}

function mapToEventDetailData(json: Record<string, unknown>): EventDetailData {
  const rawSchedule = Array.isArray(json.schedule) ? json.schedule : [];
  const schedule = rawSchedule.map((item: unknown) => {
    const s = item as Record<string, unknown>;
    return {
      id: String(s.id ?? ''),
      title: String(s.title ?? ''),
      scheduledAt: String(s.scheduledAt ?? ''),
      gameName: s.gameName != null ? String(s.gameName) : undefined,
    };
  });

  return {
    id: String(json.id ?? ''),
    title: String(json.title ?? 'Evento'),
    description: json.description != null ? String(json.description) : undefined,
    imageUrl: json.imageUrl != null ? String(json.imageUrl) : undefined,
    startDate: json.startDate != null ? String(json.startDate) : undefined,
    endDate: json.endDate != null ? String(json.endDate) : undefined,
    location: json.location != null ? String(json.location) : undefined,
    isOnline: Boolean(json.isOnline ?? false),
    organizerName: json.organizerName != null ? String(json.organizerName) : undefined,
    isOrganizer: Boolean(json.isOrganizer ?? false),
    rsvpStatus:
      json.rsvpStatus === 'confirmed' ||
      json.rsvpStatus === 'pending' ||
      json.rsvpStatus === 'declined'
        ? json.rsvpStatus
        : null,
    attendeeCount: typeof json.attendeeCount === 'number' ? json.attendeeCount : 0,
    maxAttendees: typeof json.maxAttendees === 'number' ? json.maxAttendees : undefined,
    schedule,
  };
}
