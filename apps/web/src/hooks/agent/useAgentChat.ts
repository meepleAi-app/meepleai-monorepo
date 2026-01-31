/**
 * useAgentChat - Simplified SSE chat hook (Issue #3243)
 *
 * Wrapper around useStreamingChat for agent chat components.
 * Manages messages array with user + agent messages and progressive text reveal.
 *
 * Features:
 * - User message management
 * - SSE streaming integration
 * - Progressive text reveal (chunk-by-chunk)
 * - Confidence and citations tracking
 *
 * @example
 * ```tsx
 * const { messages, isStreaming, currentChunk, sendMessage } = useAgentChat(sessionId);
 * await sendMessage('What are the rules for setup?');
 * ```
 */

import { useState, useCallback, useMemo } from 'react';

import type { Citation } from '@/lib/api/schemas/streaming.schemas';
import { useStreamingChat } from '@/lib/hooks/useStreamingChat';
import type { AgentMessage } from '@/types/agent';

export interface UseAgentChatResult {
  /** All messages (user + agent) */
  messages: AgentMessage[];

  /** Whether SSE streaming is active */
  isStreaming: boolean;

  /** Current streaming chunk (accumulated text) */
  currentChunk: string;

  /** Send message and start streaming */
  sendMessage: (query: string) => Promise<void>;

  /** Stop streaming */
  stopStreaming: () => void;

  /** Reset chat */
  reset: () => void;
}

/**
 * useAgentChat Hook
 *
 * @param sessionId - Agent session ID (optional)
 * @param gameId - Game ID for context (optional)
 * @returns Chat state and controls
 */
export function useAgentChat(sessionId?: string, gameId?: string): UseAgentChatResult {
  const [messages, setMessages] = useState<AgentMessage[]>([]);
  const [currentChunk, setCurrentChunk] = useState('');

  // SSE streaming hook (Issue #1007)
  const [streamingState, streamingControls] = useStreamingChat({
    onToken: useCallback((_token: string, accumulated: string) => {
      // Update current chunk for progressive reveal
      setCurrentChunk(accumulated);
    }, []),
    onStateUpdate: useCallback((_state: string) => {
      // Could show state in UI (e.g., "Searching vector database...")
    }, []),
    onComplete: useCallback(
      (answer: string, citations: Citation[], confidence: number | null) => {
        // Add final agent message
        const agentMessage: AgentMessage = {
          type: 'agent',
          content: answer,
          citations,
          timestamp: new Date(),
        };

        setMessages(prev => [...prev, agentMessage]);
        setCurrentChunk(''); // Clear chunk after completion

        // Log confidence for debugging
        if (confidence !== null) {
          console.log('[useAgentChat] Answer confidence:', confidence);
        }
      },
      []
    ),
    onError: useCallback((error: Error) => {
      console.error('[useAgentChat] Streaming error:', error);

      // Add error message
      const errorMessage: AgentMessage = {
        type: 'system',
        content: `Errore: ${error.message}`,
        timestamp: new Date(),
      };

      setMessages(prev => [...prev, errorMessage]);
      setCurrentChunk(''); // Clear chunk on error
    }, []),
  });

  /**
   * Send message and start streaming
   */
  const sendMessage = useCallback(
    async (query: string) => {
      if (!query.trim()) return;

      // Add user message immediately
      const userMessage: AgentMessage = {
        type: 'user',
        content: query.trim(),
        timestamp: new Date(),
      };

      setMessages(prev => [...prev, userMessage]);

      // Start SSE streaming
      if (gameId) {
        await streamingControls.startStreaming(
          gameId,
          query.trim(),
          sessionId || undefined
        );
      } else {
        console.warn('[useAgentChat] No gameId provided, streaming skipped');
      }
    },
    [gameId, sessionId, streamingControls]
  );

  /**
   * Stop streaming
   */
  const stopStreaming = useCallback(() => {
    streamingControls.stopStreaming();
    setCurrentChunk(''); // Clear chunk when stopped
  }, [streamingControls]);

  /**
   * Reset chat
   */
  const reset = useCallback(() => {
    setMessages([]);
    setCurrentChunk('');
    streamingControls.reset();
  }, [streamingControls]);

  return useMemo(
    () => ({
      messages,
      isStreaming: streamingState.isStreaming,
      currentChunk,
      sendMessage,
      stopStreaming,
      reset,
    }),
    [messages, streamingState.isStreaming, currentChunk, sendMessage, stopStreaming, reset]
  );
}
