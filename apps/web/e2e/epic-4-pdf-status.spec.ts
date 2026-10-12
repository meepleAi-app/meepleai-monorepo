import { test, expect } from '@playwright/test';

import { setupMockAuth } from './fixtures/auth';

/**
 * Epic #4: PDF Status Tracking
 *
 * Only #4111 (notification channels) is covered here. The tests for #4106-#4110
 * drove the retired PDF import wizard and were removed with it (#4257).
 */

test.describe('Epic #4: PDF Status Tracking', () => {
  test.beforeEach(async ({ page }) => {
    await setupMockAuth(page, 'Admin');

    // TODO: Login as authenticated user
    await page.goto('/');
  });

  /**
   * Issue #4111: Notification Channel Configuration
   */
  test('PDF - Configure notification delivery channels', async ({ page }) => {
    await page.goto('/settings/notifications');

    // Find PDF-related notification settings
    const pdfSection = page.locator('[data-testid="notification-section-pdf"]');
    await expect(pdfSection).toBeVisible();

    // Configure channels
    await pdfSection.locator('[data-testid="toggle-email"]').click();
    await pdfSection.locator('[data-testid="toggle-push"]').click();
    await pdfSection.locator('[data-testid="toggle-in-app"]').check();

    // Save
    await page.click('[data-testid="save-notification-preferences"]');
  });
});
