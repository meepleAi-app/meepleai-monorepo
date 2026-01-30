/**
 * Toolkit Real-Time Sync E2E Tests (Issue #3163)
 *
 * End-to-end tests for SSE real-time score synchronization
 * Uses 2 browser contexts to simulate multiple users
 */

import { test, expect } from '@playwright/test';

test.describe('Toolkit - Real-Time Sync', () => {
  test('should sync score updates between two browser contexts via SSE', async ({
    browser,
  }) => {
    // Create two separate browser contexts (simulating two users)
    const context1 = await browser.newContext();
    const context2 = await browser.newContext();

    const page1 = await context1.newPage();
    const page2 = await context2.newPage();

    try {
      // User 1: Create session
      await page1.goto('/toolkit');
      await page1.getByPlaceholder('Participant 1').fill('User 1');
      await page1.getByRole('button', { name: /add participant/i }).click();
      await page1.getByPlaceholder('Participant 2').fill('User 2');
      await page1.getByRole('button', { name: /create session/i }).click();

      // Wait for redirect and get session code
      await page1.waitForURL(/\/toolkit\/[a-f0-9-]+/);
      const sessionCode = await page1.getByText(/[A-Z0-9]{6}/).textContent();

      expect(sessionCode).toBeTruthy();

      // User 2: Join the same session
      await page2.goto('/toolkit');
      await page2.getByPlaceholder('ABC123').fill(sessionCode!);
      await page2.getByRole('button', { name: /join session/i }).click();

      // Wait for redirect
      await page2.waitForURL(/\/toolkit\/[a-f0-9-]+/);

      // Verify both users are on the same session
      await expect(page1.getByText(sessionCode!)).toBeVisible();
      await expect(page2.getByText(sessionCode!)).toBeVisible();

      // User 1: Submit a score
      // Find score input form in page1
      const scoreInput1 = page1.getByRole('spinbutton').first(); // Score value input
      await scoreInput1.fill('10');

      const submitButton1 = page1.getByRole('button', { name: /add score|submit/i });
      await submitButton1.click();

      // User 2: Should see the score update via SSE
      await page2.waitForTimeout(1000); // Wait for SSE event propagation

      // Verify score appears in User 2's view
      await expect(page2.getByText('10')).toBeVisible();

      // Verify toast notification appeared
      await expect(page2.getByText(/User 1.*\+10/)).toBeVisible();
    } finally {
      await page1.close();
      await page2.close();
      await context1.close();
      await context2.close();
    }
  });

  test('should show connection status indicator', async ({ page }) => {
    // Create a session
    await page.goto('/toolkit');
    await page.getByPlaceholder('Participant 1').fill('Test User');
    await page.getByRole('button', { name: /create session/i }).click();

    await page.waitForURL(/\/toolkit\/[a-f0-9-]+/);

    // Verify connection status indicator (green dot or "Connected" text)
    // This depends on SessionHeader implementation
    await expect(
      page.locator('[aria-label*="connected" i], [data-connected="true"]').first()
    ).toBeVisible();
  });

  test('should handle pause/resume session lifecycle', async ({ page }) => {
    // Create session
    await page.goto('/toolkit');
    await page.getByPlaceholder('Participant 1').fill('Owner');
    await page.getByRole('button', { name: /create session/i }).click();

    await page.waitForURL(/\/toolkit\/[a-f0-9-]+/);

    // Pause session
    await page.getByRole('button', { name: /pause/i }).click();

    // Verify pause toast
    await expect(page.getByText(/session paused/i)).toBeVisible();

    // Resume session
    await page.getByRole('button', { name: /resume/i }).click();

    // Verify resume toast
    await expect(page.getByText(/session resumed/i)).toBeVisible();
  });

  test('should finalize session and redirect to toolkit home', async ({ page }) => {
    // Create session
    await page.goto('/toolkit');
    await page.getByPlaceholder('Participant 1').fill('Player 1');
    await page.getByRole('button', { name: /create session/i }).click();

    await page.waitForURL(/\/toolkit\/[a-f0-9-]+/);

    // Finalize session
    await page.getByRole('button', { name: /finalize|end session/i }).click();

    // Verify success toast
    await expect(page.getByText(/session finalized/i)).toBeVisible();

    // Verify redirect to toolkit home
    await page.waitForURL('/toolkit', { timeout: 5000 });
    await expect(page.url()).toContain('/toolkit');
    await expect(page.url()).not.toContain('/toolkit/');
  });

  test('should display optimistic UI for score submission', async ({ page }) => {
    // Create session
    await page.goto('/toolkit');
    await page.getByPlaceholder('Participant 1').fill('Fast User');
    await page.getByRole('button', { name: /create session/i }).click();

    await page.waitForURL(/\/toolkit\/[a-f0-9-]+/);

    // Submit score
    const scoreInput = page.getByRole('spinbutton').first();
    await scoreInput.fill('25');

    await page.getByRole('button', { name: /add score|submit/i }).click();

    // Score should appear immediately (optimistic UI)
    // Even if API hasn't responded yet
    await expect(page.getByText('25')).toBeVisible({ timeout: 500 });
  });

  test('should show loading state during session creation', async ({ page }) => {
    await page.goto('/toolkit');

    await page.getByPlaceholder('Participant 1').fill('Test User');

    // Click create button
    await page.getByRole('button', { name: /create session/i }).click();

    // Verify loading state
    await expect(page.getByRole('button', { name: /creating/i })).toBeVisible();
  });
});
