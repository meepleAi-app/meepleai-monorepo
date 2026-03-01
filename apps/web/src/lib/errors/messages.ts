/**
 * Centralized error message constants.
 *
 * These strings MUST match what the production code throws/sets as error state.
 * Import these in tests to avoid duplicating magic strings.
 *
 * Issue #5095 — Centralize test magic strings
 */

export const ERROR_MESSAGES = {
  wizard: {
    noGameSelected: 'No game selected',
    customGameRequiresName: 'Custom game requires a name',
  },
  pdfStorage: {
    fetchFailed: 'Failed to fetch PDF storage health',
  },
  network: {
    generic: 'Network error occurred',
  },
} as const;
