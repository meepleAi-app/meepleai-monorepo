/**
 * E2E Test Tags for CI filtering.
 *
 * Usage in tests:
 *   test('Login flow @smoke @critical', async () => { ... });
 *
 * CI filtering:
 *   npx playwright test --grep @smoke          # Only smoke tests
 *   npx playwright test --grep @critical       # Only critical path
 *   npx playwright test --grep-invert @slow    # Skip slow tests
 *
 * Tag conventions:
 *   @smoke     - Fast, core functionality (run on every PR)
 *   @critical  - Business-critical paths (run on every PR)
 *   @regression - Full regression suite (nightly)
 *   @slow      - Tests > 60s (exclude from quick CI)
 *   @flow      - Multi-step serial flows (run in dedicated stage)
 *   @admin     - Admin-specific features
 *   @auth      - Authentication flows
 *   @rag       - RAG/AI/Agent tests
 */
export const TAG = {
  SMOKE: '@smoke',
  CRITICAL: '@critical',
  REGRESSION: '@regression',
  SLOW: '@slow',
  FLOW: '@flow',
  ADMIN: '@admin',
  AUTH: '@auth',
  RAG: '@rag',
} as const;
