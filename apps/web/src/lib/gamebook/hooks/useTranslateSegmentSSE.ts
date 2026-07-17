'use client';

import { useCallback, useEffect, useRef, useState } from 'react';

import type { SourceLangCode } from '@/lib/gamebook/lang-codes';

export interface TranslateState {
  partialText: string;
  isComplete: boolean;
  paragraphId?: string;
  appliedTerms: string[];
  error?: string;
  /**
   * Source language detected by BE NTextCat (PR #1787 DEC-3 BE).
   * `null` if out-of-allowlist or detection failed.
   * `undefined` if BE didn't emit (legacy backward compat).
   * Only populated on final SSE chunk per BE DEC-10.
   */
  detectedSourceLang?: SourceLangCode | null;
  /**
   * Detection confidence in [0,1] (BE tanh-normalized, raw).
   * `null` if detection failed; `undefined` if BE didn't emit.
   * FE classifies via `getLangTier()` (DEC-FE-2).
   */
  langDetectionConfidence?: number | null;
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

  // #3101: close the EventSource on unmount to prevent a dangling connection and
  // setState-after-unmount when the user navigates away mid-translate (browser
  // back, route change). Mirrors the sibling useTranslateTextSSE (#1560).
  useEffect(() => {
    return () => {
      sourceRef.current?.close();
    };
  }, []);

  const start = useCallback(
    (
      campaignId: string,
      photoId: string,
      paragraphNumber: number,
      gameBookId: string,
      sourceLangOverride?: SourceLangCode
    ) => {
      stop();
      setState(initialState);
      // C2 (multi-book generalization): `gameBookId` is required so the BE can
      // scope per-book progress correctly. Callers must derive it from
      // BookPicker / single-book auto-select.
      let url =
        `${API_BASE}/api/v1/gamebook/campaigns/${encodeURIComponent(campaignId)}/photos/translate` +
        `?photoId=${encodeURIComponent(photoId)}` +
        `&paragraphNumber=${paragraphNumber}` +
        `&gameBookId=${encodeURIComponent(gameBookId)}`;
      if (sourceLangOverride) {
        url += `&sourceLangOverride=${encodeURIComponent(sourceLangOverride)}`;
      }
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
            detectedSourceLang?: SourceLangCode | null;
            langDetectionConfidence?: number | null;
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
            detectedSourceLang:
              chunk.detectedSourceLang !== undefined
                ? chunk.detectedSourceLang
                : s.detectedSourceLang,
            langDetectionConfidence:
              chunk.langDetectionConfidence !== undefined
                ? chunk.langDetectionConfidence
                : s.langDetectionConfidence,
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
