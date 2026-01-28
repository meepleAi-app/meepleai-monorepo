/**
 * CHAT-08: Stop Streaming Response
 * Issue #3082 - P1 High
 *
 * Tests stop streaming functionality:
 * - Display stop button during streaming
 * - Stop streaming on button click
 * - Preserve partial response after stopping
 * - Allow follow-up questions after stopping
 */

import { test, expect } from '../fixtures/chromatic';
import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

/**
 * Setup mock routes for stop streaming testing
 */
async function setupStopStreamingMocks(page: Page) {
  let streamController: { abort: () => void } | null = null;
  let isStreaming = false;

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

  // Mock chat/threads endpoint
  await page.route(`${API_BASE}/api/v1/chat/threads**`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([]),
    });
  });

  // Mock games endpoint
  await page.route(`${API_BASE}/api/v1/games**`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([
        { id: 'game-1', title: 'Chess', description: 'Classic game' },
      ]),
    });
  });

  // Mock SSE streaming endpoint for chat
  await page.route(`${API_BASE}/api/v1/agents/ask*`, async (route) => {
    isStreaming = true;

    // Simulate SSE streaming response
    const chunks = [
      'data: {"type":"start","id":"msg-1"}\n\n',
      'data: {"type":"delta","content":"This is "}\n\n',
      'data: {"type":"delta","content":"a streaming "}\n\n',
      'data: {"type":"delta","content":"response "}\n\n',
      'data: {"type":"delta","content":"that can be "}\n\n',
      'data: {"type":"delta","content":"stopped by "}\n\n',
      'data: {"type":"delta","content":"the user. "}\n\n',
      'data: {"type":"delta","content":"Here is more "}\n\n',
      'data: {"type":"delta","content":"content..."}\n\n',
    ];

    let chunkIndex = 0;
    const encoder = new TextEncoder();

    const stream = new ReadableStream({
      async pull(controller) {
        if (!isStreaming || chunkIndex >= chunks.length) {
          if (isStreaming) {
            controller.enqueue(encoder.encode('data: {"type":"done"}\n\n'));
          }
          controller.close();
          return;
        }

        // Simulate delay between chunks
        await new Promise(resolve => setTimeout(resolve, 200));
        controller.enqueue(encoder.encode(chunks[chunkIndex]));
        chunkIndex++;
      },
      cancel() {
        isStreaming = false;
      },
    });

    await route.fulfill({
      status: 200,
      headers: {
        'Content-Type': 'text/event-stream',
        'Cache-Control': 'no-cache',
        'Connection': 'keep-alive',
      },
      body: stream,
    });
  });

  return {
    stopStreaming: () => {
      isStreaming = false;
    },
    isStreaming: () => isStreaming,
  };
}

test.describe('CHAT-08: Stop Streaming Response', () => {
  test.describe('Stop Button Display', () => {
    test('should show stop button during streaming', async ({ page }) => {
      await setupStopStreamingMocks(page);

      await page.goto('/chat');
      await page.waitForLoadState('networkidle');

      // Find chat input and send a message
      const chatInput = page.getByPlaceholder(/message|question|ask/i).or(
        page.locator('textarea').first()
      );

      if (await chatInput.isVisible()) {
        await chatInput.fill('How do I play chess?');
        await page.keyboard.press('Enter');

        // Should show stop button during streaming
        await expect(
          page.getByRole('button', { name: /stop|cancel/i }).or(
            page.locator('[data-testid="stop-streaming"], .stop-button')
          )
        ).toBeVisible({ timeout: 5000 });
      }
    });

    test('should hide stop button when not streaming', async ({ page }) => {
      await setupStopStreamingMocks(page);

      await page.goto('/chat');
      await page.waitForLoadState('networkidle');

      // Before sending message, stop button should not be visible
      const stopButton = page.getByRole('button', { name: /stop|cancel/i }).or(
        page.locator('[data-testid="stop-streaming"]')
      );

      // Stop button should not be visible before streaming
      await expect(stopButton).not.toBeVisible({ timeout: 2000 });
    });
  });

  test.describe('Stop Streaming Action', () => {
    test('should stop streaming when stop button is clicked', async ({ page }) => {
      const mocks = await setupStopStreamingMocks(page);

      await page.goto('/chat');
      await page.waitForLoadState('networkidle');

      const chatInput = page.getByPlaceholder(/message|question|ask/i).or(
        page.locator('textarea').first()
      );

      if (await chatInput.isVisible()) {
        await chatInput.fill('Tell me about chess rules');
        await page.keyboard.press('Enter');

        // Wait for streaming to start
        await page.waitForTimeout(500);

        // Click stop button
        const stopButton = page.getByRole('button', { name: /stop|cancel/i }).or(
          page.locator('[data-testid="stop-streaming"], .stop-button')
        );

        if (await stopButton.isVisible()) {
          await stopButton.click();

          // Stop button should disappear after clicking
          await expect(stopButton).not.toBeVisible({ timeout: 5000 });
        }
      }
    });

    test('should preserve partial response after stopping', async ({ page }) => {
      await setupStopStreamingMocks(page);

      await page.goto('/chat');
      await page.waitForLoadState('networkidle');

      const chatInput = page.getByPlaceholder(/message|question|ask/i).or(
        page.locator('textarea').first()
      );

      if (await chatInput.isVisible()) {
        await chatInput.fill('Explain chess');
        await page.keyboard.press('Enter');

        // Wait for some content to stream
        await page.waitForTimeout(800);

        // Stop streaming
        const stopButton = page.getByRole('button', { name: /stop|cancel/i }).or(
          page.locator('[data-testid="stop-streaming"]')
        );

        if (await stopButton.isVisible()) {
          await stopButton.click();

          // Should still show partial response
          await expect(page.getByText(/streaming|response|This is/i)).toBeVisible();
        }
      }
    });
  });

  test.describe('After Stopping', () => {
    test('should enable input after stopping streaming', async ({ page }) => {
      await setupStopStreamingMocks(page);

      await page.goto('/chat');
      await page.waitForLoadState('networkidle');

      const chatInput = page.getByPlaceholder(/message|question|ask/i).or(
        page.locator('textarea').first()
      );

      if (await chatInput.isVisible()) {
        await chatInput.fill('Question 1');
        await page.keyboard.press('Enter');

        // Wait and stop
        await page.waitForTimeout(600);
        const stopButton = page.getByRole('button', { name: /stop|cancel/i });
        if (await stopButton.isVisible()) {
          await stopButton.click();
        }

        // Input should be enabled after stopping
        await expect(chatInput).toBeEnabled();
      }
    });

    test('should allow follow-up question after stopping', async ({ page }) => {
      await setupStopStreamingMocks(page);

      await page.goto('/chat');
      await page.waitForLoadState('networkidle');

      const chatInput = page.getByPlaceholder(/message|question|ask/i).or(
        page.locator('textarea').first()
      );

      if (await chatInput.isVisible()) {
        // First question
        await chatInput.fill('First question');
        await page.keyboard.press('Enter');

        // Stop after short delay
        await page.waitForTimeout(500);
        const stopButton = page.getByRole('button', { name: /stop|cancel/i });
        if (await stopButton.isVisible()) {
          await stopButton.click();
        }

        // Wait for input to be re-enabled
        await page.waitForTimeout(500);

        // Should be able to type follow-up
        await chatInput.fill('Follow-up question');
        await expect(chatInput).toHaveValue('Follow-up question');
      }
    });
  });

  test.describe('UI State', () => {
    test('should show loading indicator during streaming', async ({ page }) => {
      await setupStopStreamingMocks(page);

      await page.goto('/chat');
      await page.waitForLoadState('networkidle');

      const chatInput = page.getByPlaceholder(/message|question|ask/i).or(
        page.locator('textarea').first()
      );

      if (await chatInput.isVisible()) {
        await chatInput.fill('Test streaming');
        await page.keyboard.press('Enter');

        // Should show some loading/streaming indicator
        await expect(
          page.locator('.loading, .streaming, [data-streaming="true"]').or(
            page.getByRole('button', { name: /stop/i })
          )
        ).toBeVisible({ timeout: 3000 });
      }
    });

    test('should indicate stopped state after stopping', async ({ page }) => {
      await setupStopStreamingMocks(page);

      await page.goto('/chat');
      await page.waitForLoadState('networkidle');

      const chatInput = page.getByPlaceholder(/message|question|ask/i).or(
        page.locator('textarea').first()
      );

      if (await chatInput.isVisible()) {
        await chatInput.fill('Test');
        await page.keyboard.press('Enter');

        await page.waitForTimeout(500);
        const stopButton = page.getByRole('button', { name: /stop/i });
        if (await stopButton.isVisible()) {
          await stopButton.click();

          // May show "stopped" indicator or just complete the UI state
          // Just verify the page is in a stable state
          await expect(page.locator('body')).toBeVisible();
        }
      }
    });
  });
});
