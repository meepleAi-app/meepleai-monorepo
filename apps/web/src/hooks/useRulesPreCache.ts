/**
 * useRulesPreCache Hook (Issue #5586)
 *
 * Pre-loads RulebookAnalysis for games in an active session.
 * On session start, fetches analysis data and stores in localStorage cache
 * for instant offline access.
 *
 * @module hooks/useRulesPreCache
 *
 * @example
 * ```tsx
 * function GameSessionView({ gameIds }: { gameIds: string[] }) {
 *   const { cacheStatus, cachedGameIds, refresh } = useRulesPreCache(gameIds);
 *
 *   if (cacheStatus === 'loading') return <Spinner />;
 *   // Cache ready, chat can use offline FAQ fallback
 * }
 * ```
 */

import { useCallback, useEffect, useRef, useState } from 'react';

import type { RulebookAnalysisDto } from '@/lib/api/schemas/shared-games.schemas';
import {
  cacheRulebookAnalyses,
  isGameCached,
  removeCachedGame,
  pruneExpiredEntries,
} from '@/lib/game-night/rules-cache';

// ============================================================================
// Types
// ============================================================================

export type CacheStatus = 'idle' | 'loading' | 'ready' | 'partial' | 'error';

export interface UseRulesPreCacheReturn {
  /** Current cache loading status */
  cacheStatus: CacheStatus;
  /** Game IDs that are successfully cached */
  cachedGameIds: string[];
  /** Game IDs that failed to cache */
  failedGameIds: string[];
  /** Manually trigger a refresh of the cache */
  refresh: () => void;
  /** Whether all requested games are cached */
  isFullyCached: boolean;
}

export interface UseRulesPreCacheOptions {
  /** Custom API base URL (defaults to NEXT_PUBLIC_API_BASE or localhost:8080) */
  apiBaseUrl?: string;
  /** Whether to skip pre-caching (e.g., if disabled by config) */
  disabled?: boolean;
  /** TTL for cache entries in ms (default 24h) */
  ttlMs?: number;
}

// ============================================================================
// Fetcher
// ============================================================================

async function fetchGameAnalysis(
  gameId: string,
  apiBaseUrl: string,
  signal?: AbortSignal
): Promise<RulebookAnalysisDto[]> {
  const response = await fetch(`${apiBaseUrl}/api/v1/shared-games/${gameId}/analysis`, {
    credentials: 'include',
    signal,
    headers: { Accept: 'application/json' },
  });

  if (!response.ok) {
    throw new Error(`HTTP ${response.status}`);
  }

  return response.json();
}

// ============================================================================
// Hook
// ============================================================================

export function useRulesPreCache(
  gameIds: string[],
  options: UseRulesPreCacheOptions = {}
): UseRulesPreCacheReturn {
  const {
    apiBaseUrl = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080',
    disabled = false,
    ttlMs,
  } = options;

  const [cacheStatus, setCacheStatus] = useState<CacheStatus>('idle');
  const [cachedGameIds, setCachedGameIds] = useState<string[]>([]);
  const [failedGameIds, setFailedGameIds] = useState<string[]>([]);
  const abortControllerRef = useRef<AbortController | null>(null);
  // Ref to track the current gameIds set to prevent stale closures
  const gameIdsRef = useRef<string[]>(gameIds);
  gameIdsRef.current = gameIds;

  const loadCache = useCallback(async () => {
    if (disabled || gameIds.length === 0) {
      setCacheStatus('idle');
      return;
    }

    // Prune expired entries on each load attempt
    pruneExpiredEntries();

    // Abort any in-flight request
    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
    }

    const controller = new AbortController();
    abortControllerRef.current = controller;

    // Check which games are already cached
    const uncachedIds = gameIds.filter(id => !isGameCached(id));
    const alreadyCachedIds = gameIds.filter(id => isGameCached(id));

    if (uncachedIds.length === 0) {
      setCachedGameIds(gameIds);
      setFailedGameIds([]);
      setCacheStatus('ready');
      return;
    }

    setCacheStatus('loading');

    const newCached: string[] = [...alreadyCachedIds];
    const newFailed: string[] = [];

    // Fetch uncached games in parallel (max 3 concurrent)
    const batches: string[][] = [];
    for (let i = 0; i < uncachedIds.length; i += 3) {
      batches.push(uncachedIds.slice(i, i + 3));
    }

    for (const batch of batches) {
      if (controller.signal.aborted) break;

      const results = await Promise.allSettled(
        batch.map(gameId => fetchGameAnalysis(gameId, apiBaseUrl, controller.signal))
      );

      for (let i = 0; i < batch.length; i++) {
        const gameId = batch[i];
        const result = results[i];

        if (result.status === 'fulfilled' && result.value.length > 0) {
          const stored = cacheRulebookAnalyses(gameId, result.value, ttlMs);
          if (stored) {
            newCached.push(gameId);
          } else {
            newFailed.push(gameId);
          }
        } else {
          newFailed.push(gameId);
        }
      }
    }

    // Only update state if this request is still current
    if (!controller.signal.aborted) {
      setCachedGameIds(newCached);
      setFailedGameIds(newFailed);

      if (newFailed.length === 0) {
        setCacheStatus('ready');
      } else if (newCached.length > 0) {
        setCacheStatus('partial');
      } else {
        setCacheStatus('error');
      }
    }
  }, [gameIds, apiBaseUrl, disabled, ttlMs]);

  // Auto-load on gameIds change
  useEffect(() => {
    void loadCache();

    return () => {
      if (abortControllerRef.current) {
        abortControllerRef.current.abort();
      }
    };
  }, [loadCache]);

  const refresh = useCallback(() => {
    // Clear cached entries for current games so they re-fetch
    for (const id of gameIdsRef.current) {
      removeCachedGame(id);
    }
    void loadCache();
  }, [loadCache]);

  return {
    cacheStatus,
    cachedGameIds,
    failedGameIds,
    refresh,
    isFullyCached: cachedGameIds.length === gameIds.length && gameIds.length > 0,
  };
}
