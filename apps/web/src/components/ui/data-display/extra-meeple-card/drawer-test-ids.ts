/**
 * Centralized data-testid constants for ExtraMeepleCardDrawer.
 * Import these in both the component and the test file to avoid magic strings.
 *
 * Issue #5024 — ExtraMeepleCard Drawer System (Epic #5023)
 */

export const DRAWER_TEST_IDS = {
  /** Visible entity label in the colored header (h2) */
  ENTITY_LABEL: 'drawer-entity-label',

  /** Animated skeleton shown while entity content loads */
  LOADING_SKELETON: 'drawer-loading-skeleton',

  /** Error state container (role="alert") */
  ERROR_STATE: 'drawer-error-state',

  /**
   * Coming-soon placeholder for entity types not yet implemented.
   * Use COMING_SOON(issueNumber) to get the full id.
   */
  COMING_SOON: (issueNumber: number) => `drawer-coming-soon-${issueNumber}` as const,
} as const;
