/**
 * useChatStreaming Hook - Unified Streaming Implementation (Issue #1451)
 *
 * Consolidated streaming hook that supports both real SSE and mock modes.
 * Extracted logic into separate helpers for better maintainability.
 *
 * Features:
 * - Real SSE streaming via useRealStreaming
 * - Mock streaming simulation via useMockStreaming
 * - Environment variable mode selection (NEXT_PUBLIC_USE_MOCK_STREAMING)
 * - Identical interface for both modes
 * - Zero breaking changes for existing consumers
 *
 * Migration from useChatStream:
 * - Use useChatStreaming with useMock=true or set NEXT_PUBLIC_USE_MOCK_STREAMING=true
 * - Old useChatStream is deprecated and will be removed in Sprint 5
 */

import { Citation } from '@/types';
import { useRealStreaming, type RealStreamingState, type RealStreamingControls, type RealStreamingCallbacks } from './useRealStreaming';
import { useMockStreaming, type MockStreamingState, type MockStreamingControls, type MockStreamingCallbacks } from './useMockStreaming';

// Re-export types for compatibility
export type StreamingState = RealStreamingState | MockStreamingState;
export type StreamingControls = RealStreamingControls | MockStreamingControls;

type Snippet = {
  text: string;
  source: string;
  page?: number | null;
  line?: number | null;
};

/**
 * Streaming callbacks (unified interface)
 */
export interface StreamingCallbacks {
  onComplete?: (answer: string, snippets: Snippet[], metadata: { totalTokens: number; confidence: number | null; followUpQuestions?: string[]; citations?: Citation[] }) => void;
  onError?: (error: string) => void;
}

/**
 * Hook options
 */
export interface UseChatStreamingOptions extends StreamingCallbacks {
  /**
   * Use mock streaming mode instead of real SSE
   * @default false (reads from NEXT_PUBLIC_USE_MOCK_STREAMING if not specified)
   */
  useMock?: boolean;
}

/**
 * Unified React hook for streaming QA responses
 *
 * Supports both real SSE streaming and mock streaming simulation.
 * Mode selection via `useMock` prop or `NEXT_PUBLIC_USE_MOCK_STREAMING` environment variable.
 *
 * @param options - Configuration and callbacks
 * @param options.useMock - Force mock mode (default: reads from env NEXT_PUBLIC_USE_MOCK_STREAMING)
 * @param options.onComplete - Callback when streaming completes
 * @param options.onError - Callback when streaming errors occur
 *
 * @returns [state, controls] tuple
 *
 * @example Real SSE mode (default)
 * ```tsx
 * const [streamingState, streamingControls] = useChatStreaming({
 *   onComplete: (answer, snippets, metadata) => {
 *     console.log('Streaming complete:', answer);
 *   },
 *   onError: (error) => {
 *     console.error('Streaming error:', error);
 *   }
 * });
 *
 * streamingControls.startStreaming(gameId, userQuery, chatId, searchMode);
 * ```
 *
 * @example Mock mode (for development/testing)
 * ```tsx
 * const [streamingState, streamingControls] = useChatStreaming({
 *   useMock: true,
 *   onComplete: (answer) => {
 *     console.log('Mock streaming complete:', answer);
 *   }
 * });
 *
 * streamingControls.startStreaming(gameId, userQuery);
 * ```
 *
 * @example Environment-based mode selection
 * ```bash
 * # .env.local
 * NEXT_PUBLIC_USE_MOCK_STREAMING=true
 * ```
 */
export function useChatStreaming(
  options?: UseChatStreamingOptions
): [StreamingState, StreamingControls] {
  // Determine mode: explicit prop > environment variable > default (real)
  const useMock = options?.useMock ?? (process.env.NEXT_PUBLIC_USE_MOCK_STREAMING === 'true');

  // Extract callbacks
  const callbacks: RealStreamingCallbacks | MockStreamingCallbacks | undefined = options ? {
    onComplete: options.onComplete,
    onError: options.onError,
  } : undefined;

  // Use appropriate implementation based on mode
  const [realState, realControls] = useRealStreaming(callbacks);
  const [mockState, mockControls] = useMockStreaming(callbacks);

  // Return the selected implementation
  if (useMock) {
    return [mockState, mockControls];
  }

  return [realState, realControls];
}
