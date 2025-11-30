/**
 * E2E tests for BottomNav component
 * Issue #1829 [UI-002] BottomNav Component (Mobile-First)
 *
 * Test Coverage:
 * - Mobile visibility (< 768px)
 * - Desktop hiding (≥ 768px)
 * - Navigation functionality (all 5 routes)
 * - Active state visual verification
 * - Touch target accessibility (44x44px)
 * - Keyboard navigation
 */

import { test, expect } from '@playwright/test';

// Use mobile viewport by default
test.use({
  viewport: { width: 375, height: 667 }, // iPhone SE
});

test.describe('BottomNav - Mobile Navigation', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to authenticated page (assumes auth middleware)
    await page.goto('/dashboard');
    await page.waitForLoadState('networkidle');
  });

  test('should be visible on mobile viewport (375px)', async ({ page }) => {
    const bottomNav = page.locator('nav[aria-label="Primary mobile navigation"]');
    await expect(bottomNav).toBeVisible();
  });

  test('should render all 5 navigation items with correct labels', async ({ page }) => {
    const bottomNav = page.locator('nav[aria-label="Primary mobile navigation"]');

    await expect(bottomNav.getByText('Home')).toBeVisible();
    await expect(bottomNav.getByText('Giochi')).toBeVisible();
    await expect(bottomNav.getByText('Chat')).toBeVisible();
    await expect(bottomNav.getByText('Config')).toBeVisible();
    await expect(bottomNav.getByText('Profilo')).toBeVisible();
  });

  test('should have correct ARIA labels for screen readers', async ({ page }) => {
    await expect(page.getByRole('link', { name: 'Navigate to dashboard home' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Navigate to games catalog' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Navigate to chat interface' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Navigate to settings' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Navigate to user profile' })).toBeVisible();
  });

  test('should navigate to Games page when Games tab clicked', async ({ page }) => {
    const gamesLink = page.getByRole('link', { name: 'Navigate to games catalog' });
    await gamesLink.click();

    await page.waitForURL('/giochi');
    expect(page.url()).toContain('/giochi');
  });

  test('should navigate to Chat page when Chat tab clicked', async ({ page }) => {
    const chatLink = page.getByRole('link', { name: 'Navigate to chat interface' });
    await chatLink.click();

    await page.waitForURL('/chat');
    expect(page.url()).toContain('/chat');
  });

  test('should navigate to Settings page when Config tab clicked', async ({ page }) => {
    const settingsLink = page.getByRole('link', { name: 'Navigate to settings' });
    await settingsLink.click();

    await page.waitForURL('/settings');
    expect(page.url()).toContain('/settings');
  });

  test('should navigate to Profile page when Profilo tab clicked', async ({ page }) => {
    const profileLink = page.getByRole('link', { name: 'Navigate to user profile' });
    await profileLink.click();

    await page.waitForURL('/profile');
    expect(page.url()).toContain('/profile');
  });

  test('should mark Home as active on /dashboard', async ({ page }) => {
    await page.goto('/dashboard');

    const homeLink = page.getByRole('link', { name: 'Navigate to dashboard home' });
    await expect(homeLink).toHaveAttribute('aria-current', 'page');
  });

  test('should mark Games as active on /giochi', async ({ page }) => {
    await page.goto('/giochi');
    await page.waitForLoadState('networkidle');

    const gamesLink = page.getByRole('link', { name: 'Navigate to games catalog' });
    await expect(gamesLink).toHaveAttribute('aria-current', 'page');
  });

  test('should mark Chat as active on /chat', async ({ page }) => {
    await page.goto('/chat');
    await page.waitForLoadState('networkidle');

    const chatLink = page.getByRole('link', { name: 'Navigate to chat interface' });
    await expect(chatLink).toHaveAttribute('aria-current', 'page');
  });

  test('should have only one active tab at a time', async ({ page }) => {
    await page.goto('/chat');
    await page.waitForLoadState('networkidle');

    const activeLinks = page.locator('[aria-current="page"]');
    await expect(activeLinks).toHaveCount(1);
  });

  test('should have minimum 44x44px touch targets (WCAG 2.1 AA)', async ({ page }) => {
    const links = page.locator('nav[aria-label="Primary mobile navigation"] a');
    const count = await links.count();

    for (let i = 0; i < count; i++) {
      const box = await links.nth(i).boundingBox();
      expect(box).not.toBeNull();
      expect(box!.width).toBeGreaterThanOrEqual(44);
      expect(box!.height).toBeGreaterThanOrEqual(44);
    }
  });

  test('should be fixed at bottom of viewport', async ({ page }) => {
    const bottomNav = page.locator('nav[aria-label="Primary mobile navigation"]');
    const box = await bottomNav.boundingBox();

    expect(box).not.toBeNull();
    const viewportHeight = page.viewportSize()!.height;
    expect(box!.y + box!.height).toBeCloseTo(viewportHeight, 5); // Within 5px of bottom
  });

  test('should have correct height (72px)', async ({ page }) => {
    const bottomNav = page.locator('nav[aria-label="Primary mobile navigation"]');
    const box = await bottomNav.boundingBox();

    expect(box).not.toBeNull();
    expect(box!.height).toBe(72);
  });
});

test.describe('BottomNav - Desktop Visibility', () => {
  test('should be hidden on desktop viewport (≥768px)', async ({ page }) => {
    await page.setViewportSize({ width: 1024, height: 768 });
    await page.goto('/dashboard');
    await page.waitForLoadState('networkidle');

    const bottomNav = page.locator('nav[aria-label="Primary mobile navigation"]');
    await expect(bottomNav).toBeHidden();
  });

  test('should be hidden on tablet viewport (768px)', async ({ page }) => {
    await page.setViewportSize({ width: 768, height: 1024 });
    await page.goto('/dashboard');
    await page.waitForLoadState('networkidle');

    const bottomNav = page.locator('nav[aria-label="Primary mobile navigation"]');
    await expect(bottomNav).toBeHidden();
  });
});

test.describe('BottomNav - Keyboard Navigation', () => {
  test('should support Tab key navigation', async ({ page }) => {
    await page.goto('/dashboard');

    // Tab to first link
    await page.keyboard.press('Tab');
    let focused = await page.evaluate(() => document.activeElement?.getAttribute('aria-label'));
    expect(focused).toContain('Navigate to');

    // Tab through all 5 links
    for (let i = 0; i < 4; i++) {
      await page.keyboard.press('Tab');
    }

    // Should have cycled through all nav items
    focused = await page.evaluate(() => document.activeElement?.getAttribute('aria-label'));
    expect(focused).toContain('Navigate to');
  });

  test('should activate link with Enter key', async ({ page }) => {
    await page.goto('/dashboard');

    // Tab to Games link
    const gamesLink = page.getByRole('link', { name: 'Navigate to games catalog' });
    await gamesLink.focus();
    await page.keyboard.press('Enter');

    await page.waitForURL('/giochi');
    expect(page.url()).toContain('/giochi');
  });

  test('should show focus ring on keyboard focus', async ({ page }) => {
    await page.goto('/dashboard');

    const homeLink = page.getByRole('link', { name: 'Navigate to dashboard home' });
    await homeLink.focus();

    // Check for focus-visible ring (Tailwind: ring-2 ring-primary)
    const hasRing = await homeLink.evaluate(el => {
      const styles = window.getComputedStyle(el);
      // Check for ring styles (box-shadow or outline)
      return styles.boxShadow !== 'none' || styles.outline !== 'none';
    });

    expect(hasRing).toBeTruthy();
  });
});

test.describe('BottomNav - Visual Regression (Chromatic)', () => {
  test('should match visual snapshot - Home active', async ({ page }) => {
    await page.goto('/dashboard');
    await page.waitForLoadState('networkidle');

    const bottomNav = page.locator('nav[aria-label="Primary mobile navigation"]');
    await expect(bottomNav).toHaveScreenshot('bottom-nav-home-active.png');
  });

  test('should match visual snapshot - Games active', async ({ page }) => {
    await page.goto('/giochi');
    await page.waitForLoadState('networkidle');

    const bottomNav = page.locator('nav[aria-label="Primary mobile navigation"]');
    await expect(bottomNav).toHaveScreenshot('bottom-nav-games-active.png');
  });

  test('should match visual snapshot - Chat active', async ({ page }) => {
    await page.goto('/chat');
    await page.waitForLoadState('networkidle');

    const bottomNav = page.locator('nav[aria-label="Primary mobile navigation"]');
    await expect(bottomNav).toHaveScreenshot('bottom-nav-chat-active.png');
  });
});

test.describe('BottomNav - Responsive Breakpoints', () => {
  const viewports = [
    { name: 'Mobile S (320px)', width: 320, height: 568 },
    { name: 'Mobile M (375px)', width: 375, height: 667 },
    { name: 'Mobile L (425px)', width: 425, height: 812 },
    { name: 'Tablet (768px)', width: 768, height: 1024 },
  ];

  for (const viewport of viewports) {
    test(`should render correctly on ${viewport.name}`, async ({ page }) => {
      await page.setViewportSize({ width: viewport.width, height: viewport.height });
      await page.goto('/dashboard');
      await page.waitForLoadState('networkidle');

      const bottomNav = page.locator('nav[aria-label="Primary mobile navigation"]');

      if (viewport.width < 768) {
        await expect(bottomNav).toBeVisible();

        // Verify all items are still readable
        await expect(bottomNav.getByText('Home')).toBeVisible();
        await expect(bottomNav.getByText('Giochi')).toBeVisible();
        await expect(bottomNav.getByText('Chat')).toBeVisible();
        await expect(bottomNav.getByText('Config')).toBeVisible();
        await expect(bottomNav.getByText('Profilo')).toBeVisible();
      } else {
        await expect(bottomNav).toBeHidden();
      }
    });
  }
});
