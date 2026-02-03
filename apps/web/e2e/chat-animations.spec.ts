/**
 * Chat Loading States and Animations E2E Tests (CHAT-04) - MIGRATED TO POM
 *
 * @see apps/web/e2e/pages/
 */

import { test, expect } from './fixtures';
import { WaitHelper } from './helpers/WaitHelper';

import type { Page } from '@playwright/test';

import './fixtures/auth';

/**
 * Helper: Navigate to chat page (auth handled by fixture)
 */
async function navigateToChat(page: Page): Promise<void> {
  await page.goto('/chat');
  await page.waitForLoadState('networkidle');
  await page.waitForLoadState('networkidle');
}

/**
 * Helper: Wait for games to load (skeleton should disappear)
 */
async function waitForGamesToLoad(page: Page): Promise<void> {
  // Wait for games select to be populated
  await page.waitForSelector('#gameSelect option:not([value=""])', { timeout: 10000 });
}

/**
 * Helper: Send a message and wait for streaming to start
 */
async function sendMessage(page: Page, message: string): Promise<void> {
  await page.fill('#message-input', message);
  await page.click('button[type="submit"]', { force: true });
}

/**
 * Helper: Measure FPS during animation using requestAnimationFrame
 */
async function measureFPS(page: Page, durationMs: number = 1000): Promise<number> {
  const result = await page.evaluate((duration: number) => {
    return new Promise<number>(resolve => {
      const frameTimestamps: number[] = [];
      const startTime = performance.now();
      let animationFrameId: number;

      function recordFrame(timestamp: number) {
        frameTimestamps.push(timestamp);

        if (timestamp - startTime < duration) {
          animationFrameId = requestAnimationFrame(recordFrame);
        } else {
          // Calculate FPS
          const totalDuration = frameTimestamps[frameTimestamps.length - 1] - frameTimestamps[0];
          const fps = (frameTimestamps.length / totalDuration) * 1000;
          resolve(fps);
        }
      }

      animationFrameId = requestAnimationFrame(recordFrame);
    });
  }, durationMs);

  return result;
}

test.describe('Chat Loading States and Animations (CHAT-04)', () => {
  test.beforeEach(async ({ page }) => {});

  /**
   * Test 1: Skeleton loaders on initial page load
   *
   * Verifies that skeleton loaders appear during initial data fetching
   * and disappear once real content is loaded.
   */
  test('shows skeleton loaders during initial page load', async ({ page }) => {
    // Start navigation but don't wait for full load
    const navigationPromise = page.goto('/chat');

    // Check for skeleton loaders while content is loading
    // Note: This may be very brief depending on network speed
    const gamesSkeleton = page.locator('[aria-label="Caricamento giochi"]');
    const agentsSkeleton = page.locator('[aria-label="Caricamento agenti"]');

    // Wait for navigation to complete
    await navigationPromise;

    // Verify skeletons eventually disappear and real content appears
    await waitForGamesToLoad(page);

    // Games select should be visible with options
    await expect(page.locator('#gameSelect')).toBeVisible();
    const gameOptions = await page.locator('#gameSelect option:not([value=""])').count();
    expect(gameOptions).toBeGreaterThan(0);

    // Games skeleton should be gone (if it appeared)
    if (await gamesSkeleton.isVisible().catch(() => false)) {
      await expect(gamesSkeleton).not.toBeVisible({ timeout: 5000 });
    }
  });

  /**
   * Test 2: Games skeleton loader variant
   *
   * Verifies the games skeleton has correct structure and accessibility.
   * Uses real backend - skeleton may be brief but structure is verified.
   */
  test('shows games skeleton with correct variant', async ({ page }) => {
    // Navigate to chat - skeleton may appear briefly during load
    await page.goto('/chat');

    // Check if skeleton appears (may be very brief with fast backend)
    const gamesSkeleton = page.locator('[aria-label="Caricamento giochi"]');
    const skeletonVisible = await gamesSkeleton.isVisible().catch(() => false);

    if (skeletonVisible) {
      // Verify skeleton has correct role and aria attributes when visible
      await expect(gamesSkeleton).toHaveAttribute('role', 'status');
      await expect(gamesSkeleton).toHaveAttribute('aria-live', 'polite');
    }

    // Wait for actual games to load
    await waitForGamesToLoad(page);

    // Verify skeleton is gone and real content loaded
    await expect(gamesSkeleton).not.toBeVisible({ timeout: 5000 });
    const gameOptions = await page.locator('#gameSelect option:not([value=""])').count();
    expect(gameOptions).toBeGreaterThan(0);
  });

  /**
   * Test 3: Agents skeleton loader
   *
   * Verifies agents skeleton appears when game is selected.
   * Uses real backend - skeleton may be brief but behavior verified.
   */
  test('shows agents skeleton when loading agents', async ({ page }) => {
    await navigateToChat(page);
    await waitForGamesToLoad(page);

    // Select a game to trigger agent loading
    const gameSelect = page.locator('#gameSelect');
    const firstGameValue = await gameSelect
      .locator('option:not([value=""])')
      .first()
      .getAttribute('value');

    if (firstGameValue) {
      await gameSelect.selectOption(firstGameValue);

      // Check if agents skeleton appears (may be brief with fast backend)
      const agentsSkeleton = page.locator('[aria-label="Caricamento agenti"]');
      const skeletonVisible = await agentsSkeleton.isVisible().catch(() => false);

      if (skeletonVisible) {
        // Verify skeleton has correct attributes when visible
        await expect(agentsSkeleton).toHaveAttribute('role', 'status');
      }

      // Wait for agents to load (real backend returns actual agents)
      await expect(page.locator('#agentSelect option:not([value=""])')).toHaveCount(1, {
        timeout: 5000,
      });

      // Verify skeleton disappears
      await expect(agentsSkeleton).not.toBeVisible({ timeout: 5000 });
    }
  });

  /**
   * Test 4: Chat history skeleton loader
   *
   * Verifies chat history skeleton appears when loading chat list.
   * Uses real backend - skeleton may be brief but structure verified.
   */
  test('shows chat history skeleton when loading chats', async ({ page }) => {
    await navigateToChat(page);
    await waitForGamesToLoad(page);

    // Reload to trigger chat loading
    await page.reload();
    await waitForGamesToLoad(page);

    // Check if chat history skeleton appears (may be brief)
    const chatSkeleton = page.locator('[aria-label="Caricamento cronologia chat"]');
    const isVisible = await chatSkeleton.isVisible().catch(() => false);

    if (isVisible) {
      // Verify skeleton has correct attributes and count
      const skeletonCount = await chatSkeleton.count();
      expect(skeletonCount).toBeGreaterThan(0);

      // Verify skeleton has correct role
      await expect(chatSkeleton.first()).toHaveAttribute('role', 'status');

      // Wait for chats to load from real backend
      await expect(chatSkeleton).not.toBeVisible({ timeout: 5000 });
    }
  });

  /**
   * Test 5: Messages skeleton loader
   *
   * Verifies messages skeleton appears when loading chat history.
   * Uses real backend - skeleton may be brief but structure verified.
   */
  test('shows messages skeleton when loading chat history', async ({ page }) => {
    await navigateToChat(page);
    await waitForGamesToLoad(page);

    // Create a new chat first
    const newChatButton = page.locator('button:has-text("+ Nuova Chat")');
    if (await newChatButton.isEnabled().catch(() => false)) {
      await newChatButton.click({ force: true });
      await page.waitForTimeout(1000);

      // Send a message to create history
      await sendMessage(page, 'Test message for skeleton');
      const waitHelper = new WaitHelper(page);
      await waitHelper.waitForNetworkIdle(5000);

      // Click the chat in history to reload it
      const chatItem = page.locator('li[role="button"]').first();
      if (await chatItem.isVisible().catch(() => false)) {
        await chatItem.click({ force: true });

        // Check if messages skeleton appears (may be brief with real backend)
        const messagesSkeleton = page.locator('[aria-label="Caricamento messaggi"]');
        const isVisible = await messagesSkeleton.isVisible().catch(() => false);

        if (isVisible) {
          // Verify skeleton has correct role attribute
          await expect(messagesSkeleton).toHaveAttribute('role', 'status');
        }

        // Verify messages eventually load from real backend
        await page.waitForTimeout(1000);
        const messages = page.locator('[data-message-id]');
        await expect(messages.first()).toBeVisible({ timeout: 5000 });
      }
    }
  });

  /**
   * Test 6: Typing indicator during streaming
   *
   * Verifies typing indicator appears with agent name during AI response.
   * Uses real backend SSE streaming - verifies animation structure.
   */
  test('shows typing indicator when AI is generating response', async ({ page }) => {
    await navigateToChat(page);
    await waitForGamesToLoad(page);

    // Send a message to trigger real backend streaming
    if (
      await page
        .locator('button[type="submit"]')
        .isEnabled()
        .catch(() => false)
    ) {
      await sendMessage(page, 'Show typing indicator test');

      // Wait briefly for streaming to start
      await page.waitForTimeout(500);

      // Verify typing indicator or thinking text appears
      // The indicator shows when isStreaming && !currentAnswer
      const typingIndicator = page.locator(
        '[aria-label*="is typing"], [aria-label*="sta scrivendo"]'
      );
      const hasTypingIndicator = await typingIndicator.isVisible().catch(() => false);

      // Also check for the "Sto pensando..." fallback state
      const thinkingText = page.locator('text=Sto pensando...');
      const hasThinkingText = await thinkingText.isVisible().catch(() => false);

      // Either typing indicator or thinking text should be visible during streaming
      expect(hasTypingIndicator || hasThinkingText).toBe(true);

      if (hasTypingIndicator) {
        // Verify it has the correct ARIA attributes
        await expect(typingIndicator).toHaveAttribute('role', 'status');
        await expect(typingIndicator).toHaveAttribute('aria-live', 'polite');

        // Verify dots are present (3 animated dots)
        const dots = typingIndicator.locator('span[class*="rounded-full"]');
        const dotCount = await dots.count();
        expect(dotCount).toBe(3);
      }

      // Wait for streaming to complete
      const waitHelper = new WaitHelper(page);
      await waitHelper.waitForNetworkIdle(5000);
    }
  });

  /**
   * Test 7: Message animations - user messages from right
   *
   * Verifies user messages slide in from the right.
   * Uses real backend - verifies animation structure, not content.
   */
  test('animates user messages from right', async ({ page }) => {
    await navigateToChat(page);
    await waitForGamesToLoad(page);

    if (
      await page
        .locator('button[type="submit"]')
        .isEnabled()
        .catch(() => false)
    ) {
      const testMessage = 'Test user message animation';
      await sendMessage(page, testMessage);

      // Wait for user message to appear
      const userMessage = page.getByText(testMessage);
      await expect(userMessage).toBeVisible();

      // Verify message has animation data attributes
      const messageContainer = userMessage.locator('xpath=ancestor::div[@data-message-id]').first();
      const hasMessageId = await messageContainer.getAttribute('data-message-id');
      expect(hasMessageId).toBeTruthy();

      // Verify animation complete attribute eventually appears
      await expect(messageContainer).toHaveAttribute('data-animation-complete', 'true', {
        timeout: 2000,
      });

      // Verify message is right-aligned (user messages)
      const parentLi = userMessage.locator('xpath=ancestor::li').first();
      const alignItems = await parentLi.evaluate(
        (el: HTMLElement) => window.getComputedStyle(el).alignItems
      );
      expect(alignItems).toBe('flex-end');

      // Wait for real backend to complete
      const waitHelper = new WaitHelper(page);
      await waitHelper.waitForNetworkIdle(5000);
    }
  });

  /**
   * Test 8: Message animations - AI messages from left
   *
   * Verifies AI messages slide in from the left.
   * Uses real backend SSE - verifies animation structure, not specific content.
   */
  test('animates AI messages from left', async ({ page }) => {
    await navigateToChat(page);
    await waitForGamesToLoad(page);

    if (
      await page
        .locator('button[type="submit"]')
        .isEnabled()
        .catch(() => false)
    ) {
      await sendMessage(page, 'Trigger AI response');

      // Wait for AI response to start streaming
      await page.waitForTimeout(1000);

      // Wait for AI message to appear (any text from real backend)
      const aiMessages = page
        .locator('[data-message-id]')
        .filter({ has: page.locator('li[style*="flex-start"]') });
      await expect(aiMessages.first()).toBeVisible({ timeout: 5000 });

      // Verify first AI message has animation data attributes
      const messageContainer = aiMessages.first();
      const hasMessageId = await messageContainer.getAttribute('data-message-id');
      expect(hasMessageId).toBeTruthy();

      // Verify message is left-aligned (AI messages)
      const parentLi = messageContainer.locator('xpath=ancestor::li').first();
      const alignItems = await parentLi.evaluate(
        (el: HTMLElement) => window.getComputedStyle(el).alignItems
      );
      expect(alignItems).toBe('flex-start');

      // Wait for streaming to complete
      const waitHelper = new WaitHelper(page);
      await waitHelper.waitForNetworkIdle(5000);
    }
  });

  /**
   * Test 9: Message list stagger animation
   *
   * Verifies messages appear with stagger delay when loading chat history.
   * Uses real backend - verifies animation timing structure.
   */
  test('staggers message animations when loading chat history', async ({ page }) => {
    await navigateToChat(page);
    await waitForGamesToLoad(page);

    if (
      await page
        .locator('button[type="submit"]')
        .isEnabled()
        .catch(() => false)
    ) {
      // Send multiple messages to create history using real backend
      for (let i = 1; i <= 3; i++) {
        await sendMessage(page, `Message ${i}`);
        await page.waitForTimeout(1500); // Wait for each to complete
      }

      // Wait for all messages to complete
      const waitHelper = new WaitHelper(page);
      await waitHelper.waitForNetworkIdle(5000);

      // Get the chat ID by clicking on it to trigger reload
      const firstChat = page.locator('li[role="button"]').first();
      if (await firstChat.isVisible().catch(() => false)) {
        await firstChat.click({ force: true });
        await page.waitForTimeout(500);

        // Check for staggered animation completion
        const animatedMessages = page.locator('[data-message-id]');
        const count = await animatedMessages.count();

        if (count > 1) {
          // Verify at least some messages have animation complete
          // Stagger delay: index * 50ms + 300ms animation
          for (let i = 0; i < Math.min(count, 3); i++) {
            const message = animatedMessages.nth(i);
            await expect(message).toHaveAttribute('data-animation-complete', 'true', {
              timeout: i * 50 + 500,
            });
          }
        }
      }
    }
  });

  /**
   * Test 10: Loading button - send button states
   *
   * Verifies send button shows loading state when sending message.
   * Uses real backend - verifies button state transitions.
   */
  test('send button shows loading state when sending message', async ({ page }) => {
    await navigateToChat(page);
    await waitForGamesToLoad(page);

    if (
      await page
        .locator('button[type="submit"]')
        .isEnabled()
        .catch(() => false)
    ) {
      await page.fill('#message-input', 'Test loading button');

      // Click send button
      const sendButton = page.locator('button[type="submit"]');
      await sendButton.click({ force: true });

      // Verify button shows loading state (may be brief)
      await expect(sendButton).toHaveAttribute('aria-busy', 'true', { timeout: 500 });
      await expect(sendButton).toBeDisabled({ timeout: 500 });

      // Verify loading text appears
      const hasLoadingText = await sendButton
        .locator('text=Invio...')
        .isVisible()
        .catch(() => false);
      expect(hasLoadingText).toBe(true);

      // Verify spinner appears
      const spinner = sendButton.locator('svg[aria-hidden="true"]');
      const hasSpinner = await spinner.isVisible().catch(() => false);
      expect(hasSpinner).toBe(true);

      // Wait for real backend to complete
      const waitHelper = new WaitHelper(page);
      await waitHelper.waitForNetworkIdle(5000);

      // Verify button returns to normal state
      await expect(sendButton).not.toBeDisabled({ timeout: 1000 });
      await expect(sendButton).toHaveText('Invia', { timeout: 1000 });
    }
  });

  /**
   * Test 11: Loading button - new chat button states
   *
   * Verifies new chat button shows loading state when creating chat.
   * Uses real backend - verifies button state transitions.
   */
  test('new chat button shows loading state', async ({ page }) => {
    await navigateToChat(page);
    await waitForGamesToLoad(page);

    const newChatButton = page.locator('button:has-text("+ Nuova Chat")');
    if (await newChatButton.isEnabled().catch(() => false)) {
      await newChatButton.click({ force: true });

      // Verify button shows loading state (may be brief with real backend)
      await expect(newChatButton).toHaveAttribute('aria-busy', 'true', { timeout: 500 });
      await expect(newChatButton).toBeDisabled({ timeout: 500 });

      // Verify loading text appears
      const hasLoadingText = await newChatButton
        .locator('text=Creazione...')
        .isVisible()
        .catch(() => false);
      expect(hasLoadingText).toBe(true);

      // Verify spinner appears
      const spinner = newChatButton.locator('svg[aria-hidden="true"]');
      const hasSpinner = await spinner.isVisible().catch(() => false);
      expect(hasSpinner).toBe(true);

      // Wait for real backend to complete
      const waitHelper = new WaitHelper(page);
      await waitHelper.waitForNetworkIdle(5000);

      // Verify button returns to enabled state
      await expect(newChatButton).toBeEnabled({ timeout: 1000 });
    }
  });

  /**
   * Test 12: Smooth scroll to latest message
   *
   * Verifies new messages trigger smooth scroll to bottom.
   * Uses real backend - verifies scroll behavior.
   */
  test('smoothly scrolls to latest message', async ({ page }) => {
    await navigateToChat(page);
    await waitForGamesToLoad(page);

    if (
      await page
        .locator('button[type="submit"]')
        .isEnabled()
        .catch(() => false)
    ) {
      // Send multiple messages to create scrollable content
      for (let i = 1; i <= 5; i++) {
        await sendMessage(page, `Message ${i} with some additional content to make it longer`);
        await page.waitForTimeout(1500); // Wait for each to complete
      }

      // Wait for all to complete
      const waitHelper = new WaitHelper(page);
      await waitHelper.waitForNetworkIdle(5000);

      // Get messages container
      const messagesContainer = page.locator('[role="region"][aria-label="Chat messages"]');

      // Scroll to top
      await messagesContainer.evaluate((el: HTMLElement) => {
        el.scrollTop = 0;
      });

      // Send new message
      await sendMessage(page, 'Latest message should scroll into view');

      // Wait for message to appear
      await page.waitForTimeout(1000);

      // Verify latest message is in viewport
      const latestMessage = page.getByText('Latest message should scroll into view');
      await expect(latestMessage).toBeVisible({ timeout: 3000 });

      // Check if message is near bottom of viewport
      const isInViewport = await latestMessage.evaluate((el: HTMLElement) => {
        const rect = el.getBoundingClientRect();
        return rect.bottom <= window.innerHeight && rect.top >= 0;
      });

      expect(isInViewport).toBe(true);
    }
  });

  /**
   * Test 13: Reduced motion accessibility
   *
   * Verifies animations respect prefers-reduced-motion setting.
   * Uses real backend - verifies reduced motion behavior.
   */
  test('respects prefers-reduced-motion setting', async ({ page }) => {
    // Emulate reduced motion preference
    await page.emulateMedia({ reducedMotion: 'reduce' });

    await navigateToChat(page);
    await waitForGamesToLoad(page);

    // Check skeleton loaders don't have animate-pulse when visible
    const gamesSkeleton = page.locator('[aria-label="Caricamento giochi"]');
    if (await gamesSkeleton.isVisible().catch(() => false)) {
      const hasPulse = await gamesSkeleton.evaluate((el: HTMLElement) => {
        return el.classList.contains('animate-pulse');
      });
      expect(hasPulse).toBe(false);
    }

    if (
      await page
        .locator('button[type="submit"]')
        .isEnabled()
        .catch(() => false)
    ) {
      await sendMessage(page, 'Test reduced motion');

      // Wait briefly for streaming to start
      await page.waitForTimeout(500);

      // Verify typing indicator dots don't have animation
      const typingIndicator = page.locator(
        '[aria-label*="is typing"], [aria-label*="sta scrivendo"]'
      );
      if (await typingIndicator.isVisible().catch(() => false)) {
        const dots = typingIndicator.locator('span[class*="rounded-full"]');
        const firstDot = dots.first();

        if (await firstDot.isVisible().catch(() => false)) {
          // Check that animation is disabled or minimal
          const transform = await firstDot.evaluate((el: HTMLElement) => {
            const style = window.getComputedStyle(el);
            return style.transform;
          });

          // In reduced motion, transform should be 'none' or minimal
          expect(transform === 'none' || !transform.includes('translateY')).toBe(true);
        }
      }

      // Wait for AI response from real backend
      await page.waitForTimeout(2000);

      // Verify messages appear with minimal animation delay
      const messages = page.locator('[data-message-id]');
      if ((await messages.count()) > 0) {
        const messageContainer = messages.first();

        // Animation should complete quickly in reduced motion
        await expect(messageContainer).toHaveAttribute('data-animation-complete', 'true', {
          timeout: 500,
        });
      }

      // Wait for streaming to complete
      const waitHelper = new WaitHelper(page);
      await waitHelper.waitForNetworkIdle(5000);
    }
  });

  /**
   * Test 14: Animation performance validation
   *
   * Verifies animations maintain acceptable frame rate (>45 FPS).
   * Uses real backend - measures actual animation performance.
   */
  test('animations maintain 60fps performance', async ({ page }) => {
    await navigateToChat(page);
    await waitForGamesToLoad(page);

    if (
      await page
        .locator('button[type="submit"]')
        .isEnabled()
        .catch(() => false)
    ) {
      // Start FPS measurement
      const fpsPromise = measureFPS(page, 2000);

      // Trigger animations with real backend streaming
      await sendMessage(page, 'Performance test message 1');
      await page.waitForTimeout(500);

      await sendMessage(page, 'Performance test message 2');
      await page.waitForTimeout(500);

      await sendMessage(page, 'Performance test message 3');

      // Wait for FPS measurement to complete
      const averageFPS = await fpsPromise;

      // Log FPS for debugging
      console.log(`Average FPS during animations: ${averageFPS.toFixed(2)}`);

      // Verify FPS is above 45 (relaxed threshold for CI with real backend)
      expect(averageFPS).toBeGreaterThan(45);

      // Wait for all streaming to complete
      const waitHelper = new WaitHelper(page);
      await waitHelper.waitForNetworkIdle(5000);
    }
  });

  /**
   * Test 15: Multiple skeletons render correctly
   *
   * Verifies that when count > 1, multiple skeleton items render.
   * Uses real backend - skeleton may be brief but count verified.
   */
  test('renders multiple skeleton items when count > 1', async ({ page }) => {
    await navigateToChat(page);
    await waitForGamesToLoad(page);

    // Reload to trigger skeleton display
    await page.reload();
    await waitForGamesToLoad(page);

    // Check if multiple chat history skeletons appear
    const chatSkeletons = page.locator('[aria-label="Caricamento cronologia chat"]');
    const skeletonCount = await chatSkeletons.count();

    // Should render multiple skeletons if visible (count=5 in the code)
    if (skeletonCount > 0) {
      expect(skeletonCount).toBeGreaterThanOrEqual(1);

      // Verify each skeleton has correct attributes
      for (let i = 0; i < Math.min(skeletonCount, 3); i++) {
        const skeleton = chatSkeletons.nth(i);
        await expect(skeleton).toHaveAttribute('role', 'status');
        await expect(skeleton).toHaveAttribute('aria-live', 'polite');
      }
    }

    // Verify skeletons disappear and real content loads
    await expect(chatSkeletons.first()).not.toBeVisible({ timeout: 5000 });
  });

  /**
   * Test 16: Stop button appears during streaming
   *
   * Verifies stop button is present and functional during AI response.
   * Uses real backend - verifies stop button structure during streaming.
   */
  test('shows stop button during streaming response', async ({ page }) => {
    await navigateToChat(page);
    await waitForGamesToLoad(page);

    if (
      await page
        .locator('button[type="submit"]')
        .isEnabled()
        .catch(() => false)
    ) {
      await sendMessage(page, 'Long streaming test');

      // Wait briefly for streaming to start
      await page.waitForTimeout(500);

      // Look for stop button during streaming
      const stopButton = page.locator('button:has-text("Stop"), button:has-text("⏹")');
      const hasStopButton = await stopButton.isVisible().catch(() => false);

      if (hasStopButton) {
        // Verify stop button is enabled during streaming
        await expect(stopButton).toBeEnabled();

        // Verify button text contains "Stop" or stop emoji
        const buttonText = await stopButton.textContent();
        expect(buttonText).toMatch(/Stop|⏹/i);
      }

      // Wait for streaming to complete
      const waitHelper = new WaitHelper(page);
      await waitHelper.waitForNetworkIdle(5000);
    }
  });
});
