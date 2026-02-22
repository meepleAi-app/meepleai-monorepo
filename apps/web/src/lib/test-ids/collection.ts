/**
 * Centralized data-testid constants for Collection components.
 * Import these in both the component and the test file to avoid magic strings.
 *
 * Issue #5095 — Centralize test magic strings
 */

export const COLLECTION_TEST_IDS = {
  /** Root dashboard container */
  dashboard: 'collection-dashboard',

  /** Toolbar containing search and view-mode controls */
  toolbar: 'collection-toolbar',

  /** Search input element */
  search: 'collection-search',

  /** Grid view mode toggle button */
  viewModeGrid: 'view-mode-grid',

  /** List view mode toggle button */
  viewModeList: 'view-mode-list',

  /**
   * Hero stat card by stat id.
   * @example COLLECTION_TEST_IDS.heroStat('total') // => "hero-stat-total"
   */
  heroStat: (id: string) => `hero-stat-${id}` as const,

  /**
   * Filter chip by filter label (lowercased).
   * @example COLLECTION_TEST_IDS.filterChip('all') // => "filter-chip-all"
   */
  filterChip: (id: string) => `filter-chip-${id.toLowerCase()}` as const,

  /** Game grid container */
  grid: 'collection-grid',

  /** Empty state panel shown when no games match */
  emptyState: 'collection-empty-state',
} as const;
