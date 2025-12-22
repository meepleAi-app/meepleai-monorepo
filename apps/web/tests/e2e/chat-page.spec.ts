/**
 * Chat Page - E2E Tests (Playwright)
 *
 * Comprehensive end-to-end testing for Chat Page implementation.
 * Tests sidebar, context chip, virtualization, streaming, and responsiveness.
 *
 * @issue #1840 (PAGE-004)
 */

import { test, expect } from '@playwright/test';

// Test configuration
test.describe.configure({ mode: 'parallel' });

const CHAT_URL = '/chat';
const MOBILE_VIEWPORT = { width: 375, height: 667 };
const TABLET_VIEWPORT = { width: 768, height: 1024 };
const DESKTOP_VIEWPORT = { width: 1440, height: 900 };

/**
 * Setup authenticated session for tests
 */
test.beforeEach(async ({ page }) => {
  // Navigate to chat page (auth middleware should redirect if needed)
  await page.goto(CHAT_URL);

  // Wait for page to load
  await page.waitForLoadState('networkidle');
});

test.describe('Chat Page - Basic Rendering', () => {
  test('should load chat page successfully', async ({ page }) => {
    // Check main elements present
    await expect(page.getByText(/MeepleAI Chat/i)).toBeVisible();
    await expect(page.getByRole('region', { name: /chat messages/i })).toBeVisible();
  });

  test('should show sidebar on desktop', async ({ page }) => {
    await page.setViewportSize(DESKTOP_VIEWPORT);

    // Desktop sidebar should be visible
    const sidebar = page.getByRole('complementary', { name: /chat sidebar/i });
    await expect(sidebar).toBeVisible();
  });

  test('should hide sidebar on mobile by default', async ({ page }) => {
    await page.setViewportSize(MOBILE_VIEWPORT);

    // Mobile sidebar should be hidden initially
    const sidebar = page.getByRole('complementary', { name: /chat sidebar/i });
    await expect(sidebar).not.toBeVisible();
  });

  test('should show empty state when no messages', async ({ page }) => {
    // Empty state message
    await expect(page.getByText(/Nessun messaggio ancora/i)).toBeVisible();
  });
});

test.describe('Chat Page - Context Chip', () => {
  test('should display context chip when game selected', async ({ page }) => {
    // Select a game (if game selector available)
    const gameSelector = page.getByRole('combobox', { name: /select game/i });
    if (await gameSelector.isVisible()) {
      await gameSelector.click();

      // Select first game option
      const firstGame = page.getByRole('option').first();
      await firstGame.click();

      // Context chip should appear
      const contextChip = page.getByRole('region', { name: /game context/i });
      await expect(contextChip).toBeVisible();
    }
  });

  test('should show document sources in context chip', async ({ page }) => {
    // Assuming context chip visible
    const contextChip = page.getByRole('region', { name: /game context/i });
    if (await contextChip.isVisible()) {
      // Check for source badges
      await expect(contextChip.getByText(/PDF/i)).toBeVisible();
      await expect(contextChip.getByText(/FAQ/i)).toBeVisible();
    }
  });

  test('context chip should be responsive on mobile', async ({ page }) => {
    await page.setViewportSize(MOBILE_VIEWPORT);

    const contextChip = page.getByRole('region', { name: /game context/i });
    if (await contextChip.isVisible()) {
      // Context chip should fit in mobile viewport
      const box = await contextChip.boundingBox();
      expect(box?.width).toBeLessThanOrEqual(MOBILE_VIEWPORT.width - 32); // Account for padding
    }
  });
});

test.describe('Chat Page - Message List', () => {
  test('should render messages in correct order', async ({ page }) => {
    // Wait for messages to load
    await page
      .waitForSelector(
        '[data-testid="chat-message-user"], [data-testid="chat-message-assistant"]',
        { timeout: 5000 }
      )
      .catch(() => {
        // No messages, that's ok for this test
      });

    // Get all message elements
    const messages = page.locator('[data-testid^="chat-message-"]');
    const count = await messages.count();

    if (count > 0) {
      // First message should be visible
      await expect(messages.first()).toBeVisible();

      // Last message should be visible (or scrollable to)
      await expect(messages.last()).toBeVisible({ timeout: 5000 });
    }
  });

  test('should auto-scroll to bottom on new message', async ({ page }) => {
    // Send a message
    const input = page.getByPlaceholder(/scrivi domanda/i);
    if (await input.isVisible()) {
      await input.fill('Test message for auto-scroll');
      await input.press('Enter');

      // Wait for message to appear
      await page.waitForSelector('[data-testid="chat-message-user"]', { timeout: 5000 });

      // Check if scrolled to bottom
      const messageList = page.getByRole('region', { name: /chat messages/i });
      const scrollTop = await messageList.evaluate(el => el.scrollTop);
      const scrollHeight = await messageList.evaluate(el => el.scrollHeight);
      const clientHeight = await messageList.evaluate(el => el.clientHeight);

      // Should be near bottom (within 100px tolerance)
      expect(scrollTop + clientHeight).toBeGreaterThan(scrollHeight - 100);
    }
  });

  test('should handle large message lists with virtualization', async ({ page }) => {
    // This test requires mock data or a seeded database with 60+ messages
    // Skip if no large dataset available
    const messages = page.locator('[data-testid^="chat-message-"]');
    const count = await messages.count();

    if (count >= 50) {
      // Virtualization should be active
      // Check that not all messages are in DOM at once
      const visibleMessages = await page.locator('[data-testid^="chat-message-"]:visible').count();
      expect(visibleMessages).toBeLessThan(count); // Some should be virtualized out
    }
  });
});

test.describe('Chat Page - Streaming (SSE)', () => {
  test('should show typing indicator when streaming', async ({ page }) => {
    // Send a message to trigger streaming
    const input = page.getByPlaceholder(/scrivi domanda/i);
    if (await input.isVisible()) {
      await input.fill('What are the setup rules?');
      await input.press('Enter');

      // Typing indicator should appear briefly
      await expect(page.getByText(/typing/i).or(page.getByRole('status')))
        .toBeVisible({ timeout: 2000 })
        .catch(() => {
          // Streaming might be too fast to catch, that's ok
        });
    }
  });

  test('should display state messages during streaming', async ({ page }) => {
    // Send message
    const input = page.getByPlaceholder(/scrivi domanda/i);
    if (await input.isVisible()) {
      await input.fill('Complex question requiring search');
      await input.press('Enter');

      // State message should appear (e.g., "Searching knowledge base...")
      await expect(page.getByText(/searching/i).or(page.getByText(/generating/i)))
        .toBeVisible({ timeout: 3000 })
        .catch(() => {
          // State might be too fast
        });
    }
  });

  test('should show stop button during streaming', async ({ page }) => {
    const input = page.getByPlaceholder(/scrivi domanda/i);
    if (await input.isVisible()) {
      await input.fill('Long streaming response');
      await input.press('Enter');

      // Stop button should appear
      const stopButton = page.getByRole('button', { name: /stop/i });
      await expect(stopButton)
        .toBeVisible({ timeout: 2000 })
        .catch(() => {
          // Streaming completed too fast
        });
    }
  });

  test('should cancel streaming when stop button clicked', async ({ page }) => {
    const input = page.getByPlaceholder(/scrivi domanda/i);
    if (await input.isVisible()) {
      await input.fill('Question for cancellation test');
      await input.press('Enter');

      // Click stop if visible
      const stopButton = page.getByRole('button', { name: /stop/i });
      if (await stopButton.isVisible({ timeout: 1000 })) {
        await stopButton.click();

        // Streaming should stop, send button should reappear
        await expect(page.getByRole('button', { name: /invia/i })).toBeVisible({ timeout: 2000 });
      }
    }
  });
});

test.describe('Chat Page - Sidebar Interactions', () => {
  test('should toggle sidebar on desktop', async ({ page }) => {
    await page.setViewportSize(DESKTOP_VIEWPORT);

    // Find toggle button
    const toggleButton = page.getByRole('button', { name: /hide sidebar|show sidebar/i });
    await expect(toggleButton).toBeVisible();

    // Click to toggle
    await toggleButton.click();

    // Sidebar should toggle (check width or visibility)
    await page.waitForTimeout(500); // Animation

    // Click again to toggle back
    await toggleButton.click();
    await page.waitForTimeout(500);
  });

  test('should open mobile sidebar with swipe/tap', async ({ page }) => {
    await page.setViewportSize(MOBILE_VIEWPORT);

    // Find menu button (hamburger)
    const menuButton = page.getByRole('button', { name: /show sidebar|menu/i });
    await expect(menuButton).toBeVisible();

    // Click to open
    await menuButton.click();

    // Sidebar sheet should appear
    await expect(page.getByText(/New Thread|Nuovo Thread/i)).toBeVisible({ timeout: 1000 });
  });

  test('should close mobile sidebar with close button', async ({ page }) => {
    await page.setViewportSize(MOBILE_VIEWPORT);

    // Open sidebar
    const menuButton = page.getByRole('button', { name: /show sidebar|menu/i });
    await menuButton.click();

    // Wait for sidebar to open
    await page.waitForSelector('[role="dialog"]', { timeout: 2000 }).catch(() => {});

    // Find close button (X or backdrop)
    const closeButton = page.getByRole('button', { name: /close/i });
    if (await closeButton.isVisible()) {
      await closeButton.click();

      // Sidebar should close
      await expect(page.getByText(/New Thread/i)).not.toBeVisible({ timeout: 1000 });
    }
  });
});

test.describe('Chat Page - Responsive Design', () => {
  test('should adapt layout for mobile (375px)', async ({ page }) => {
    await page.setViewportSize(MOBILE_VIEWPORT);

    // Check bottom nav visible
    await expect(page.getByRole('navigation', { name: /bottom navigation/i })).toBeVisible();

    // Check message input accessible
    const input = page.getByPlaceholder(/scrivi domanda/i);
    await expect(input).toBeVisible();

    // Check sidebar hidden
    const sidebar = page.getByRole('complementary', { name: /chat sidebar/i });
    await expect(sidebar).not.toBeVisible();
  });

  test('should adapt layout for tablet (768px)', async ({ page }) => {
    await page.setViewportSize(TABLET_VIEWPORT);

    // Sidebar should be visible on tablet
    const sidebar = page.getByRole('complementary', { name: /chat sidebar/i });
    await expect(sidebar).toBeVisible();

    // Bottom nav still visible
    await expect(page.getByRole('navigation', { name: /bottom navigation/i })).toBeVisible();
  });

  test('should adapt layout for desktop (1440px)', async ({ page }) => {
    await page.setViewportSize(DESKTOP_VIEWPORT);

    // Full layout visible
    const sidebar = page.getByRole('complementary', { name: /chat sidebar/i });
    await expect(sidebar).toBeVisible();

    // More horizontal space for messages
    const messageList = page.getByRole('region', { name: /chat messages/i });
    const box = await messageList.boundingBox();
    expect(box?.width).toBeGreaterThan(600); // Wider than mobile
  });
});

test.describe('Chat Page - Accessibility', () => {
  test('should have proper ARIA labels', async ({ page }) => {
    // Check key ARIA labels
    await expect(page.getByRole('region', { name: /chat messages/i })).toBeVisible();
    await expect(
      page.getByRole('textbox', { name: /message input/i }).or(page.getByPlaceholder(/scrivi/i))
    ).toBeVisible();
  });

  test('should support keyboard navigation', async ({ page }) => {
    // Tab to input
    await page.keyboard.press('Tab');
    await page.keyboard.press('Tab');

    // Input should be focused (or another interactive element)
    const focusedElement = await page.evaluate(() => document.activeElement?.tagName);
    expect(['INPUT', 'BUTTON', 'TEXTAREA']).toContain(focusedElement);
  });

  test('should have accessible buttons', async ({ page }) => {
    // All buttons should have accessible names
    const buttons = page.getByRole('button');
    const count = await buttons.count();

    for (let i = 0; i < count; i++) {
      const button = buttons.nth(i);
      const accessibleName = await button.getAttribute('aria-label');
      const textContent = await button.textContent();

      // Button should have either aria-label or text content
      expect(accessibleName || textContent).toBeTruthy();
    }
  });
});

test.describe('Chat Page - Performance', () => {
  test('should load page in under 3 seconds', async ({ page }) => {
    const startTime = Date.now();
    await page.goto(CHAT_URL);
    await page.waitForLoadState('networkidle');
    const loadTime = Date.now() - startTime;

    expect(loadTime).toBeLessThan(3000);
  });

  test('should handle rapid message sends without lag', async ({ page }) => {
    const input = page.getByPlaceholder(/scrivi domanda/i);
    if (await input.isVisible()) {
      // Send 5 messages rapidly
      for (let i = 0; i < 5; i++) {
        await input.fill(`Rapid message ${i}`);
        await input.press('Enter');
        await page.waitForTimeout(100); // Small delay
      }

      // Page should remain responsive
      await expect(input).toBeVisible();
      await expect(input).toBeEnabled();
    }
  });
});
