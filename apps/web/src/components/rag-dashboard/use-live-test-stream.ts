/**
 * useLiveTestStream Hook
 *
 * Connects to POST /api/v1/rag-dashboard/query/stream for admin live RAG testing.
 * Streams SSE events (stateUpdate, citations, token, complete, error) via fetch + ReadableStream.
 *
 * Issue #5109: SSE streaming for Live Testing in ExampleIOModal.
 */

import { useState, useCallback, useRef } from 'react';

import { getApiBase } from '@/lib/api';

// ── Types ────────────────────────────────────────────────────────────────────

export interface LiveTestCitation {
  text: string;
  source: string;
  page: number | null;
  score: number | null;
}

export interface LiveTestState {
  isStreaming: boolean;
  statusMessage: string | null;
  currentAnswer: string;
  citations: LiveTestCitation[];
  confidence: number | null;
  totalTokens: number | null;
  error: string | null;
  isComplete: boolean;
}

export interface LiveTestControls {
  startTest: (query: string, gameContext: string) => void;
  cancelTest: () => void;
  reset: () => void;
}

// ── Constants ────────────────────────────────────────────────────────────────

const INITIAL_STATE: LiveTestState = {
  isStreaming: false,
  statusMessage: null,
  currentAnswer: '',
  citations: [],
  confidence: null,
  totalTokens: null,
  error: null,
  isComplete: false,
};

// ── Hook ─────────────────────────────────────────────────────────────────────

export function useLiveTestStream(): [LiveTestState, LiveTestControls] {
  const [state, setState] = useState<LiveTestState>(INITIAL_STATE);
  const abortControllerRef = useRef<AbortController | null>(null);

  const cancelTest = useCallback(() => {
    abortControllerRef.current?.abort();
    abortControllerRef.current = null;
    setState(prev => ({ ...prev, isStreaming: false }));
  }, []);

  const reset = useCallback(() => {
    cancelTest();
    setState(INITIAL_STATE);
  }, [cancelTest]);

  const startTest = useCallback(
    (query: string, gameContext: string) => {
      // Cancel any existing stream
      cancelTest();

      // Reset to streaming state
      setState({ ...INITIAL_STATE, isStreaming: true, statusMessage: 'Starting...' });

      const abortController = new AbortController();
      abortControllerRef.current = abortController;

      const url = `${getApiBase()}/api/v1/rag-dashboard/query/stream`;

      fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ query, gameContext }),
        credentials: 'include',
        signal: abortController.signal,
      })
        .then(async response => {
          if (!response.ok) {
            const errorText = await response.text().catch(() => `HTTP ${response.status}`);
            let errorMessage = `HTTP ${response.status}`;
            try {
              const json = JSON.parse(errorText) as { error?: string };
              if (json.error) errorMessage = json.error;
            } catch {
              // Not JSON
            }
            setState(prev => ({
              ...prev,
              isStreaming: false,
              error: errorMessage,
            }));
            return;
          }

          const reader = response.body?.getReader();
          if (!reader) {
            setState(prev => ({ ...prev, isStreaming: false, error: 'No response body' }));
            return;
          }

          const decoder = new TextDecoder();
          let buffer = '';

          while (true) {
            const { done, value } = await reader.read();
            if (done) break;

            buffer += decoder.decode(value, { stream: true });

            // Split on SSE event boundaries (\n\n)
            const parts = buffer.split('\n\n');
            buffer = parts.pop() ?? '';

            for (const part of parts) {
              if (!part.trim()) continue;

              // Find the data: line
              const dataLine = part
                .split('\n')
                .find(l => l.startsWith('data:'));
              if (!dataLine) continue;

              const jsonStr = dataLine.slice(5).trim(); // Remove "data:"
              if (!jsonStr) continue;

              try {
                const event = JSON.parse(jsonStr) as {
                  type: string;
                  data: unknown;
                };
                handleEvent(event.type, event.data);
              } catch {
                // Ignore malformed events
              }
            }
          }

          // Stream finished without explicit complete event
          setState(prev => {
            if (prev.isStreaming) {
              return { ...prev, isStreaming: false, isComplete: true };
            }
            return prev;
          });
        })
        .catch(err => {
          if ((err as Error).name === 'AbortError') return;
          setState(prev => ({
            ...prev,
            isStreaming: false,
            error: (err as Error).message ?? 'Stream error',
          }));
        });

      function handleEvent(type: string, data: unknown) {
        switch (type) {
          case 'stateUpdate': {
            const d = data as { state?: string; message?: string } | null;
            const msg = d?.state ?? d?.message ?? null;
            if (msg) {
              setState(prev => ({ ...prev, statusMessage: msg }));
            }
            break;
          }

          case 'citations': {
            const d = data as { citations?: unknown[]; snippets?: unknown[] } | null;
            const raw = d?.citations ?? d?.snippets ?? [];
            const citations = raw.map(c => {
              const citation = c as Record<string, unknown>;
              return {
                text: (citation.text as string) ?? '',
                source: (citation.source as string) ?? '',
                page:
                  (citation.page as number | null) ??
                  (citation.pageNumber as number | null) ??
                  null,
                score:
                  (citation.score as number | null) ??
                  (citation.relevanceScore as number | null) ??
                  null,
              } satisfies LiveTestCitation;
            });
            setState(prev => ({ ...prev, citations }));
            break;
          }

          case 'token': {
            const d = data as { token?: string } | null;
            if (d?.token) {
              setState(prev => ({ ...prev, currentAnswer: prev.currentAnswer + d.token }));
            }
            break;
          }

          case 'complete': {
            const d = data as {
              confidence?: number | null;
              totalTokens?: number | null;
            } | null;
            setState(prev => ({
              ...prev,
              isStreaming: false,
              isComplete: true,
              statusMessage: null,
              confidence: d?.confidence ?? null,
              totalTokens: d?.totalTokens ?? null,
            }));
            break;
          }

          case 'error': {
            const d = data as {
              message?: string;
              errorMessage?: string;
              code?: string;
              errorCode?: string;
            } | null;
            const errMsg = d?.message ?? d?.errorMessage ?? 'Unknown error';
            setState(prev => ({
              ...prev,
              isStreaming: false,
              error: errMsg,
            }));
            break;
          }

          default:
            break;
        }
      }
    },
    [cancelTest],
  );

  return [state, { startTest, cancelTest, reset }];
}
