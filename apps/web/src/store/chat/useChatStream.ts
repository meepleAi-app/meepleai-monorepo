/**
 * useChatStream Hook (Issue #1083)
 *
 * SSE streaming hook for real-time chat responses.
 * Currently uses functional mock with setTimeout.
 * Will be enhanced in Phase 4 with real SSE integration.
 *
 * Features:
 * - Simulated streaming with word-by-word delivery
 * - Optimistic updates during streaming
 * - Error handling and cancellation
 * - Integration with Zustand store
 */

import { useState, useCallback, useRef, useEffect } from 'react';
import { useChatStore } from './store';
import { Message } from '@/types';

export interface StreamState {
  isStreaming: boolean;
  streamedContent: string;
  error: string | null;
}

export interface StreamControls {
  startStream: (userMessage: string) => Promise<void>;
  stopStream: () => void;
}

/**
 * Mock streaming configuration
 */
const MOCK_STREAM_CONFIG = {
  wordsPerSecond: 15, // Simulated typing speed
  mockResponses: [
    "Ecco la risposta alla tua domanda. Secondo le regole del gioco, dovrai seguire questi passaggi per completare la mossa.",
    "Ottima domanda! Nel manuale, questa situazione è descritta nella sezione 3.2. Ti consiglio di consultare anche gli esempi a pagina 15.",
    "In questo scenario, devi considerare due fattori principali: la posizione dei tuoi pezzi e le carte azione disponibili.",
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
 * Hook for streaming chat responses
 *
 * @param chatId - Active chat thread ID
 * @returns Stream state and controls
 *
 * @example
 * const { isStreaming, streamedContent, startStream, stopStream } = useChatStream(activeChatId);
 *
 * // Start streaming
 * await startStream("What are the setup rules?");
 *
 * // Stop streaming
 * stopStream();
 */
export function useChatStream(chatId: string | null): StreamState & StreamControls {
  const [isStreaming, setIsStreaming] = useState(false);
  const [streamedContent, setStreamedContent] = useState('');
  const [error, setError] = useState<string | null>(null);

  const abortControllerRef = useRef<AbortController | null>(null);
  const streamTimeoutRef = useRef<NodeJS.Timeout | null>(null);

  const { addOptimisticMessage, updateMessageInThread, removeOptimisticMessage } = useChatStore();

  // Cleanup on unmount or chatId change
  useEffect(() => {
    return () => {
      if (streamTimeoutRef.current) {
        clearTimeout(streamTimeoutRef.current);
      }
      if (abortControllerRef.current) {
        abortControllerRef.current.abort();
      }
    };
  }, [chatId]);

  const startStream = useCallback(
    async (userMessage: string) => {
      if (!chatId) {
        setError('No active chat selected');
        return;
      }

      // Reset state
      setIsStreaming(true);
      setStreamedContent('');
      setError(null);

      // Create abort controller for cancellation
      abortControllerRef.current = new AbortController();

      // Add optimistic assistant message
      const tempAssistantId = `temp-assistant-${Date.now()}`;
      const assistantMessage: Message = {
        id: tempAssistantId,
        role: 'assistant',
        content: '',
        timestamp: new Date(),
        isOptimistic: true,
      };

      addOptimisticMessage(assistantMessage, chatId);

      try {
        // TODO (Phase 4): Replace with real SSE endpoint
        // const response = await fetch('/api/v1/chat/stream', {
        //   method: 'POST',
        //   headers: { 'Content-Type': 'application/json' },
        //   body: JSON.stringify({ chatId, message: userMessage }),
        //   signal: abortControllerRef.current.signal,
        // });

        // MOCK: Simulate streaming response
        const mockResponse =
          MOCK_STREAM_CONFIG.mockResponses[
            Math.floor(Math.random() * MOCK_STREAM_CONFIG.mockResponses.length)
          ];

        const wordGenerator = wordStreamGenerator(mockResponse);
        const msPerWord = 1000 / MOCK_STREAM_CONFIG.wordsPerSecond;

        // Simulate word-by-word streaming
        // Convert generator to array for TypeScript compatibility
        const chunks = Array.from(wordGenerator);
        for (const chunk of chunks) {
          // Check if streaming was cancelled
          if (abortControllerRef.current?.signal.aborted) {
            throw new Error('Stream cancelled by user');
          }

          // Update streamed content
          setStreamedContent(chunk);

          // Update optimistic message in store
          updateMessageInThread(chatId, tempAssistantId, {
            content: chunk,
          });

          // Wait before next word
          await new Promise((resolve) => {
            streamTimeoutRef.current = setTimeout(resolve, msPerWord);
          });
        }

        // Streaming complete - remove optimistic flag
        updateMessageInThread(chatId, tempAssistantId, {
          isOptimistic: false,
        });

        setIsStreaming(false);
      } catch (err) {
        console.error('Streaming error:', err);

        // Remove optimistic message on error
        removeOptimisticMessage(tempAssistantId, chatId);

        if (err instanceof Error && err.message !== 'Stream cancelled by user') {
          setError('Errore durante lo streaming della risposta');
        }

        setIsStreaming(false);
        setStreamedContent('');
      }
    },
    [chatId, addOptimisticMessage, updateMessageInThread, removeOptimisticMessage]
  );

  const stopStream = useCallback(() => {
    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
    }
    if (streamTimeoutRef.current) {
      clearTimeout(streamTimeoutRef.current);
    }
    setIsStreaming(false);
  }, []);

  return {
    isStreaming,
    streamedContent,
    error,
    startStream,
    stopStream,
  };
}

/**
 * TODO (Phase 4): Real SSE Integration
 *
 * Replace mock implementation with:
 *
 * 1. EventSource for SSE connection
 * 2. Real backend endpoint: POST /api/v1/chat/stream
 * 3. Message chunking and reassembly
 * 4. Citation parsing from streamed data
 * 5. Follow-up question extraction
 * 6. Proper error handling for network issues
 * 7. Reconnection logic
 * 8. Progress indicators
 *
 * Example real implementation:
 *
 * const eventSource = new EventSource(`/api/v1/chat/stream?chatId=${chatId}`);
 *
 * eventSource.onmessage = (event) => {
 *   const data = JSON.parse(event.data);
 *   if (data.type === 'content') {
 *     setStreamedContent(prev => prev + data.chunk);
 *   } else if (data.type === 'citation') {
 *     // Handle citation
 *   } else if (data.type === 'done') {
 *     eventSource.close();
 *     setIsStreaming(false);
 *   }
 * };
 *
 * eventSource.onerror = (err) => {
 *   console.error('SSE error:', err);
 *   setError('Connection lost');
 *   eventSource.close();
 * };
 */
