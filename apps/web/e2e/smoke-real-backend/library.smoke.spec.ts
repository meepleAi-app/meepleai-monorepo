import { test, expect } from '@playwright/test';

import { smokeLogin, applySessionToPage } from './_helpers/auth';

const API_BASE = process.env.SMOKE_API_BASE ?? 'http://localhost:8080';

test.describe('SMOKE — /library real backend round-trip', () => {
  test('GET /api/v1/library returns paginated shape', async ({ request }) => {
    const { cookieHeader } = await smokeLogin(request);
    const res = await request.get(`${API_BASE}/api/v1/library?page=1&pageSize=20`, {
      headers: { Cookie: cookieHeader },
    });
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body).toMatchObject({
      items: expect.any(Array),
      page: 1,
      pageSize: 20,
      totalCount: expect.any(Number),
      totalPages: expect.any(Number),
      hasNextPage: expect.any(Boolean),
      hasPreviousPage: expect.any(Boolean),
    });
  });

  test('frontend /library renders without kind="error"', async ({ page, request }) => {
    const { cookieHeader } = await smokeLogin(request);
    await applySessionToPage(page, cookieHeader);
    await page.goto('/library', { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('[data-slot="library-hub-v2"]', { timeout: 30_000 });
    const errorState = await page.locator('[data-state="error"]').count();
    expect(errorState).toBe(0);
  });
});
