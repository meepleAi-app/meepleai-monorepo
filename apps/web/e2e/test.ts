/**
 * Central test export for E2E tests with coverage tracking
 * Issue #1498: E2E Code Coverage Reporting
 *
 * This file merges @bgotink/playwright-coverage with existing fixtures
 * to enable code coverage tracking for Chromium-based tests.
 *
 * Usage:
 * ```ts
 * import { test, expect } from './test';
 * // or
 * import { test, expect } from '../test'; // from subdirectories
 * ```
 */

import { test as testWithCoverage } from '@bgotink/playwright-coverage';
import { mergeTests } from '@playwright/test';

import { test as testWithAuth } from './fixtures/auth';

/**
 * Merged test that includes both coverage tracking and auth fixtures
 * - Coverage tracking works on Chromium browsers only (V8 API limitation)
 * - Auth fixtures provide: adminPage, editorPage, userPage, setupUserPage
 */
export const test = mergeTests(testWithCoverage, testWithAuth);

export { expect } from '@playwright/test';
