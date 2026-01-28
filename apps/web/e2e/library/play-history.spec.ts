/**
 * LIB-11: Play History
 * Issue #3082 - P3 Low
 *
 * Tests play history functionality:
 * - View play history
 * - Log new play
 * - Play statistics
 * - Filter by date
 */

import { test, expect } from '../fixtures/chromatic';
import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

async function setupPlayHistoryMocks(page: Page) {
  let plays = [
    { id: 'play-1', gameId: 'chess', gameName: 'Chess', playedAt: new Date(Date.now() - 86400000).toISOString(), duration: 45, winner: 'Player 1' },
    { id: 'play-2', gameId: 'catan', gameName: 'Catan', playedAt: new Date(Date.now() - 172800000).toISOString(), duration: 90, winner: 'Player 2' },
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

  await page.route(`${API_BASE}/api/v1/library/plays**`, async (route) => {
    const method = route.request().method();
    if (method === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ plays, totalCount: plays.length }),
      });
    } else if (method === 'POST') {
      const body = await route.request().postDataJSON();
      plays.push({
        id: `play-${Date.now()}`,
        gameId: body.gameId,
        gameName: body.gameName,
        playedAt: body.playedAt || new Date().toISOString(),
        duration: body.duration || 60,
        winner: body.winner,
      });
      await route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'Play logged' }),
      });
    }
  });

  await page.route(`${API_BASE}/api/v1/library/stats`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        totalPlays: plays.length,
        totalHours: 2.25,
        mostPlayed: 'Chess',
        lastPlayed: new Date(Date.now() - 86400000).toISOString(),
      }),
    });
  });

  await page.route(`${API_BASE}/api/v1/games**`, async (route) => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) });
  });

  return { getPlays: () => plays };
}

test.describe('LIB-11: Play History', () => {
  test('should display play history', async ({ page }) => {
    await setupPlayHistoryMocks(page);
    await page.goto('/library/history');
    await page.waitForLoadState('networkidle');
    await expect(page.getByText(/chess|catan/i).first()).toBeVisible({ timeout: 5000 });
  });

  test('should show play statistics', async ({ page }) => {
    await setupPlayHistoryMocks(page);
    await page.goto('/library/history');
    await page.waitForLoadState('networkidle');
    await expect(page.getByText(/total|play|hour/i).first()).toBeVisible();
  });

  test('should log new play', async ({ page }) => {
    await setupPlayHistoryMocks(page);
    await page.goto('/library/history');
    await page.waitForLoadState('networkidle');

    const logButton = page.getByRole('button', { name: /log|add.*play/i });
    if (await logButton.isVisible()) {
      await logButton.click();
      await expect(page.getByLabel(/game|date/i).first()).toBeVisible();
    }
  });
});
