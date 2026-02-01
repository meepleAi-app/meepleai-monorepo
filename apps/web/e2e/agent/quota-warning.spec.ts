/**
 * E2E-FRONT-004: Token Quota Warning
 * Issue #3248 (FRONT-012)
 *
 * Tests quota warning functionality:
 * 1. Mock 95% quota usage
 * 2. Verify warning displays (yellow color)
 * 3. Verify warning text and icon
 * 4. Verify progress bar color change
 * 5. Verify quota decrement after message
 *
 * Expected: Warning system works, quota tracking accurate.
 */

import { test, expect } from '@playwright/test';

test.describe('Token Quota Warning', () => {
  test.beforeEach(async ({ page }) => {
    // Mock auth
    await page.route('**/api/v1/auth/me', async route => {
      await route.fulfill({
        json: {
          id: 'test-user',
          email: 'test@example.com',
          tier: 'free',
        },
      });
    });
  });

  test('should display warning when quota is 95% used', async ({ page }) => {
    // Mock quota API - 95% used (475/500)
    await page.route('**/api/v1/users/me/quota', async route => {
      await route.fulfill({
        json: {
          used: 475,
          limit: 500,
          resetsAt: new Date(Date.now() + 3 * 24 * 60 * 60 * 1000).toISOString(), // 3 days
          percentUsed: 95,
        },
      });
    });

    // Mock session with quota info
    await page.route('**/api/v1/game-sessions/*', async route => {
      await route.fulfill({
        json: {
          sessionId: 'session-123',
          gameTitle: '7 Wonders',
          typologyName: 'Rules Helper',
          modelId: 'gpt-3.5-turbo',
          tokensUsed: 475,
          tokensLimit: 500,
        },
      });
    });

    await page.goto('/library/games/game-1?session=session-123');

    // Open agent config to see quota display
    const configButton = page.locator('[data-testid="open-agent-config"]');
    if (await configButton.isVisible()) {
      await configButton.click();
    }

    // Step 1: Verify warning displays (yellow color)
    const quotaWarning = page.locator('[data-testid="quota-warning"]');
    await expect(quotaWarning).toBeVisible();
    await expect(quotaWarning).toHaveClass(/text-yellow|text-amber|bg-yellow|bg-amber/);

    // Step 2: Verify warning text: "25 tokens until limit"
    await expect(quotaWarning).toContainText('25');
    await expect(quotaWarning).toContainText('limit');

    // Step 3: Verify warning icon (⚠️) visible
    const warningIcon = quotaWarning.locator('svg, [aria-label*="warning" i]');
    await expect(warningIcon).toBeVisible();

    // Step 4: Verify progress bar color is yellow/red, not cyan
    const progressBar = page.locator('[data-testid="quota-progress-bar"]');
    await expect(progressBar).toBeVisible();

    const progressBarColor = await progressBar.evaluate(el => {
      const computedStyle = window.getComputedStyle(el);
      return computedStyle.backgroundColor;
    });

    // Should NOT be cyan (rgb(0, 255, 255) or similar)
    expect(progressBarColor).not.toContain('0, 255, 255');

    // Step 5: Verify countdown timer shows "Resets in Xd Yh"
    const resetTimer = page.locator('[data-testid="quota-reset-timer"]');
    await expect(resetTimer).toContainText(/Resets in|Reset in|Rinnovo tra/i);
    await expect(resetTimer).toContainText(/\d+[dh]/i);
  });

  test('should update quota after sending message', async ({ page }) => {
    let currentUsed = 475;

    // Mock quota API with dynamic value
    await page.route('**/api/v1/users/me/quota', async route => {
      await route.fulfill({
        json: {
          used: currentUsed,
          limit: 500,
          resetsAt: new Date(Date.now() + 3 * 24 * 60 * 60 * 1000).toISOString(),
          percentUsed: (currentUsed / 500) * 100,
        },
      });
    });

    // Mock chat endpoint that updates quota
    await page.route('**/api/v1/game-sessions/*/agent/chat', async route => {
      currentUsed += 5; // Add 5 tokens for the response

      await route.fulfill({
        status: 200,
        headers: { 'Content-Type': 'text/event-stream' },
        body:
          'data: {"type":"chunk","content":"Response"}\n\n' +
          `data: {"type":"complete","metadata":{"tokensUsed":5,"newTotal":${currentUsed}}}\n\n`,
      });
    });

    await page.goto('/library/games/game-1?session=session-123');

    // Open chat
    const chatInput = page.locator('[data-testid="chat-input"]');
    if (await chatInput.isVisible()) {
      await chatInput.fill('Test question');

      const sendButton = page.locator('[data-testid="send-message-button"]');
      await sendButton.click();

      // Wait for response
      await page.waitForResponse('**/api/v1/game-sessions/*/agent/chat');

      // Verify quota decremented (475 → 480)
      const quotaDisplay = page.locator('[data-testid="quota-display"]');
      await expect(quotaDisplay).toContainText('480');
    }
  });

  test('should show critical warning when quota exceeded', async ({ page }) => {
    // Mock quota API - 100% used
    await page.route('**/api/v1/users/me/quota', async route => {
      await route.fulfill({
        json: {
          used: 500,
          limit: 500,
          resetsAt: new Date(Date.now() + 3 * 24 * 60 * 60 * 1000).toISOString(),
          percentUsed: 100,
        },
      });
    });

    await page.goto('/library/games/game-1?session=session-123');

    // Open agent config
    const configButton = page.locator('[data-testid="open-agent-config"]');
    if (await configButton.isVisible()) {
      await configButton.click();
    }

    // Verify critical warning (red color)
    const quotaWarning = page.locator('[data-testid="quota-warning"]');
    await expect(quotaWarning).toHaveClass(/text-red|bg-red/);

    // Verify "limit reached" message
    await expect(quotaWarning).toContainText(/limit|reached|raggiunto/i);

    // Verify chat input is disabled
    const chatInput = page.locator('[data-testid="chat-input"]');
    if (await chatInput.isVisible()) {
      await expect(chatInput).toBeDisabled();
    }
  });

  test('should show low warning threshold at 80%', async ({ page }) => {
    // Mock quota API - 80% used (400/500)
    await page.route('**/api/v1/users/me/quota', async route => {
      await route.fulfill({
        json: {
          used: 400,
          limit: 500,
          resetsAt: new Date(Date.now() + 5 * 24 * 60 * 60 * 1000).toISOString(),
          percentUsed: 80,
        },
      });
    });

    await page.goto('/library/games/game-1?session=session-123');

    // Open agent config
    const configButton = page.locator('[data-testid="open-agent-config"]');
    if (await configButton.isVisible()) {
      await configButton.click();
    }

    // At 80%, should show mild warning (not critical)
    const quotaWarning = page.locator('[data-testid="quota-warning"]');
    await expect(quotaWarning).toBeVisible();
    await expect(quotaWarning).toContainText('100'); // 100 tokens remaining
  });

  test('should not show warning when quota is healthy', async ({ page }) => {
    // Mock quota API - 30% used (150/500)
    await page.route('**/api/v1/users/me/quota', async route => {
      await route.fulfill({
        json: {
          used: 150,
          limit: 500,
          resetsAt: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString(),
          percentUsed: 30,
        },
      });
    });

    await page.goto('/library/games/game-1?session=session-123');

    // Open agent config
    const configButton = page.locator('[data-testid="open-agent-config"]');
    if (await configButton.isVisible()) {
      await configButton.click();
    }

    // Warning should NOT be visible at 30%
    const quotaWarning = page.locator('[data-testid="quota-warning"]');
    await expect(quotaWarning).not.toBeVisible();

    // Progress bar should be cyan (healthy)
    const progressBar = page.locator('[data-testid="quota-progress-bar"]');
    const progressBarColor = await progressBar.evaluate(el => {
      const computedStyle = window.getComputedStyle(el);
      return computedStyle.backgroundColor;
    });

    // Should be cyan-ish
    expect(progressBarColor).toMatch(/0, 255, 255|0, 200|cyan/);
  });
});
