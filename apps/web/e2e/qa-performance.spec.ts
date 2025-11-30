/**
 * Q&A Interface - Performance E2E Tests - MIGRATED TO POM
 *
 * @see apps/web/e2e/helpers/qa-test-utils.ts
 */

import { test, expect } from '@playwright/test';
import { setupQATestEnvironment, QAResponse } from './helpers/qa-test-utils';

test.describe('Q&A Interface - Performance (Issue #1009)', () => {
  test('should respond within acceptable time limits (< 3s for simple query)', async ({ page }) => {
    const { mockQA } = await setupQATestEnvironment(page);

    const testResponse: QAResponse = {
      answer: 'Chess is played on an 8x8 board.',
      snippets: [{ text: 'The board is 8x8.', source: 'rules.pdf', page: 1, line: null }],
      messageId: 'msg-perf-1',
      tokenUsage: { promptTokens: 50, completionTokens: 20, totalTokens: 70 },
    };

    await mockQA(testResponse);
    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    // Measure response time
    const startTime = Date.now();

    await page.fill('#message-input', 'What is the board size?');
    await page.locator('button[type="submit"]').click();

    await expect(page.getByText(/Chess is played on an 8x8 board/i)).toBeVisible({
      timeout: 5000,
    });

    const responseTime = Date.now() - startTime;

    // Performance assertion: < 3000ms for P95
    expect(responseTime).toBeLessThan(3000);
  });

  test('should handle timeout gracefully after 30 seconds', async ({ page }) => {
    await setupQATestEnvironment(page);

    // Mock extremely slow response (timeout scenario)
    await page.route('**/api/v1/agents/qa', async route => {
      // Simulate timeout - don't respond for 35 seconds
      await new Promise(resolve => setTimeout(resolve, 35000));
      await route.fulfill({
        status: 504,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Gateway Timeout' }),
      });
    });

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

  test('should render large snippet collections quickly (< 500ms)', async ({ page }) => {
    const { mockQA } = await setupQATestEnvironment(page);

    // Create response with 10 large snippets
    const largeSnippets = Array.from({ length: 10 }, (_, i) => ({
      text: `Snippet ${i + 1}: ${'Lorem ipsum dolor sit amet. '.repeat(20)}`,
      source: `document-${i + 1}.pdf`,
      page: i + 1,
      line: null,
    }));

    const testResponse: QAResponse = {
      answer: 'Here is a comprehensive answer with many citations.',
      snippets: largeSnippets,
      messageId: 'msg-perf-2',
    };

    await mockQA(testResponse);
    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    await page.fill('#message-input', 'Detailed question');
    await page.locator('button[type="submit"]').click();

    // Wait for answer
    await expect(page.getByText(/comprehensive answer/i)).toBeVisible({ timeout: 5000 });

    // Measure snippet rendering time
    const renderStart = Date.now();
    await expect(page.getByText('Fonti:')).toBeVisible();

    // Verify all snippets rendered
    for (let i = 1; i <= 10; i++) {
      await expect(page.getByText(`document-${i}.pdf (Pagina ${i})`)).toBeVisible();
    }

    const renderTime = Date.now() - renderStart;

    // Rendering should be fast (< 500ms)
    expect(renderTime).toBeLessThan(500);
  });

  test('should handle concurrent requests without degradation', async ({ page }) => {
    const { mockQA } = await setupQATestEnvironment(page);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    // Send first request
    await mockQA({
      answer: 'First answer.',
      snippets: [],
      messageId: 'msg-concurrent-1',
    });

    const start1 = Date.now();
    await page.fill('#message-input', 'First question');
    await page.locator('button[type="submit"]').click();
    await expect(page.getByText('First answer.')).toBeVisible({ timeout: 5000 });
    const time1 = Date.now() - start1;

    // Send second request immediately after
    await mockQA({
      answer: 'Second answer.',
      snippets: [],
      messageId: 'msg-concurrent-2',
    });

    const start2 = Date.now();
    await page.fill('#message-input', 'Second question');
    await page.locator('button[type="submit"]').click();
    await expect(page.getByText('Second answer.')).toBeVisible({ timeout: 5000 });
    const time2 = Date.now() - start2;

    // Second request shouldn't be significantly slower (< 50% degradation)
    expect(time2).toBeLessThan(time1 * 1.5);
  });

  test('should maintain UI responsiveness during streaming', async ({ page }) => {
    await setupQATestEnvironment(page);

    // Mock slow streaming response
    await page.route('**/api/v1/agents/qa/stream', async route => {
      const sseData =
        Array.from({ length: 50 }, (_, i) => `event: token\ndata: {"token":"word${i} "}\n\n`).join(
          ''
        ) + 'event: complete\ndata: {"totalTokens":50,"confidence":0.9,"snippets":[]}\n\n';

      await route.fulfill({
        status: 200,
        headers: {
          'Content-Type': 'text/event-stream',
          'Cache-Control': 'no-cache',
        },
        body: sseData,
      });
    });

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    await page.fill('#message-input', 'Long streaming response');
    await page.locator('button[type="submit"]').click();

    // During streaming, UI should remain interactive

    // Verify input field is still accessible (though may be disabled during streaming)
    const inputField = page.locator('#message-input');
    await expect(inputField).toBeVisible();

    // Scroll should work during streaming
    await page.mouse.wheel(0, 100);

    // Wait for completion
    await expect(page.getByText(/word49/)).toBeVisible({ timeout: 10000 });
  });

  test('should handle rapid successive requests without UI freeze', async ({ page }) => {
    const { mockQA } = await setupQATestEnvironment(page);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    // Send 3 requests in rapid succession
    for (let i = 1; i <= 3; i++) {
      await mockQA({
        answer: `Answer ${i}.`,
        snippets: [],
        messageId: `msg-rapid-${i}`,
      });

      await page.fill('#message-input', `Question ${i}`);
      await page.locator('button[type="submit"]').click();

      // Small delay between requests
    }

    // All answers should eventually appear
    await expect(page.getByText('Answer 1.')).toBeVisible({ timeout: 10000 });
    await expect(page.getByText('Answer 2.')).toBeVisible({ timeout: 10000 });
    await expect(page.getByText('Answer 3.')).toBeVisible({ timeout: 10000 });

    // UI should not be frozen
    await expect(page.locator('button[type="submit"]:has-text("Invia")')).toBeVisible();
  });

  test('should measure P95 latency for typical Q&A workflow', async ({ page }) => {
    const { mockQA } = await setupQATestEnvironment(page);

    const latencies: number[] = [];

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    // Perform 20 requests to measure P95
    for (let i = 1; i <= 20; i++) {
      await mockQA({
        answer: `Answer ${i}.`,
        snippets: [{ text: `Snippet ${i}`, source: 'test.pdf', page: 1, line: null }],
        messageId: `msg-p95-${i}`,
      });

      const start = Date.now();
      await page.fill('#message-input', `Question ${i}`);
      await page.locator('button[type="submit"]').click();
      await expect(page.getByText(`Answer ${i}.`)).toBeVisible({ timeout: 5000 });

      latencies.push(Date.now() - start);
    }

    // Calculate P95 (95th percentile)
    latencies.sort((a, b) => a - b);
    const p95Index = Math.ceil(latencies.length * 0.95) - 1;
    const p95Latency = latencies[p95Index];

    // P95 should be under 3000ms
    expect(p95Latency).toBeLessThan(3000);
  }, 120000); // Increase timeout for 20-request test

  test('should handle page visibility changes without performance impact', async ({ page }) => {
    const { mockQA } = await setupQATestEnvironment(page);

    await mockQA({
      answer: 'Background test answer.',
      snippets: [],
      messageId: 'msg-bg-1',
    });

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
    await expect(page.getByText('Background test answer.')).toBeVisible({ timeout: 5000 });
  });
});
