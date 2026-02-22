/**
 * Centralized data-testid constants for AddGameWizard.
 * Import these in both the component and the test file to avoid magic strings.
 *
 * Issue #5095 — Centralize test magic strings
 */

export const WIZARD_TEST_IDS = {
  /** Main wizard title (h1) */
  title: 'wizard-title',

  /** Wizard subtitle / description */
  subtitle: 'wizard-subtitle',

  /**
   * Step description span for screen readers.
   * Use stepDescription(n) to get the full id.
   *
   * @example
   * data-testid={WIZARD_TEST_IDS.stepDescription(1)} // => "step-1-description"
   */
  stepDescription: (n: number) => `step-${n}-description` as const,
} as const;
