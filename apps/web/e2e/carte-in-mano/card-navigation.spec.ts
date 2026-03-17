/**
 * E2E Tests for Carte in Mano — Card Navigation System
 *
 * Verifies the UnifiedShell card-stack layout, pinned section cards,
 * bottom nav default actions, and expand/collapse toggle behavior.
 *
 * Uses page.context().route() for API mocking (NOT page.route()).
 * Auth bypass via PLAYWRIGHT_AUTH_BYPASS=true env var.
 */

import { test, expect, type Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

async function mockUserAuth(page: Page) {
  await page.context().route(`${API_BASE}/api/v1/auth/me`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: {
          id: 'test-user-1',
          email: 'test@test.com',
          displayName: 'Test User',
          role: 'User',
        },
        expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
      }),
    })
  );

  await page.context().route(`${API_BASE}/api/v1/auth/session`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: {
          id: 'test-user-1',
          email: 'test@test.com',
          displayName: 'Test User',
          role: 'User',
        },
        isAuthenticated: true,
      }),
    })
  );

  // Catch-all for other API calls to prevent 500s
  await page.context().route(`${API_BASE}/api/v1/**`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ data: [], items: [], totalCount: 0 }),
    })
  );
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

test.describe('Carte in Mano — Card Navigation', () => {
  test.beforeEach(async ({ page }) => {
    await mockUserAuth(page);
  });

  test('card stack is visible on /library', async ({ page }) => {
    await page.goto('/library', { waitUntil: 'domcontentloaded' });

    const cardStack = page.getByTestId('card-stack');
    await expect(cardStack).toBeVisible({ timeout: 10000 });
  });

  test('pinned section shows 4 default cards', async ({ page }) => {
    await page.goto('/library', { waitUntil: 'domcontentloaded' });

    const pinnedSection = page.getByTestId('card-stack-pinned');
    await expect(pinnedSection).toBeVisible({ timeout: 10000 });

    // Default pinned cards: Library, Discover, Chat, Sessions
    // They are rendered as card-stack-item-section-{id}
    const pinnedItems = pinnedSection.locator('[data-testid^="card-stack-item-"]');

    // Wait for at least one pinned card to appear before counting
    await expect(pinnedItems.first()).toBeVisible({ timeout: 10000 });

    const count = await pinnedItems.count();
    expect(count).toBe(4);
  });

  test('bottom nav shows default section actions', async ({ page }) => {
    await page.goto('/library', { waitUntil: 'domcontentloaded' });

    const bottomNav = page.getByTestId('contextual-bottom-nav');
    await expect(bottomNav).toBeVisible({ timeout: 10000 });

    // Default actions when no card is focused: Library, Discover, Chat, Sessions
    // The ContextualBottomNavItem renders buttons with the action labels
    await expect(bottomNav.getByText('Library').first()).toBeVisible();
    await expect(bottomNav.getByText('Discover').first()).toBeVisible();
    await expect(bottomNav.getByText('Chat').first()).toBeVisible();
    await expect(bottomNav.getByText('Sessions').first()).toBeVisible();
  });

  test('expand/collapse toggle changes card stack width', async ({ page }) => {
    await page.goto('/library', { waitUntil: 'domcontentloaded' });

    const cardStack = page.getByTestId('card-stack');
    await expect(cardStack).toBeVisible({ timeout: 10000 });

    // Default state: collapsed (w-14 = 56px)
    const initialBox = await cardStack.boundingBox();
    expect(initialBox).toBeTruthy();
    // Collapsed width is w-14 (56px) — allow some tolerance
    expect(initialBox!.width).toBeLessThanOrEqual(80);

    // Click expand toggle
    const expandButton = page.getByRole('button', { name: 'Expand card stack' });
    await expect(expandButton).toBeVisible();
    await expandButton.click();

    // Wait for transition (200ms defined in component)
    await page.waitForTimeout(300);

    // Expanded state: w-[180px]
    const expandedBox = await cardStack.boundingBox();
    expect(expandedBox).toBeTruthy();
    expect(expandedBox!.width).toBeGreaterThanOrEqual(150);

    // Click collapse toggle
    const collapseButton = page.getByRole('button', { name: 'Collapse card stack' });
    await expect(collapseButton).toBeVisible();
    await collapseButton.click();

    await page.waitForTimeout(300);

    // Back to collapsed
    const collapsedBox = await cardStack.boundingBox();
    expect(collapsedBox).toBeTruthy();
    expect(collapsedBox!.width).toBeLessThanOrEqual(80);
  });

  test('unified top nav is visible with MeepleAI branding', async ({ page }) => {
    await page.goto('/library', { waitUntil: 'domcontentloaded' });

    const topNav = page.getByTestId('unified-top-nav');
    await expect(topNav).toBeVisible({ timeout: 10000 });

    // Branding text visible
    await expect(topNav.getByText('MeepleAI').first()).toBeVisible();
  });
});

// ---------------------------------------------------------------------------
// Mobile viewport tests
// ---------------------------------------------------------------------------

test.describe('Carte in Mano — Mobile Responsive', () => {
  test.use({ viewport: { width: 390, height: 844 } });

  test.beforeEach(async ({ page }) => {
    await mockUserAuth(page);
  });

  test('card stack is narrow (56px) on mobile', async ({ page }) => {
    await page.goto('/library', { waitUntil: 'domcontentloaded' });

    const cardStack = page.getByTestId('card-stack');
    await expect(cardStack).toBeVisible({ timeout: 10000 });

    const box = await cardStack.boundingBox();
    expect(box).toBeTruthy();
    // w-14 = 56px, allow small tolerance for borders/padding
    expect(box!.width).toBeLessThanOrEqual(70);
  });

  test('bottom nav is visible on mobile', async ({ page }) => {
    await page.goto('/library', { waitUntil: 'domcontentloaded' });

    const bottomNav = page.getByTestId('contextual-bottom-nav');
    await expect(bottomNav).toBeVisible({ timeout: 10000 });
  });
});
