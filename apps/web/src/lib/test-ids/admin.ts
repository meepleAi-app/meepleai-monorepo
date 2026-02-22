/**
 * Centralized data-testid constants for Admin components.
 * Import these in both the component and the test file to avoid magic strings.
 *
 * Issue #5095 — Centralize test magic strings
 */

export const ADMIN_TEST_IDS = {
  /**
   * Vector collection card by collection name.
   * @example ADMIN_TEST_IDS.collectionCard('Game Rules') // => "collection-card-Game Rules"
   */
  collectionCard: (name: string) => `collection-card-${name}` as const,
} as const;
