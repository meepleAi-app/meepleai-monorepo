/**
 * Chromatic Playwright Fixture
 *
 * Use this instead of @playwright/test to enable Chromatic visual snapshots
 * in E2E tests. Chromatic will auto-capture screenshots at end of each test.
 *
 * @see docs/chromatic-setup.md
 */

export { test, expect } from '@chromatic-com/playwright';
