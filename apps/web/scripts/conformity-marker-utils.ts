/**
 * WS-C single-screen marker utilities — shared by bootstrap.spec.ts and unit tests.
 *
 * Extracts viewport detection, selector construction, and marker count validation
 * to enable unit testing the contract enforcement logic without spinning up Playwright.
 *
 * Spec: docs/superpowers/specs/2026-05-18-conformity-baseline-single-screen-design.md
 * Refs: #1269 (single-screen refactor), #1069 (WS-C), #1066 (umbrella).
 */

export type ConformityViewport = 'desktop' | 'mobile';

const PROJECT_TO_VIEWPORT: Record<string, ConformityViewport> = {
  'conformity-bootstrap-desktop': 'desktop',
  'conformity-bootstrap-mobile': 'mobile',
  'conformity-verify-desktop': 'desktop',
  'conformity-verify-mobile': 'mobile',
};

/**
 * Map Playwright project name → viewport label. Throws for unrecognized projects
 * to prevent silent misclassification.
 */
export function getViewportFromProjectName(projectName: string): ConformityViewport {
  const viewport = PROJECT_TO_VIEWPORT[projectName];
  if (!viewport) {
    throw new Error(
      `Unknown conformity project: ${projectName}. ` +
        `Expected one of: ${Object.keys(PROJECT_TO_VIEWPORT).join(', ')}.`
    );
  }
  return viewport;
}

/**
 * Build the CSS selector for the single-screen marker.
 */
export function buildMarkerSelector(viewport: ConformityViewport): string {
  return `[data-conformity-screen="default-${viewport}"]`;
}

/**
 * Validate that exactly one marker element was found in the mockup DOM.
 * Throws with actionable error messages.
 */
export function validateMarkerCount(
  count: number,
  mockupFilename: string,
  markerValue: string
): void {
  if (count === 1) return;

  const viewportLabel = markerValue.endsWith('-desktop') ? 'desktop' : 'mobile';

  if (count === 0) {
    throw new Error(
      `Mockup ${mockupFilename} missing required marker. ` +
        `Add data-conformity-screen="${markerValue}" to the element that ` +
        `represents the canonical default ${viewportLabel} screen.`
    );
  }

  throw new Error(
    `Mockup ${mockupFilename} has ${count} elements with data-conformity-screen="${markerValue}". ` +
      `Exactly one is required.`
  );
}
