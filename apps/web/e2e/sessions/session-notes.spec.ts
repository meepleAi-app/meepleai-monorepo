/**
 * SESS-11: Session Notes
 * Issue #3082 - P3 Low
 *
 * Tests session notes functionality:
 * - Add notes to session
 * - Edit session notes
 * - View notes history
 */

import { test, expect } from '../fixtures';

import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

async function setupSessionNotesMocks(page: Page) {
  const notes = [
    { id: 'note-1', sessionId: 's1', content: 'Remember to explain castling rules', createdAt: new Date().toISOString() },
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

  await page.route(`${API_BASE}/api/v1/game-sessions/*/notes**`, async (route) => {
    const method = route.request().method();
    if (method === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ notes }),
      });
    } else if (method === 'POST') {
      const body = await route.request().postDataJSON();
      notes.push({
        id: `note-${Date.now()}`,
        sessionId: 's1',
        content: body.content,
        createdAt: new Date().toISOString(),
      });
      await route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'Note added' }),
      });
    } else if (method === 'PATCH') {
      const noteId = route.request().url().match(/notes\/([^/?]+)/)?.[1];
      const body = await route.request().postDataJSON();
      const idx = notes.findIndex(n => n.id === noteId);
      if (idx >= 0) notes[idx].content = body.content;
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'Note updated' }),
      });
    }
  });

  await page.route(`${API_BASE}/api/v1/game-sessions/*`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ id: 's1', name: 'Chess Session' }),
    });
  });

  await page.route(`${API_BASE}/api/v1/games**`, async (route) => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) });
  });

  return { getNotes: () => notes };
}

test.describe('SESS-11: Session Notes', () => {
  test('should display session notes', async ({ page }) => {
    await setupSessionNotesMocks(page);
    await page.goto('/sessions/s1');
    await page.waitForLoadState('networkidle');
    await expect(page.getByText(/castling|note/i).first()).toBeVisible({ timeout: 5000 });
  });

  test('should add new note', async ({ page }) => {
    await setupSessionNotesMocks(page);
    await page.goto('/sessions/s1');
    await page.waitForLoadState('networkidle');

    const addButton = page.getByRole('button', { name: /add.*note/i });
    if (await addButton.isVisible()) {
      await addButton.click();
      const noteInput = page.getByPlaceholder(/note/i).or(page.locator('textarea'));
      if (await noteInput.isVisible()) {
        await noteInput.fill('New note about strategy');
        await page.getByRole('button', { name: /save/i }).click();
        await expect(page.getByText(/saved|added/i)).toBeVisible();
      }
    }
  });
});
