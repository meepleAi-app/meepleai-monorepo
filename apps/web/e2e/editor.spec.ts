/**
 * Editor E2E Tests - MIGRATED TO POM
 *
 * @see apps/web/e2e/pages/
 */

import { test, expect, type Page } from './fixtures/chromatic';
import { getTextMatcher } from './fixtures/i18n';

/**
 * E2E Tests for RuleSpec Editor (EDIT-01)
 *
 * Feature: RuleSpec Editor with Undo/Redo and Validation
 * As an Admin or Editor
 * I want to edit RuleSpecs with a visual interface
 * So that I can maintain game rules efficiently
 */

test.describe('RuleSpec Editor', () => {
  let page: Page;

  test.beforeEach(async ({ browser }) => {
    const context = await browser.newContext();
    page = await context.newPage();
  });

  test.afterEach(async () => {
    await page.close();
  });

  /**
   * Helper: Register and login with specific role
   */
  async function loginAs(role: 'Admin' | 'Editor' | 'User') {
    await page.goto('http://localhost:3000/');
    await page.waitForLoadState('networkidle');

    const email = `${role.toLowerCase()}-e2e-${Date.now()}@example.com`;
    const password = 'TestPassword123!';

    // Register
    await page.fill('input[type="email"]', email);
    await page.fill('input[type="password"]', password);
    await page.fill('input[placeholder*="Name" i]', `${role} Test User`);
    await page.click('button:has-text("Register")', { force: true });

    // Wait for successful registration
    await page.waitForURL('http://localhost:3000/chat', { timeout: 5000 });

    return { email, password };
  }

  /**
   * Helper: Create a test game
   */
  async function createTestGame(gameId: string) {
    // Navigate to upload page and create game
    await page.goto('http://localhost:3000/upload');
    await page.waitForLoadState('networkidle');
    await page.fill('input[placeholder*="game" i]', gameId);
    await page.click('button:has-text("Create Game")', { force: true });
    await page.waitForSelector(`text=Game "${gameId}" created`, { timeout: 5000 });
  }

  /**
   * Helper: Get the editor textarea
   */
  async function getEditorTextarea() {
    return page.locator('textarea[spellcheck="false"]');
  }

  test.describe('Access Control', () => {
    test('allows Admin to access editor', async () => {
      await loginAs('Admin');
      await page.goto('http://localhost:3000/editor?gameId=demo-chess');
      await page.waitForLoadState('networkidle');

      await expect(page.locator('h1:has-text("Editor RuleSpec")')).toBeVisible();
      await expect(page.locator('text=Game: demo-chess')).toBeVisible();
    });

    test('allows Editor to access editor', async () => {
      await loginAs('Editor');
      await page.goto('http://localhost:3000/editor?gameId=demo-chess');
      await page.waitForLoadState('networkidle');

      await expect(page.locator('h1:has-text("Editor RuleSpec")')).toBeVisible();
    });

    test('blocks User from accessing editor', async () => {
      await loginAs('User');
      await page.goto('http://localhost:3000/editor?gameId=demo-chess');
      await page.waitForLoadState('networkidle');

      await expect(page.getByText(getTextMatcher('common.permissionDenied'))).toBeVisible();
    });

    test('redirects unauthenticated users to home', async () => {
      await page.goto('http://localhost:3000/editor?gameId=demo-chess');
      await page.waitForLoadState('networkidle');

      await expect(page.getByText(getTextMatcher('common.loginRequired'))).toBeVisible();
      await expect(page.getByRole('link', { name: getTextMatcher('nav.home') })).toBeVisible();
    });
  });

  test.describe('Editor Loading and Display', () => {
    test.beforeEach(async () => {
      await loginAs('Admin');
    });

    test('loads existing RuleSpec for demo-chess', async () => {
      await page.goto('http://localhost:3000/editor?gameId=demo-chess');
      await page.waitForLoadState('networkidle');

      const textarea = await getEditorTextarea();
      await expect(textarea).toBeVisible();

      const content = await textarea.inputValue();
      expect(content).toContain('"gameId": "demo-chess"');
      expect(content).toContain('"version"');
      expect(content).toContain('"rules"');
    });

    test('shows validation indicator for valid JSON', async () => {
      await page.goto('http://localhost:3000/editor?gameId=demo-chess');
      await page.waitForLoadState('networkidle');

      await expect(page.getByText(getTextMatcher('editor.validJson'))).toBeVisible();
      await expect(page.getByRole('button', { name: getTextMatcher('editor.save') })).toBeEnabled();
    });

    test('displays preview panel with rule count', async () => {
      await page.goto('http://localhost:3000/editor?gameId=demo-chess');
      await page.waitForLoadState('networkidle');

      await expect(page.locator('h2:has-text("Preview")')).toBeVisible();
      await expect(page.getByText(getTextMatcher('editor.ruleCount'))).toBeVisible();
    });

    test('shows error when gameId is missing', async () => {
      await page.goto('http://localhost:3000/editor');
      await page.waitForLoadState('networkidle');

      await expect(page.getByText(getTextMatcher('editor.missingGameId'))).toBeVisible();
    });

    test('shows error when RuleSpec not found', async () => {
      const nonExistentGameId = `nonexistent-${Date.now()}`;
      await page.goto(`http://localhost:3000/editor?gameId=${nonExistentGameId}`);
      await page.waitForLoadState('networkidle');

      // Should show error or loading state
      await expect(
        page
          .getByText(getTextMatcher('editor.notFound'))
          .or(page.getByText(getTextMatcher('common.loading')))
      ).toBeVisible({ timeout: 10000 });
    });
  });

  test.describe('JSON Validation', () => {
    test.beforeEach(async () => {
      await loginAs('Admin');
      await page.goto('http://localhost:3000/editor?gameId=demo-chess');
      await page.waitForLoadState('networkidle');
    });

    test('detects invalid JSON syntax', async () => {
      const textarea = await getEditorTextarea();

      await textarea.fill('{ invalid json');

      await expect(page.locator('text=/Expected property name/i')).toBeVisible();
      await expect(
        page.getByRole('button', { name: getTextMatcher('editor.save') })
      ).toBeDisabled();
    });

    test('validates required gameId field', async () => {
      const textarea = await getEditorTextarea();

      const invalidSpec = JSON.stringify(
        {
          version: '1.0.0',
          createdAt: new Date().toISOString(),
          rules: [],
        },
        null,
        2
      );

      await textarea.fill(invalidSpec);

      await expect(page.getByText(/gameId.*required/i)).toBeVisible();
      await expect(
        page.getByRole('button', { name: getTextMatcher('editor.save') })
      ).toBeDisabled();
    });

    test('validates required version field', async () => {
      const textarea = await getEditorTextarea();

      const invalidSpec = JSON.stringify(
        {
          gameId: 'test',
          createdAt: new Date().toISOString(),
          rules: [],
        },
        null,
        2
      );

      await textarea.fill(invalidSpec);

      await expect(page.getByText(/version.*required/i)).toBeVisible();
      await expect(
        page.getByRole('button', { name: getTextMatcher('editor.save') })
      ).toBeDisabled();
    });

    test('validates rules array structure', async () => {
      const textarea = await getEditorTextarea();

      const invalidSpec = JSON.stringify(
        {
          gameId: 'test',
          version: '1.0.0',
          createdAt: new Date().toISOString(),
          rules: 'not an array',
        },
        null,
        2
      );

      await textarea.fill(invalidSpec);

      await expect(page.getByText(/rules.*array/i)).toBeVisible();
      await expect(
        page.getByRole('button', { name: getTextMatcher('editor.save') })
      ).toBeDisabled();
    });

    test('validates rule atom required fields', async () => {
      const textarea = await getEditorTextarea();

      const invalidSpec = JSON.stringify(
        {
          gameId: 'test',
          version: '1.0.0',
          createdAt: new Date().toISOString(),
          rules: [{ text: 'Missing id field' }],
        },
        null,
        2
      );

      await textarea.fill(invalidSpec);

      await expect(page.getByText(/rules.*id.*required/i)).toBeVisible();
      await expect(
        page.getByRole('button', { name: getTextMatcher('editor.save') })
      ).toBeDisabled();
    });

    test('shows valid state after fixing invalid JSON', async () => {
      const textarea = await getEditorTextarea();

      // First make it invalid
      await textarea.fill('{ invalid');
      await expect(page.locator('text=/Expected property name/i')).toBeVisible();

      // Then fix it
      const validSpec = JSON.stringify(
        {
          gameId: 'demo-chess',
          version: '1.0.0',
          createdAt: new Date().toISOString(),
          rules: [{ id: 'r1', text: 'Valid rule' }],
        },
        null,
        2
      );

      await textarea.fill(validSpec);

      await expect(page.getByText(getTextMatcher('editor.validJson'))).toBeVisible();
      await expect(page.getByRole('button', { name: getTextMatcher('editor.save') })).toBeEnabled();
    });
  });

  test.describe('Save Functionality', () => {
    test.beforeEach(async () => {
      await loginAs('Admin');
    });

    test('successfully saves valid RuleSpec', async ({ page: testPage }) => {
      // Use a unique game ID for this test
      const gameId = `save-test-${Date.now()}`;

      // Create game first
      await createTestGame(gameId);

      // Navigate to editor
      await testPage.goto(`http://localhost:3000/editor?gameId=${gameId}`);

      const textarea = await testPage.locator('textarea[spellcheck="false"]');

      const newSpec = JSON.stringify(
        {
          gameId,
          version: 'v1',
          createdAt: new Date().toISOString(),
          rules: [
            { id: 'r1', text: 'Test rule 1', section: 'Setup', page: '1', line: '1' },
            { id: 'r2', text: 'Test rule 2', section: 'Gameplay', page: '2', line: '5' },
          ],
        },
        null,
        2
      );

      await textarea.fill(newSpec);

      // Save (use force: true to handle nextjs-portal overlay)
      const saveButton = testPage.getByRole('button', { name: getTextMatcher('editor.save') });
      await saveButton.click({ force: true });

      // Wait for success message
      await expect(testPage.getByText(/successfully saved|salvato con successo/i)).toBeVisible({
        timeout: 10000,
      });
    });

    test('shows error when save fails', async () => {
      await page.goto('http://localhost:3000/editor?gameId=demo-chess');
      await page.waitForLoadState('networkidle');

      const textarea = await getEditorTextarea();

      // Try to save with duplicate version (assuming v0-demo already exists)
      const content = await textarea.inputValue();
      const spec = JSON.parse(content);
      spec.version = 'v0-demo'; // This should already exist

      await textarea.fill(JSON.stringify(spec, null, 2));

      const saveButton = page.getByRole('button', { name: getTextMatcher('editor.save') });
      await saveButton.click({ force: true });

      // Should show error (either conflict or other error)
      await expect(page.getByText(/error/i).or(page.locator('[style*="fce4e4"]'))).toBeVisible({
        timeout: 10000,
      });
    });

    test('disables save button while saving', async () => {
      await page.goto('http://localhost:3000/editor?gameId=demo-chess');
      await page.waitForLoadState('networkidle');

      const textarea = await getEditorTextarea();
      const content = await textarea.inputValue();
      const spec = JSON.parse(content);
      spec.version = `v-test-${Date.now()}`;

      await textarea.fill(JSON.stringify(spec, null, 2));

      const saveButton = page.getByRole('button', { name: getTextMatcher('editor.save') });
      await saveButton.click({ force: true });

      // Button should show "Salvataggio..." and be disabled temporarily
      await expect(page.getByRole('button', { name: /saving|salvataggio/i })).toBeVisible();
    });
  });

  test.describe('Undo/Redo Functionality', () => {
    test.beforeEach(async () => {
      await loginAs('Admin');
      await page.goto('http://localhost:3000/editor?gameId=demo-chess');
      await page.waitForLoadState('networkidle');
    });

    test('undo button is disabled initially', async () => {
      const undoButton = page.getByRole('button', { name: /undo|annulla/i });
      await expect(undoButton).toBeDisabled();
    });

    test('redo button is disabled initially', async () => {
      const redoButton = page.getByRole('button', { name: /redo|ripeti/i });
      await expect(redoButton).toBeDisabled();
    });

    test('enables undo after making a change', async () => {
      const textarea = await getEditorTextarea();
      const originalContent = await textarea.inputValue();

      // Make a change
      const spec = JSON.parse(originalContent);
      spec.version = 'v-modified';
      await textarea.fill(JSON.stringify(spec, null, 2));

      // Blur to add to history
      await textarea.blur();

      // Undo should be enabled
      const undoButton = page.getByRole('button', { name: /undo|annulla/i });
      await expect(undoButton).toBeEnabled();
    });

    test('restores previous content when undo is clicked', async () => {
      const textarea = await getEditorTextarea();
      const originalContent = await textarea.inputValue();

      // Make a change
      const spec = JSON.parse(originalContent);
      spec.version = 'v-modified';
      const modifiedContent = JSON.stringify(spec, null, 2);

      await textarea.fill(modifiedContent);
      await textarea.blur();

      // Verify change applied
      expect(await textarea.inputValue()).toBe(modifiedContent);

      // Undo (use force: true to handle nextjs-portal overlay)
      const undoButton = page.getByRole('button', { name: /undo|annulla/i });
      await undoButton.click({ force: true });

      // Should restore original
      await expect(textarea).toHaveValue(originalContent);
    });

    test('enables redo after undo', async () => {
      const textarea = await getEditorTextarea();
      const originalContent = await textarea.inputValue();

      // Make a change
      const spec = JSON.parse(originalContent);
      spec.version = 'v-modified';
      await textarea.fill(JSON.stringify(spec, null, 2));
      await textarea.blur();

      // Undo (use force: true to handle nextjs-portal overlay)
      const undoButton = page.getByRole('button', { name: /undo|annulla/i });
      await undoButton.click({ force: true });

      // Redo should be enabled
      const redoButton = page.getByRole('button', { name: /redo|ripeti/i });
      await expect(redoButton).toBeEnabled();
    });

    test('restores undone content when redo is clicked', async () => {
      const textarea = await getEditorTextarea();
      const originalContent = await textarea.inputValue();

      // Make a change
      const spec = JSON.parse(originalContent);
      spec.version = 'v-modified';
      const modifiedContent = JSON.stringify(spec, null, 2);

      await textarea.fill(modifiedContent);
      await textarea.blur();

      // Undo (use force: true to handle nextjs-portal overlay)
      const undoButton = page.getByRole('button', { name: /undo|annulla/i });
      await undoButton.click({ force: true });

      // Redo (use force: true to handle nextjs-portal overlay)
      const redoButton = page.getByRole('button', { name: /redo|ripeti/i });
      await redoButton.click({ force: true });

      // Should restore modified content
      await expect(textarea).toHaveValue(modifiedContent);
    });

    test('supports multiple undo/redo operations', async () => {
      const textarea = await getEditorTextarea();
      const originalContent = await textarea.inputValue();
      const spec = JSON.parse(originalContent);

      // Make multiple changes
      const versions = ['v1', 'v2', 'v3'];
      for (const version of versions) {
        spec.version = version;
        await textarea.fill(JSON.stringify(spec, null, 2));
        await textarea.blur();
      }

      const undoButton = page.getByRole('button', { name: /undo|annulla/i });
      const redoButton = page.getByRole('button', { name: /redo|ripeti/i });

      // Undo all changes (use force: true to handle nextjs-portal overlay)
      for (let i = versions.length - 1; i >= 0; i--) {
        await undoButton.click({ force: true });
      }

      // Should be back to original
      await expect(textarea).toHaveValue(originalContent);

      // Redo all changes (use force: true to handle nextjs-portal overlay)
      for (const version of versions) {
        await redoButton.click({ force: true });
        const content = await textarea.inputValue();
        expect(JSON.parse(content).version).toBe(version);
      }
    });
  });

  test.describe('Preview Panel', () => {
    test.beforeEach(async () => {
      await loginAs('Admin');
      await page.goto('http://localhost:3000/editor?gameId=demo-chess');
      await page.waitForLoadState('networkidle');
    });

    test('shows game metadata in preview', async () => {
      await expect(page.getByText(/Game ID|GameID/i)).toBeVisible();
      await expect(page.getByText(/Version|Versione/i)).toBeVisible();
      await expect(page.getByText(/Created|Creato/i)).toBeVisible();
      await expect(page.getByText(getTextMatcher('editor.ruleCount'))).toBeVisible();
    });

    test('updates preview when JSON changes', async () => {
      const textarea = await getEditorTextarea();
      const originalContent = await textarea.inputValue();
      const spec = JSON.parse(originalContent);

      // Add a new rule
      spec.rules.push({
        id: 'new-rule',
        text: 'This is a brand new rule',
        section: 'Testing',
        page: '99',
        line: '1',
      });

      await textarea.fill(JSON.stringify(spec, null, 2));

      // Wait for preview to update
      await expect(page.getByText('This is a brand new rule')).toBeVisible({ timeout: 2000 });
      await expect(page.getByText(/Section|Sezione/i)).toBeVisible();
      await expect(page.getByText('99')).toBeVisible();
    });

    test('shows error message when JSON is invalid', async () => {
      const textarea = await getEditorTextarea();

      await textarea.fill('{ invalid json');

      await expect(page.getByText(/fix.*errors|correggi.*errori/i)).toBeVisible();
    });
  });

  test.describe('Navigation', () => {
    test.beforeEach(async () => {
      await loginAs('Admin');
      await page.goto('http://localhost:3000/editor?gameId=demo-chess');
      await page.waitForLoadState('networkidle');
    });

    test('navigates to version history', async () => {
      const historyLink = page.getByRole('link', { name: /version history|storico versioni/i });
      await historyLink.click({ force: true });

      await expect(page).toHaveURL(/\/versions\?gameId=demo-chess/);
    });

    test('navigates to home', async () => {
      const homeLink = page.getByRole('link', { name: getTextMatcher('nav.home') });
      await homeLink.click({ force: true });

      await expect(page).toHaveURL('http://localhost:3000/');
    });
  });
});
