/**
 * E2E-FRONT-001: Agent Configuration Flow
 * Issue #3248 (FRONT-012)
 *
 * Tests the complete agent configuration flow:
 * 1. Navigate to library
 * 2. Click "Ask Agent" button on game card
 * 3. Verify config sheet opens
 * 4. Select game, template, model
 * 5. Verify slot cards display
 * 6. Launch agent
 * 7. Verify chat sheet opens
 *
 * Expected: Complete config flow without errors, <3s total time.
 */

import { test, expect } from '@playwright/test';

test.describe('Agent Configuration Flow', () => {
  test.beforeEach(async ({ page }) => {
    // Mock auth and navigate to library
    await page.route('**/api/v1/auth/me', async route => {
      await route.fulfill({
        json: {
          id: 'test-user',
          email: 'test@example.com',
          roles: ['user'],
          tier: 'free',
        },
      });
    });

    // Mock games in library
    await page.route('**/api/v1/library/games*', async route => {
      await route.fulfill({
        json: {
          items: [
            {
              id: 'game-1',
              title: '7 Wonders',
              imageUrl: '/images/7wonders.jpg',
              hasDocuments: true,
            },
            {
              id: 'game-2',
              title: 'Catan',
              imageUrl: '/images/catan.jpg',
              hasDocuments: true,
            },
          ],
          total: 2,
        },
      });
    });

    // Mock typologies
    await page.route('**/api/v1/agent-typologies/approved', async route => {
      await route.fulfill({
        json: [
          {
            id: 'rules-helper',
            name: 'Rules Helper',
            description: 'Helps with game rules',
            icon: '📖',
          },
          {
            id: 'strategy',
            name: 'Strategy',
            description: 'Game strategy advice',
            icon: '🎯',
          },
        ],
      });
    });

    // Mock models
    await page.route('**/api/v1/models', async route => {
      await route.fulfill({
        json: [
          { id: 'gpt-3.5-turbo', name: 'GPT-3.5 Turbo', tier: 'free' },
          { id: 'gpt-4', name: 'GPT-4', tier: 'premium' },
        ],
      });
    });

    // Mock slot management
    await page.route('**/api/v1/agent-sessions/slots', async route => {
      await route.fulfill({
        json: {
          activeSlots: 1,
          totalSlots: 5,
          lockedSlots: 3,
          slots: [
            { id: '1', status: 'active', gameTitle: 'Splendor' },
            { id: '2', status: 'available' },
            { id: '3', status: 'available' },
            { id: '4', status: 'available' },
            { id: '5', status: 'available' },
            { id: '6', status: 'locked' },
            { id: '7', status: 'locked' },
            { id: '8', status: 'locked' },
          ],
        },
      });
    });

    await page.goto('/library');
  });

  test('should complete full agent configuration flow', async ({ page }) => {
    const startTime = Date.now();

    // Step 1: Click "Ask Agent" button on game card
    const gameCard = page.locator('[data-testid="game-card"]').first();
    await expect(gameCard).toBeVisible();

    const askAgentButton = gameCard.locator('button:has-text("Ask Agent")');
    await askAgentButton.click();

    // Step 2: Verify config sheet opens
    const configSheet = page.locator('[data-testid="agent-config-sheet"]');
    await expect(configSheet).toBeVisible();

    // Step 3: Verify game is pre-selected (from card click)
    const gameSelector = page.locator('[data-testid="game-selector"]');
    await expect(gameSelector).toContainText('7 Wonders');

    // Step 4: Select template from carousel
    const templateCard = page.locator('[data-testid="template-card"]').first();
    await templateCard.click();
    await expect(templateCard).toHaveClass(/border-cyan-400/);

    // Step 5: Verify template description updates
    const templateDescription = page.locator('[data-testid="template-description"]');
    await expect(templateDescription).toContainText('Helps with game rules');

    // Step 6: Select AI model
    const modelSelector = page.locator('[data-testid="model-selector"]');
    await modelSelector.click();
    await page.locator('[data-testid="model-option-gpt-3.5-turbo"]').click();

    // Step 7: Verify cost estimate displays
    const costEstimate = page.locator('[data-testid="cost-estimate"]');
    await expect(costEstimate).toBeVisible();

    // Step 8: Verify token quota displays
    const tokenQuota = page.locator('[data-testid="token-quota"]');
    await expect(tokenQuota).toBeVisible();
    await expect(tokenQuota).toContainText('tokens');

    // Step 9: Verify slot cards display
    const slotCards = page.locator('[data-testid="slot-card"]');
    await expect(slotCards).toHaveCount(8);

    // Verify different slot states
    const activeSlots = page.locator('[data-testid="slot-card"][data-status="active"]');
    await expect(activeSlots).toHaveCount(1);

    const availableSlots = page.locator('[data-testid="slot-card"][data-status="available"]');
    await expect(availableSlots).toHaveCount(4);

    const lockedSlots = page.locator('[data-testid="slot-card"][data-status="locked"]');
    await expect(lockedSlots).toHaveCount(3);

    // Step 10: Verify launch button is enabled
    const launchButton = page.locator('button:has-text("Launch Agent")');
    await expect(launchButton).toBeEnabled();

    // Step 11: Mock session creation and launch
    await page.route('**/api/v1/agent-sessions', async route => {
      await route.fulfill({
        json: {
          sessionId: 'session-123',
          gameId: 'game-1',
          typologyId: 'rules-helper',
          modelId: 'gpt-3.5-turbo',
        },
      });
    });

    await launchButton.click();

    // Step 12: Verify chat sheet opens
    const chatSheet = page.locator('[data-testid="agent-chat-sheet"]');
    await expect(chatSheet).toBeVisible();

    // Step 13: Verify header shows game + typology
    const chatHeader = page.locator('[data-testid="chat-header"]');
    await expect(chatHeader).toContainText('7 Wonders');
    await expect(chatHeader).toContainText('Rules Helper');

    // Verify total time < 3s
    const totalTime = Date.now() - startTime;
    expect(totalTime).toBeLessThan(3000);
  });

  test('should show validation when config is incomplete', async ({ page }) => {
    // Open config sheet without pre-selection
    await page.goto('/library');

    const configTrigger = page.locator('[data-testid="open-agent-config"]');
    if (await configTrigger.isVisible()) {
      await configTrigger.click();
    }

    // Launch button should be disabled without selections
    const launchButton = page.locator('button:has-text("Launch Agent")');
    await expect(launchButton).toBeDisabled();
  });

  test('should cancel configuration and close sheet', async ({ page }) => {
    // Open config
    const gameCard = page.locator('[data-testid="game-card"]').first();
    const askAgentButton = gameCard.locator('button:has-text("Ask Agent")');
    await askAgentButton.click();

    // Verify config sheet is open
    const configSheet = page.locator('[data-testid="agent-config-sheet"]');
    await expect(configSheet).toBeVisible();

    // Click cancel
    const cancelButton = page.locator('button:has-text("Cancel")');
    await cancelButton.click();

    // Verify sheet is closed
    await expect(configSheet).not.toBeVisible();
  });
});
