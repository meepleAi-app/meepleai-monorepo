/**
 * AdvancedFiltersDrawer — type definitions.
 *
 * SP4 mockup conformance (Issue #1585-followup, plan
 * docs/superpowers/plans/2026-06-03-library-sp4-mockup-conformance.md Task 3.3).
 * Mapped from `admin-mockups/design_files/sp4-library-desktop.jsx`
 * (AdvancedFiltersDrawer + DRAWER_SECTIONS, lines 312–637).
 *
 * REFACTORED from scope-conditional (game/agent/session/kb/chat discriminated
 * union with per-scope filter shapes) to **cross-entity hub-level** filter
 * model. The drawer now exposes the same 7 sections regardless of which tab
 * the user is viewing; filter outcomes apply across all hub entities. This
 * matches the mockup which has no `scope` discriminator and presents a
 * unified library-wide filter surface.
 *
 * Each field is independently optional — absence means "no filter on that
 * dimension". `period === 'range'` enables the optional `periodFrom`/`periodTo`
 * ISO-date bounds for custom date pickers (UI for the custom range itself
 * is intentionally out of scope for this PR — the radio option is rendered
 * but the bound fields stay undefined until a future enhancement).
 */

export type LibraryFilterStatus = 'owned' | 'wishlist' | 'setup' | 'archived';

/**
 * @deprecated Issue #2186 — the AdvancedFiltersDrawer no longer ships an
 * entity-type filter section because it duplicated the LibraryTabs entity
 * scope (Tutti / Giochi / Agenti / KB / Sessioni / Chat). The type is kept
 * for one release so external consumers can migrate; remove in a follow-up.
 */
export type LibraryFilterEntity = 'game' | 'agent' | 'kb' | 'session' | 'chat';

export type LibraryFilterPeriod = '7d' | '30d' | '1y' | 'all' | 'range';

export type LibraryFilterTag =
  | 'family'
  | 'strategy'
  | 'coop'
  | 'engine'
  | 'auction'
  | 'roll-and-write'
  | 'card-driven'
  | 'tableau';

export type LibraryFilterWeight = 'light' | 'medium' | 'heavy' | 'extra';

export interface LibraryFilters {
  readonly statuses?: ReadonlyArray<LibraryFilterStatus>;
  /**
   * @deprecated Issue #2186 — removed in favor of the LibraryTabs entity
   * scope. Field kept optional for one release so persisted user state can
   * be migrated. Always undefined for new state.
   */
  readonly entities?: ReadonlyArray<LibraryFilterEntity>;
  readonly games?: ReadonlyArray<string>;
  readonly period?: LibraryFilterPeriod;
  readonly periodFrom?: string;
  readonly periodTo?: string;
  readonly tags?: ReadonlyArray<LibraryFilterTag>;
  readonly ratingMin?: number;
  readonly ratingMax?: number;
  readonly weights?: ReadonlyArray<LibraryFilterWeight>;
}

export interface AdvancedFiltersDrawerGameOption {
  readonly id: string;
  readonly title: string;
}

export interface AdvancedFiltersDrawerProps {
  readonly open: boolean;
  readonly onOpenChange: (open: boolean) => void;
  /** Active filters applied to the surface; shown in the drawer when it opens. */
  readonly activeFilters: LibraryFilters;
  /**
   * Optional list of games surfaced in the "Gioco" select-multi section.
   * When omitted, the section renders an empty-state hint instead of options.
   */
  readonly availableGames?: ReadonlyArray<AdvancedFiltersDrawerGameOption>;
  /** Called with the new draft when the user clicks Applica. Drawer closes after. */
  readonly onApply: (filters: LibraryFilters) => void;
  /** Called when the user clicks Reset. Drawer resets draft to {} but stays open. */
  readonly onClear: () => void;
}

/**
 * Count of non-empty filter fields, used to drive the header subtitle "N attivi"
 * and the footer Apply button suffix "(N)".
 */
export function countActiveFilters(filters: LibraryFilters): number {
  let count = 0;
  for (const value of Object.values(filters)) {
    if (value === undefined || value === null) continue;
    if (Array.isArray(value)) {
      if (value.length > 0) count += value.length;
    } else if (typeof value === 'string' && value.length === 0) {
      continue;
    } else {
      count += 1;
    }
  }
  return count;
}
