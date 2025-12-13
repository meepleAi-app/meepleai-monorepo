/**
 * useMockStreaming Helper (Issue #1451)
 *
 * Mock streaming implementation using setTimeout to simulate word-by-word streaming.
 * Extracted from useChatStream for consolidation into unified useChatStreaming hook.
 *
 * Features:
 * - Simulated streaming with configurable typing speed
 * - Random mock responses in Italian
 * - Controlled cancellation and cleanup
 * - Compatible with unified streaming interface
 */

import { useState, useCallback, useRef, useEffect } from 'react';
import type { Citation } from '@/types';

/**
 * Mock streaming configuration
 */
const MOCK_STREAM_CONFIG = {
  wordsPerSecond: 15, // Simulated typing speed
  mockResponses: [
    'Ecco la risposta alla tua domanda. Secondo le regole del gioco, dovrai seguire questi passaggi per completare la mossa.',
    'Ottima domanda! Nel manuale, questa situazione è descritta nella sezione 3.2. Ti consiglio di consultare anche gli esempi a pagina 15.',
    'In questo scenario, devi considerare due fattori principali: la posizione dei tuoi pezzi e le carte azione disponibili.',
  ],
};

/**
 * Simulates word-by-word streaming
 */
function* wordStreamGenerator(text: string): Generator<string> {
  const words = text.split(' ');
  let accumulated = '';

  for (const word of words) {
    accumulated += (accumulated ? ' ' : '') + word;
    yield accumulated;
  }
}

/**
 * Mock streaming state
 */
export interface MockStreamingState {
  state: string | null;
  currentAnswer: string;
  snippets: Array<{ text: string; source: string; page?: number | null; line?: number | null }>;
  citations: Citation[];
  followUpQuestions: string[];
  totalTokens: number;
  confidence: number | null;
  isStreaming: boolean;
  error: string | null;
}

/**
 * Mock streaming controls
 */
export interface MockStreamingControls {
  startStreaming: (
    gameId: string,
    query: string,
    chatId?: string,
    searchMode?: string
  ) => Promise<void>;
  stopStreaming: () => void;
  reset: () => void;
}

/**
 * Mock streaming callbacks
 */
export interface MockStreamingCallbacks {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any -- Snippets type varies by stream source
  onComplete?: (
    answer: string,
    snippets: any[],
    metadata: {
      totalTokens: number;
      confidence: number | null;
      followUpQuestions?: string[];
      citations?: Citation[];
    }
  ) => void;
  onError?: (error: string) => void;
}

const INITIAL_STATE: MockStreamingState = {
  state: null,
  currentAnswer: '',
  snippets: [],
  citations: [],
  followUpQuestions: [],
  totalTokens: 0,
  confidence: null,
  isStreaming: false,
  error: null,
};

/**
 * Mock streaming implementation hook
 *
 * @param callbacks - Optional completion and error callbacks
 * @returns [state, controls] tuple
 *
 * @example
 * ```tsx
 * const [state, controls] = useMockStreaming({
 *   onComplete: (answer, snippets, metadata) => {
 *     console.log('Streaming complete:', answer);
 *   }
 * });
 *
 * controls.startStreaming(gameId, query);
 * ```
 */
export function useMockStreaming(
  callbacks?: MockStreamingCallbacks
): [MockStreamingState, MockStreamingControls] {
  const [state, setState] = useState<MockStreamingState>(INITIAL_STATE);
  const abortControllerRef = useRef<AbortController | null>(null);
  const streamTimeoutRef = useRef<NodeJS.Timeout | null>(null);

  /**
   * Stable ref to callbacks to prevent startStreaming recreation
   */
  const callbacksRef = useRef(callbacks);
  callbacksRef.current = callbacks;

  // Cleanup on unmount
  useEffect(() => {
    return () => {
      if (streamTimeoutRef.current) {
        clearTimeout(streamTimeoutRef.current);
      }
      if (abortControllerRef.current) {
        abortControllerRef.current.abort();
      }
    };
  }, []);

  const stopStreaming = useCallback(() => {
    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
      abortControllerRef.current = null;
    }
    if (streamTimeoutRef.current) {
      clearTimeout(streamTimeoutRef.current);
      streamTimeoutRef.current = null;
    }
    setState(prev => ({ ...prev, isStreaming: false }));
  }, []);

  const reset = useCallback(() => {
    stopStreaming();
    setState(INITIAL_STATE);
  }, [stopStreaming]);

  const startStreaming = useCallback(
    async (gameId: string, query: string, chatId?: string, searchMode: string = 'Hybrid') => {
      // Stop any existing stream
      stopStreaming();

      // Reset state
      setState({
        ...INITIAL_STATE,
        isStreaming: true,
        state: 'Generating mock response...',
      });

      // Create abort controller for cancellation
      abortControllerRef.current = new AbortController();

      try {
        // Simulate initial processing state
        setState(prev => ({ ...prev, state: 'Searching documents...' }));
        await new Promise(resolve => {
          streamTimeoutRef.current = setTimeout(resolve, 200);
        });

        if (abortControllerRef.current?.signal.aborted) {
          throw new Error('Stream cancelled by user');
        }

        // Update state to generation phase
        setState(prev => ({ ...prev, state: 'Generating answer...' }));

        // Select random mock response
        const mockResponse =
          MOCK_STREAM_CONFIG.mockResponses[
            Math.floor(Math.random() * MOCK_STREAM_CONFIG.mockResponses.length)
          ];

        const wordGenerator = wordStreamGenerator(mockResponse);
        const msPerWord = 1000 / MOCK_STREAM_CONFIG.wordsPerSecond;

        // Generate mock citations
        const mockCitations: Citation[] = [
          {
            documentId: `game-${gameId}`,
            pageNumber: 1,
            snippet: 'Example rule from page 1',
            relevanceScore: 0.95,
          },
        ];

        // Add citations during streaming
        setState(prev => ({
          ...prev,
          citations: mockCitations,
          snippets: mockCitations.map(c => ({
            text: c.snippet || '',
            source: c.documentId,
            page: c.pageNumber,
            line: null,
          })),
        }));

        // Simulate word-by-word streaming
        const chunks = Array.from(wordGenerator);
        for (const chunk of chunks) {
          // Check if streaming was cancelled
          if (abortControllerRef.current?.signal.aborted) {
            throw new Error('Stream cancelled by user');
          }

          // Update streamed content
          setState(prev => ({
            ...prev,
            currentAnswer: chunk,
          }));

          // Wait before next word
          await new Promise(resolve => {
            streamTimeoutRef.current = setTimeout(resolve, msPerWord);
          });
        }

        // Check cancellation one last time
        if (abortControllerRef.current?.signal.aborted) {
          throw new Error('Stream cancelled by user');
        }

        // Generate mock follow-up questions
        const mockFollowUpQuestions = [
          'Can you explain the scoring system?',
          'What happens in edge cases?',
        ];

        // Streaming complete
        const finalState = {
          currentAnswer: mockResponse,
          citations: mockCitations,
          snippets: mockCitations.map(c => ({
            text: c.snippet || '',
            source: c.documentId,
            page: c.pageNumber,
            line: null,
          })),
          followUpQuestions: mockFollowUpQuestions,
          totalTokens: mockResponse.split(' ').length,
          confidence: 0.85,
          isStreaming: false,
          state: null,
          error: null,
        };

        setState(finalState);

        // Call completion callback
        if (callbacksRef.current?.onComplete) {
          callbacksRef.current.onComplete(finalState.currentAnswer, finalState.snippets, {
            totalTokens: finalState.totalTokens,
            confidence: finalState.confidence,
            followUpQuestions: finalState.followUpQuestions,
            citations: finalState.citations,
          });
        }
      } catch (err) {
        // Don't treat abort as an error
        if (err instanceof Error && err.message === 'Stream cancelled by user') {
          setState(prev => ({
            ...prev,
            isStreaming: false,
            state: null,
          }));
          return;
        }

        const errorMessage = err instanceof Error ? err.message : 'Mock streaming failed';
        setState(prev => ({
          ...prev,
          error: errorMessage,
          isStreaming: false,
          state: null,
        }));

        if (callbacksRef.current?.onError) {
          callbacksRef.current.onError(errorMessage);
        }
      }
    },
    [stopStreaming]
  );

  return [
    state,
    {
      startStreaming,
      stopStreaming,
      reset,
    },
  ];
}
