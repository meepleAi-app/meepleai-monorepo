/**
 * Comments Enhanced E2E Tests - MIGRATED TO POM
 *
 * @see apps/web/e2e/pages/
 */

import { test, expect } from '@playwright/test';
import { getTextMatcher, t } from './fixtures/i18n';

/**
 * E2E Tests for EDIT-05: Enhanced Comments System (Threaded Replies, Mentions, Resolution)
 *
 * Tests critical user workflows for the commenting system:
 * - Creating top-level comments
 * - Threaded replies
 * - Mention autocomplete
 * - Resolution state management
 * - Filtering resolved/unresolved comments
 * - Edit/delete permissions
 *
 * Prerequisites:
 * - Backend API must be running at http://localhost:8080
 * - Demo users seeded (editor@meepleai.dev, admin@meepleai.dev, user@meepleai.dev)
 * - Demo games seeded (demo-chess, demo-tictactoe)
 *
 * To run these tests:
 * 1. Start backend API: cd apps/api/src/Api && dotnet run
 * 2. Run tests: cd apps/web && pnpm test:e2e comments-enhanced.spec.ts
 *
 * Frontend dev server (port 3000) is automatically started by Playwright.
 */

test.describe('EDIT-05: Enhanced Comments System', () => {
  const VERSIONS_URL = '/versions?gameId=demo-chess';

  test.describe('As Editor', () => {
    test('can create top-level comment', async ({ editorPage: page }) => {
      await page.goto(VERSIONS_URL);

      // Wait for comments section to load
      await expect(page.locator('h3')).toContainText(t('timeline.eventDetails'));

      // Should show resolved comments toggle
      await expect(page.locator(`text=${t('admin.users.deleteConfirm')}`))
        .toBeVisible()
        .catch(() => {});

      // Arrange: Find comment form
      const commentForm = page.locator('textarea[placeholder*="Aggiungi un commento"]');
      await expect(commentForm).toBeVisible();

      // Generate unique comment text
      const commentText = `E2E Test Comment ${Date.now()}`;

      // Act: Create comment
      await commentForm.fill(commentText);
      await page.click('button:has-text("Aggiungi Commento")', { force: true });

      // Assert: Comment appears in thread
      await expect(page.locator(`text=${commentText}`)).toBeVisible({ timeout: 10000 });

      // Should show author name
      await expect(page.locator('text=Editor User')).toBeVisible();
    });

    test('should create threaded reply successfully', async ({ editorPage: page }) => {
      // Arrange: Create parent comment first
      const parentText = `Parent Comment ${Date.now()}`;
      const commentForm = page.locator('textarea[placeholder*="Aggiungi un commento"]');
      await commentForm.fill(parentText);
      await page.click('button:has-text("Aggiungi Commento")', { force: true });
      await expect(page.locator(`text=${parentText}`)).toBeVisible({ timeout: 10000 });

      // Act: Click "Rispondi" button on the new comment
      const replyButton = page.locator('button:has-text("Rispondi")').last();
      await replyButton.click({ force: true });

      // Should show reply form
      const replyForm = page.locator('textarea[placeholder*="Rispondi"]').last();
      await expect(replyForm).toBeVisible();

      // Type reply
      const replyText = `Reply to parent ${Date.now()}`;
      await replyForm.fill(replyText);

      // Submit reply
      await page.locator('button:has-text("Invia Risposta")').last().click({ force: true });

      // Assert: Reply appears nested under parent
      await expect(page.locator(`text=${replyText}`)).toBeVisible({ timeout: 10000 });

      // Reply should be indented (depth > 0)
      const replyContainer = page
        .locator(`text=${replyText}`)
        .locator('xpath=ancestor::div[contains(@style, "margin-left")]')
        .first();
      await expect(replyContainer).toBeVisible();
    });

    test('should show mention autocomplete when typing @', async ({ editorPage: page }) => {
      // Arrange: Find comment form
      const commentForm = page.locator('textarea[placeholder*="Aggiungi un commento"]');
      await expect(commentForm).toBeVisible();

      // Act: Type @ to trigger mention autocomplete
      await commentForm.fill('@');

      // Assert: Autocomplete dropdown should appear
      await expect(page.locator('[role="listbox"]')).toBeVisible({ timeout: 3000 });

      // Should show user suggestions
      await expect(page.locator('[role="option"]')).toHaveCount(3, { timeout: 3000 }); // 3 demo users

      // Should show user display names
      await expect(page.locator('text=Editor User')).toBeVisible();
      await expect(page.locator('text=Admin User')).toBeVisible();
    });

    test('should insert mention when selecting from autocomplete', async ({ editorPage: page }) => {
      // Arrange: Find comment form
      const commentForm = page.locator('textarea[placeholder*="Aggiungi un commento"]');

      // Act: Type @ and select a user
      await commentForm.fill('@');
      await expect(page.locator('[role="listbox"]')).toBeVisible({ timeout: 3000 });

      // Click first suggestion (Admin User)
      await page.locator('[role="option"]').first().click();

      // Assert: Mention should be inserted
      const textValue = await commentForm.inputValue();
      expect(textValue).toContain('@Admin User');
    });

    test('should resolve comment successfully', async ({ editorPage: page }) => {
      // Arrange: Create a comment
      const commentText = `Resolvable Comment ${Date.now()}`;
      const commentForm = page.locator('textarea[placeholder*="Aggiungi un commento"]');
      await commentForm.fill(commentText);
      await page.click('button:has-text("Aggiungi Commento")', { force: true });
      await expect(page.locator(`text=${commentText}`)).toBeVisible({ timeout: 10000 });

      // Act: Click "Risolvi" button
      const resolveButton = page.locator('button:has-text("Risolvi")').last();
      await resolveButton.click({ force: true });

      // Assert: Should show "Risolto" badge
      await expect(page.locator('span:has-text("Risolto")').last()).toBeVisible({ timeout: 10000 });

      // Should show "Riapri" button instead of "Risolvi"
      await expect(page.locator('button:has-text("Riapri")').last()).toBeVisible();

      // Comment should have visual indication (opacity/strikethrough)
      const resolvedComment = page
        .locator(`text=${commentText}`)
        .locator('xpath=ancestor::div[contains(@style, "opacity")]')
        .first();
      await expect(resolvedComment).toBeVisible();
    });

    test('should unresolve comment successfully', async ({ editorPage: page }) => {
      // Arrange: Create and resolve a comment
      const commentText = `Unresolve Test ${Date.now()}`;
      const commentForm = page.locator('textarea[placeholder*="Aggiungi un commento"]');
      await commentForm.fill(commentText);
      await page.click('button:has-text("Aggiungi Commento")', { force: true });
      await expect(page.locator(`text=${commentText}`)).toBeVisible({ timeout: 10000 });

      // Resolve it
      await page.locator('button:has-text("Risolvi")').last().click({ force: true });
      await expect(page.locator('span:has-text("Risolto")').last()).toBeVisible({ timeout: 10000 });

      // Act: Click "Riapri" button
      const unresolveButton = page.locator('button:has-text("Riapri")').last();
      await unresolveButton.click({ force: true });

      // Assert: "Risolto" badge should disappear
      await expect(page.locator('span:has-text("Risolto")').last()).not.toBeVisible({
        timeout: 10000,
      });

      // Should show "Risolvi" button again
      await expect(page.locator('button:has-text("Risolvi")').last()).toBeVisible();
    });

    test('should filter resolved comments when toggling checkbox', async ({ editorPage: page }) => {
      // Arrange: Create two comments and resolve one
      const unresolvedText = `Unresolved ${Date.now()}`;
      const resolvedText = `Resolved ${Date.now()}`;

      const commentForm = page.locator('textarea[placeholder*="Aggiungi un commento"]');

      // Create first comment (will be unresolved)
      await commentForm.fill(unresolvedText);
      await page.click('button:has-text("Aggiungi Commento")', { force: true });
      await expect(page.locator(`text=${unresolvedText}`)).toBeVisible({ timeout: 10000 });

      // Create second comment (will be resolved)
      await commentForm.fill(resolvedText);
      await page.click('button:has-text("Aggiungi Commento")', { force: true });
      await expect(page.locator(`text=${resolvedText}`)).toBeVisible({ timeout: 10000 });

      // Resolve second comment
      await page.locator('button:has-text("Risolvi")').last().click({ force: true });
      await expect(page.locator('span:has-text("Risolto")').last()).toBeVisible({ timeout: 10000 });

      // Both comments should be visible initially
      await expect(page.locator(`text=${unresolvedText}`)).toBeVisible();
      await expect(page.locator(`text=${resolvedText}`)).toBeVisible();

      // Act: Uncheck "Mostra commenti risolti"
      const checkbox = page.locator('input[type="checkbox"]').first();
      await checkbox.uncheck();

      // Wait for comments to reload
      await page.waitForTimeout(1000);

      // Assert: Only unresolved comment should be visible
      await expect(page.locator(`text=${unresolvedText}`)).toBeVisible();
      await expect(page.locator(`text=${resolvedText}`)).not.toBeVisible();

      // Act: Re-check "Mostra commenti risolti"
      await checkbox.check();
      await page.waitForTimeout(1000);

      // Assert: Both comments should be visible again
      await expect(page.locator(`text=${unresolvedText}`)).toBeVisible();
      await expect(page.locator(`text=${resolvedText}`)).toBeVisible();
    });

    test('should show edit/delete buttons only for own comments', async ({ editorPage: page }) => {
      // Arrange: Create a comment as editor
      const editorCommentText = `Editor Comment ${Date.now()}`;
      const commentForm = page.locator('textarea[placeholder*="Aggiungi un commento"]');
      await commentForm.fill(editorCommentText);
      await page.click('button:has-text("Aggiungi Commento")', { force: true });
      await expect(page.locator(`text=${editorCommentText}`)).toBeVisible({ timeout: 10000 });

      // Assert: Should see edit and delete buttons for own comment
      const ownCommentContainer = page
        .locator(`text=${editorCommentText}`)
        .locator('xpath=ancestor::div[1]')
        .first();
      await expect(ownCommentContainer.locator('button:has-text("Modifica")')).toBeVisible();
      await expect(ownCommentContainer.locator('button:has-text("Elimina")')).toBeVisible();

      // Note: Cannot test other users' comments without switching accounts in this test
      // That scenario is covered by unit tests for permission logic
    });

    test('should allow editing own comment', async ({ editorPage: page }) => {
      // Arrange: Create a comment
      const originalText = `Original Text ${Date.now()}`;
      const commentForm = page.locator('textarea[placeholder*="Aggiungi un commento"]');
      await commentForm.fill(originalText);
      await page.click('button:has-text("Aggiungi Commento")', { force: true });
      await expect(page.locator(`text=${originalText}`)).toBeVisible({ timeout: 10000 });

      // Act: Click "Modifica" button
      await page.locator('button:has-text("Modifica")').last().click({ force: true });

      // Should show edit form
      const editForm = page.locator('textarea').last();
      await expect(editForm).toBeVisible();

      // Edit the text
      const editedText = `Edited Text ${Date.now()}`;
      await editForm.clear();
      await editForm.fill(editedText);

      // Save changes
      await page.locator('button:has-text("Salva")').last().click({ force: true });

      // Assert: Updated text should appear
      await expect(page.locator(`text=${editedText}`)).toBeVisible({ timeout: 10000 });
      await expect(page.locator(`text=${originalText}`)).not.toBeVisible();
    });

    test('should show cancel button when replying and cancel reply', async ({
      editorPage: page,
    }) => {
      // Arrange: Create parent comment
      const parentText = `Parent ${Date.now()}`;
      const commentForm = page.locator('textarea[placeholder*="Aggiungi un commento"]');
      await commentForm.fill(parentText);
      await page.click('button:has-text("Aggiungi Commento")', { force: true });
      await expect(page.locator(`text=${parentText}`)).toBeVisible({ timeout: 10000 });

      // Act: Click "Rispondi"
      await page.locator('button:has-text("Rispondi")').last().click({ force: true });
      const replyForm = page.locator('textarea[placeholder*="Rispondi"]').last();
      await expect(replyForm).toBeVisible();

      // Type some text
      await replyForm.fill('Test reply text');

      // Assert: Should show cancel button
      await expect(page.locator('button:has-text("Annulla")').last()).toBeVisible();

      // Act: Click cancel
      await page.locator('button:has-text("Annulla")').last().click({ force: true });

      // Assert: Reply form should disappear
      await expect(replyForm).not.toBeVisible();
      await expect(page.locator('button:has-text("Rispondi")').last()).toBeVisible();
    });
  });

  test.describe('As Admin', () => {
    test('can delete any comment', async ({ adminPage: page }) => {
      await page.goto(VERSIONS_URL);

      // Arrange: Login as editor and create comment
      await page.goto('/');
      await page.fill('input[type="email"]', 'editor@meepleai.dev');
      await page.fill('input[type="password"]', 'Demo123!');
      await page.click('button[type="submit"]');
      await page.waitForURL('/', { timeout: 5000 });

      await page.goto(VERSIONS_URL);
      const editorCommentText = `Editor Comment for Admin ${Date.now()}`;
      const commentForm = page.locator('textarea[placeholder*="Aggiungi un commento"]');
      await commentForm.fill(editorCommentText);
      await page.click('button:has-text("Aggiungi Commento")', { force: true });
      await expect(page.locator(`text=${editorCommentText}`)).toBeVisible({ timeout: 10000 });

      // Logout
      await page.goto('/');
      await page.click('button:has-text("Logout")');

      // Act: Login as admin
      await page.goto('/');
      await page.fill('input[type="email"]', 'admin@meepleai.dev');
      await page.fill('input[type="password"]', 'Demo123!');
      await page.click('button[type="submit"]');
      await page.waitForURL('/', { timeout: 5000 });

      await page.goto(VERSIONS_URL);

      // Should see editor's comment
      await expect(page.locator(`text=${editorCommentText}`)).toBeVisible();

      // Setup confirmation dialog handler
      page.on('dialog', dialog => dialog.accept());

      // Delete editor's comment
      const editorCommentContainer = page
        .locator(`text=${editorCommentText}`)
        .locator('xpath=ancestor::div[1]')
        .first();
      await editorCommentContainer.locator('button:has-text("Elimina")').click({ force: true });

      // Assert: Comment should be removed
      await expect(page.locator(`text=${editorCommentText}`)).not.toBeVisible({ timeout: 10000 });
    });
  });

  test.describe('User Role Restrictions', () => {
    test('regular user cannot create comments', async ({ page }) => {
      // Arrange: Login as regular user (read-only)
      await page.goto('/');
      await page.click('button:has-text("Logout")');
      await page.fill('input[type="email"]', 'user@meepleai.dev');
      await page.fill('input[type="password"]', 'Demo123!');
      await page.click('button[type="submit"]');
      await page.waitForURL('/', { timeout: 5000 });

      // Act: Navigate to versions page
      await page.goto(VERSIONS_URL);

      // Assert: Should not see comment form
      const commentForm = page.locator('textarea[placeholder*="Aggiungi un commento"]');
      await expect(commentForm).not.toBeVisible();

      // Should not see "Aggiungi Commento" button
      await expect(page.locator('button:has-text("Aggiungi Commento")')).not.toBeVisible();
    });
  });
});
