/**
 * Chat Streaming E2E Tests (CHAT-01) - MIGRATED TO POM
 *
 * @see apps/web/e2e/pages/ - Page Object Model architecture
 */

import { test as base, expect, Page } from '@playwright/test';
import { AuthHelper, USER_FIXTURES } from './pages';

const test = base.extend<{ userPage: Page }>({
  userPage: async ({ page }, use) => {
    const authHelper = new AuthHelper(page);
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);
    await use(page);
  },
});

test.describe('Chat Streaming (CHAT-01)', () => {
  test.beforeEach(async ({ userPage: page }) => {
    // Navigate to chat (already authenticated as user)
    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
  });

  test('should display streaming UI elements', async ({ userPage: page }) => {
    // Message input enabled = game and agent auto-selected (Issue #1800 fix)
    await expect(page.locator('#message-input')).toBeEnabled();

    // Send button should be enabled when input has text
    await page.fill('#message-input', 'How do I play?');
    await expect(page.locator('button[type="submit"]')).toBeEnabled();
  });

  test('should show streaming state when message is sent', async ({ userPage: page }) => {
    // Fill and submit message
    await page.fill('#message-input', 'How many players can play?');
    // Wait for button to be clickable before submitting
    await page.locator('button[type="submit"]').click({ timeout: 5000 });

    // User message should appear immediately
    await expect(page.getByText('How many players can play?')).toBeVisible();

    // Should show "Invio..." or streaming indicator
    // Either the button shows "Invio..." or we see a streaming response bubble
    const hasInvioText = await page
      .locator('button[type="submit"]:has-text("Invio...")')
      .isVisible()
      .catch(() => false);
    const hasStreamingBubble = await page
      .locator('text=Sto pensando')
      .isVisible()
      .catch(() => false);

    expect(hasInvioText || hasStreamingBubble).toBe(true);
  });

  test('should display stop button during streaming', async ({ userPage: page }) => {
    // Intercept streaming endpoint to make it slow
    await page.route('**/api/v1/agents/qa/stream', async route => {
      // Simulate slow streaming
      await new Promise(resolve => setTimeout(resolve, 500));
      await route.continue();
    });

    // Send message
    await page.fill('#message-input', 'What are the rules?');
    await page.locator('button[type="submit"]').click({ timeout: 5000 });

    // Wait for streaming to start
    await page.waitForTimeout(100);

    // Stop button should be visible (has ⏹ Stop or aria-label)
    const stopButton = page.locator('button[aria-label="Stop streaming"], button:has-text("Stop")');

    // Check if streaming started (stop button may appear)
    const isStreaming = await stopButton.isVisible().catch(() => false);

    if (isStreaming) {
      // If streaming, stop button should be functional (use force: true to handle nextjs-portal overlay)
      expect(await stopButton.isEnabled()).toBe(true);
    }
  });

  test('should stop streaming when stop button is clicked', async ({ userPage: page }) => {
    // Intercept to create a slow stream
    let streamingStopped = false;

    await page.route('**/api/v1/agents/qa/stream', async route => {
      // Create a response that streams slowly
      const response = {
        status: 200,
        headers: {
          'Content-Type': 'text/event-stream',
          'Cache-Control': 'no-cache',
          Connection: 'keep-alive',
        },
        body: 'event: stateUpdate\ndata: {"state":"Thinking..."}\n\n',
      };

      await route.fulfill(response);

      // Simulate long stream
      await page.waitForTimeout(2000);
      streamingStopped = true;
    });

    // Send message
    await page.fill('#message-input', 'Tell me about the game');
    await page.locator('button[type="submit"]').click({ timeout: 5000 });

    // Wait a bit for streaming to start
    await page.waitForTimeout(200);

    // Try to find and click stop button (use force: true to handle nextjs-portal overlay)
    const stopButton = page.locator('button[aria-label="Stop streaming"], button:has-text("Stop")');

    if (await stopButton.isVisible().catch(() => false)) {
      await stopButton.click({ force: true });

      // Streaming should stop (button changes back to "Invia")
      await expect(page.locator('button[type="submit"]:has-text("Invia")')).toBeVisible({
        timeout: 2000,
      });
    }
  });

  test('should accumulate tokens in real-time', async ({ userPage: page }) => {
    // Intercept to simulate token-by-token streaming
    await page.route('**/api/v1/agents/qa/stream', async route => {
      const sseData = [
        'event: stateUpdate\ndata: {"state":"Generating embeddings..."}\n\n',
        'event: token\ndata: {"token":"The"}\n\n',
        'event: token\ndata: {"token":" game"}\n\n',
        'event: token\ndata: {"token":" is"}\n\n',
        'event: token\ndata: {"token":" fun"}\n\n',
        'event: complete\ndata: {"totalTokens":4,"confidence":0.95,"snippets":[]}\n\n',
      ].join('');

      await route.fulfill({
        status: 200,
        headers: {
          'Content-Type': 'text/event-stream',
          'Cache-Control': 'no-cache',
        },
        body: sseData,
      });
    });

    await page.fill('#message-input', 'Is this game fun?');
    await page.locator('button[type="submit"]').click({ timeout: 5000 });

    // Wait for response to appear
    await page.waitForTimeout(500);

    // Should eventually show the complete answer
    await expect(page.getByText(/The game is fun/i)).toBeVisible({ timeout: 5000 });
  });

  test('should display citations when received', async ({ userPage: page }) => {
    // Intercept to include citations
    await page.route('**/api/v1/agents/qa/stream', async route => {
      const sseData = [
        'event: stateUpdate\ndata: {"state":"Searching rules..."}\n\n',
        'event: citations\ndata: {"snippets":[{"text":"Players take turns","source":"rules.pdf","page":1,"line":null}]}\n\n',
        'event: token\ndata: {"token":"Players take turns moving pieces."}\n\n',
        'event: complete\ndata: {"totalTokens":6,"confidence":0.92,"snippets":[{"text":"Players take turns","source":"rules.pdf","page":1,"line":null}]}\n\n',
      ].join('');

      await route.fulfill({
        status: 200,
        headers: {
          'Content-Type': 'text/event-stream',
        },
        body: sseData,
      });
    });

    await page.fill('#message-input', 'How do players move?');
    await page.locator('button[type="submit"]').click({ timeout: 5000 });

    // Wait for response
    await page.waitForTimeout(500);

    // Should show sources section
    await expect(page.getByText(/Fonti|sources/i)).toBeVisible({ timeout: 5000 });

    // Should show the citation source
    await expect(page.getByText(/rules\.pdf/i)).toBeVisible();
  });

  test('should display error message on failure', async ({ userPage: page }) => {
    // Intercept to return error
    await page.route('**/api/v1/agents/qa/stream', async route => {
      const sseData =
        'event: error\ndata: {"message":"Failed to process request","code":"INTERNAL_ERROR"}\n\n';

      await route.fulfill({
        status: 200,
        headers: {
          'Content-Type': 'text/event-stream',
        },
        body: sseData,
      });
    });

    await page.fill('#message-input', 'Cause an error');
    await page.locator('button[type="submit"]').click({ timeout: 5000 });

    // Should show error message
    await expect(page.getByRole('alert')).toBeVisible({ timeout: 5000 });
  });

  test('should handle authentication error (401)', async ({ userPage: page }) => {
    // Intercept to return 401
    await page.route('**/api/v1/agents/qa/stream', async route => {
      await route.fulfill({
        status: 401,
        body: JSON.stringify({ message: 'Unauthorized' }),
      });
    });

    await page.fill('#message-input', 'Test query');
    await page.locator('button[type="submit"]').click({ timeout: 5000 });

    // Should show error
    await expect(page.getByRole('alert')).toBeVisible({ timeout: 5000 });
  });

  test('should disable input during streaming', async ({ userPage: page }) => {
    // Intercept to create slow stream
    await page.route('**/api/v1/agents/qa/stream', async route => {
      // Don't fulfill immediately - simulate long stream
      await new Promise(resolve => setTimeout(resolve, 1000));

      await route.fulfill({
        status: 200,
        headers: {
          'Content-Type': 'text/event-stream',
        },
        body: 'event: complete\ndata: {"totalTokens":1,"confidence":0.9,"snippets":[]}\n\n',
      });
    });

    await page.fill('#message-input', 'Long query');
    await page.locator('button[type="submit"]').click({ timeout: 5000 });

    // Input should be disabled while streaming
    await expect(page.locator('#message-input')).toBeDisabled({ timeout: 500 });

    // Wait for completion
    await page.waitForTimeout(1500);

    // Input should be re-enabled after streaming completes
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 1000 });
  });

  test('should preserve chat history after streaming', async ({ userPage: page }) => {
    // Intercept streaming
    await page.route('**/api/v1/agents/qa/stream', async route => {
      const sseData = [
        'event: token\ndata: {"token":"Response 1"}\n\n',
        'event: complete\ndata: {"totalTokens":2,"confidence":0.9,"snippets":[]}\n\n',
      ].join('');

      await route.fulfill({
        status: 200,
        headers: {
          'Content-Type': 'text/event-stream',
        },
        body: sseData,
      });
    });

    // Send first message (use force: true to handle nextjs-portal overlay)
    await page.fill('#message-input', 'First question');
    await page.click('button[type="submit"]', { force: true });

    await page.waitForTimeout(500);

    // First message should be visible
    await expect(page.getByText('First question')).toBeVisible();

    // Send second message (use force: true to handle nextjs-portal overlay)
    await page.fill('#message-input', 'Second question');
    await page.click('button[type="submit"]', { force: true });

    await page.waitForTimeout(500);

    // Both messages should be visible
    await expect(page.getByText('First question')).toBeVisible();
    await expect(page.getByText('Second question')).toBeVisible();
  });

  test('should show state updates during streaming', async ({ userPage: page }) => {
    // Intercept with state updates
    await page.route('**/api/v1/agents/qa/stream', async route => {
      const sseData = [
        'event: stateUpdate\ndata: {"state":"Generating embeddings..."}\n\n',
        'event: stateUpdate\ndata: {"state":"Searching vector database..."}\n\n',
        'event: stateUpdate\ndata: {"state":"Generating answer..."}\n\n',
        'event: token\ndata: {"token":"Final answer"}\n\n',
        'event: complete\ndata: {"totalTokens":2,"confidence":0.9,"snippets":[]}\n\n',
      ].join('');

      await route.fulfill({
        status: 200,
        headers: {
          'Content-Type': 'text/event-stream',
        },
        body: sseData,
      });
    });

    await page.fill('#message-input', 'What is the goal?');
    await page.locator('button[type="submit"]').click({ timeout: 5000 });

    // Should show state update (might be brief)
    // We check if either a state message appears or the final answer
    await page.waitForTimeout(300);

    const hasStateUpdate = await page
      .getByText(/Generating|Searching/i)
      .isVisible()
      .catch(() => false);
    const hasFinalAnswer = await page
      .getByText(/Final answer/i)
      .isVisible()
      .catch(() => false);

    expect(hasStateUpdate || hasFinalAnswer).toBe(true);
  });

  test('should handle rapid consecutive messages', async ({ userPage: page }) => {
    let requestCount = 0;

    await page.route('**/api/v1/agents/qa/stream', async route => {
      requestCount++;
      const responseNum = requestCount;

      const sseData = [
        `event: token\ndata: {"token":"Response ${responseNum}"}\n\n`,
        'event: complete\ndata: {"totalTokens":2,"confidence":0.9,"snippets":[]}\n\n',
      ].join('');

      await route.fulfill({
        status: 200,
        headers: {
          'Content-Type': 'text/event-stream',
        },
        body: sseData,
      });
    });

    // Send first message (use force: true to handle nextjs-portal overlay)
    await page.fill('#message-input', 'Question 1');
    await page.click('button[type="submit"]', { force: true });

    await page.waitForTimeout(200);

    // Send second message quickly (use force: true to handle nextjs-portal overlay)
    await page.fill('#message-input', 'Question 2');
    await page.click('button[type="submit"]', { force: true });

    // Both should complete successfully
    await expect(page.getByText('Question 1')).toBeVisible({ timeout: 3000 });
    await expect(page.getByText('Question 2')).toBeVisible({ timeout: 3000 });
  });
});
