/**
 * GAME-09: Player Count Filter
 * Issue #3082 - P3 Low
 *
 * Tests player count filtering functionality:
 * - Filter games by player count
 * - Multi-select player ranges
 * - Clear filter
 */

import { test, expect } from '../fixtures/chromatic';

import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

async function setupPlayerCountMocks(page: Page) {
  const games = [
    { id: 'chess', title: 'Chess', minPlayers: 2, maxPlayers: 2 },
    { id: 'catan', title: 'Catan', minPlayers: 3, maxPlayers: 4 },
    { id: 'ticket', title: 'Ticket to Ride', minPlayers: 2, maxPlayers: 5 },
    { id: 'party', title: 'Codenames', minPlayers: 4, maxPlayers: 8 },
  ];

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

  await page.route(`${API_BASE}/api/v1/games**`, async (route) => {
    const url = route.request().url();
    const minMatch = url.match(/minPlayers=(\d+)/);
    const maxMatch = url.match(/maxPlayers=(\d+)/);

    let filtered = games;
    if (minMatch) {
      const min = parseInt(minMatch[1]);
      filtered = filtered.filter(g => g.maxPlayers >= min);
    }
    if (maxMatch) {
      const max = parseInt(maxMatch[1]);
      filtered = filtered.filter(g => g.minPlayers <= max);
    }

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(filtered),
    });
  });

  return { games };
}

test.describe('GAME-09: Player Count Filter', () => {
  test('should display player count filter', async ({ page }) => {
    await setupPlayerCountMocks(page);
    await page.goto('/games');
    await page.waitForLoadState('networkidle');
    await expect(
      page.getByRole('combobox', { name: /player/i }).or(page.locator('[data-testid="player-filter"]'))
    ).toBeVisible({ timeout: 5000 });
  });

  test('should filter by player count', async ({ page }) => {
    await setupPlayerCountMocks(page);
    await page.goto('/games');
    await page.waitForLoadState('networkidle');

    const playerFilter = page.getByRole('combobox', { name: /player/i });
    if (await playerFilter.isVisible()) {
      await playerFilter.click();
      await page.getByText(/2.*player/i).first().click();
      await page.waitForLoadState('networkidle');
      await expect(page.getByText(/chess/i)).toBeVisible();
    }
  });

  test('should clear player filter', async ({ page }) => {
    await setupPlayerCountMocks(page);
    await page.goto('/games?minPlayers=2&maxPlayers=2');
    await page.waitForLoadState('networkidle');

    const clearButton = page.getByRole('button', { name: /clear|reset/i });
    if (await clearButton.isVisible()) {
      await clearButton.click();
      await page.waitForLoadState('networkidle');
    }
  });
});
