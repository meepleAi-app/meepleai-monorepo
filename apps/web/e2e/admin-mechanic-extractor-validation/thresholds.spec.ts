/**
 * E2E — Mechanic Extractor AI Validation: Certification Thresholds form
 * (ADR-051 Sprint 2 / Task 27).
 *
 * Verifies the operator end-to-end loop on
 * `/admin/knowledge-base/mechanic-extractor/dashboard`:
 *
 *  1. `GET /api/v1/admin/mechanic-extractor/thresholds` populates the four
 *     numeric inputs (defense-in-depth: same singleton VO surface as backend).
 *  2. Editing a field flips Save from disabled → enabled.
 *  3. Submitting fires `PUT /thresholds`, surfaces the success toast, and
 *     resets the dirty baseline so Save goes back to disabled (no re-prompt).
 *  4. After reload, the GET returns the new values and the form re-prefills.
 *
 * Backend wire format and bounds: `CertificationThresholds` VO +
 * `UpdateCertificationThresholdsValidator`. Tests run against fully-mocked
 * routes — no live DB or auth — so they're hermetic and stable in CI.
 *
 * Feature flag: requires `NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED=true` at
 * server start (set in `apps/web/.env.development.example` and forwarded by
 * `playwright.config.ts` webServer env). When off the dashboard route 404s
 * via `notFound()` in the page component.
 */

import { test, expect, type Page, type Route } from '@playwright/test';

import { setupMockAuth } from '../fixtures/auth';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

const DASHBOARD_PATH = '/admin/knowledge-base/mechanic-extractor/dashboard';

const INITIAL_THRESHOLDS = {
  minCoveragePct: 70,
  maxPageTolerance: 1,
  minBggMatchPct: 60,
  minOverallScore: 75,
};

const UPDATED_MIN_COVERAGE = 80;

/**
 * Mocks `GET /thresholds` to return a mutable in-memory snapshot and
 * `PUT /thresholds` to write through to that snapshot. Returns a `state`
 * handle so the test can assert what was last persisted.
 *
 * The handler honours the request method instead of using two separate
 * `route(...)` registrations because Playwright matches the most-recent
 * registration for the same URL pattern.
 */
async function mockThresholdsRoutes(page: Page) {
  const state = { current: { ...INITIAL_THRESHOLDS } };
  let putCount = 0;

  await page
    .context()
    .route(`${API_BASE}/api/v1/admin/mechanic-extractor/thresholds`, async (route: Route) => {
      const method = route.request().method();
      if (method === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(state.current),
        });
        return;
      }
      if (method === 'PUT') {
        const body = route.request().postDataJSON() as typeof state.current | null;
        if (body) {
          state.current = body;
        }
        putCount += 1;
        await route.fulfill({
          status: 204,
        });
        return;
      }
      await route.continue();
    });

  // Dashboard summary GET — the page renders the thresholds card alongside
  // the dashboard table. Stub it with an empty rows array so the page is
  // happy without us caring about table content for this test.
  await page
    .context()
    .route(`${API_BASE}/api/v1/admin/mechanic-extractor/dashboard`, async (route: Route) => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([]),
        });
        return;
      }
      await route.continue();
    });

  return {
    state,
    getPutCount: () => putCount,
  };
}

test.describe('Admin · Mechanic Extractor · Certification Thresholds', () => {
  test('admin updates a threshold, sees the success toast, and the new value persists across reload', async ({
    page,
  }) => {
    await setupMockAuth(page, 'Admin', 'admin@meepleai.dev');
    const mocks = await mockThresholdsRoutes(page);

    await page.goto(DASHBOARD_PATH);
    await page.waitForLoadState('networkidle');

    // Form should be present and pre-filled from the GET.
    const minCoverage = page.getByLabel(/minimum coverage/i);
    await expect(minCoverage).toBeVisible();
    await expect(minCoverage).toHaveValue(String(INITIAL_THRESHOLDS.minCoveragePct));

    const saveButton = page.getByRole('button', { name: /save thresholds/i });
    // Pristine form → Save disabled (no dirty fields yet).
    await expect(saveButton).toBeDisabled();

    // Edit one field — flips dirty state, enables Save.
    await minCoverage.fill(String(UPDATED_MIN_COVERAGE));
    await expect(saveButton).toBeEnabled();

    await saveButton.click();

    // Sonner toast surfaces the success message from `useUpdateThresholds`.
    await expect(page.getByText(/certification thresholds updated successfully/i)).toBeVisible();

    // Mutation completed → form resets dirty baseline, Save disabled again.
    await expect(saveButton).toBeDisabled();
    expect(mocks.getPutCount()).toBe(1);
    expect(mocks.state.current.minCoveragePct).toBe(UPDATED_MIN_COVERAGE);

    // Reload — GET returns the persisted snapshot, form re-prefills from it.
    await page.reload();
    await page.waitForLoadState('networkidle');
    await expect(page.getByLabel(/minimum coverage/i)).toHaveValue(String(UPDATED_MIN_COVERAGE));
  });

  test('Save stays disabled while the form is pristine', async ({ page }) => {
    await setupMockAuth(page, 'Admin', 'admin@meepleai.dev');
    await mockThresholdsRoutes(page);

    await page.goto(DASHBOARD_PATH);
    await page.waitForLoadState('networkidle');

    const saveButton = page.getByRole('button', { name: /save thresholds/i });
    const resetButton = page.getByRole('button', { name: /^reset$/i });

    await expect(saveButton).toBeDisabled();
    await expect(resetButton).toBeDisabled();
  });
});
