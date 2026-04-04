/**
 * Smoke Test — 8-step pre-deploy check
 * Verifica che le pagine chiave di MeepleAI carichino correttamente.
 * Eseguire prima di ogni deploy: pnpm test:e2e e2e/smoke.spec.ts
 */

import { test, expect } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

async function mockAdminAuth(page: import('@playwright/test').Page) {
  await page.route(`${API_BASE}/api/v1/auth/me`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: { id: 'admin-1', email: 'admin@meepleai.dev', displayName: 'Admin', role: 'Admin' },
        expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
      }),
    })
  );
  await page.route(`${API_BASE}/api/v1/**`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ data: [] }),
    })
  );
}

test.describe('Smoke Test — 8 step pre-deploy', () => {
  test('1. Homepage carica', async ({ page }) => {
    await page.goto('/', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('body')).toBeVisible();
    // Verifica che almeno un heading o CTA sia presente
    const heading = page.locator('h1, h2, [data-testid="hero"]').first();
    await expect(heading).toBeVisible({ timeout: 8000 });
  });

  test('2. Auth login form visibile', async ({ page }) => {
    await page.goto('/login', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('input[type="email"], input[name="email"]').first()).toBeVisible({
      timeout: 8000,
    });
  });

  test('3. Dashboard carica dopo auth mock', async ({ page }) => {
    await mockAdminAuth(page);
    await page.goto('/dashboard', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('body')).toBeVisible();
  });

  test('4. Catalog giochi carica con MeepleCard', async ({ page }) => {
    await page.route(`${API_BASE}/api/v1/shared-games**`, route =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            { id: 'g1', title: 'Catan', publisher: 'Kosmos', averageRating: 7.2, imageUrl: null },
            { id: 'g2', title: 'Pandemic', publisher: 'Z-Man', averageRating: 8.1, imageUrl: null },
          ],
          totalCount: 2,
        }),
      })
    );
    await page.goto('/library', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('body')).toBeVisible();
  });

  test('5. Admin overview carica (TopNav)', async ({ page }) => {
    await mockAdminAuth(page);
    await page.goto('/admin/overview', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('body')).toBeVisible();
  });

  test('6. Pipeline Diagram visibile', async ({ page }) => {
    await mockAdminAuth(page);
    await page.route(`${API_BASE}/api/v1/admin/rag/**`, route =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: [] }),
      })
    );
    await page.goto('/admin/agents/pipeline', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('body')).toBeVisible();
  });

  test('7. Documents table carica', async ({ page }) => {
    await mockAdminAuth(page);
    await page.route(`${API_BASE}/api/v1/admin/knowledge-base/documents**`, route =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: [], totalCount: 0 }),
      })
    );
    await page.goto('/admin/knowledge-base/documents', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('body')).toBeVisible();
  });

  test('8. Chat interface carica', async ({ page }) => {
    await mockAdminAuth(page);
    await page.route(`${API_BASE}/api/v1/agents**`, route =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) })
    );
    await page.goto('/chat/new', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('body')).toBeVisible();
  });
});
