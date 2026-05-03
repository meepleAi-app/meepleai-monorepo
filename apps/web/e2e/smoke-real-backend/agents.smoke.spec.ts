import { test, expect } from '@playwright/test';

import { smokeLogin, applySessionToPage } from './_helpers/auth';

const API_BASE = process.env.SMOKE_API_BASE ?? 'http://localhost:8080';

test.describe('SMOKE — /agents real backend round-trip', () => {
  test('GET /api/v1/agents returns success+agents+count shape', async ({ request }) => {
    const { cookieHeader } = await smokeLogin(request);
    const res = await request.get(`${API_BASE}/api/v1/agents`, {
      headers: { Cookie: cookieHeader },
    });
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body).toMatchObject({
      success: true,
      agents: expect.any(Array),
      count: expect.any(Number),
    });
  });

  test('frontend /agents renders without entering kind="error"', async ({ page, request }) => {
    const { cookieHeader } = await smokeLogin(request);
    await applySessionToPage(page, cookieHeader);
    await page.goto('/agents', { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('[data-slot="agents-library-view"]', { timeout: 30_000 });
    const errorSurface = await page
      .locator('[data-slot="agents-empty-state"][data-kind="error"]')
      .count();
    expect(errorSurface).toBe(0);
  });
});
