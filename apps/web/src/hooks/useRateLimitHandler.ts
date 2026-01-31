/**
 * React hook for managing rate limit countdown state and UI interactions
 *
 * Features:
 * - Automatic countdown timer from RateLimitError
 * - Auto-enables UI when countdown expires
 * - User-friendly message formatting
 * - Accessible state management
 *
 * @example
 * ```tsx
 * const { isRateLimited, remainingSeconds, message, handleError, reset } = useRateLimitHandler();
 *
 * try {
 *   await apiCall();
 * } catch (error) {
 *   if (error instanceof RateLimitError) {
 *     handleError(error);
 *   }
 * }
 *
 * <button disabled={isRateLimited}>{message || 'Submit'}</button>
 * ```
 */

import { useState, useEffect, useCallback, useRef } from 'react';

import { RateLimitError } from '@/lib/api/core/errors';

export interface UseRateLimitHandlerResult {
  /** Whether rate limit is currently active */
  isRateLimited: boolean;
  /** Seconds remaining until retry is allowed (0 if not rate limited) */
  remainingSeconds: number;
  /** User-friendly message describing the rate limit status */
  message: string | null;
  /** Call this when a RateLimitError occurs */
  handleError: (error: RateLimitError) => void;
  /** Reset the rate limit state manually */
  reset: () => void;
  /** The original RateLimitError if present */
  error: RateLimitError | null;
}

/**
 * Hook for handling rate limit errors with countdown timer
 */
export function useRateLimitHandler(): UseRateLimitHandlerResult {
  const [error, setError] = useState<RateLimitError | null>(null);
  const [remainingSeconds, setRemainingSeconds] = useState(0);
  const timerRef = useRef<NodeJS.Timeout | null>(null);

  // Cleanup timer on unmount
  useEffect(() => {
    return () => {
      if (timerRef.current) {
        clearInterval(timerRef.current);
      }
    };
  }, []);

  // Handle rate limit error and start countdown
  const handleError = useCallback((rateLimitError: RateLimitError) => {
    setError(rateLimitError);
    setRemainingSeconds(rateLimitError.getRetryAfterSeconds());

    // Clear any existing timer
    if (timerRef.current) {
      clearInterval(timerRef.current);
    }

    // Start countdown timer
    timerRef.current = setInterval(() => {
      setRemainingSeconds(prev => {
        if (prev <= 1) {
          // Timer expired
          if (timerRef.current) {
            clearInterval(timerRef.current);
            timerRef.current = null;
          }
          setError(null);
          return 0;
        }
        return prev - 1;
      });
    }, 1000);
  }, []);

  // Reset rate limit state
  const reset = useCallback(() => {
    if (timerRef.current) {
      clearInterval(timerRef.current);
      timerRef.current = null;
    }
    setError(null);
    setRemainingSeconds(0);
  }, []);

  // Generate user-friendly message
  const message = error ? error.getUserFriendlyMessage(remainingSeconds) : null;

  return {
    isRateLimited: remainingSeconds > 0,
    remainingSeconds,
    message,
    handleError,
    reset,
    error,
  };
}
