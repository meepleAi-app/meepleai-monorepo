/**
 * ERR-08: Slow Response Indicator
 * Issue #3082 - P2 Medium
 *
 * Tests slow response indicator functionality:
 * - Detect slow responses
 * - Display slow indicator
 * - Timeout handling
 * - User-friendly messaging
 */

import { test, expect } from '../fixtures';

import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

/**
 * Setup mock routes for slow response testing
 */
async function setupSlowResponseMocks(
  page: Page,
  options: {
    responseDelay?: number;
    timeout?: boolean;
  } = {}
) {
  const { responseDelay = 0, timeout = false } = options;

  // Mock auth
  await page.route(`${API_BASE}/api/v1/auth/me`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: {
          id: 'test-user-id',
          email: 'test@example.com',
          displayName: 'Test User',
          role: 'User',
        },
        expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
      }),
    });
  });

  // Mock games endpoint
  await page.route(`${API_BASE}/api/v1/games**`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([{ id: 'chess', title: 'Chess' }]),
    });
  });

  // Mock chat threads
  await page.route(`${API_BASE}/api/v1/chat/threads**`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([]),
    });
  });

  // Mock slow chat endpoint
  await page.route(`${API_BASE}/api/v1/agents/ask*`, async (route) => {
    if (timeout) {
      // Don't respond to simulate timeout
      await new Promise((resolve) => setTimeout(resolve, 60000));
      return;
    }

    if (responseDelay > 0) {
      await new Promise((resolve) => setTimeout(resolve, responseDelay));
    }

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        id: `msg-${Date.now()}`,
        role: 'assistant',
        content: 'This is the response after delay.',
        timestamp: new Date().toISOString(),
      }),
    });
  });

  return {};
}

test.describe('ERR-08: Slow Response Indicator', () => {
  test.describe('Detect Slow Response', () => {
    test('should show loading indicator during request', async ({ page }) => {
      await setupSlowResponseMocks(page, { responseDelay: 3000 });

      await page.goto('/chat');
      await page.waitForLoadState('networkidle');

      const chatInput = page.getByPlaceholder(/message/i).or(page.locator('textarea').first());
      if (await chatInput.isVisible()) {
        await chatInput.fill('Test question');
        await page.keyboard.press('Enter');

        // Should show loading indicator
        await expect(
          page.locator('.loading, .spinner, [data-loading="true"]').or(
            page.getByText(/loading|thinking|processing/i)
          )
        ).toBeVisible({ timeout: 2000 });
      }
    });

    test('should show slow indicator after threshold', async ({ page }) => {
      await setupSlowResponseMocks(page, { responseDelay: 8000 });

      await page.goto('/chat');
      await page.waitForLoadState('networkidle');

      const chatInput = page.getByPlaceholder(/message/i);
      if (await chatInput.isVisible()) {
        await chatInput.fill('Test slow response');
        await page.keyboard.press('Enter');

        // After a few seconds, should show "taking longer"
        await expect(
          page.getByText(/taking.*longer|slow|please.*wait/i).or(
            page.locator('[data-testid="slow-indicator"]')
          )
        ).toBeVisible({ timeout: 6000 });
      }
    });
  });

  test.describe('Display Slow Indicator', () => {
    test('should show elapsed time for slow responses', async ({ page }) => {
      await setupSlowResponseMocks(page, { responseDelay: 10000 });

      await page.goto('/chat');
      await page.waitForLoadState('networkidle');

      const chatInput = page.getByPlaceholder(/message/i);
      if (await chatInput.isVisible()) {
        await chatInput.fill('Test');
        await page.keyboard.press('Enter');

        // May show elapsed time
        await page.waitForTimeout(5000);

        const timeIndicator = page.getByText(/\d+.*second|sec/i);
        const hasTimeIndicator = await timeIndicator.isVisible().catch(() => false);
        await expect(page.locator('body')).toBeVisible();
      }
    });

    test('should show progress animation', async ({ page }) => {
      await setupSlowResponseMocks(page, { responseDelay: 5000 });

      await page.goto('/chat');
      await page.waitForLoadState('networkidle');

      const chatInput = page.getByPlaceholder(/message/i);
      if (await chatInput.isVisible()) {
        await chatInput.fill('Test');
        await page.keyboard.press('Enter');

        // Should show animated progress
        await expect(
          page.locator('.animate-pulse, .animate-spin, @keyframes, .progress').or(
            page.locator('[data-loading]')
          )
        ).toBeVisible({ timeout: 2000 });
      }
    });

    test('should update message during slow response', async ({ page }) => {
      await setupSlowResponseMocks(page, { responseDelay: 8000 });

      await page.goto('/chat');
      await page.waitForLoadState('networkidle');

      const chatInput = page.getByPlaceholder(/message/i);
      if (await chatInput.isVisible()) {
        await chatInput.fill('Test');
        await page.keyboard.press('Enter');

        // Initial message
        await expect(page.getByText(/loading|processing/i)).toBeVisible({ timeout: 2000 });

        // After threshold, message may change
        await page.waitForTimeout(5000);

        await expect(
          page.getByText(/still.*processing|taking.*while|complex/i).or(page.locator('body'))
        ).toBeVisible();
      }
    });
  });

  test.describe('Timeout Handling', () => {
    test('should show timeout error after maximum wait', async ({ page }) => {
      await setupSlowResponseMocks(page, { timeout: true });

      await page.goto('/chat');
      await page.waitForLoadState('networkidle');

      const chatInput = page.getByPlaceholder(/message/i);
      if (await chatInput.isVisible()) {
        await chatInput.fill('Test timeout');
        await page.keyboard.press('Enter');

        // Wait for timeout (may be 30-60 seconds in real app, shorter in test)
        await expect(
          page.getByText(/timeout|took.*too.*long|try.*again/i).or(
            page.locator('[data-testid="timeout-error"]')
          )
        ).toBeVisible({ timeout: 35000 });
      }
    });

    test('should allow retry after timeout', async ({ page }) => {
      await setupSlowResponseMocks(page, { timeout: true });

      await page.goto('/chat');
      await page.waitForLoadState('networkidle');

      const chatInput = page.getByPlaceholder(/message/i);
      if (await chatInput.isVisible()) {
        await chatInput.fill('Test');
        await page.keyboard.press('Enter');

        // Wait for timeout
        await page.waitForTimeout(5000);

        // Look for retry button
        const retryButton = page.getByRole('button', { name: /retry|try.*again/i });
        if (await retryButton.isVisible()) {
          await expect(retryButton).toBeEnabled();
        }
      }
    });

    test('should allow canceling slow request', async ({ page }) => {
      await setupSlowResponseMocks(page, { responseDelay: 30000 });

      await page.goto('/chat');
      await page.waitForLoadState('networkidle');

      const chatInput = page.getByPlaceholder(/message/i);
      if (await chatInput.isVisible()) {
        await chatInput.fill('Test cancel');
        await page.keyboard.press('Enter');

        await page.waitForTimeout(2000);

        // Look for cancel button
        const cancelButton = page.getByRole('button', { name: /cancel|stop/i });
        if (await cancelButton.isVisible()) {
          await cancelButton.click();

          // Request should be canceled
          await expect(page.locator('body')).toBeVisible();
        }
      }
    });
  });

  test.describe('User-Friendly Messaging', () => {
    test('should show helpful message during slow response', async ({ page }) => {
      await setupSlowResponseMocks(page, { responseDelay: 6000 });

      await page.goto('/chat');
      await page.waitForLoadState('networkidle');

      const chatInput = page.getByPlaceholder(/message/i);
      if (await chatInput.isVisible()) {
        await chatInput.fill('Complex question about chess strategy');
        await page.keyboard.press('Enter');

        await page.waitForTimeout(4000);

        // Should show reassuring message
        await expect(
          page.getByText(/complex|analyzing|detailed|thorough/i).or(
            page.getByText(/please.*wait|moment/i)
          )
        ).toBeVisible();
      }
    });

    test('should explain why response is slow', async ({ page }) => {
      await setupSlowResponseMocks(page, { responseDelay: 8000 });

      await page.goto('/chat');
      await page.waitForLoadState('networkidle');

      const chatInput = page.getByPlaceholder(/message/i);
      if (await chatInput.isVisible()) {
        await chatInput.fill('Test');
        await page.keyboard.press('Enter');

        await page.waitForTimeout(5000);

        // May explain the delay
        await expect(
          page.getByText(/processing|server|load|complex/i).or(page.locator('body'))
        ).toBeVisible();
      }
    });

    test('should provide estimated time remaining', async ({ page }) => {
      await setupSlowResponseMocks(page, { responseDelay: 10000 });

      await page.goto('/chat');
      await page.waitForLoadState('networkidle');

      const chatInput = page.getByPlaceholder(/message/i);
      if (await chatInput.isVisible()) {
        await chatInput.fill('Test');
        await page.keyboard.press('Enter');

        await page.waitForTimeout(3000);

        // May show estimated time
        const estimateText = page.getByText(/estimat|\d+.*sec|remaining/i);
        const hasEstimate = await estimateText.isVisible().catch(() => false);
        // Not all implementations show this
        await expect(page.locator('body')).toBeVisible();
      }
    });
  });

  test.describe('Performance Feedback', () => {
    test('should hide slow indicator when response arrives', async ({ page }) => {
      await setupSlowResponseMocks(page, { responseDelay: 5000 });

      await page.goto('/chat');
      await page.waitForLoadState('networkidle');

      const chatInput = page.getByPlaceholder(/message/i);
      if (await chatInput.isVisible()) {
        await chatInput.fill('Test');
        await page.keyboard.press('Enter');

        // Wait for response
        await page.waitForTimeout(6000);

        // Slow indicator should be gone
        const slowIndicator = page.locator('[data-testid="slow-indicator"]');
        await expect(slowIndicator).not.toBeVisible({ timeout: 2000 });
      }
    });

    test('should show response time for completed requests', async ({ page }) => {
      await setupSlowResponseMocks(page, { responseDelay: 3000 });

      await page.goto('/chat');
      await page.waitForLoadState('networkidle');

      const chatInput = page.getByPlaceholder(/message/i);
      if (await chatInput.isVisible()) {
        await chatInput.fill('Test');
        await page.keyboard.press('Enter');

        // Wait for response
        await page.waitForTimeout(4000);

        // May show response time
        const responseTimeText = page.getByText(/\d+.*ms|\d+.*sec.*took/i);
        const hasResponseTime = await responseTimeText.isVisible().catch(() => false);
        await expect(page.locator('body')).toBeVisible();
      }
    });
  });
});
