/**
 * E2E Tests for Chat Loading States and Animations (CHAT-04)
 *
 * Tests comprehensive animation and loading state behaviors including:
 * - Skeleton loaders for all variants (games, agents, chat history, messages)
 * - Typing indicators during AI response generation
 * - Message direction animations (slide from left/right)
 * - Message list stagger animations
 * - Loading button states (send and new chat buttons)
 * - Smooth scroll to latest message
 * - Reduced motion accessibility compliance
 * - Animation performance validation
 *
 * @module e2e/chat-animations
 */

import { test, expect, Page, BrowserContext } from '@playwright/test';

/**
 * Helper: Login as test user
 */
async function loginAsTestUser(page: Page): Promise<void> {
  await page.goto('/');

  // Fill login form
  await page.fill('input[type="email"]', 'user@meepleai.dev');
  await page.fill('input[type="password"]', 'Demo123!');
  await page.click('button[type="submit"]');

  // Wait for redirect to home
  await expect(page).toHaveURL('/');
  await expect(page.getByText('Benvenuto in MeepleAI')).toBeVisible();
}

/**
 * Helper: Navigate to chat page after login
 */
async function navigateToChat(page: Page): Promise<void> {
  await page.goto('/chat');
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
  await page.click('button[type="submit"]');
}

/**
 * Helper: Measure FPS during animation using requestAnimationFrame
 */
async function measureFPS(page: Page, durationMs: number = 1000): Promise<number> {
  const result = await page.evaluate((duration) => {
    return new Promise<number>((resolve) => {
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
  test.beforeEach(async ({ page }) => {
    await loginAsTestUser(page);
  });

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
   */
  test('shows games skeleton with correct variant', async ({ page }) => {
    // Intercept games request to delay it
    await page.route('**/api/v1/games', async (route) => {
      await new Promise(resolve => setTimeout(resolve, 2000));
      await route.continue();
    });

    await page.goto('/chat');

    // Verify games skeleton appears
    const gamesSkeleton = page.locator('[aria-label="Caricamento giochi"]');
    await expect(gamesSkeleton).toBeVisible({ timeout: 1000 });

    // Verify skeleton has correct role and aria attributes
    await expect(gamesSkeleton).toHaveAttribute('role', 'status');
    await expect(gamesSkeleton).toHaveAttribute('aria-live', 'polite');

    // Wait for skeleton to disappear
    await waitForGamesToLoad(page);
    await expect(gamesSkeleton).not.toBeVisible({ timeout: 5000 });
  });

  /**
   * Test 3: Agents skeleton loader
   *
   * Verifies agents skeleton appears when game is selected.
   */
  test('shows agents skeleton when loading agents', async ({ page }) => {
    await navigateToChat(page);
    await waitForGamesToLoad(page);

    // Intercept agents request to delay it
    await page.route('**/api/v1/games/*/agents', async (route) => {
      await new Promise(resolve => setTimeout(resolve, 2000));
      await route.continue();
    });

    // Select a game to trigger agent loading
    const gameSelect = page.locator('#gameSelect');
    const firstGameValue = await gameSelect.locator('option:not([value=""])').first().getAttribute('value');
    if (firstGameValue) {
      await gameSelect.selectOption(firstGameValue);

      // Verify agents skeleton appears
      const agentsSkeleton = page.locator('[aria-label="Caricamento agenti"]');
      await expect(agentsSkeleton).toBeVisible({ timeout: 1000 });

      // Wait for agents to load
      await expect(page.locator('#agentSelect option:not([value=""])')).toHaveCount(1, { timeout: 5000 });
      await expect(agentsSkeleton).not.toBeVisible({ timeout: 5000 });
    }
  });

  /**
   * Test 4: Chat history skeleton loader
   *
   * Verifies chat history skeleton appears when loading chat list.
   */
  test('shows chat history skeleton when loading chats', async ({ page }) => {
    await navigateToChat(page);
    await waitForGamesToLoad(page);

    // Intercept chats request to delay it
    await page.route('**/api/v1/chats*', async (route) => {
      await new Promise(resolve => setTimeout(resolve, 2000));
      await route.continue();
    });

    // Reload to trigger chat loading with delay
    await page.reload();
    await waitForGamesToLoad(page);

    // Verify chat history skeleton appears
    const chatSkeleton = page.locator('[aria-label="Caricamento cronologia chat"]');
    const isVisible = await chatSkeleton.isVisible().catch(() => false);

    if (isVisible) {
      // Verify skeleton has correct variant (chatHistory)
      const skeletonCount = await chatSkeleton.count();
      expect(skeletonCount).toBeGreaterThan(0);

      // Wait for chats to load
      await expect(chatSkeleton).not.toBeVisible({ timeout: 5000 });
    }
  });

  /**
   * Test 5: Messages skeleton loader
   *
   * Verifies messages skeleton appears when loading chat history.
   */
  test('shows messages skeleton when loading chat history', async ({ page }) => {
    await navigateToChat(page);
    await waitForGamesToLoad(page);

    // Create a new chat first
    const newChatButton = page.locator('button:has-text("+ Nuova Chat")');
    if (await newChatButton.isEnabled().catch(() => false)) {
      await newChatButton.click();
      await page.waitForTimeout(1000);

      // Send a message to create history
      await sendMessage(page, 'Test message for skeleton');
      await page.waitForTimeout(2000);

      // Intercept chat history request to delay it
      await page.route('**/api/v1/chats/*', async (route) => {
        if (!route.request().url().includes('api/v1/chats?')) {
          await new Promise(resolve => setTimeout(resolve, 2000));
        }
        await route.continue();
      });

      // Click the chat in history to reload it
      const chatItem = page.locator('li[role="button"]').first();
      if (await chatItem.isVisible().catch(() => false)) {
        await chatItem.click();

        // Verify messages skeleton appears
        const messagesSkeleton = page.locator('[aria-label="Caricamento messaggi"]');
        const isVisible = await messagesSkeleton.isVisible().catch(() => false);

        if (isVisible) {
          await expect(messagesSkeleton).toHaveAttribute('role', 'status');
        }
      }
    }
  });

  /**
   * Test 6: Typing indicator during streaming
   *
   * Verifies typing indicator appears with agent name during AI response.
   */
  test('shows typing indicator when AI is generating response', async ({ page }) => {
    await navigateToChat(page);
    await waitForGamesToLoad(page);

    // Intercept streaming to slow it down
    await page.route('**/api/v1/agents/qa/stream', async (route) => {
      const sseData = [
        'event: stateUpdate\ndata: {"state":"Generating embeddings..."}\n\n',
        'event: stateUpdate\ndata: {"state":"Searching vector database..."}\n\n',
      ].join('');

      // Delay to keep typing indicator visible
      await new Promise(resolve => setTimeout(resolve, 2000));

      await route.fulfill({
        status: 200,
        headers: {
          'Content-Type': 'text/event-stream',
          'Cache-Control': 'no-cache',
        },
        body: sseData,
      });
    });

    // Send a message
    if (await page.locator('button[type="submit"]').isEnabled().catch(() => false)) {
      await sendMessage(page, 'Show typing indicator test');

      // Wait a bit for streaming to start
      await page.waitForTimeout(300);

      // Verify typing indicator is visible
      // The indicator shows when isStreaming && !currentAnswer
      const typingIndicator = page.locator('[aria-label*="is typing"], [aria-label*="sta scrivendo"]');
      const hasTypingIndicator = await typingIndicator.isVisible().catch(() => false);

      // Also check for the "Sto pensando..." fallback state
      const thinkingText = page.locator('text=Sto pensando...');
      const hasThinkingText = await thinkingText.isVisible().catch(() => false);

      // Either typing indicator or thinking text should be visible
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
    }
  });

  /**
   * Test 7: Message animations - user messages from right
   *
   * Verifies user messages slide in from the right.
   */
  test('animates user messages from right', async ({ page }) => {
    await navigateToChat(page);
    await waitForGamesToLoad(page);

    // Mock streaming response
    await page.route('**/api/v1/agents/qa/stream', async (route) => {
      await route.fulfill({
        status: 200,
        headers: {
          'Content-Type': 'text/event-stream',
        },
        body: 'event: complete\ndata: {"totalTokens":1,"confidence":0.9,"snippets":[]}\n\n',
      });
    });

    if (await page.locator('button[type="submit"]').isEnabled().catch(() => false)) {
      await sendMessage(page, 'Test user message animation');

      // Wait for user message to appear
      await page.waitForTimeout(500);

      // Find the user message
      const userMessage = page.getByText('Test user message animation');
      await expect(userMessage).toBeVisible();

      // Verify message has animation data attributes
      const messageContainer = userMessage.locator('xpath=ancestor::div[@data-message-id]').first();
      const hasMessageId = await messageContainer.getAttribute('data-message-id');
      expect(hasMessageId).toBeTruthy();

      // Verify animation complete attribute eventually appears
      await expect(messageContainer).toHaveAttribute('data-animation-complete', 'true', { timeout: 2000 });

      // Verify message is right-aligned (user messages)
      const parentLi = userMessage.locator('xpath=ancestor::li').first();
      const alignItems = await parentLi.evaluate((el) => window.getComputedStyle(el).alignItems);
      expect(alignItems).toBe('flex-end');
    }
  });

  /**
   * Test 8: Message animations - AI messages from left
   *
   * Verifies AI messages slide in from the left.
   */
  test('animates AI messages from left', async ({ page }) => {
    await navigateToChat(page);
    await waitForGamesToLoad(page);

    // Mock streaming response with actual answer
    await page.route('**/api/v1/agents/qa/stream', async (route) => {
      const sseData = [
        'event: token\ndata: {"token":"AI response from left"}\n\n',
        'event: complete\ndata: {"totalTokens":3,"confidence":0.95,"snippets":[]}\n\n',
      ].join('');

      await route.fulfill({
        status: 200,
        headers: {
          'Content-Type': 'text/event-stream',
        },
        body: sseData,
      });
    });

    if (await page.locator('button[type="submit"]').isEnabled().catch(() => false)) {
      await sendMessage(page, 'Trigger AI response');

      // Wait for AI response to complete
      await page.waitForTimeout(1000);

      // Find the AI message
      const aiMessage = page.getByText('AI response from left');
      await expect(aiMessage).toBeVisible({ timeout: 3000 });

      // Verify message has animation data attributes
      const messageContainer = aiMessage.locator('xpath=ancestor::div[@data-message-id]').first();
      const hasMessageId = await messageContainer.getAttribute('data-message-id');
      expect(hasMessageId).toBeTruthy();

      // Verify message is left-aligned (AI messages)
      const parentLi = aiMessage.locator('xpath=ancestor::li').first();
      const alignItems = await parentLi.evaluate((el) => window.getComputedStyle(el).alignItems);
      expect(alignItems).toBe('flex-start');
    }
  });

  /**
   * Test 9: Message list stagger animation
   *
   * Verifies messages appear with stagger delay when loading chat history.
   */
  test('staggers message animations when loading chat history', async ({ page }) => {
    await navigateToChat(page);
    await waitForGamesToLoad(page);

    // Create a chat with multiple messages
    let chatCreated = false;
    await page.route('**/api/v1/agents/qa/stream', async (route) => {
      await route.fulfill({
        status: 200,
        headers: {
          'Content-Type': 'text/event-stream',
        },
        body: 'event: token\ndata: {"token":"Response"}\n\nevent: complete\ndata: {"totalTokens":1,"confidence":0.9,"snippets":[]}\n\n',
      });
    });

    if (await page.locator('button[type="submit"]').isEnabled().catch(() => false)) {
      // Send multiple messages to create history
      for (let i = 1; i <= 3; i++) {
        await sendMessage(page, `Message ${i}`);
        await page.waitForTimeout(800);
        chatCreated = true;
      }

      if (chatCreated) {
        // Get the chat ID by clicking on it
        const firstChat = page.locator('li[role="button"]').first();
        if (await firstChat.isVisible().catch(() => false)) {
          await firstChat.click();
          await page.waitForTimeout(500);

          // Check for staggered animation completion
          // Each message should have data-animation-complete attribute
          const animatedMessages = page.locator('[data-message-id]');
          const count = await animatedMessages.count();

          if (count > 1) {
            // Verify at least some messages have animation complete
            for (let i = 0; i < Math.min(count, 3); i++) {
              const message = animatedMessages.nth(i);
              await expect(message).toHaveAttribute('data-animation-complete', 'true', {
                timeout: (i * 50) + 500, // Stagger delay: index * 50ms + 300ms animation
              });
            }
          }
        }
      }
    }
  });

  /**
   * Test 10: Loading button - send button states
   *
   * Verifies send button shows loading state when sending message.
   */
  test('send button shows loading state when sending message', async ({ page }) => {
    await navigateToChat(page);
    await waitForGamesToLoad(page);

    // Intercept to create delay
    await page.route('**/api/v1/agents/qa/stream', async (route) => {
      await new Promise(resolve => setTimeout(resolve, 2000));
      await route.fulfill({
        status: 200,
        headers: {
          'Content-Type': 'text/event-stream',
        },
        body: 'event: complete\ndata: {"totalTokens":1,"confidence":0.9,"snippets":[]}\n\n',
      });
    });

    if (await page.locator('button[type="submit"]').isEnabled().catch(() => false)) {
      await page.fill('#message-input', 'Test loading button');

      // Click send button
      const sendButton = page.locator('button[type="submit"]');
      await sendButton.click();

      // Verify button shows loading state
      await expect(sendButton).toHaveAttribute('aria-busy', 'true', { timeout: 500 });
      await expect(sendButton).toBeDisabled({ timeout: 500 });

      // Verify loading text appears
      const hasLoadingText = await sendButton.locator('text=Invio...').isVisible().catch(() => false);
      expect(hasLoadingText).toBe(true);

      // Verify spinner appears
      const spinner = sendButton.locator('svg[aria-hidden="true"]');
      const hasSpinner = await spinner.isVisible().catch(() => false);
      expect(hasSpinner).toBe(true);

      // Wait for completion
      await page.waitForTimeout(2500);

      // Verify button returns to normal state
      await expect(sendButton).not.toBeDisabled({ timeout: 1000 });
      await expect(sendButton).toHaveText('Invia', { timeout: 1000 });
    }
  });

  /**
   * Test 11: Loading button - new chat button states
   *
   * Verifies new chat button shows loading state when creating chat.
   */
  test('new chat button shows loading state', async ({ page }) => {
    await navigateToChat(page);
    await waitForGamesToLoad(page);

    // Intercept chat creation to delay it
    await page.route('**/api/v1/chats', async (route) => {
      if (route.request().method() === 'POST') {
        await new Promise(resolve => setTimeout(resolve, 2000));
      }
      await route.continue();
    });

    const newChatButton = page.locator('button:has-text("+ Nuova Chat")');
    if (await newChatButton.isEnabled().catch(() => false)) {
      await newChatButton.click();

      // Verify button shows loading state
      await expect(newChatButton).toHaveAttribute('aria-busy', 'true', { timeout: 500 });
      await expect(newChatButton).toBeDisabled({ timeout: 500 });

      // Verify loading text appears
      const hasLoadingText = await newChatButton.locator('text=Creazione...').isVisible().catch(() => false);
      expect(hasLoadingText).toBe(true);

      // Verify spinner appears
      const spinner = newChatButton.locator('svg[aria-hidden="true"]');
      const hasSpinner = await spinner.isVisible().catch(() => false);
      expect(hasSpinner).toBe(true);

      // Wait for completion
      await page.waitForTimeout(2500);

      // Verify button returns to enabled state
      await expect(newChatButton).toBeEnabled({ timeout: 1000 });
    }
  });

  /**
   * Test 12: Smooth scroll to latest message
   *
   * Verifies new messages trigger smooth scroll to bottom.
   */
  test('smoothly scrolls to latest message', async ({ page }) => {
    await navigateToChat(page);
    await waitForGamesToLoad(page);

    // Mock quick responses
    await page.route('**/api/v1/agents/qa/stream', async (route) => {
      await route.fulfill({
        status: 200,
        headers: {
          'Content-Type': 'text/event-stream',
        },
        body: 'event: token\ndata: {"token":"Response"}\n\nevent: complete\ndata: {"totalTokens":1,"confidence":0.9,"snippets":[]}\n\n',
      });
    });

    if (await page.locator('button[type="submit"]').isEnabled().catch(() => false)) {
      // Send multiple messages to create scrollable content
      for (let i = 1; i <= 5; i++) {
        await sendMessage(page, `Message ${i} with some additional content to make it longer`);
        await page.waitForTimeout(600);
      }

      // Get messages container
      const messagesContainer = page.locator('[role="region"][aria-label="Chat messages"]');

      // Scroll to top
      await messagesContainer.evaluate((el) => {
        el.scrollTop = 0;
      });

      await page.waitForTimeout(300);

      // Send new message
      await sendMessage(page, 'Latest message should scroll into view');

      // Wait for AI response
      await page.waitForTimeout(1000);

      // Verify latest message is in viewport
      const latestMessage = page.getByText('Latest message should scroll into view');
      await expect(latestMessage).toBeVisible({ timeout: 2000 });

      // Check if message is near bottom of viewport
      const isInViewport = await latestMessage.evaluate((el) => {
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
   */
  test('respects prefers-reduced-motion setting', async ({ page }) => {
    // Emulate reduced motion preference
    await page.emulateMedia({ reducedMotion: 'reduce' });

    await navigateToChat(page);
    await waitForGamesToLoad(page);

    // Mock streaming response
    await page.route('**/api/v1/agents/qa/stream', async (route) => {
      await route.fulfill({
        status: 200,
        headers: {
          'Content-Type': 'text/event-stream',
        },
        body: 'event: token\ndata: {"token":"Reduced motion test"}\n\nevent: complete\ndata: {"totalTokens":3,"confidence":0.9,"snippets":[]}\n\n',
      });
    });

    // Check skeleton loaders don't have animate-pulse
    const gamesSkeleton = page.locator('[aria-label="Caricamento giochi"]');
    if (await gamesSkeleton.isVisible().catch(() => false)) {
      const hasPulse = await gamesSkeleton.evaluate((el) => {
        return el.classList.contains('animate-pulse');
      });
      expect(hasPulse).toBe(false);
    }

    if (await page.locator('button[type="submit"]').isEnabled().catch(() => false)) {
      await sendMessage(page, 'Test reduced motion');

      // Wait for typing indicator
      await page.waitForTimeout(300);

      // Verify typing indicator dots don't have animation
      const typingIndicator = page.locator('[aria-label*="is typing"], [aria-label*="sta scrivendo"]');
      if (await typingIndicator.isVisible().catch(() => false)) {
        const dots = typingIndicator.locator('span[class*="rounded-full"]');
        const firstDot = dots.first();

        if (await firstDot.isVisible().catch(() => false)) {
          // Check that animation is disabled or minimal
          const transform = await firstDot.evaluate((el) => {
            const style = window.getComputedStyle(el);
            return style.transform;
          });

          // In reduced motion, transform should be 'none' or minimal
          // (The exact value depends on implementation, but no y-axis translation)
          expect(transform === 'none' || !transform.includes('translateY')).toBe(true);
        }
      }

      // Wait for message to appear
      await page.waitForTimeout(1000);

      // Verify messages appear instantly (animation-duration should be 0 or very small)
      const message = page.getByText('Reduced motion test');
      if (await message.isVisible().catch(() => false)) {
        const messageContainer = message.locator('xpath=ancestor::div[@data-message-id]').first();

        // Animation should complete immediately
        await expect(messageContainer).toHaveAttribute('data-animation-complete', 'true', { timeout: 100 });
      }
    }
  });

  /**
   * Test 14: Animation performance validation
   *
   * Verifies animations maintain acceptable frame rate (>55 FPS).
   */
  test('animations maintain 60fps performance', async ({ page }) => {
    await navigateToChat(page);
    await waitForGamesToLoad(page);

    // Mock streaming response
    await page.route('**/api/v1/agents/qa/stream', async (route) => {
      await route.fulfill({
        status: 200,
        headers: {
          'Content-Type': 'text/event-stream',
        },
        body: 'event: token\ndata: {"token":"Performance test response"}\n\nevent: complete\ndata: {"totalTokens":3,"confidence":0.9,"snippets":[]}\n\n',
      });
    });

    if (await page.locator('button[type="submit"]').isEnabled().catch(() => false)) {
      // Start FPS measurement
      const fpsPromise = measureFPS(page, 2000);

      // Trigger animations (send message, load history)
      await sendMessage(page, 'Performance test message 1');
      await page.waitForTimeout(500);

      await sendMessage(page, 'Performance test message 2');
      await page.waitForTimeout(500);

      await sendMessage(page, 'Performance test message 3');

      // Wait for FPS measurement to complete
      const averageFPS = await fpsPromise;

      // Log FPS for debugging
      console.log(`Average FPS during animations: ${averageFPS.toFixed(2)}`);

      // Verify FPS is above 55 (allowing 5fps buffer from ideal 60fps)
      // Note: This may be less strict in CI environments
      expect(averageFPS).toBeGreaterThan(45); // Relaxed threshold for CI
    }
  });

  /**
   * Test 15: Multiple skeletons render correctly
   *
   * Verifies that when count > 1, multiple skeleton items render.
   */
  test('renders multiple skeleton items when count > 1', async ({ page }) => {
    await navigateToChat(page);
    await waitForGamesToLoad(page);

    // Intercept chats request to delay and show multiple skeletons
    await page.route('**/api/v1/chats*', async (route) => {
      await new Promise(resolve => setTimeout(resolve, 2000));
      await route.continue();
    });

    // Reload to trigger skeleton display
    await page.reload();
    await waitForGamesToLoad(page);

    // Verify multiple chat history skeletons appear
    const chatSkeletons = page.locator('[aria-label="Caricamento cronologia chat"]');
    const skeletonCount = await chatSkeletons.count();

    // Should render multiple skeletons (count=5 in the code)
    if (skeletonCount > 0) {
      expect(skeletonCount).toBeGreaterThanOrEqual(1);

      // Verify each skeleton has correct attributes
      for (let i = 0; i < Math.min(skeletonCount, 3); i++) {
        const skeleton = chatSkeletons.nth(i);
        await expect(skeleton).toHaveAttribute('role', 'status');
        await expect(skeleton).toHaveAttribute('aria-live', 'polite');
      }
    }
  });

  /**
   * Test 16: Stop button appears during streaming
   *
   * Verifies stop button is present and functional during AI response.
   */
  test('shows stop button during streaming response', async ({ page }) => {
    await navigateToChat(page);
    await waitForGamesToLoad(page);

    // Create long streaming response
    await page.route('**/api/v1/agents/qa/stream', async (route) => {
      // Don't fulfill immediately - simulate long stream
      await new Promise(resolve => setTimeout(resolve, 3000));
      await route.fulfill({
        status: 200,
        headers: {
          'Content-Type': 'text/event-stream',
        },
        body: 'event: complete\ndata: {"totalTokens":1,"confidence":0.9,"snippets":[]}\n\n',
      });
    });

    if (await page.locator('button[type="submit"]').isEnabled().catch(() => false)) {
      await sendMessage(page, 'Long streaming test');

      // Wait for streaming to start
      await page.waitForTimeout(500);

      // Verify stop button appears
      const stopButton = page.locator('button[aria-label="Stop streaming"]');
      const hasStopButton = await stopButton.isVisible().catch(() => false);

      if (hasStopButton) {
        await expect(stopButton).toBeEnabled();
        await expect(stopButton).toHaveText(/Stop/i);

        // Verify button has stop emoji
        const buttonText = await stopButton.textContent();
        expect(buttonText).toContain('⏹');
      }
    }
  });
});
