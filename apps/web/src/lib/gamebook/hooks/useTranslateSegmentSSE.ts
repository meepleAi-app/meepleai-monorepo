'use client';

import { useCallback, useRef, useState } from 'react';

export interface TranslateState {
  partialText: string;
  isComplete: boolean;
  paragraphId?: string;
  appliedTerms: string[];
  error?: string;
}

const initialState: TranslateState = { partialText: '', isComplete: false, appliedTerms: [] };

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

export function useTranslateSegmentSSE() {
  const [state, setState] = useState<TranslateState>(initialState);
  const sourceRef = useRef<EventSource | null>(null);

  const stop = useCallback(() => {
    sourceRef.current?.close();
    sourceRef.current = null;
  }, []);

  const start = useCallback(
    (campaignId: string, photoId: string, paragraphNumber: number, gameBookId: string) => {
      stop();
      setState(initialState);
      // C2 (multi-book generalization): `gameBookId` is required so the BE can
      // scope per-book progress correctly. Callers must derive it from
      // BookPicker / single-book auto-select.
      const url =
        `${API_BASE}/api/v1/gamebook/campaigns/${encodeURIComponent(campaignId)}/photos/translate` +
        `?photoId=${encodeURIComponent(photoId)}` +
        `&paragraphNumber=${paragraphNumber}` +
        `&gameBookId=${encodeURIComponent(gameBookId)}`;
      const es = new EventSource(url, { withCredentials: true });
      sourceRef.current = es;

      es.onmessage = (ev: MessageEvent<string>) => {
        try {
          const chunk = JSON.parse(ev.data) as {
            delta?: string;
            isComplete?: boolean;
            paragraphId?: string;
            appliedTerms?: string[];
            error?: string;
          };
          if (chunk.error) {
            setState(s => ({ ...s, error: chunk.error }));
            es.close();
            return;
          }
          setState(s => ({
            partialText: s.partialText + (chunk.delta ?? ''),
            isComplete: chunk.isComplete ?? false,
            paragraphId: chunk.paragraphId ?? s.paragraphId,
            appliedTerms: chunk.appliedTerms ?? s.appliedTerms,
          }));
          if (chunk.isComplete) es.close();
        } catch {
          // malformed JSON — ignore
        }
      };

      es.onerror = () => {
        setState(s => ({ ...s, error: s.error ?? 'stream_error' }));
        es.close();
      };
    },
    [stop]
  );

  return { ...state, start, stop };
}
