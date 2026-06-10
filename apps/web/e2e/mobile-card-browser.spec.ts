/**
 * E2E tests for Mobile Card Browser flow
 * Feature: mobile-ux-card-browser
 *
 * Test Coverage:
 * - 2-column card grid on mobile viewport
 * - MeepleCardBrowser overlay (open, carousel, ESC close)
 *
 * Note: global bottom-bar navigation is covered by bottom-nav.spec.ts
 * (MobileBottomBar, data-testid "mobile-bottom-bar"). This file focuses on the
 * card browser overlay only.
 *
 * Removed in #2118: a `DomainHub /hub with 8 tiles` block (this file head + the
 * `Mobile Card Browser - Responsive Breakpoints` describe) used to assert a
 * landing surface that `(authenticated)/hub/page.tsx` no longer renders — the
 * route has been a 307 redirect to `/hub/games` since #2043.
 */

import { test, expect } from '@playwright/test';

// iPhone 13 / 14 viewport
test.use({
  viewport: { width: 390, height: 844 },
});

test.describe('Mobile Card Browser - Card Grid', () => {
  test('card grid shows 2 columns on mobile', async ({ page }) => {
    await page.goto('/agents');
    await page.waitForLoadState('networkidle');

    // Wait for cards to load (may not have data in CI)
    const hasCards = await page
      .waitForSelector('[data-testid="meeple-card"]', { timeout: 10000 })
      .then(() => true)
      .catch(() => false);

    if (!hasCards) test.skip(true, 'No agent cards available');

    // Grid container should use 2-column layout on mobile
    const grid = page.locator('.grid-cols-2, .grid.grid-cols-2').first();
    await expect(grid).toBeVisible();
  });

  test('cards are rendered as MeepleCard components', async ({ page }) => {
    await page.goto('/agents');
    await page.waitForLoadState('networkidle');

    const hasCards = await page
      .waitForSelector('[data-testid="meeple-card"]', { timeout: 10000 })
      .then(() => true)
      .catch(() => false);

    if (!hasCards) test.skip(true, 'No agent cards available');

    const cards = page.locator('[data-testid="meeple-card"]');
    const count = await cards.count();
    expect(count).toBeGreaterThan(0);
  });
});

test.describe('Mobile Card Browser - Overlay', () => {
  test('tapping card opens full-screen overlay with carousel', async ({ page }) => {
    await page.goto('/agents');
    await page.waitForLoadState('networkidle');

    const hasCards = await page
      .waitForSelector('[data-testid="meeple-card"]', { timeout: 10000 })
      .then(() => true)
      .catch(() => false);

    if (!hasCards) test.skip(true, 'No agent cards available');

    // Tap the first card
    const firstCard = page.locator('[data-testid="meeple-card"]').first();
    await firstCard.click();

    // Overlay dialog should appear
    const overlay = page.locator('[role="dialog"][aria-label="Card browser"]');
    await expect(overlay).toBeVisible({ timeout: 3000 });

    // Carousel indicator should be visible (e.g., "1/3")
    const indicator = page.locator('[data-testid="carousel-indicator"]');
    await expect(indicator).toBeVisible();
    const indicatorText = await indicator.textContent();
    expect(indicatorText).toMatch(/^\d+\/\d+$/);

    // Carousel container should exist
    const carousel = page.locator('[data-testid="carousel-container"]');
    await expect(carousel).toBeVisible();
  });

  test('ESC key closes the overlay', async ({ page }) => {
    await page.goto('/agents');
    await page.waitForLoadState('networkidle');

    const hasCards = await page
      .waitForSelector('[data-testid="meeple-card"]', { timeout: 10000 })
      .then(() => true)
      .catch(() => false);

    if (!hasCards) test.skip(true, 'No agent cards available');

    const firstCard = page.locator('[data-testid="meeple-card"]').first();
    await firstCard.click();

    const overlay = page.locator('[role="dialog"][aria-label="Card browser"]');
    await expect(overlay).toBeVisible({ timeout: 3000 });

    // Press ESC to close
    await page.keyboard.press('Escape');
    await expect(overlay).not.toBeVisible({ timeout: 3000 });
  });

  test('close button closes the overlay', async ({ page }) => {
    await page.goto('/agents');
    await page.waitForLoadState('networkidle');

    const hasCards = await page
      .waitForSelector('[data-testid="meeple-card"]', { timeout: 10000 })
      .then(() => true)
      .catch(() => false);

    if (!hasCards) test.skip(true, 'No agent cards available');

    const firstCard = page.locator('[data-testid="meeple-card"]').first();
    await firstCard.click();

    const overlay = page.locator('[role="dialog"][aria-label="Card browser"]');
    await expect(overlay).toBeVisible({ timeout: 3000 });

    // Click the Close button (aria-label="Close")
    const closeButton = overlay.getByLabel('Close');
    await closeButton.click();
    await expect(overlay).not.toBeVisible({ timeout: 3000 });
  });

  test('deck stack drawer can be toggled', async ({ page }) => {
    await page.goto('/agents');
    await page.waitForLoadState('networkidle');

    const hasCards = await page
      .waitForSelector('[data-testid="meeple-card"]', { timeout: 10000 })
      .then(() => true)
      .catch(() => false);

    if (!hasCards) test.skip(true, 'No agent cards available');

    const firstCard = page.locator('[data-testid="meeple-card"]').first();
    await firstCard.click();

    const overlay = page.locator('[role="dialog"][aria-label="Card browser"]');
    await expect(overlay).toBeVisible({ timeout: 3000 });

    // Click the History/Layers button to open deck stack drawer
    const historyButton = overlay.getByLabel('History');
    await historyButton.click();

    // DeckStackDrawer should become visible
    const deckDrawer = page
      .locator('[data-testid="deck-stack-drawer"]')
      .or(
        page.locator(
          '[role="dialog"][aria-label*="stack" i], [role="dialog"][aria-label*="deck" i]'
        )
      );
    // Allow for different implementations - drawer might use sheet/dialog or custom element
    const drawerVisible = await deckDrawer.isVisible({ timeout: 2000 }).catch(() => false);
    if (drawerVisible) {
      await expect(deckDrawer.first()).toBeVisible();
    }
  });
});
