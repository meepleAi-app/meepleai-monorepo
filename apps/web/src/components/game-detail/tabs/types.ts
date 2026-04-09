/**
 * Shared tab component contract for the library-to-game epic.
 *
 * Both S4 (desktop `GameTabsPanel`) and S5 (mobile `GameDetailsDrawer`)
 * import the same 5 tab components and pass `variant` to control layout.
 * This avoids duplication and keeps the two viewports in sync.
 *
 * Reference: docs/superpowers/specs/2026-04-09-library-to-game-epic-design.md §4.4.1
 */

export type GameTabId = 'info' | 'aiChat' | 'toolbox' | 'houseRules' | 'partite';

export type GameTabVariant = 'desktop' | 'mobile';

/**
 * Props shared by all game detail tab components.
 *
 * Contract:
 * - Each tab owns its data fetching via React Query (no data props).
 * - Each tab must render correctly in both `desktop` and `mobile` containers.
 * - No `position: sticky` on direct children; the container manages scroll.
 * - Each tab root exposes `role="tabpanel"` and `aria-labelledby={tabId}`.
 * - Each tab handles its own loading / error / empty states.
 */
export interface GameTabProps {
  /** The SharedGameId OR PrivateGameId for which to render content. */
  gameId: string;

  /** Layout hint — affects padding, font scale, touch targets. */
  variant: GameTabVariant;

  /** True if gameId refers to a private game upload (not a shared catalog entry). */
  isPrivateGame?: boolean;

  /**
   * True if the user does NOT have this game in their library.
   * Locks non-Info tabs with a CTA placeholder.
   */
  isNotInLibrary?: boolean;
}

/**
 * Metadata for a single tab in the rail.
 */
export interface GameTabDescriptor {
  id: GameTabId;
  label: string;
  icon: string;
}

export const GAME_TABS: readonly GameTabDescriptor[] = [
  { id: 'info', label: 'Info', icon: '📖' },
  { id: 'aiChat', label: 'AI Chat', icon: '🤖' },
  { id: 'toolbox', label: 'Toolbox', icon: '🧰' },
  { id: 'houseRules', label: 'House Rules', icon: '🏠' },
  { id: 'partite', label: 'Partite', icon: '🎲' },
] as const;

/**
 * Type guard to validate a raw string as a GameTabId.
 * Useful for parsing URL `?tab=<id>` query parameters.
 */
export function isGameTabId(value: string | null | undefined): value is GameTabId {
  return (
    value === 'info' ||
    value === 'aiChat' ||
    value === 'toolbox' ||
    value === 'houseRules' ||
    value === 'partite'
  );
}
