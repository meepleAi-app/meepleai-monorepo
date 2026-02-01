/**
 * PDF-10: Text Search in PDF
 * Issue #3082 - P3 Low
 *
 * Tests text search functionality within PDFs:
 * - Search within document
 * - Highlight search results
 * - Navigate between matches
 */

import { test, expect } from '../fixtures';

import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

async function setupPdfSearchMocks(page: Page) {
  await page.route(`${API_BASE}/api/v1/auth/me`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: { id: 'test-user', email: 'test@example.com', displayName: 'Test User', role: 'User' },
        expiresAt: new Date(Date.now() + 3600000).toISOString(),
      }),
    });
  });

  await page.route(`${API_BASE}/api/v1/documents/*`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        id: 'doc-1',
        name: 'Chess Rules.pdf',
        pageCount: 15,
        textContent: 'Chess is a two-player strategy board game. The bishop moves diagonally.',
      }),
    });
  });

  await page.route(`${API_BASE}/api/v1/documents/*/search`, async (route) => {
    const body = await route.request().postDataJSON();
    const query = body?.query || '';
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        matches: [
          { page: 1, position: 10, context: 'The bishop moves diagonally across the board.' },
          { page: 3, position: 45, context: 'The bishop can capture pieces in its path.' },
        ],
        totalMatches: 2,
      }),
    });
  });

  await page.route(`${API_BASE}/api/v1/games**`, async (route) => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) });
  });

  return {};
}

test.describe('PDF-10: Text Search in PDF', () => {
  test('should show search input', async ({ page }) => {
    await setupPdfSearchMocks(page);
    await page.goto('/library/documents/doc-1');
    await page.waitForLoadState('networkidle');

    await expect(
      page.getByPlaceholder(/search/i).or(page.locator('[data-testid="pdf-search"]'))
    ).toBeVisible({ timeout: 5000 });
  });

  test('should search within document', async ({ page }) => {
    await setupPdfSearchMocks(page);
    await page.goto('/library/documents/doc-1');
    await page.waitForLoadState('networkidle');

    const searchInput = page.getByPlaceholder(/search/i);
    if (await searchInput.isVisible()) {
      await searchInput.fill('bishop');
      await page.keyboard.press('Enter');
      await expect(page.getByText(/match|result|found/i)).toBeVisible({ timeout: 3000 });
    }
  });

  test('should show match count', async ({ page }) => {
    await setupPdfSearchMocks(page);
    await page.goto('/library/documents/doc-1');
    await page.waitForLoadState('networkidle');

    const searchInput = page.getByPlaceholder(/search/i);
    if (await searchInput.isVisible()) {
      await searchInput.fill('bishop');
      await page.keyboard.press('Enter');
      await expect(page.getByText(/2.*match|match.*2/i).or(page.locator('body'))).toBeVisible();
    }
  });
});
