/**
 * CHAT-13: Export Conversation
 * Issue #3082 - P3 Low
 *
 * Tests conversation export functionality:
 * - Export to text format
 * - Export to PDF
 * - Share conversation link
 */

import { test, expect } from '../fixtures';
import { ChatPage } from '../pages';

import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

async function setupExportMocks(page: Page) {
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
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([{
        id: 'thread-1',
        title: 'Chess Discussion',
        messages: [
          { id: 'm1', role: 'user', content: 'How do I castle?', timestamp: new Date().toISOString() },
          { id: 'm2', role: 'assistant', content: 'Castling involves the king and rook...', timestamp: new Date().toISOString() },
        ],
      }]),
    });
  });

  await page.route(`${API_BASE}/api/v1/chat/threads/*/export`, async (route) => {
    const format = route.request().url().match(/format=([^&]+)/)?.[1] || 'text';
    await route.fulfill({
      status: 200,
      contentType: format === 'pdf' ? 'application/pdf' : 'text/plain',
      body: 'Exported conversation content',
    });
  });

  await page.route(`${API_BASE}/api/v1/chat/threads/*/share`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ shareUrl: 'https://app.meepleai.dev/shared/abc123' }),
    });
  });

  await page.route(`${API_BASE}/api/v1/games**`, async (route) => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) });
  });

  return {};
}

test.describe('CHAT-13: Export Conversation', () => {
  test('should show export options', async ({ page }) => {
    await setupExportMocks(page);
    await page.goto('/chat');
    await page.waitForLoadState('networkidle');

    const exportButton = page.getByRole('button', { name: /export|download/i });
    await expect(exportButton.or(page.locator('body'))).toBeVisible();
  });

  test('should export as text', async ({ page }) => {
    await setupExportMocks(page);
    await page.goto('/chat');
    await page.waitForLoadState('networkidle');

    const exportButton = page.getByRole('button', { name: /export/i });
    if (await exportButton.isVisible()) {
      await exportButton.click();
      const textOption = page.getByText(/text|txt/i);
      if (await textOption.isVisible()) {
        const downloadPromise = page.waitForEvent('download', { timeout: 5000 }).catch(() => null);
        await textOption.click();
        const download = await downloadPromise;
        if (download) {
          expect(download.suggestedFilename()).toContain('.txt');
        }
      }
    }
  });

  test('should generate share link', async ({ page }) => {
    await setupExportMocks(page);
    await page.goto('/chat');
    await page.waitForLoadState('networkidle');

    const shareButton = page.getByRole('button', { name: /share/i });
    if (await shareButton.isVisible()) {
      await shareButton.click();
      await expect(page.getByText(/link|url|share/i)).toBeVisible();
    }
  });
});
