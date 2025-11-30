/**
 * E2E Test: Game Detail Page (/giochi/[id])
 *
 * Tests:
 * - Navigation to game detail page
 * - Hero section visible (16:9 aspect ratio)
 * - Info grid displays 3 cards (Players, Time, Complexity)
 * - Tabs keyboard navigation
 * - Tab content switching (Overview, FAQ, Chat)
 * - Chat tab functional
 *
 * Issue #1841 (PAGE-005)
 */

import { test, expect } from '@playwright/test';

const TEST_GAME_ID = '1'; // Assumes test database has game with ID=1

test.describe('Game Detail Page (/giochi/[id])', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to game detail page
    await page.goto(`/giochi/${TEST_GAME_ID}`);
  });

  test('should display hero section with 16:9 aspect ratio', async ({ page }) => {
    const hero = page.getByTestId('hero-section');
    await expect(hero).toBeVisible();

    // Verify aspect ratio class
    await expect(hero).toHaveClass(/aspect-video/);

    // Hero should contain title
    await expect(hero.locator('h1')).toBeVisible();
  });

  test('should display info grid with 3 cards', async ({ page }) => {
    const infoGrid = page.getByTestId('info-grid');
    await expect(infoGrid).toBeVisible();

    // Should have 3 cards (Players, Time, Complexity)
    const cards = infoGrid.locator('.border'); // Shadcn Card has border class
    await expect(cards).toHaveCount(3);

    // Verify card titles
    await expect(infoGrid.getByText('Giocatori')).toBeVisible();
    await expect(infoGrid.getByText('Durata')).toBeVisible();
    await expect(infoGrid.getByText('Complessità')).toBeVisible();
  });

  test('should have 3 keyboard-navigable tabs', async ({ page }) => {
    // Find tab list
    const tabList = page.locator('[role="tablist"]');
    await expect(tabList).toBeVisible();

    // Should have 3 tabs
    const tabs = page.locator('[role="tab"]');
    await expect(tabs).toHaveCount(3);

    // Verify tab labels
    await expect(page.getByRole('tab', { name: /Panoramica|Info/i })).toBeVisible();
    await expect(page.getByRole('tab', { name: /FAQ/i })).toBeVisible();
    await expect(page.getByRole('tab', { name: /Chat AI/i })).toBeVisible();
  });

  test('should switch tab content when clicking tabs', async ({ page }) => {
    // Default: Overview tab active
    await expect(page.getByRole('tab', { name: /Panoramica|Info/i })).toHaveAttribute(
      'data-state',
      'active'
    );

    // Click FAQ tab
    await page.getByRole('tab', { name: /FAQ/i }).click();
    await expect(page.getByTestId('faq-tab')).toBeVisible();

    // Click Chat tab
    await page.getByRole('tab', { name: /Chat AI/i }).click();
    await expect(page.getByTestId('chat-tab')).toBeVisible();
  });

  test('should navigate tabs with keyboard (Enter key)', async ({ page }) => {
    // Focus FAQ tab and press Enter
    const faqTab = page.getByRole('tab', { name: /FAQ/i });
    await faqTab.focus();
    await page.keyboard.press('Enter');

    // FAQ tab should be active
    await expect(faqTab).toHaveAttribute('data-state', 'active');
    await expect(page.getByTestId('faq-tab')).toBeVisible();
  });

  test('should display chat interface in Chat tab', async ({ page }) => {
    // Click Chat tab
    await page.getByRole('tab', { name: /Chat AI/i }).click();

    // Chat interface should be visible
    const chatInterface = page.getByTestId('chat-interface');
    await expect(chatInterface).toBeVisible();

    // Quick questions chips should be visible
    await expect(page.getByText('Domande Rapide')).toBeVisible();
  });

  test('should have functional quick questions chips', async ({ page }) => {
    // Navigate to Chat tab
    await page.getByRole('tab', { name: /Chat AI/i }).click();

    // Find quick question chips
    const quickQuestion = page.getByRole('button', { name: /Come si gioca?/i });
    await expect(quickQuestion).toBeVisible();

    // Chip should be clickable (keyboard accessible)
    await quickQuestion.focus();
    await expect(quickQuestion).toBeFocused();
  });

  test('should be responsive on mobile (375px)', async ({ page }) => {
    // Set mobile viewport
    await page.setViewportSize({ width: 375, height: 667 });

    // Hero, InfoGrid, Tabs should all be visible
    await expect(page.getByTestId('hero-section')).toBeVisible();
    await expect(page.getByTestId('info-grid')).toBeVisible();
    await expect(page.locator('[role="tablist"]')).toBeVisible();
  });

  test('should be responsive on tablet (768px)', async ({ page }) => {
    // Set tablet viewport
    await page.setViewportSize({ width: 768, height: 1024 });

    // All elements should still be visible
    await expect(page.getByTestId('hero-section')).toBeVisible();
    await expect(page.getByTestId('info-grid')).toBeVisible();
    await expect(page.locator('[role="tablist"]')).toBeVisible();
  });

  test('should be responsive on desktop (1440px)', async ({ page }) => {
    // Set desktop viewport
    await page.setViewportSize({ width: 1440, height: 900 });

    // Verify info grid uses 3 columns on desktop
    const infoGrid = page.getByTestId('info-grid');
    await expect(infoGrid).toHaveClass(/lg:grid-cols-3/);
  });
});
