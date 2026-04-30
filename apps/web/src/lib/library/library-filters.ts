/**
 * Pure helpers for the `/library` desktop v2 surface (Issue #574, Wave B.3).
 *
 * Mirrors the patterns from `lib/games/library-filters.ts` (Wave B.1) and
 * `lib/agents/library-filters.ts` (Wave B.2). No React, no API client —
 * exclusively transforms over `UserLibraryEntry` so it stays unit-testable
 * in isolation.
 *
 * Entity tab scope (decision C3, see spec §3.3):
 *   - `all`    → all library entries
 *   - `kb`     → entries with at least one PDF in the knowledge base
 *   - `loaned` → entries currently lent out (`currentState === 'InPrestito'`)
 *
 * The legacy `game` tab from Phase-0 stubs is dropped (YAGNI: it was an
 * alias of `all`).
 *
 * `loaned` replaces the misleading `archived` label from earlier drafts
 * (decision C2): "InPrestito" is not an archived state, so naming the tab
 * `loaned` aligns the UI vocabulary with the domain.
 */

import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

export type LibraryEntityKey = 'all' | 'kb' | 'loaned';
export type LibrarySortKey = 'recent' | 'title' | 'rating' | 'state';
export type LibraryUiState = 'default' | 'loading' | 'empty' | 'filtered-empty' | 'error';

export interface LibraryHeroStats {
  totalGames: number;
  kbReady: number;
  wishlist: number;
  loaned: number;
}

/**
 * KB-presence test. An entry counts as "in the knowledge base" if either:
 *   - the backend computed `hasKb = true` (≥ 1 PDF fully indexed in RAG), or
 *   - any PDF document is linked, even if still processing (`kbCardCount > 0`).
 *
 * The OR is intentional: we want the `kb` tab to surface entries with a PDF
 * uploaded but still in the pipeline so the user sees their work in flight.
 */
export function isKbEntry(entry: UserLibraryEntry): boolean {
  return entry.hasKb || entry.kbCardCount > 0;
}

export function filterByEntity(
  entries: readonly UserLibraryEntry[],
  entity: LibraryEntityKey
): UserLibraryEntry[] {
  switch (entity) {
    case 'kb':
      return entries.filter(isKbEntry);
    case 'loaned':
      return entries.filter(e => e.currentState === 'InPrestito');
    case 'all':
    default:
      return entries.slice();
  }
}

export function matchQuery(entry: UserLibraryEntry, query: string): boolean {
  const trimmed = query.trim().toLowerCase();
  if (trimmed.length === 0) {
    return true;
  }
  if (entry.gameTitle.toLowerCase().includes(trimmed)) {
    return true;
  }
  if (entry.gamePublisher && entry.gamePublisher.toLowerCase().includes(trimmed)) {
    return true;
  }
  if (entry.gameYearPublished != null && String(entry.gameYearPublished).includes(trimmed)) {
    return true;
  }
  return false;
}

function compareNullable(a: number | null | undefined, b: number | null | undefined): number {
  const aMissing = a == null;
  const bMissing = b == null;
  if (aMissing && bMissing) return 0;
  if (aMissing) return 1;
  if (bMissing) return -1;
  return (b as number) - (a as number);
}

/**
 * Stable, semantic ordering of `currentState` for the `state` sort key.
 * Lower number = sorted earlier. Unknown / fallback states sink to the end.
 */
const STATE_ORDER: Record<string, number> = {
  Owned: 0,
  Nuovo: 1,
  InPrestito: 2,
  Wishlist: 3,
};

function compareState(a: UserLibraryEntry, b: UserLibraryEntry): number {
  const aOrder = STATE_ORDER[a.currentState] ?? 99;
  const bOrder = STATE_ORDER[b.currentState] ?? 99;
  if (aOrder !== bOrder) return aOrder - bOrder;
  // Tie-break by title for deterministic ordering inside a group.
  return a.gameTitle.localeCompare(b.gameTitle, undefined, { sensitivity: 'base' });
}

export function sortLibraryEntries(
  entries: readonly UserLibraryEntry[],
  sortKey: LibrarySortKey
): UserLibraryEntry[] {
  const copy = entries.slice();
  switch (sortKey) {
    case 'title':
      return copy.sort((a, b) =>
        a.gameTitle.localeCompare(b.gameTitle, undefined, { sensitivity: 'base' })
      );
    case 'rating':
      return copy.sort((a, b) => compareNullable(a.averageRating, b.averageRating));
    case 'state':
      return copy.sort(compareState);
    case 'recent':
    default:
      return copy.sort((a, b) => Date.parse(b.addedAt) - Date.parse(a.addedAt));
  }
}

export function deriveHeroStats(entries: readonly UserLibraryEntry[]): LibraryHeroStats {
  let kbReady = 0;
  let wishlist = 0;
  let loaned = 0;
  for (const entry of entries) {
    if (entry.hasKb) kbReady += 1;
    if (entry.currentState === 'Wishlist') wishlist += 1;
    else if (entry.currentState === 'InPrestito') loaned += 1;
  }
  return {
    totalGames: entries.length,
    kbReady,
    wishlist,
    loaned,
  };
}

/**
 * 5-state FSM resolver for the orchestrator. Priority (highest first):
 *   1. `override` — visual-test / state-preview hatch (env-gated by caller)
 *   2. `error`    — non-null fetch error
 *   3. `loading`  — query in flight
 *   4. `empty`    — fetch resolved but the user has zero entries
 *   5. `filtered-empty` — entries exist but the active filter eliminated all
 *   6. `default`  — fall-through, render the hybrid grid
 */
export function deriveLibraryUiState(args: {
  isLoading: boolean;
  error: unknown | null | undefined;
  totalCount: number;
  filteredCount: number;
  override?: LibraryUiState;
}): LibraryUiState {
  if (args.override) return args.override;
  if (args.error != null) return 'error';
  if (args.isLoading) return 'loading';
  if (args.totalCount === 0) return 'empty';
  if (args.filteredCount === 0) return 'filtered-empty';
  return 'default';
}
