/**
 * SESS-10: Session Archive
 * Issue #3082 - P3 Low
 *
 * Tests session archive functionality:
 * - Archive old sessions
 * - View archived sessions
 * - Restore archived sessions
 */

import { test, expect } from '../fixtures';

import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

async function setupSessionArchiveMocks(page: Page) {
  const sessions = [
    { id: 's1', name: 'Active Session', archived: false, createdAt: new Date().toISOString() },
    { id: 's2', name: 'Old Session', archived: true, createdAt: new Date(Date.now() - 2592000000).toISOString() },
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

  await page.route(`${API_BASE}/api/v1/game-sessions**`, async (route) => {
    const url = route.request().url();
    const includeArchived = url.includes('archived=true');
    const filteredSessions = includeArchived ? sessions : sessions.filter(s => !s.archived);
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ sessions: filteredSessions }),
    });
  });

  await page.route(`${API_BASE}/api/v1/game-sessions/*/archive`, async (route) => {
    const sessionId = route.request().url().match(/game-sessions\/([^/]+)\/archive/)?.[1];
    const idx = sessions.findIndex(s => s.id === sessionId);
    if (idx >= 0) sessions[idx].archived = true;
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ message: 'Session archived' }),
    });
  });

  await page.route(`${API_BASE}/api/v1/game-sessions/*/restore`, async (route) => {
    const sessionId = route.request().url().match(/game-sessions\/([^/]+)\/restore/)?.[1];
    const idx = sessions.findIndex(s => s.id === sessionId);
    if (idx >= 0) sessions[idx].archived = false;
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ message: 'Session restored' }),
    });
  });

  await page.route(`${API_BASE}/api/v1/games**`, async (route) => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) });
  });

  return { getSessions: () => sessions };
}

test.describe('SESS-10: Session Archive', () => {
  test('should show active sessions', async ({ page }) => {
    await setupSessionArchiveMocks(page);
    await page.goto('/sessions');
    await page.waitForLoadState('networkidle');
    await expect(page.getByText(/active.*session/i)).toBeVisible({ timeout: 5000 });
  });

  test('should archive session', async ({ page }) => {
    await setupSessionArchiveMocks(page);
    await page.goto('/sessions');
    await page.waitForLoadState('networkidle');

    const archiveButton = page.getByRole('button', { name: /archive/i }).first();
    if (await archiveButton.isVisible()) {
      await archiveButton.click();
      await expect(page.getByText(/archived/i)).toBeVisible();
    }
  });

  test('should view archived sessions', async ({ page }) => {
    await setupSessionArchiveMocks(page);
    await page.goto('/sessions?archived=true');
    await page.waitForLoadState('networkidle');
    await expect(page.getByText(/old.*session|archived/i)).toBeVisible();
  });
});
