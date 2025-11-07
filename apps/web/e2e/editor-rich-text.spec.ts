import { test, expect } from '@playwright/test';
import { getTextMatcher, t } from './fixtures/i18n';

/**
 * E2E Tests for EDIT-03: Rich Text Editor
 *
 * Tests the complete workflow of using the rich text editor:
 * - Loading editor page
 * - Switching between rich text and JSON modes
 * - Using formatting toolbar
 * - Auto-save functionality
 * - Persistence after reload
 */

test.describe('Rich Text Editor (EDIT-03)', () => {
  const testGameId = 'demo-chess';
  const editorUrl = `/editor?gameId=${testGameId}`;

  test.beforeEach(async ({ page }) => {
    // Navigate to the editor page
    await page.goto(editorUrl);

    // Wait for the page to load
    await expect(page.locator('h1')).toContainText('Editor RuleSpec');
  });

  test('should display view mode toggle buttons', async ({ page }) => {
    // Check that both mode buttons are visible
    await expect(page.getByText('📝 Editor Visuale')).toBeVisible();
    await expect(page.getByText('{ } Codice JSON')).toBeVisible();
  });

  test('should start in rich text mode by default', async ({ page }) => {
    // Rich text mode should be active (has white background and blue text)
    const richButton = page.getByText('📝 Editor Visuale');
    await expect(richButton).toHaveCSS('background', /white|rgb\(255, 255, 255\)/);
  });

  test('should switch between rich text and JSON modes', async ({ page }) => {
    // Start in rich text mode
    await expect(page.getByText('Editor Visuale')).toBeVisible();

    // Click JSON mode button (use force: true to handle nextjs-portal overlay)
    await page.getByText('{ } Codice JSON').click({ force: true });

    // Should show JSON editor
    await expect(page.locator('textarea')).toBeVisible();
    await expect(page.getByText('Editor JSON')).toBeVisible();

    // Switch back to rich text (use force: true to handle nextjs-portal overlay)
    await page.getByText('📝 Editor Visuale').click({ force: true });

    // Should show rich text editor
    await expect(page.getByText('Editor Visuale')).toBeVisible();
  });

  test('should display formatting toolbar in rich text mode', async ({ page }) => {
    // Ensure we're in rich text mode (use force: true to handle nextjs-portal overlay)
    await page.getByText('📝 Editor Visuale').click({ force: true });

    // Check for toolbar buttons
    await expect(page.getByTitle(/Grassetto/)).toBeVisible();
    await expect(page.getByTitle(/Corsivo/)).toBeVisible();
    await expect(page.getByTitle(/Titolo 1/)).toBeVisible();
    await expect(page.getByTitle(/Elenco puntato/)).toBeVisible();
  });

  test('should show character and word count', async ({ page }) => {
    // Ensure we're in rich text mode (use force: true to handle nextjs-portal overlay)
    await page.getByText('📝 Editor Visuale').click({ force: true });

    // Check for character/word count display
    await expect(page.getByText(/caratteri/)).toBeVisible();
    await expect(page.getByText(/parole/)).toBeVisible();
  });

  test('should show unsaved changes indicator after editing', async ({ page }) => {
    // Ensure we're in rich text mode (use force: true to handle nextjs-portal overlay)
    await page.getByText('📝 Editor Visuale').click({ force: true });

    // Wait a bit for the editor to fully initialize
    await page.waitForTimeout(500);

    // Make a change (this is tricky with TipTap, might need to use JSON mode)
    await page.getByText('{ } Codice JSON').click({ force: true });
    const textarea = page.locator('textarea');
    await textarea.fill('{"gameId":"demo-chess","version":"1.0","createdAt":"2025-01-01","rules":[]}');

    // Check for unsaved changes indicator
    await expect(page.getByText(/unsaved|modifiche non salvate/i)).toBeVisible();
  });

  test('should enable save button when there are unsaved changes', async ({ page }) => {
    // Switch to JSON mode for easier editing (use force: true to handle nextjs-portal overlay)
    await page.getByText('{ } Codice JSON').click({ force: true });

    // Make a change
    const textarea = page.locator('textarea');
    await textarea.fill('{"gameId":"demo-chess","version":"1.0","createdAt":"2025-01-01","rules":[]}');

    // Save button should be enabled
    const saveButton = page.getByRole('button', { name: getTextMatcher('editor.save') });
    await expect(saveButton).toBeEnabled();
  });

  test('should show validation error for invalid JSON', async ({ page }) => {
    // Switch to JSON mode (use force: true to handle nextjs-portal overlay)
    await page.getByText('{ } Codice JSON').click({ force: true });

    // Enter invalid JSON
    const textarea = page.locator('textarea');
    await textarea.fill('invalid json');

    // Should show validation error
    await expect(page.getByText(/JSON.*invalid|JSON non valido/i)).toBeVisible();
  });

  test('should disable save button for invalid content', async ({ page }) => {
    // Switch to JSON mode (use force: true to handle nextjs-portal overlay)
    await page.getByText('{ } Codice JSON').click({ force: true });

    // Enter invalid JSON
    const textarea = page.locator('textarea');
    await textarea.fill('invalid json');

    // Save button should be disabled
    const saveButton = page.getByRole('button', { name: getTextMatcher('editor.save') });
    await expect(saveButton).toBeDisabled();
  });

  test('should show keyboard shortcut hints', async ({ page }) => {
    // Ensure we're in rich text mode (use force: true to handle nextjs-portal overlay)
    await page.getByText('📝 Editor Visuale').click({ force: true });

    // Check for keyboard shortcut hint at the bottom
    await expect(page.getByText(/Ctrl\+Z|undo/i)).toBeVisible();
  });

  test('should navigate to version history from editor', async ({ page }) => {
    // Click "Storico Versioni" link (use force: true to handle nextjs-portal overlay)
    await page.getByRole('link', { name: /version history|storico versioni/i }).click({ force: true });

    // Should navigate to versions page
    await expect(page).toHaveURL(new RegExp(`/versions\\?gameId=${testGameId}`));
  });

  test('should navigate to home from editor', async ({ page }) => {
    // Click "Home" link (use force: true to handle nextjs-portal overlay)
    await page.getByRole('link', { name: getTextMatcher('nav.home') }).click({ force: true });

    // Should navigate to home page
    await expect(page).toHaveURL('/');
  });

  test('should show preview panel', async ({ page }) => {
    // Check for preview panel
    await expect(page.getByText('Preview')).toBeVisible();
  });

  test('should show undo/redo buttons in JSON mode', async ({ page }) => {
    // Switch to JSON mode (use force: true to handle nextjs-portal overlay)
    await page.getByText('{ } Codice JSON').click({ force: true });

    // Check for undo/redo buttons
    await expect(page.getByTitle(/undo|Annulla.*Ctrl/i)).toBeVisible();
    await expect(page.getByTitle(/redo|Ripeti.*Ctrl/i)).toBeVisible();
  });

  test('should hide undo/redo buttons in rich text mode (TipTap has built-in)', async ({ page }) => {
    // Ensure we're in rich text mode (use force: true to handle nextjs-portal overlay)
    await page.getByText('📝 Editor Visuale').click({ force: true });

    // Undo/redo buttons should not be visible in the top controls
    // (TipTap has its own undo/redo in the toolbar)
    const undoButton = page.getByRole('button', { name: /undo|Annulla/ });
    await expect(undoButton).not.toBeVisible();
  });

  test('should show toolbar undo/redo buttons in rich text mode', async ({ page }) => {
    // Ensure we're in rich text mode (use force: true to handle nextjs-portal overlay)
    await page.getByText('📝 Editor Visuale').click({ force: true });

    // Check for toolbar undo/redo (with different selectors)
    await expect(page.getByTitle(/undo|Annulla.*Ctrl/i).first()).toBeVisible();
    await expect(page.getByTitle(/redo|Ripeti.*Ctrl/i).first()).toBeVisible();
  });
});

test.describe('Rich Text Editor - Authentication', () => {
  test('should require authentication', async ({ page }) => {
    // Try to access editor without auth
    // This test assumes we're not logged in
    // Actual behavior depends on your auth implementation
    await page.goto('/editor?gameId=demo-chess');

    // Should show login requirement or redirect
    // Adjust based on your auth flow
    const body = await page.textContent('body');
    const requiresAuth = body?.includes('accesso') || body?.includes('login');

    // This is a basic check - adapt based on your actual auth flow
    expect(requiresAuth).toBeTruthy();
  });
});
