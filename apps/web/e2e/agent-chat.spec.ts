/**
 * Agent Chat E2E Tests (Task #6)
 *
 * Full user journey tests:
 * - Game detail → Chat flow
 * - Admin agent chat
 * - Multi-agent switching
 * - Offline recovery
 */

import { test, expect } from '@playwright/test';

test.describe('Agent Chat - User Flow', () => {
  test.beforeEach(async ({ page }) => {
    // Login
    await page.goto('/login');
    await page.fill('input[name="username"]', 'testuser');
    await page.fill('input[name="password"]', 'password123');
    await page.click('button[type="submit"]');
    await page.waitForURL('/dashboard');
  });

  test('navigates from game detail to chat', async ({ page }) => {
    // Go to game detail
    await page.goto('/library/games/test-game-id');
    await expect(page.locator('h1')).toContainText('Catan'); // Game title

    // Click "Chat con AI" button
    await page.click('text=💬 Chat con AI');

    // Should navigate to chat page
    await page.waitForURL(/\/library\/games\/.*\/chat/);
    await expect(page.locator('h2')).toContainText('MeepleAI Assistant');
  });

  test('sends message and receives streaming response', async ({ page }) => {
    await page.goto('/library/games/test-game-id/chat');

    // Type message
    const input = page.locator('textarea[placeholder*="Chiedi"]');
    await input.fill('How do I set up Catan?');

    // Send message
    await page.click('button:has-text("Send")');

    // User message appears
    await expect(page.locator('text=How do I set up Catan?')).toBeVisible();

    // Agent response streams in
    await expect(page.locator('text=/Place the board/')).toBeVisible({ timeout: 5000 });

    // Citations appear
    await expect(page.locator('text=Sources:')).toBeVisible();
  });

  test('shows typing indicator during streaming', async ({ page }) => {
    await page.goto('/library/games/test-game-id/chat');

    const input = page.locator('textarea[placeholder*="Chiedi"]');
    await input.fill('Test question');
    await page.click('button:has-text("Send")');

    // Typing indicator should appear
    await expect(page.locator('[data-testid="typing-indicator"]')).toBeVisible();

    // Should disappear after response completes
    await expect(page.locator('[data-testid="typing-indicator"]')).not.toBeVisible({
      timeout: 10000,
    });
  });

  test('auto-scrolls to newest message', async ({ page }) => {
    await page.goto('/library/games/test-game-id/chat');

    // Send multiple messages
    for (let i = 1; i <= 5; i++) {
      const input = page.locator('textarea[placeholder*="Chiedi"]');
      await input.fill(`Message ${i}`);
      await page.click('button:has-text("Send")');
      await page.waitForTimeout(500);
    }

    // Last message should be visible
    await expect(page.locator('text=Message 5')).toBeInViewport();
  });
});

test.describe('Agent Chat - Admin Flow', () => {
  test.beforeEach(async ({ page }) => {
    // Login as admin
    await page.goto('/login');
    await page.fill('input[name="username"]', 'admin');
    await page.fill('input[name="password"]', 'admin123');
    await page.click('button[type="submit"]');
  });

  test('shows debug panel in admin chat', async ({ page }) => {
    await page.goto('/admin/agent-definitions/playground');

    // Select agent
    await page.click('text=Select an agent');
    await page.click('text=Tutor');

    // Send message
    const input = page.locator('textarea[placeholder*="message"]');
    await input.fill('Test admin question');
    await page.click('button:has-text("Send")');

    // Debug panel should show metrics
    await expect(page.locator('text=Debug Metrics')).toBeVisible();
    await expect(page.locator('text=/Tokens:/i')).toBeVisible();
    await expect(page.locator('text=/Latency:/i')).toBeVisible();
    await expect(page.locator('text=/Confidence:/i')).toBeVisible();
  });

  test('opens chat in popout window', async ({ page, context }) => {
    await page.goto('/admin/agent-definitions/test-agent-id');

    // Wait for popout button
    const popoutBtn = page.locator('button:has-text("Popout Window")');
    await expect(popoutBtn).toBeVisible();

    // Click popout (will open new window)
    const [popupPage] = await Promise.all([context.waitForEvent('page'), popoutBtn.click()]);

    // Verify new window opened
    await popupPage.waitForLoadState();
    await expect(popupPage).toHaveURL(/\/chat-popout$/);
    await expect(popupPage.locator('h2')).toContainText('Admin Agent Chat');
  });
});

test.describe('Agent Chat - Multi-Agent Switching', () => {
  test('switches between agents mid-conversation', async ({ page }) => {
    await page.goto('/library/games/test-game-id/chat');

    // Send first message (default agent)
    const input = page.locator('textarea[placeholder*="Chiedi"]');
    await input.fill('Setup question');
    await page.click('button:has-text("Send")');

    // Wait for response
    await expect(page.locator('text=/Tutor/i')).toBeVisible();

    // Switch to Decisore agent
    await page.click('[data-testid="agent-selector"]');
    await page.click('text=Decisore');

    // Send strategic question
    await input.fill('What move should I make?');
    await page.click('button:has-text("Send")');

    // Response should come from Decisore
    await expect(page.locator('text=/Decisore/i')).toBeVisible();
  });
});

test.describe('Agent Chat - Error Handling', () => {
  test('shows error message on connection failure', async ({ page }) => {
    // Mock network failure
    await page.route('**/api/v1/agents/**/chat', route => {
      route.abort('failed');
    });

    await page.goto('/library/games/test-game-id/chat');

    const input = page.locator('textarea[placeholder*="Chiedi"]');
    await input.fill('Test question');
    await page.click('button:has-text("Send")');

    // Should show error
    await expect(page.locator('text=/Failed to send message/i')).toBeVisible();
  });

  test('allows retry after error', async ({ page }) => {
    let attempts = 0;
    await page.route('**/api/v1/agents/**/chat', route => {
      attempts++;
      if (attempts === 1) {
        route.abort('failed');
      } else {
        route.fulfill({ status: 200, body: 'data: {"type":"Complete"}' });
      }
    });

    await page.goto('/library/games/test-game-id/chat');

    const input = page.locator('textarea[placeholder*="Chiedi"]');
    await input.fill('Test question');
    await page.click('button:has-text("Send")');

    // First attempt fails
    await expect(page.locator('text=/Failed/i')).toBeVisible();

    // Click retry
    await page.click('button:has-text("Retry")');

    // Second attempt succeeds
    await expect(page.locator('text=/Failed/i')).not.toBeVisible();
  });
});
