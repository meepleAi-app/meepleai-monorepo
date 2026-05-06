/**
 * Smoke tests — /players real backend round-trip (Wave 4 D1, Issue #682).
 *
 * The players surface derives data from `GET /api/v1/play-records/statistics`
 * (aggregated player statistics from play records). There is no dedicated
 * `/api/v1/players` endpoint — this is a known anti-pattern carryover from v1,
 * tracked as a followup backend redesign issue.
 *
 * Smoke suite:
 *   1. API contract: `GET /api/v1/play-records/statistics` returns the expected
 *      `PlayerStatistics` shape (`totalSessions`, `gamePlayCounts`).
 *   2. Frontend rendering: `/players` renders the hero or empty shell without
 *      entering the error state (dual-selector hero|empty).
 */
import { test, expect } from '@playwright/test';

import { applySessionToPage, smokeLogin } from './_helpers/auth';

const API_BASE = process.env.SMOKE_API_BASE ?? 'http://localhost:8080';

test.describe('SMOKE — /players real backend round-trip', () => {
  test('GET /api/v1/play-records/statistics returns PlayerStatistics shape', async ({
    request,
  }) => {
    const { cookieHeader } = await smokeLogin(request);
    const res = await request.get(`${API_BASE}/api/v1/play-records/statistics`, {
      headers: { Cookie: cookieHeader },
    });
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body).toMatchObject({
      totalSessions: expect.any(Number),
      gamePlayCounts: expect.any(Object),
    });
  });

  test('frontend /players renders hero or empty shell without error state', async ({
    page,
    request,
  }) => {
    const { cookieHeader } = await smokeLogin(request);
    await applySessionToPage(page, cookieHeader);
    await page.goto('/players', { waitUntil: 'domcontentloaded' });
    // Accept either the populated hero (data present) or the empty shell
    // (no play records yet). Both indicate the frontend rendered correctly.
    await page.waitForSelector('[data-slot="players-hero"], [data-slot="players-empty"]', {
      timeout: 30_000,
    });
    // The error surface must NOT be visible — confirms no API failure.
    const errorSurface = await page
      .locator('[data-slot="players-empty"][data-kind="error"]')
      .count();
    expect(errorSurface).toBe(0);
  });
});
