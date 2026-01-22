import { useMemo } from 'react';
import { useRateLimitStatus, useShareRequests } from './useShareRequests';

/**
 * Hook to check if a game can be shared
 * Issue #2743: Frontend - UI Condivisione da Libreria
 */

export interface CanShareGameResult {
  /** Whether the game can be shared */
  canShare: boolean;
  /** Reason why the game cannot be shared (null if can share) */
  reason: string | null;
  /** Whether the check is still loading */
  isLoading: boolean;
}

export function useCanShareGame(gameId: string): CanShareGameResult {
  const { data: rateLimitStatus, isLoading: isLoadingRateLimit } = useRateLimitStatus();
  const { data: shareRequests, isLoading: isLoadingRequests } = useShareRequests({
    status: 'Pending',
  });

  const result = useMemo<CanShareGameResult>(() => {
    // Still loading
    if (isLoadingRateLimit || isLoadingRequests) {
      return { canShare: false, reason: 'Checking availability...', isLoading: true };
    }

    // Check for pending request for this specific game
    const hasPendingRequest = shareRequests?.items.some(
      (request) => request.sourceGameId === gameId && request.status === 'Pending'
    );

    if (hasPendingRequest) {
      return {
        canShare: false,
        reason: 'You already have a pending share request for this game',
        isLoading: false,
      };
    }

    // Check if in cooldown
    if (rateLimitStatus?.isInCooldown) {
      const cooldownDate = rateLimitStatus.cooldownEndsAt
        ? new Date(rateLimitStatus.cooldownEndsAt).toLocaleString()
        : 'soon';
      return {
        canShare: false,
        reason: `You are in cooldown. Try again after ${cooldownDate}`,
        isLoading: false,
      };
    }

    // Check pending count limit
    if (
      rateLimitStatus &&
      rateLimitStatus.currentPendingCount >= rateLimitStatus.maxPendingAllowed
    ) {
      return {
        canShare: false,
        reason: `You have reached the maximum of ${rateLimitStatus.maxPendingAllowed} pending requests`,
        isLoading: false,
      };
    }

    // Check monthly limit
    if (
      rateLimitStatus &&
      rateLimitStatus.currentMonthlyCount >= rateLimitStatus.maxMonthlyAllowed
    ) {
      const resetDate = new Date(rateLimitStatus.monthResetAt).toLocaleDateString();
      return {
        canShare: false,
        reason: `Monthly limit reached (${rateLimitStatus.maxMonthlyAllowed}). Resets on ${resetDate}`,
        isLoading: false,
      };
    }

    // All checks passed
    return { canShare: true, reason: null, isLoading: false };
  }, [rateLimitStatus, shareRequests, gameId, isLoadingRateLimit, isLoadingRequests]);

  return result;
}
