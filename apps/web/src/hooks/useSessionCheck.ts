/**
 * useSessionCheck Hook (AUTH-05)
 *
 * Monitors session timeout status and triggers warning when near expiry.
 * Polls the backend every 5 minutes to check remaining session time.
 *
 * @returns {UseSessionCheckResult} Session status and checking state
 *
 * @example
 * ```tsx
 * const { remainingMinutes, isNearExpiry } = useSessionCheck();
 *
 * if (isNearExpiry) {
 *   return <SessionWarningModal remainingMinutes={remainingMinutes!} />;
 * }
 * ```
 */

import { useState, useEffect, useCallback } from 'react';
import { api } from '@/lib/api';

const POLL_INTERVAL_MS = 5 * 60 * 1000; // 5 minutes
const NEAR_EXPIRY_THRESHOLD_MINUTES = 5;

export interface UseSessionCheckResult {
  remainingMinutes: number | null;
  isNearExpiry: boolean;
  isChecking: boolean;
  error: Error | null;
  checkNow: () => Promise<void>;
}

export function useSessionCheck(): UseSessionCheckResult {
  const [remainingMinutes, setRemainingMinutes] = useState<number | null>(null);
  const [isNearExpiry, setIsNearExpiry] = useState(false);
  const [isChecking, setIsChecking] = useState(false);
  const [error, setError] = useState<Error | null>(null);

  const checkSessionStatus = useCallback(async () => {
    try {
      setIsChecking(true);
      setError(null);

      const status = await api.auth.getSessionStatus();

      if (status === null) {
        // User is not authenticated, skip
        setRemainingMinutes(null);
        setIsNearExpiry(false);
        return;
      }

      setRemainingMinutes(status.remainingMinutes);

      // Check if near expiry (< 5 minutes remaining)
      const nearExpiry = status.remainingMinutes < NEAR_EXPIRY_THRESHOLD_MINUTES;
      setIsNearExpiry(nearExpiry);

      // Auto-redirect to login when session expires
      if (status.remainingMinutes <= 0) {
        window.location.href = '/login?reason=session_expired';
      }
    } catch (err) {
      // Don't force logout on network errors - session might still be valid
      const errorObj = err instanceof Error ? err : new Error('Session check failed');
      setError(errorObj);
      console.error('Session check error:', errorObj);
    } finally {
      setIsChecking(false);
    }
  }, []);

  // Poll every 5 minutes
  useEffect(() => {
    // Initial check
    checkSessionStatus();

    // Setup polling
    const intervalId = setInterval(checkSessionStatus, POLL_INTERVAL_MS);

    // Cleanup on unmount
    return () => clearInterval(intervalId);
  }, [checkSessionStatus]);

  return {
    remainingMinutes,
    isNearExpiry,
    isChecking,
    error,
    checkNow: checkSessionStatus,
  };
}
