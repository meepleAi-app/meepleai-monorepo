/**
 * Rules Cache Service (Issue #5586)
 *
 * Provides localStorage-based caching for RulebookAnalysis data.
 * Enables instant offline access to FAQ, mechanics, phases, and resources.
 *
 * Key design:
 * - Cache key pattern: `rules-cache:{gameId}`
 * - TTL-based expiration (default 24h)
 * - Max ~5MB per game (localStorage JSON)
 * - SSR-safe via safeStorage
 *
 * @module lib/game-night/rules-cache
 */

import type { RulebookAnalysisDto } from '@/lib/api/schemas/shared-games.schemas';

// ============================================================================
// Constants
// ============================================================================

const CACHE_PREFIX = 'rules-cache:';
const CACHE_INDEX_KEY = 'rules-cache:__index__';

/** Default TTL: 24 hours in milliseconds */
const DEFAULT_TTL_MS = 24 * 60 * 60 * 1000;

/** Max approximate size per game entry in bytes (~5MB) */
const MAX_ENTRY_SIZE_BYTES = 5 * 1024 * 1024;

// ============================================================================
// Types
// ============================================================================

export interface CachedRulebookEntry {
  /** The full analysis data */
  analyses: RulebookAnalysisDto[];
  /** ISO timestamp when the entry was cached */
  cachedAt: string;
  /** ISO timestamp when the entry expires */
  expiresAt: string;
  /** Game title for display purposes */
  gameTitle: string;
}

export interface CacheIndex {
  /** Map of gameId to cache metadata */
  entries: Record<string, { cachedAt: string; expiresAt: string; gameTitle: string }>;
}

export interface RulesCacheStats {
  /** Number of games currently cached */
  cachedGameCount: number;
  /** Approximate total size in bytes */
  approximateSizeBytes: number;
  /** List of cached game IDs with titles */
  cachedGames: Array<{ gameId: string; gameTitle: string; expiresAt: string }>;
}

// ============================================================================
// Helpers
// ============================================================================

function isSSR(): boolean {
  return typeof window === 'undefined';
}

function getCacheKey(gameId: string): string {
  return `${CACHE_PREFIX}${gameId}`;
}

function getApproximateSize(value: string): number {
  // Each character is roughly 2 bytes in UTF-16
  return value.length * 2;
}

// ============================================================================
// Cache Service
// ============================================================================

/**
 * Store rulebook analyses for a game in localStorage.
 *
 * @param gameId - The game UUID
 * @param analyses - Array of RulebookAnalysisDto from the API
 * @param ttlMs - Time-to-live in milliseconds (default 24h)
 * @returns true if cached successfully, false on failure
 */
export function cacheRulebookAnalyses(
  gameId: string,
  analyses: RulebookAnalysisDto[],
  ttlMs: number = DEFAULT_TTL_MS
): boolean {
  if (isSSR() || analyses.length === 0) return false;

  const now = new Date();
  const gameTitle = analyses[0]?.gameTitle ?? 'Unknown';

  const entry: CachedRulebookEntry = {
    analyses,
    cachedAt: now.toISOString(),
    expiresAt: new Date(now.getTime() + ttlMs).toISOString(),
    gameTitle,
  };

  try {
    const serialized = JSON.stringify(entry);

    // Guard against oversized entries
    if (getApproximateSize(serialized) > MAX_ENTRY_SIZE_BYTES) {
      if (process.env.NODE_ENV !== 'production') {
        console.warn(`[rules-cache] Entry for game ${gameId} exceeds max size, skipping`);
      }
      return false;
    }

    window.localStorage.setItem(getCacheKey(gameId), serialized);

    // Update index
    updateIndex(gameId, {
      cachedAt: entry.cachedAt,
      expiresAt: entry.expiresAt,
      gameTitle,
    });

    return true;
  } catch (error) {
    if (process.env.NODE_ENV !== 'production') {
      console.warn(`[rules-cache] Failed to cache game ${gameId}:`, error);
    }
    return false;
  }
}

/**
 * Retrieve cached rulebook analyses for a game.
 * Returns null if not cached or expired.
 */
export function getCachedAnalyses(gameId: string): CachedRulebookEntry | null {
  if (isSSR()) return null;

  try {
    const raw = window.localStorage.getItem(getCacheKey(gameId));
    if (!raw) return null;

    const entry: CachedRulebookEntry = JSON.parse(raw);

    // Check expiration
    if (new Date(entry.expiresAt) < new Date()) {
      // Expired - clean up
      removeCachedGame(gameId);
      return null;
    }

    return entry;
  } catch {
    // Corrupted entry - remove it
    removeCachedGame(gameId);
    return null;
  }
}

/**
 * Check if a game has a valid (non-expired) cache entry.
 */
export function isGameCached(gameId: string): boolean {
  return getCachedAnalyses(gameId) !== null;
}

/**
 * Remove cached data for a specific game.
 */
export function removeCachedGame(gameId: string): void {
  if (isSSR()) return;

  try {
    window.localStorage.removeItem(getCacheKey(gameId));
    removeFromIndex(gameId);
  } catch {
    // Ignore removal errors
  }
}

/**
 * Clear all cached rulebook data.
 */
export function clearAllCachedRules(): void {
  if (isSSR()) return;

  try {
    const index = getIndex();
    for (const gameId of Object.keys(index.entries)) {
      window.localStorage.removeItem(getCacheKey(gameId));
    }
    window.localStorage.removeItem(CACHE_INDEX_KEY);
  } catch {
    // Best effort cleanup
  }
}

/**
 * Remove expired entries from cache.
 * Call periodically or on app startup.
 */
export function pruneExpiredEntries(): number {
  if (isSSR()) return 0;

  const index = getIndex();
  const now = new Date();
  let pruned = 0;

  for (const [gameId, meta] of Object.entries(index.entries)) {
    if (new Date(meta.expiresAt) < now) {
      window.localStorage.removeItem(getCacheKey(gameId));
      delete index.entries[gameId];
      pruned++;
    }
  }

  if (pruned > 0) {
    try {
      window.localStorage.setItem(CACHE_INDEX_KEY, JSON.stringify(index));
    } catch {
      // Ignore
    }
  }

  return pruned;
}

/**
 * Get cache statistics.
 */
export function getCacheStats(): RulesCacheStats {
  if (isSSR()) {
    return { cachedGameCount: 0, approximateSizeBytes: 0, cachedGames: [] };
  }

  const index = getIndex();
  let totalSize = 0;
  const cachedGames: RulesCacheStats['cachedGames'] = [];

  for (const [gameId, meta] of Object.entries(index.entries)) {
    const raw = window.localStorage.getItem(getCacheKey(gameId));
    if (raw) {
      totalSize += getApproximateSize(raw);
      cachedGames.push({
        gameId,
        gameTitle: meta.gameTitle,
        expiresAt: meta.expiresAt,
      });
    }
  }

  return {
    cachedGameCount: cachedGames.length,
    approximateSizeBytes: totalSize,
    cachedGames,
  };
}

// ============================================================================
// Index Management (internal)
// ============================================================================

function getIndex(): CacheIndex {
  try {
    const raw = window.localStorage.getItem(CACHE_INDEX_KEY);
    if (!raw) return { entries: {} };
    return JSON.parse(raw) as CacheIndex;
  } catch {
    return { entries: {} };
  }
}

function updateIndex(
  gameId: string,
  meta: { cachedAt: string; expiresAt: string; gameTitle: string }
): void {
  try {
    const index = getIndex();
    index.entries[gameId] = meta;
    window.localStorage.setItem(CACHE_INDEX_KEY, JSON.stringify(index));
  } catch {
    // Ignore index update errors
  }
}

function removeFromIndex(gameId: string): void {
  try {
    const index = getIndex();
    delete index.entries[gameId];
    window.localStorage.setItem(CACHE_INDEX_KEY, JSON.stringify(index));
  } catch {
    // Ignore
  }
}
