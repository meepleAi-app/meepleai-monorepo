/**
 * Pure mappers DTO → HybridHubItem — Phase 1 (Issue #1591).
 *
 * Each function is a deterministic transform over a single source DTO; no IO,
 * no React, no global state — so unit-testable in isolation. Phase 2's
 * orchestrator will call these inside `useMemo` after the per-entity hooks
 * resolve.
 *
 * Conventions:
 *   - `subtitle`, `gameName`, optional fields: convert `null` from the DTO to
 *     `undefined` (the FE convention for "absent"); the union types only model
 *     `undefined`.
 *   - `updatedAt`: pick the most meaningful recency signal on the source.
 *   - `href`: stick to current public routes; final routing in Phase 2.
 */

import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

import type { GameHubItem } from './hybrid-hub.types';

export function libraryEntryToHubItem(entry: UserLibraryEntry): GameHubItem {
  return {
    id: entry.id,
    entity: 'game',
    title: entry.gameTitle,
    subtitle: entry.gamePublisher ?? undefined,
    updatedAt: entry.stateChangedAt ?? entry.addedAt,
    href: `/library/${entry.gameId}`,
    gameId: entry.gameId,
    rating: entry.averageRating ?? undefined,
    state: entry.currentState,
    imageUrl: entry.gameImageUrl ?? entry.gameIconUrl ?? undefined,
  };
}
