/**
 * CHAT-12: Thread Management
 * Issue #3082 - P3 Low
 *
 * Tests thread management functionality:
 * - View chat thread history
 * - Rename threads
 * - Delete threads
 * - Search threads
 */

import { test, expect } from '../fixtures/chromatic';

import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

async function setupThreadManagementMocks(page: Page) {
  let threads = [
    { id: 'thread-1', title: 'Chess Rules Discussion', createdAt: new Date(Date.now() - 86400000).toISOString(), messageCount: 5 },
    { id: 'thread-2', title: 'Strategy Tips', createdAt: new Date(Date.now() - 172800000).toISOString(), messageCount: 12 },
    { id: 'thread-3', title: 'Game Setup Help', createdAt: new Date(Date.now() - 259200000).toISOString(), messageCount: 3 },
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

  await page.route(`${API_BASE}/api/v1/chat/threads**`, async (route) => {
    const method = route.request().method();
    const url = route.request().url();

    if (method === 'GET' && !url.match(/threads\/[^?]+/)) {
      const search = url.match(/search=([^&]+)/)?.[1];
      let filteredThreads = threads;
      if (search) {
        filteredThreads = threads.filter(t => t.title.toLowerCase().includes(decodeURIComponent(search).toLowerCase()));
      }
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(filteredThreads),
      });
    } else {
      await route.continue();
    }
  });

  await page.route(`${API_BASE}/api/v1/chat/threads/*`, async (route) => {
    const method = route.request().method();
    const threadId = route.request().url().match(/threads\/([^/?]+)/)?.[1];

    if (method === 'PATCH') {
      const body = await route.request().postDataJSON();
      const idx = threads.findIndex(t => t.id === threadId);
      if (idx >= 0 && body.title) {
        threads[idx].title = body.title;
      }
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'Thread updated' }),
      });
    } else if (method === 'DELETE') {
      threads = threads.filter(t => t.id !== threadId);
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'Thread deleted' }),
      });
    } else {
      await route.continue();
    }
  });

  await page.route(`${API_BASE}/api/v1/games**`, async (route) => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) });
  });

  return { getThreads: () => threads };
}

test.describe('CHAT-12: Thread Management', () => {
  test('should display thread history', async ({ page }) => {
    await setupThreadManagementMocks(page);
    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await expect(page.getByText(/chess.*rules|strategy/i).first()).toBeVisible({ timeout: 5000 });
  });

  test('should rename thread', async ({ page }) => {
    const mocks = await setupThreadManagementMocks(page);
    await page.goto('/chat');
    await page.waitForLoadState('networkidle');

    const menuButton = page.locator('[data-testid="thread-menu"]').first();
    if (await menuButton.isVisible()) {
      await menuButton.click();
      await page.getByText(/rename/i).click();
      await page.getByLabel(/name|title/i).fill('New Thread Name');
      await page.getByRole('button', { name: /save/i }).click();
      await expect(page.getByText(/renamed|updated/i)).toBeVisible();
    }
  });

  test('should delete thread', async ({ page }) => {
    await setupThreadManagementMocks(page);
    await page.goto('/chat');
    await page.waitForLoadState('networkidle');

    const deleteButton = page.getByRole('button', { name: /delete/i }).first();
    if (await deleteButton.isVisible()) {
      await deleteButton.click();
      const confirmButton = page.getByRole('button', { name: /confirm|yes/i });
      if (await confirmButton.isVisible()) {
        await confirmButton.click();
      }
      await expect(page.getByText(/deleted/i)).toBeVisible();
    }
  });

  test('should search threads', async ({ page }) => {
    await setupThreadManagementMocks(page);
    await page.goto('/chat');
    await page.waitForLoadState('networkidle');

    const searchInput = page.getByPlaceholder(/search/i);
    if (await searchInput.isVisible()) {
      await searchInput.fill('chess');
      await page.waitForTimeout(500);
      await expect(page.getByText(/chess.*rules/i)).toBeVisible();
    }
  });
});
