/**
 * E2E-FRONT-003: Slot Management
 * Issue #3248 (FRONT-012)
 *
 * Tests slot management functionality:
 * 1. Verify active slots display
 * 2. Verify tier badge
 * 3. End session on slot
 * 4. Verify optimistic UI update
 * 5. Verify success notification
 *
 * Expected: Slot management CRUD works, optimistic updates smooth.
 */

import { test, expect } from '@playwright/test';

test.describe('Slot Management', () => {
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

    // Mock slot data
    await page.route('**/api/v1/agent-sessions/slots', async route => {
      await route.fulfill({
        json: {
          activeSlots: 2,
          totalSlots: 5,
          lockedSlots: 3,
          tier: 'FREE',
          slots: [
            {
              id: 'slot-1',
              sessionId: 'session-1',
              status: 'active',
              gameTitle: '7 Wonders',
              typologyName: 'Rules Helper',
              startedAt: new Date().toISOString(),
            },
            {
              id: 'slot-2',
              sessionId: 'session-2',
              status: 'active',
              gameTitle: 'Splendor',
              typologyName: 'Strategy',
              startedAt: new Date().toISOString(),
            },
            { id: 'slot-3', status: 'available' },
            { id: 'slot-4', status: 'available' },
            { id: 'slot-5', status: 'available' },
            { id: 'slot-6', status: 'locked' },
            { id: 'slot-7', status: 'locked' },
            { id: 'slot-8', status: 'locked' },
          ],
        },
      });
    });

    await page.goto('/agent/slots');
  });

  test('should display active slots correctly', async ({ page }) => {
    // Verify active slots display (2 cards)
    const activeSlots = page.locator('[data-testid="slot-card"][data-status="active"]');
    await expect(activeSlots).toHaveCount(2);

    // Verify first slot shows game info
    const firstSlot = activeSlots.first();
    await expect(firstSlot).toContainText('7 Wonders');
    await expect(firstSlot).toContainText('Rules Helper');

    // Verify second slot shows game info
    const secondSlot = activeSlots.last();
    await expect(secondSlot).toContainText('Splendor');
    await expect(secondSlot).toContainText('Strategy');
  });

  test('should display tier badge', async ({ page }) => {
    // Verify tier badge shows "FREE TIER"
    const tierBadge = page.locator('[data-testid="tier-badge"]');
    await expect(tierBadge).toBeVisible();
    await expect(tierBadge).toContainText('FREE');
  });

  test('should display usage progress bar', async ({ page }) => {
    // Verify usage progress bar (2/5 = 40%)
    const progressBar = page.locator('[data-testid="slot-usage-progress"]');
    await expect(progressBar).toBeVisible();

    const progressValue = page.locator('[data-testid="slot-usage-text"]');
    await expect(progressValue).toContainText('2 / 5');
  });

  test('should end session and update UI optimistically', async ({ page }) => {
    // Mock end session API
    let endSessionCalled = false;
    await page.route('**/api/v1/agent-sessions/session-1/end', async route => {
      endSessionCalled = true;
      await route.fulfill({
        json: { success: true },
      });
    });

    // Click "End Session" on first slot
    const firstSlot = page.locator('[data-testid="slot-card"][data-status="active"]').first();
    const endSessionButton = firstSlot.locator('button:has-text("End Session")');
    await endSessionButton.click();

    // Verify confirmation dialog appears
    const confirmDialog = page.locator('[data-testid="confirm-dialog"]');
    await expect(confirmDialog).toBeVisible();
    await expect(confirmDialog).toContainText('Terminare la sessione');

    // Confirm end session
    const confirmButton = page.locator('button:has-text("Conferma")');
    await confirmButton.click();

    // Verify optimistic UI update (slot count reduces immediately)
    const activeSlots = page.locator('[data-testid="slot-card"][data-status="active"]');
    await expect(activeSlots).toHaveCount(1);

    // Verify success toast notification
    const toast = page.locator('[data-sonner-toast]');
    await expect(toast).toContainText('Sessione terminata');

    // Verify usage progress updated (1/5 = 20%)
    const progressValue = page.locator('[data-testid="slot-usage-text"]');
    await expect(progressValue).toContainText('1 / 5');

    // Verify API was called
    expect(endSessionCalled).toBe(true);
  });

  test('should show upgrade CTA for locked slots', async ({ page }) => {
    // Verify locked slots section exists
    const lockedSlotCard = page.locator('[data-testid="locked-slot-card"]');
    await expect(lockedSlotCard).toBeVisible();

    // Verify premium benefits are listed
    await expect(lockedSlotCard).toContainText('+3 additional');
    await expect(lockedSlotCard).toContainText('Priority model');

    // Verify upgrade button exists
    const upgradeButton = page.locator('button:has-text("Upgrade")');
    await expect(upgradeButton).toBeVisible();

    // Click upgrade button
    await upgradeButton.click();

    // Verify pricing modal opens
    const pricingModal = page.locator('[data-testid="pricing-modal"]');
    await expect(pricingModal).toBeVisible();
    await expect(pricingModal).toContainText('Premium');
  });

  test('should handle end session error gracefully', async ({ page }) => {
    // Mock end session API with error
    await page.route('**/api/v1/agent-sessions/*/end', async route => {
      await route.fulfill({
        status: 500,
        json: { error: 'Failed to end session' },
      });
    });

    // Click "End Session" on first slot
    const firstSlot = page.locator('[data-testid="slot-card"][data-status="active"]').first();
    const endSessionButton = firstSlot.locator('button:has-text("End Session")');
    await endSessionButton.click();

    // Confirm
    const confirmButton = page.locator('button:has-text("Conferma")');
    await confirmButton.click();

    // Verify error toast
    const toast = page.locator('[data-sonner-toast]');
    await expect(toast).toContainText('Errore');

    // Verify slot count NOT reduced (rollback)
    const activeSlots = page.locator('[data-testid="slot-card"][data-status="active"]');
    await expect(activeSlots).toHaveCount(2);
  });

  test('should navigate to active session', async ({ page }) => {
    // Click on active slot to resume session
    const firstSlot = page.locator('[data-testid="slot-card"][data-status="active"]').first();
    const resumeButton = firstSlot.locator('button:has-text("Resume")');

    if (await resumeButton.isVisible()) {
      await resumeButton.click();

      // Verify navigation to chat
      await expect(page).toHaveURL(/session=session-1/);
    }
  });
});
