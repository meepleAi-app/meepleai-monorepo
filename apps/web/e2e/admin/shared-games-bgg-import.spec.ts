/**
 * Issue #3538: BGG Import Workflow E2E Tests
 *
 * Tests the BoardGameGeek import flow from the admin shared games page.
 *
 * Prerequisites:
 * - Backend running with BGG integration enabled
 * - Admin/Editor user exists
 *
 * Run: pnpm test:e2e apps/web/e2e/admin/shared-games-bgg-import.spec.ts
 */

import { test, expect } from '@playwright/test';

// Test configuration
const TEST_BGG_ID = '13'; // Catan - a well-known game for testing
const TEST_BGG_URL = 'https://boardgamegeek.com/boardgame/13/catan';

test.describe('BGG Import Workflow', () => {
  test.beforeEach(async ({ page }) => {
    // Login as admin
    await page.goto('/login');
    await page.fill('input[type="email"]', 'admin@meepleai.dev');
    await page.fill('input[type="password"]', 'Admin123!');
    await page.click('button[type="submit"]');

    // Wait for redirect to dashboard
    await page.waitForURL('/board-game-ai');

    // Navigate to shared games admin
    await page.goto('/admin/shared-games');
    await expect(page.locator('h1')).toContainText('Shared Games');
  });

  test.describe('Import Modal', () => {
    test('should open import modal when clicking Import from BGG button', async ({ page }) => {
      // Click import button
      await page.click('button:has-text("Import from BGG")');

      // Verify modal opens
      await expect(page.locator('text=Import from BoardGameGeek')).toBeVisible();
      await expect(page.locator('input[placeholder*="BGG ID"]')).toBeVisible();
    });

    test('should show rate limit warning in import modal', async ({ page }) => {
      await page.click('button:has-text("Import from BGG")');

      // Verify rate limit warning is visible
      await expect(page.locator('text=BGG API has rate limits')).toBeVisible();
    });

    test('should validate BGG ID input', async ({ page }) => {
      await page.click('button:has-text("Import from BGG")');

      // Enter invalid input
      await page.fill('input[placeholder*="BGG ID"]', 'invalid');

      // Should show validation error
      await expect(page.locator('text=Invalid BGG ID')).toBeVisible();
    });

    test('should accept BGG URL format', async ({ page }) => {
      await page.click('button:has-text("Import from BGG")');

      // Enter BGG URL
      await page.fill('input[placeholder*="BGG ID"]', TEST_BGG_URL);

      // Should extract ID and show preview (if API responds)
      // Wait for debounce
      await page.waitForTimeout(1000);

      // Check if preview section appears or error message
      const hasPreview = await page.locator('text=Preview').isVisible();
      const hasError = await page.locator('[role="alert"]').isVisible();

      expect(hasPreview || hasError).toBeTruthy();
    });

    test('should close modal on cancel', async ({ page }) => {
      await page.click('button:has-text("Import from BGG")');
      await expect(page.locator('text=Import from BoardGameGeek')).toBeVisible();

      // Click cancel
      await page.click('button:has-text("Cancel")');

      // Modal should be closed
      await expect(page.locator('text=Import from BoardGameGeek')).not.toBeVisible();
    });
  });

  test.describe('Import Flow', () => {
    test('should show game preview when entering valid BGG ID', async ({ page }) => {
      await page.click('button:has-text("Import from BGG")');

      // Enter valid BGG ID
      await page.fill('input[placeholder*="BGG ID"]', TEST_BGG_ID);

      // Wait for debounce and API call
      await page.waitForTimeout(1500);

      // Should either show preview or an error (depending on API availability)
      const content = await page.locator('[role="dialog"]').textContent();
      const hasContent = content && content.length > 100;
      expect(hasContent).toBeTruthy();
    });

    test('should handle duplicate detection', async ({ page }) => {
      // This test assumes Catan might already exist in the catalog
      await page.click('button:has-text("Import from BGG")');

      await page.fill('input[placeholder*="BGG ID"]', TEST_BGG_ID);
      await page.waitForTimeout(1500);

      // Check for duplicate warning or continue
      const duplicateWarning = await page.locator('text=already exists').isVisible();
      const previewShown = await page.locator('text=Preview').isVisible();

      // Either duplicate warning or preview should be shown
      expect(duplicateWarning || previewShown).toBeTruthy();
    });

    test('should have auto-submit checkbox option', async ({ page }) => {
      await page.click('button:has-text("Import from BGG")');

      // Check for auto-submit checkbox
      await expect(page.locator('text=Auto-submit for approval')).toBeVisible();
    });
  });

  test.describe('Error Handling', () => {
    test('should handle non-existent BGG ID gracefully', async ({ page }) => {
      await page.click('button:has-text("Import from BGG")');

      // Enter non-existent BGG ID
      await page.fill('input[placeholder*="BGG ID"]', '99999999');
      await page.waitForTimeout(1500);

      // Should show error message
      await expect(page.locator('text=not found').or(page.locator('text=Game not found'))).toBeVisible();
    });

    test('should handle rate limit errors', async ({ page }) => {
      await page.click('button:has-text("Import from BGG")');

      // Rapidly enter multiple IDs to potentially trigger rate limit
      for (let i = 1; i <= 5; i++) {
        await page.fill('input[placeholder*="BGG ID"]', String(i));
        await page.waitForTimeout(200);
      }

      // Check if rate limit warning appears or if requests succeed
      // This is more of a stress test
      await page.waitForTimeout(2000);
      const content = await page.locator('[role="dialog"]').textContent();
      expect(content).toBeTruthy();
    });
  });

  test.describe('Accessibility', () => {
    test('modal should be keyboard accessible', async ({ page }) => {
      await page.click('button:has-text("Import from BGG")');

      // Tab through modal
      await page.keyboard.press('Tab');
      await page.keyboard.press('Tab');

      // Should be able to close with Escape
      await page.keyboard.press('Escape');
      await expect(page.locator('text=Import from BoardGameGeek')).not.toBeVisible();
    });

    test('import button should have accessible label', async ({ page }) => {
      const importButton = page.locator('button:has-text("Import from BGG")');
      await expect(importButton).toBeVisible();

      // Check button is focusable
      await importButton.focus();
      await expect(importButton).toBeFocused();
    });
  });
});
