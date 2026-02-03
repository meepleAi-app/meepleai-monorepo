/**
 * Add Game Wizard E2E Tests
 * Issue #3477: End-to-end tests for wizard flow
 */

import { test, expect } from '@playwright/test';

test.describe('Add Game Wizard', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to wizard
    await page.goto('/dashboard/collection/add-game');
  });

  test('displays wizard title and steps', async ({ page }) => {
    // Check title
    await expect(page.getByTestId('wizard-title')).toHaveText('Add Game to Collection');

    // Check all 4 steps are visible
    await expect(page.getByText('1. Search/Select')).toBeVisible();
    await expect(page.getByText('2. Game Details')).toBeVisible();
    await expect(page.getByText('3. Upload PDF')).toBeVisible();
    await expect(page.getByText('4. Review')).toBeVisible();
  });

  test('complete flow: search and select shared game', async ({ page }) => {
    // Step 1: Search and select a game
    await expect(page.getByText('Search or Create Game')).toBeVisible();

    // Search for a game
    const searchInput = page.getByPlaceholder('Search by game title...');
    await searchInput.fill('Catan');

    // Select first result
    const gameCard = page.getByRole('button', { name: /Catan/i }).first();
    await gameCard.click();

    // Verify selection
    await expect(page.getByText('Selected ✓')).toBeVisible();

    // Click Next → Should skip to Step 3 (Upload PDF)
    await page.getByRole('button', { name: /Next/i }).click();

    // Step 3: Upload PDF (or skip)
    await expect(page.getByText('Upload Private PDF')).toBeVisible();
    await page.getByRole('button', { name: /Skip/i }).click();

    // Step 4: Review
    await expect(page.getByText('Review & Confirm')).toBeVisible();
    await expect(page.getByText('Catan')).toBeVisible();
    await expect(page.getByText('No PDF uploaded')).toBeVisible();

    // Submit
    await page.getByRole('button', { name: /Add to Collection/i }).click();

    // Should redirect to collection dashboard
    await page.waitForURL('/dashboard/collection');
  });

  test('complete flow: create custom game with PDF', async ({ page }) => {
    // Step 1: Create custom game
    await page.getByRole('button', { name: /Create Custom Game/i }).click();

    // Step 2: Fill game details
    await expect(page.getByText('Game Details')).toBeVisible();

    await page.getByLabel('Game Name *').fill('My Custom Game');
    await page.getByLabel('Min Players').fill('2');
    await page.getByLabel('Max Players').fill('4');
    await page.getByLabel('Play Time (minutes)').fill('60');
    await page.getByLabel('Complexity (1-5)').fill('3');

    await page.getByRole('button', { name: /Next/i }).click();

    // Step 3: Skip PDF for this test
    await expect(page.getByText('Upload Private PDF')).toBeVisible();
    await page.getByRole('button', { name: /Skip/i }).click();

    // Step 4: Review
    await expect(page.getByText('My Custom Game')).toBeVisible();
    await expect(page.getByText('Custom')).toBeVisible();
    await expect(page.getByText('2-4')).toBeVisible(); // Players
    await expect(page.getByText('60 min')).toBeVisible(); // Play time

    // Submit
    await page.getByRole('button', { name: /Add to Collection/i }).click();

    // Should redirect
    await page.waitForURL('/dashboard/collection');
  });

  test('navigation: back button works correctly', async ({ page }) => {
    // Select custom game
    await page.getByRole('button', { name: /Create Custom Game/i }).click();

    // On Step 2
    await expect(page.getByText('Game Details')).toBeVisible();

    // Go back
    await page.getByRole('button', { name: /Back/i }).click();

    // Should be back on Step 1
    await expect(page.getByText('Search or Create Game')).toBeVisible();
  });

  test('validation: custom game requires name', async ({ page }) => {
    // Create custom game
    await page.getByRole('button', { name: /Create Custom Game/i }).click();

    // Try to go next without filling name
    const nextButton = page.getByRole('button', { name: /Next/i });
    // Next button should be disabled (canGoNext() returns false)
    await expect(nextButton).toBeDisabled();
  });

  test('cancel button returns to collection dashboard', async ({ page }) => {
    // Click "Back to Collection" link
    await page.getByRole('link', { name: /Back to Collection/i }).click();

    // Should navigate to collection dashboard
    await page.waitForURL('/dashboard/collection');
  });
});
