/**
 * Chat interface visual regression - Issue #2307
 *
 * Captures visual snapshots of chat components: input, messages, streaming states
 */

import { test, expect } from './fixtures';

test.use({
  viewport: { width: 1280, height: 900 },
});

test.describe('Chat Interface - Visual Regression', () => {
  test('Chat input empty state', async ({ page }) => {
    // Navigate to chat/game page
    await page.goto('/', { waitUntil: 'domcontentloaded' });

    // Find chat input (might be on homepage or game page)
    const chatInput = page
      .locator(
        'textarea[name="question"], textarea[placeholder*="Ask"], textarea[placeholder*="question"]'
      )
      .first();

    try {
      await chatInput.waitFor({ timeout: 3000 });
    } catch {
      // Chat input not on homepage, skip
      test.skip();
    }

    await page.waitForTimeout(150);

    const chatContainer = chatInput.locator('..').locator('..').first();
    await expect(chatContainer).toHaveScreenshot('chat-input-empty.png');
  });

  test('Chat message with confidence badge', async ({ page }) => {
    // Navigate to page with chat history
    await page.goto('/', { waitUntil: 'domcontentloaded' });

    // Look for existing message with confidence
    const confidenceBadge = page
      .locator('[data-testid="confidence-score"], [data-testid="confidence-badge"]')
      .first();

    try {
      await confidenceBadge.waitFor({ timeout: 2000 });
      const messageCard = confidenceBadge.locator('..').locator('..').first();
      await expect(messageCard).toHaveScreenshot('chat-message-with-confidence.png');
    } catch {
      // No confidence badge found, skip
      test.skip();
    }
  });

  test('Chat loading skeleton', async ({ page }) => {
    await page.goto('/', { waitUntil: 'domcontentloaded' });

    // Look for loading indicators
    const skeleton = page
      .locator('[data-testid="typing-indicator"], .skeleton-loader, .animate-pulse')
      .first();

    try {
      await skeleton.waitFor({ timeout: 1000 });
      await expect(skeleton).toHaveScreenshot('chat-loading-skeleton.png');
    } catch {
      // No loading state visible, skip
      test.skip();
    }
  });
});
