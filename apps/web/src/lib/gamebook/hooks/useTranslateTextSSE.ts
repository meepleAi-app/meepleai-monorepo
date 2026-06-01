'use client';

import { useCallback, useRef, useState } from 'react';

import type { SourceLangCode } from '@/lib/gamebook/lang-codes';

export interface TranslateTextState {
  partialText: string;
  isComplete: boolean;
  paragraphId?: string | null;
  appliedTerms: string[];
  error?: string;
  detectedSourceLang?: SourceLangCode | null;
  langDetectionConfidence?: number | null;
}

const initialState: TranslateTextState = { partialText: '', isComplete: false, appliedTerms: [] };
const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

export function useTranslateTextSSE() {
  const [state, setState] = useState<TranslateTextState>(initialState);
  const abortRef = useRef<AbortController | null>(null);

  const stop = useCallback(() => {
    abortRef.current?.abort();
    abortRef.current = null;
  }, []);

  const start = useCallback(
    async (
      campaignId: string,
      text: string,
      sourceLang: SourceLangCode,
      gameBookId: string,
      targetLang: SourceLangCode = 'IT'
    ) => {
      stop();
      setState(initialState);
      const ac = new AbortController();
      abortRef.current = ac;

      const url = `${API_BASE}/api/v1/gamebook/campaigns/${encodeURIComponent(campaignId)}/text/translate`;
      let response: Response;
      try {
        response = await fetch(url, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', Accept: 'text/event-stream' },
          body: JSON.stringify({ text, sourceLang, targetLang, gameBookId }),
          credentials: 'include',
          signal: ac.signal,
        });
      } catch {
        if (ac.signal.aborted) return;
        setState(s => ({ ...s, error: 'network_error' }));
        return;
      }

      if (!response.ok) {
        const errorCode =
          response.status === 400
            ? 'validation_error'
            : response.status === 401
              ? 'unauthorized'
              : response.status === 403
                ? 'forbidden'
                : response.status === 404
                  ? 'not_found'
                  : 'stream_error';
        setState(s => ({ ...s, error: errorCode }));
        return;
      }

      const reader = response.body?.getReader();
      if (!reader) {
        setState(s => ({ ...s, error: 'stream_error' }));
        return;
      }

      const decoder = new TextDecoder();
      let buffer = '';
      try {
        while (true) {
          const { done, value } = await reader.read();
          if (done) break;
          buffer += decoder.decode(value, { stream: true });
          const events = buffer.split('\n\n');
          buffer = events.pop() ?? '';
          for (const ev of events) {
            const line = ev.trim();
            if (!line.startsWith('data:')) continue;
            const json = line.slice(5).trim();
            try {
              const chunk = JSON.parse(json) as Partial<TranslateTextState & { delta: string }>;
              if (chunk.error) {
                setState(s => ({ ...s, error: chunk.error }));
                return;
              }
              setState(s => ({
                partialText: s.partialText + (chunk.delta ?? ''),
                isComplete: chunk.isComplete ?? false,
                paragraphId: chunk.paragraphId !== undefined ? chunk.paragraphId : s.paragraphId,
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
            } catch {
              // malformed JSON line — ignore
            }
          }
        }
      } catch {
        if (!ac.signal.aborted) setState(s => ({ ...s, error: 'stream_error' }));
      }
    },
    [stop]
  );

  return { ...state, start, stop };
}
