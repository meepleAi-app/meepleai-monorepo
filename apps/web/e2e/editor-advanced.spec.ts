/**
 * Editor Advanced E2E Tests - MIGRATED TO POM
 *
 * @see apps/web/e2e/pages/
 */

import { test, expect } from './fixtures/chromatic';
import { getTextMatcher } from './fixtures/i18n';
import { WaitHelper } from './helpers/WaitHelper';

test.describe('RuleSpecEditor E2E', () => {
  test.beforeEach(async ({ page }) => {
    // Mock API responses
    await page.route('**/api/v1/auth/me', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          user: {
            id: 'user-1',
            email: 'editor@test.com',
            displayName: 'Test Editor',
            role: 'Editor',
          },
          expiresAt: new Date(Date.now() + 3600_000).toISOString(),
        }),
      });
    });

    await page.route('**/api/v1/games/demo-chess/rulespec', async route => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            gameId: 'demo-chess',
            version: '1.0.0',
            createdAt: '2025-01-15T10:00:00Z',
            rules: [{ id: 'rule-1', text: 'Initial rule text' }],
          }),
        });
      } else if (route.request().method() === 'PUT') {
        const body = await route.request().postDataJSON();
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            ...body,
            version: '1.0.1',
          }),
        });
      }
    });
  });

  test('complete user flow: login → edit → auto-save → undo → manual save', async ({ page }) => {
    await page.goto('http://localhost:3000/editor?gameId=demo-chess');
    await page.waitForLoadState('networkidle');

    // Wait for editor to load
    await expect(page.getByText('Editor RuleSpec')).toBeVisible({ timeout: 10000 });

    // Verify initial content is loaded
    await expect(page.getByText(/demo-chess/i)).toBeVisible();

    // Switch to JSON mode if not already
    const textarea = page.locator('textarea');
    await expect(textarea).toBeVisible({ timeout: 5000 });

    // Edit content
    await textarea.fill(
      JSON.stringify(
        {
          gameId: 'demo-chess',
          version: '1.0.1',
          createdAt: '2025-01-15T10:00:00Z',
          rules: [{ id: 'rule-1', text: 'Updated rule text' }],
        },
        null,
        2
      )
    );
    await textarea.blur();

    // Verify unsaved changes indicator
    await expect(page.getByText(/unsaved|modifiche non salvate/i)).toBeVisible({ timeout: 2000 });

    // Wait for auto-save (2 second debounce)
    const waitHelper = new WaitHelper(page);
    await waitHelper.waitForNetworkIdle(5000);
    await expect(page.getByText(/auto.saved|auto.salvato/i)).toBeVisible({ timeout: 5000 });

    // Test undo functionality (use force: true to handle nextjs-portal overlay)
    const undoButton = page.getByRole('button', { name: /undo|annulla/i });
    await undoButton.click({ force: true });

    // Verify content reverted
    const content = await textarea.inputValue();
    expect(content).toContain('"version": "1.0.0"');

    // Make another edit
    await textarea.fill(
      JSON.stringify(
        {
          gameId: 'demo-chess',
          version: '1.0.2',
          createdAt: '2025-01-15T10:00:00Z',
          rules: [{ id: 'rule-1', text: 'Final rule text' }],
        },
        null,
        2
      )
    );
    await textarea.blur();

    // Manual save (use force: true to handle nextjs-portal overlay)
    const saveButton = page.getByRole('button', { name: getTextMatcher('editor.save') });
    await expect(saveButton).toBeEnabled({ timeout: 2000 });
    await saveButton.click({ force: true });

    // Verify save success
    await expect(page.getByText(/successfully saved|salvato con successo/i)).toBeVisible({
      timeout: 5000,
    });
  });

  test('handles network failure gracefully', async ({ page }) => {
    // Mock network failure for save
    await page.route('**/api/v1/games/demo-chess/rulespec', async route => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            gameId: 'demo-chess',
            version: '1.0.0',
            createdAt: '2025-01-15T10:00:00Z',
            rules: [{ id: 'rule-1', text: 'Initial text' }],
          }),
        });
      } else if (route.request().method() === 'PUT') {
        await route.fulfill({
          status: 500,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Internal server error' }),
        });
      }
    });

    await page.goto('http://localhost:3000/editor?gameId=demo-chess');
    await page.waitForLoadState('networkidle');
    await expect(page.getByText('Editor RuleSpec')).toBeVisible({ timeout: 10000 });

    const textarea = page.locator('textarea');
    await textarea.fill(
      JSON.stringify(
        {
          gameId: 'demo-chess',
          version: '1.0.1',
          createdAt: '2025-01-15T10:00:00Z',
          rules: [{ id: 'rule-1', text: 'Updated text' }],
        },
        null,
        2
      )
    );
    await textarea.blur();

    const saveButton = page.getByRole('button', { name: getTextMatcher('editor.save') });
    await saveButton.click({ force: true });

    // Should show error message
    await expect(page.getByText(/error|impossibile|failed/i)).toBeVisible({ timeout: 5000 });
  });

  test('validates JSON content in real-time', async ({ page }) => {
    await page.goto('http://localhost:3000/editor?gameId=demo-chess');
    await page.waitForLoadState('networkidle');
    await expect(page.getByText('Editor RuleSpec')).toBeVisible({ timeout: 10000 });

    const textarea = page.locator('textarea');

    // Enter invalid JSON
    await textarea.fill('{ invalid json syntax');
    await textarea.blur();

    // Should show validation error
    await expect(page.getByText(/✗|invalid/i)).toBeVisible({ timeout: 2000 });
    await expect(page.getByText(/JSON.*invalid|JSON non valido/i)).toBeVisible();

    // Save button should be disabled
    const saveButton = page.getByRole('button', { name: getTextMatcher('editor.save') });
    await expect(saveButton).toBeDisabled();
  });

  test('keyboard shortcuts work correctly', async ({ page }) => {
    await page.goto('http://localhost:3000/editor?gameId=demo-chess');
    await page.waitForLoadState('networkidle');
    await expect(page.getByText('Editor RuleSpec')).toBeVisible({ timeout: 10000 });

    const textarea = page.locator('textarea');

    // Make an edit
    await textarea.fill(
      JSON.stringify(
        {
          gameId: 'demo-chess',
          version: '2.0.0',
          createdAt: '2025-01-15T10:00:00Z',
          rules: [{ id: 'rule-1', text: 'New text' }],
        },
        null,
        2
      )
    );
    await textarea.blur();

    // Use Ctrl+Z to undo
    await page.keyboard.press('Control+z');

    const content = await textarea.inputValue();
    expect(content).toContain('"version": "1.0.0"');

    // Use Ctrl+Y to redo
    await page.keyboard.press('Control+y');

    const redoneContent = await textarea.inputValue();
    expect(redoneContent).toContain('"version": "2.0.0"');
  });

  test('handles session expiry during operation', async ({ page }) => {
    let requestCount = 0;

    await page.route('**/api/v1/auth/me', async route => {
      requestCount++;
      if (requestCount === 1) {
        // First request succeeds
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            user: {
              id: 'user-1',
              email: 'editor@test.com',
              role: 'Editor',
            },
            expiresAt: new Date(Date.now() + 3600_000).toISOString(),
          }),
        });
      } else {
        // Subsequent requests fail (session expired)
        await route.fulfill({
          status: 401,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Unauthorized' }),
        });
      }
    });

    await page.goto('http://localhost:3000/editor?gameId=demo-chess');
    await page.waitForLoadState('networkidle');
    await expect(page.getByText('Editor RuleSpec')).toBeVisible({ timeout: 10000 });

    // Reload page to trigger session check
    await page.reload();

    // Should show login prompt
    await expect(page.getByText(/login required|devi effettuare l'accesso/i)).toBeVisible({
      timeout: 10000,
    });
  });

  test('preserves content during view mode toggle', async ({ page }) => {
    await page.goto('http://localhost:3000/editor?gameId=demo-chess');
    await page.waitForLoadState('networkidle');
    await expect(page.getByText('Editor RuleSpec')).toBeVisible({ timeout: 10000 });

    const textarea = page.locator('textarea');
    const originalContent = await textarea.inputValue();

    // Toggle to rich mode (if toggle button exists, use force: true to handle nextjs-portal overlay)
    const toggleButton = page.getByTestId('view-toggle').or(page.getByText(/Switch to|toggle/i));
    if ((await toggleButton.count()) > 0) {
      await toggleButton.click({ force: true });

      // Toggle back to JSON (use force: true to handle nextjs-portal overlay)
      await toggleButton.click({ force: true });

      // Content should be preserved
      const newContent = await textarea.inputValue();
      expect(newContent).toBe(originalContent);
    }
  });
});
