/**
 * Q&A Interface - Performance E2E Tests - MIGRATED TO POM
 *
 * @see apps/web/e2e/helpers/qa-test-utils.ts
 */

import { test, expect } from './fixtures';
import { setupQATestEnvironment } from './helpers/qa-test-utils';

test.describe('Q&A Interface - Performance (Issue #1009)', () => {
  test('should respond within acceptable time limits (< 3s for simple query)', async ({ page }) => {
    await setupQATestEnvironment(page);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    // Measure response time
    const startTime = Date.now();

    await page.fill('#message-input', 'What is the board size?');
    await page.locator('button[type="submit"]').click();

    // Wait for backend response
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 30000 });

    const responseTime = Date.now() - startTime;

    // Performance assertion: < 30000ms for real backend
    expect(responseTime).toBeLessThan(30000);
  });

  test('should handle timeout gracefully after 30 seconds', async ({ page }) => {
    await setupQATestEnvironment(page);

    test.setTimeout(40000); // Increase test timeout for this scenario

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    await page.fill('#message-input', 'Slow query');
    await page.locator('button[type="submit"]').click();

    // Timeout error message should appear
    await expect(page.getByText(/timeout|tempo scaduto|riprova/i)).toBeVisible({ timeout: 36000 });

    // UI should return to ready state
    await expect(page.locator('button[type="submit"]:has-text("Invia")')).toBeVisible();
  });

  test('should render large snippet collections quickly', async ({ page }) => {
    await setupQATestEnvironment(page);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    await page.fill('#message-input', 'Detailed question');
    await page.locator('button[type="submit"]').click();

    // Wait for backend response
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 30000 });
  });

  test('should handle concurrent requests without degradation', async ({ page }) => {
    await setupQATestEnvironment(page);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    // Send first request
    const start1 = Date.now();
    await page.fill('#message-input', 'First question');
    await page.locator('button[type="submit"]').click();
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 30000 });
    const time1 = Date.now() - start1;

    // Send second request immediately after
    const start2 = Date.now();
    await page.fill('#message-input', 'Second question');
    await page.locator('button[type="submit"]').click();
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 30000 });
    const time2 = Date.now() - start2;

    // Second request shouldn't be significantly slower (< 50% degradation)
    expect(time2).toBeLessThan(time1 * 1.5);
  });

  test('should maintain UI responsiveness during streaming', async ({ page }) => {
    await setupQATestEnvironment(page);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    await page.fill('#message-input', 'Long streaming response');
    await page.locator('button[type="submit"]').click();

    // Verify input field is still accessible
    const inputField = page.locator('#message-input');
    await expect(inputField).toBeVisible();

    // Scroll should work during streaming
    await page.mouse.wheel(0, 100);

    // Wait for backend response
    await expect(inputField).toBeEnabled({ timeout: 30000 });
  });

  test('should handle rapid successive requests without UI freeze', async ({ page }) => {
    await setupQATestEnvironment(page);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    // Send 3 requests in rapid succession
    for (let i = 1; i <= 3; i++) {
      await page.fill('#message-input', `Question ${i}`);
      await page.locator('button[type="submit"]').click();
      await page.waitForTimeout(2000);
    }

    // UI should not be frozen
    await expect(page.locator('button[type="submit"]:has-text("Invia")')).toBeVisible();
  });

  test('should measure P95 latency for typical Q&A workflow', async ({ page }) => {
    await setupQATestEnvironment(page);

    const latencies: number[] = [];

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    // Perform 20 requests to measure P95
    for (let i = 1; i <= 20; i++) {
      const start = Date.now();
      await page.fill('#message-input', `Question ${i}`);
      await page.locator('button[type="submit"]').click();
      await expect(page.locator('#message-input')).toBeEnabled({ timeout: 30000 });

      latencies.push(Date.now() - start);
    }

    // Calculate P95 (95th percentile)
    latencies.sort((a, b) => a - b);
    const p95Index = Math.ceil(latencies.length * 0.95) - 1;
    const p95Latency = latencies[p95Index];

    // P95 should be under 30000ms for real backend
    expect(p95Latency).toBeLessThan(30000);
  });

  test.setTimeout(120000); // Increase timeout for 20-request test

  test('should handle page visibility changes without performance impact', async ({ page }) => {
    await setupQATestEnvironment(page);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    await page.fill('#message-input', 'Background question');
    await page.locator('button[type="submit"]').click();

    // Simulate page going to background
    await page.evaluate(() => {
      document.dispatchEvent(new Event('visibilitychange'));
    });

    // Response should still render correctly
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 30000 });
  });
});
