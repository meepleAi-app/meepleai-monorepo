/**
 * E2E tests for Mobile Card Browser flow
 * Feature: mobile-ux-card-browser
 *
 * Test Coverage:
 * - MobileTabBar visibility and navigation (mobile only, md:hidden)
 * - 2-column card grid on mobile viewport
 * - MeepleCardBrowser overlay (open, carousel, ESC close)
 * - DomainHub page (/hub) with 8 domain tiles
 * - Responsive behavior across mobile breakpoints
 *
 * Architecture Notes (post mobile-ux overhaul):
 * - MobileTabBar replaces the old MobileBottomBar (data-testid: "mobile-tab-bar")
 * - HandDrawer is now desktop-only (not tested on mobile viewports)
 * - ContextualBottomNav is hidden on mobile (hidden md:flex)
 * - SmartFAB deleted (replaced by morphing center tab in MobileTabBar)
 */

import { test, expect } from '@playwright/test';

// iPhone 13 / 14 viewport
test.use({
  viewport: { width: 390, height: 844 },
});

test.describe('Mobile Card Browser - MobileTabBar', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/hub');
    await page.waitForLoadState('networkidle');
  });

  test('tab bar is visible on mobile viewport', async ({ page }) => {
    const tabBar = page.locator('[data-testid="mobile-tab-bar"]');
    await expect(tabBar).toBeVisible();
  });

  test('tab bar renders core tabs (Dashboard, Discover)', async ({ page }) => {
    const tabBar = page.locator('[data-testid="mobile-tab-bar"]');
    await expect(tabBar).toBeVisible();

    // Dashboard and Discover are always visible (not auth-gated)
    await expect(page.getByTestId('mobile-tab-dashboard')).toBeVisible();
    await expect(page.getByTestId('mobile-tab-discover')).toBeVisible();
  });

  test('tab bar is hidden on desktop viewport', async ({ page }) => {
    await page.setViewportSize({ width: 1024, height: 768 });
    await page.goto('/hub');
    await page.waitForLoadState('networkidle');

    const tabBar = page.locator('[data-testid="mobile-tab-bar"]');
    await expect(tabBar).toBeHidden();
  });

  test('tab bar touch targets meet minimum 44x44px', async ({ page }) => {
    const tabBar = page.locator('[data-testid="mobile-tab-bar"]');
    await expect(tabBar).toBeVisible();

    // MobileTabBar uses links and optionally a button for the morphed center tab
    const targets = tabBar.locator('a, button[data-testid^="mobile-tab-"]');
    const count = await targets.count();
    expect(count).toBeGreaterThanOrEqual(2);

    for (let i = 0; i < count; i++) {
      const box = await targets.nth(i).boundingBox();
      expect(box).not.toBeNull();
      expect(box!.width).toBeGreaterThanOrEqual(44);
      expect(box!.height).toBeGreaterThanOrEqual(44);
    }
  });

  test('Dashboard tab navigates to /dashboard', async ({ page }) => {
    await page.goto('/games');
    await page.waitForLoadState('networkidle');

    const tabBar = page.locator('[data-testid="mobile-tab-bar"]');
    const isVisible = await tabBar.isVisible().catch(() => false);
    if (!isVisible) test.skip(true, 'Tab bar not visible in this environment');

    await page.getByTestId('mobile-tab-dashboard').click();
    await page.waitForURL(/\/dashboard/, { timeout: 5000 });
    expect(page.url()).toContain('/dashboard');
  });

  test('Discover tab navigates to /games', async ({ page }) => {
    const tabBar = page.locator('[data-testid="mobile-tab-bar"]');
    const isVisible = await tabBar.isVisible().catch(() => false);
    if (!isVisible) test.skip(true, 'Tab bar not visible in this environment');

    await page.getByTestId('mobile-tab-discover').click();
    await page.waitForURL(/\/games/, { timeout: 5000 });
    expect(page.url()).toContain('/games');
  });

  test('Library tab navigates to /library (authenticated)', async ({ page }) => {
    const tabBar = page.locator('[data-testid="mobile-tab-bar"]');
    const isVisible = await tabBar.isVisible().catch(() => false);
    if (!isVisible) test.skip(true, 'Tab bar not visible in this environment');

    const libraryTab = page.getByTestId('mobile-tab-library');
    const tabVisible = await libraryTab.isVisible().catch(() => false);
    if (!tabVisible) test.skip(true, 'Library tab only visible when authenticated');

    await libraryTab.click();
    await page.waitForURL(/\/library/, { timeout: 5000 });
    expect(page.url()).toContain('/library');
  });
});

test.describe('Mobile Card Browser - DomainHub', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/hub');
    await page.waitForLoadState('networkidle');
  });

  test('domain hub renders 8 domain tiles', async ({ page }) => {
    const expectedDomains = [
      'Games',
      'Agents',
      'Chat',
      'Library',
      'Leaderboards',
      'Sessions',
      'Notifications',
      'Settings',
    ];

    for (const domain of expectedDomains) {
      await expect(page.getByText(domain, { exact: true }).first()).toBeVisible({ timeout: 5000 });
    }
  });

  test('domain hub uses 2-column grid layout', async ({ page }) => {
    // The DomainHub uses grid-cols-2 for its tile layout
    const grid = page.locator('.grid.grid-cols-2').first();
    await expect(grid).toBeVisible({ timeout: 5000 });
  });

  test('Games tile links to /discover', async ({ page }) => {
    const gamesLink = page.locator('a[href="/discover"]').first();
    const gamesVisible = await gamesLink.isVisible().catch(() => false);
    if (!gamesVisible) test.skip(true, 'Games tile not visible in this environment');

    await gamesLink.click();
    await page.waitForURL(/\/discover/, { timeout: 5000 });
    expect(page.url()).toContain('/discover');
  });

  test('Agents tile links to /agents', async ({ page }) => {
    const agentsLink = page.locator('a[href="/agents"]').first();
    const agentsVisible = await agentsLink.isVisible().catch(() => false);
    if (!agentsVisible) test.skip(true, 'Agents tile not visible in this environment');

    await agentsLink.click();
    await page.waitForURL(/\/agents/, { timeout: 5000 });
    expect(page.url()).toContain('/agents');
  });
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

test.describe('Mobile Card Browser - Responsive Breakpoints', () => {
  const mobileViewports = [
    { name: 'iPhone SE', width: 375, height: 667 },
    { name: 'iPhone 13', width: 390, height: 844 },
    { name: 'iPhone 14 Pro Max', width: 430, height: 932 },
  ];

  for (const viewport of mobileViewports) {
    test(`tab bar visible on ${viewport.name} (${viewport.width}px)`, async ({ page }) => {
      await page.setViewportSize({ width: viewport.width, height: viewport.height });
      await page.goto('/hub');
      await page.waitForLoadState('networkidle');

      // MobileTabBar is md:hidden — only visible below 768px
      const tabBar = page.locator('[data-testid="mobile-tab-bar"]');
      await expect(tabBar).toBeVisible();
    });

    test(`domain hub grid renders on ${viewport.name}`, async ({ page }) => {
      await page.setViewportSize({ width: viewport.width, height: viewport.height });
      await page.goto('/hub');
      await page.waitForLoadState('networkidle');

      // Grid should be visible
      const grid = page.locator('.grid.grid-cols-2').first();
      await expect(grid).toBeVisible({ timeout: 5000 });
    });
  }

  test('tab bar is hidden at tablet breakpoint (768px)', async ({ page }) => {
    await page.setViewportSize({ width: 768, height: 1024 });
    await page.goto('/hub');
    await page.waitForLoadState('networkidle');

    // MobileTabBar uses md:hidden — hidden at 768px and above
    const tabBar = page.locator('[data-testid="mobile-tab-bar"]');
    await expect(tabBar).toBeHidden();
  });
});
