/**
 * useFeedback Hook - AI Response Feedback Management
 *
 * Manages feedback state and submission for AI messages.
 * Integrates with agents API for persistence.
 *
 * @issue #3352 (AI Response Feedback System)
 */

import { useState, useCallback, useMemo } from 'react';

import type { FeedbackValue } from '@/components/ui/meeple/feedback-buttons';
import { api } from '@/lib/api';

// ============================================================================
// Types
// ============================================================================

export interface FeedbackState {
  value: FeedbackValue;
  comment?: string;
  isLoading: boolean;
  error?: string;
}

export interface UseFeedbackOptions {
  /** User ID for feedback attribution */
  userId: string;
  /** Game ID context (optional) */
  gameId?: string;
  /** Endpoint that generated the response (e.g., 'qa', 'explain') */
  endpoint: string;
  /** Initial feedback values by message ID */
  initialFeedback?: Record<string, FeedbackValue>;
  /** Callback when feedback is successfully submitted */
  onFeedbackSubmitted?: (messageId: string, feedback: FeedbackValue, comment?: string) => void;
  /** Callback when feedback submission fails */
  onFeedbackError?: (messageId: string, error: Error) => void;
}

export interface UseFeedbackReturn {
  /** Get feedback state for a message */
  getFeedback: (messageId: string) => FeedbackState;
  /** Submit feedback for a message */
  submitFeedback: (messageId: string, feedback: FeedbackValue, comment?: string) => Promise<void>;
  /** Get handler function for a specific message (memoized) */
  createFeedbackHandler: (messageId: string) => (feedback: FeedbackValue, comment?: string) => Promise<void>;
  /** Check if any feedback is currently being submitted */
  isAnyLoading: boolean;
  /** All feedback states */
  feedbackStates: Record<string, FeedbackState>;
}

// ============================================================================
// Hook Implementation
// ============================================================================

/**
 * Custom hook for managing AI response feedback
 *
 * @example
 * ```tsx
 * const { getFeedback, createFeedbackHandler } = useFeedback({
 *   userId: session.user.id,
 *   gameId: currentGame.id,
 *   endpoint: 'qa',
 * });
 *
 * return (
 *   <ChatMessage
 *     {...messageProps}
 *     feedback={getFeedback(message.id).value}
 *     onFeedbackChange={createFeedbackHandler(message.id)}
 *     isFeedbackLoading={getFeedback(message.id).isLoading}
 *   />
 * );
 * ```
 */
export function useFeedback({
  userId,
  gameId,
  endpoint,
  initialFeedback = {},
  onFeedbackSubmitted,
  onFeedbackError,
}: UseFeedbackOptions): UseFeedbackReturn {
  // State for all message feedback
  const [feedbackStates, setFeedbackStates] = useState<Record<string, FeedbackState>>(() => {
    const initial: Record<string, FeedbackState> = {};
    for (const [messageId, value] of Object.entries(initialFeedback)) {
      // eslint-disable-next-line security/detect-object-injection
      initial[messageId] = { value, isLoading: false };
    }
    return initial;
  });

  // Get feedback state for a message
  const getFeedback = useCallback(
    (messageId: string): FeedbackState => {
      // eslint-disable-next-line security/detect-object-injection
      return feedbackStates[messageId] ?? { value: null, isLoading: false };
    },
    [feedbackStates]
  );

  // Submit feedback for a message
  const submitFeedback = useCallback(
    async (messageId: string, feedback: FeedbackValue, comment?: string) => {
      // Set loading state
      setFeedbackStates(prev => ({
        ...prev,
        [messageId]: {
          // eslint-disable-next-line security/detect-object-injection
          ...prev[messageId],
          // eslint-disable-next-line security/detect-object-injection
          value: prev[messageId]?.value ?? null,
          isLoading: true,
          error: undefined,
        },
      }));

      try {
        await api.agents.submitFeedback({
          messageId,
          endpoint,
          userId,
          outcome: feedback,
          gameId,
          comment,
        });

        // Update state with new feedback
        setFeedbackStates(prev => ({
          ...prev,
          [messageId]: {
            value: feedback,
            comment,
            isLoading: false,
            error: undefined,
          },
        }));

        onFeedbackSubmitted?.(messageId, feedback, comment);
      } catch (error) {
        const errorMessage = error instanceof Error ? error.message : 'Failed to submit feedback';

        setFeedbackStates(prev => ({
          ...prev,
          [messageId]: {
            // eslint-disable-next-line security/detect-object-injection
            ...prev[messageId],
            // eslint-disable-next-line security/detect-object-injection
            value: prev[messageId]?.value ?? null,
            isLoading: false,
            error: errorMessage,
          },
        }));

        onFeedbackError?.(messageId, error instanceof Error ? error : new Error(errorMessage));
        throw error;
      }
    },
    [userId, gameId, endpoint, onFeedbackSubmitted, onFeedbackError]
  );

  // Create memoized handler for a specific message
  const createFeedbackHandler = useCallback(
    (messageId: string) => {
      return async (feedback: FeedbackValue, comment?: string) => {
        await submitFeedback(messageId, feedback, comment);
      };
    },
    [submitFeedback]
  );

  // Check if any feedback is loading
  const isAnyLoading = useMemo(
    () => Object.values(feedbackStates).some(state => state.isLoading),
    [feedbackStates]
  );

  return {
    getFeedback,
    submitFeedback,
    createFeedbackHandler,
    isAnyLoading,
    feedbackStates,
  };
}

// ============================================================================
// Simplified Hook for Single Message
// ============================================================================

export interface UseSingleFeedbackOptions {
  /** Message ID */
  messageId: string;
  /** User ID for feedback attribution */
  userId: string;
  /** Game ID context (optional) */
  gameId?: string;
  /** Endpoint that generated the response */
  endpoint: string;
  /** Initial feedback value */
  initialValue?: FeedbackValue;
}

/**
 * Simplified hook for single message feedback
 *
 * @example
 * ```tsx
 * const { feedback, isLoading, handleFeedback } = useSingleFeedback({
 *   messageId: message.id,
 *   userId: session.user.id,
 *   endpoint: 'qa',
 * });
 *
 * return (
 *   <FeedbackButtons
 *     value={feedback}
 *     onFeedbackChange={handleFeedback}
 *     isLoading={isLoading}
 *   />
 * );
 * ```
 */
export function useSingleFeedback({
  messageId,
  userId,
  gameId,
  endpoint,
  initialValue = null,
}: UseSingleFeedbackOptions) {
  const [feedback, setFeedback] = useState<FeedbackValue>(initialValue);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string>();

  const handleFeedback = useCallback(
    async (newFeedback: FeedbackValue, comment?: string) => {
      setIsLoading(true);
      setError(undefined);

      try {
        await api.agents.submitFeedback({
          messageId,
          endpoint,
          userId,
          outcome: newFeedback,
          gameId,
          comment,
        });

        setFeedback(newFeedback);
      } catch (err) {
        const errorMessage = err instanceof Error ? err.message : 'Failed to submit feedback';
        setError(errorMessage);
        throw err;
      } finally {
        setIsLoading(false);
      }
    },
    [messageId, userId, gameId, endpoint]
  );

  return {
    feedback,
    isLoading,
    error,
    handleFeedback,
  };
}

export default useFeedback;
