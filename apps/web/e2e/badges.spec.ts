/**
 * Badges Page E2E Tests (Issue #2747)
 *
 * Epic #2718: Game Sharing from User Library to Shared Catalog
 * Milestone: 5 - Gamification
 */

import { test, expect } from '@playwright/test';

test.describe('Badges Page', () => {
  test.beforeEach(async ({ page }) => {
    // Login and navigate to badges page
    await page.goto('/login');
    await page.fill('input[name="email"]', 'test@example.com');
    await page.fill('input[name="password"]', 'password123');
    await page.click('button[type="submit"]');
    await page.waitForURL('/dashboard');
    await page.goto('/badges');
  });

  test('should display badge grid with tier grouping', async ({ page }) => {
    await expect(page.locator('h3:has-text("Badges")')).toBeVisible();

    // Check tier headers
    await expect(page.locator('h3:has-text("Diamond Badges")')).toBeVisible();
    await expect(page.locator('h3:has-text("Gold Badges")')).toBeVisible();
    await expect(page.locator('h3:has-text("Bronze Badges")')).toBeVisible();
  });

  test('should display leaderboard with period filters', async ({ page }) => {
    await expect(page.locator('text=Top Contributors')).toBeVisible();

    // Check period tabs
    await expect(page.getByRole('button', { name: 'This Week' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'This Month' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'All Time' })).toBeVisible();
  });

  test('should switch leaderboard periods', async ({ page }) => {
    const weekTab = page.getByRole('button', { name: 'This Week' });

    await weekTab.click();

    // Verify active tab styling
    await expect(weekTab).toHaveClass(/bg-background/);
  });

  test('should open badge detail sheet on click', async ({ page }) => {
    const firstBadge = page.locator('[data-testid="badge-item"]').first();

    await firstBadge.click();

    // Verify detail sheet opens
    await expect(page.locator('text=Badge Details')).toBeVisible();
    await expect(page.getByRole('button', { name: /close/i })).toBeVisible();
  });

  test('should toggle badge visibility', async ({ page }) => {
    const firstBadge = page.locator('[data-testid="badge-item"]').first();

    await firstBadge.click();

    // Find and toggle visibility switch
    const visibilitySwitch = page.locator('[role="switch"]');
    const initialState = await visibilitySwitch.getAttribute('aria-checked');

    await visibilitySwitch.click();

    // Verify toggle changed
    await expect(visibilitySwitch).not.toHaveAttribute('aria-checked', initialState);
  });

  test('should highlight current user in leaderboard', async ({ page }) => {
    // Find leaderboard row with "(You)" indicator
    const currentUserRow = page.locator('text=(You)').locator('..');

    await expect(currentUserRow).toHaveClass(/border-primary/);
  });

  test('should be responsive', async ({ page }) => {
    // Test mobile viewport
    await page.setViewportSize({ width: 375, height: 667 });
    await expect(page.locator('h1:has-text("My Badges")')).toBeVisible();

    // Test tablet viewport
    await page.setViewportSize({ width: 768, height: 1024 });
    await expect(page.locator('h1:has-text("My Badges")')).toBeVisible();

    // Test desktop viewport
    await page.setViewportSize({ width: 1920, height: 1080 });
    await expect(page.locator('h1:has-text("My Badges")')).toBeVisible();
  });
});
