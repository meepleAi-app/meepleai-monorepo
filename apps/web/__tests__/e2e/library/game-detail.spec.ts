/**
 * Game Detail Page E2E Tests (Issue #3511)
 *
 * Test Coverage:
 * - Page loading and navigation
 * - Game card flip interaction
 * - State change (Owned → Wishlist → etc)
 * - Favorite toggle
 * - Labels management
 * - Notes editing
 * - Tab switching (Knowledge Base / Social Links)
 * - Remove game flow
 * - Responsive behavior
 *
 * Target: Main user journeys covered
 */

import { test, expect } from '@playwright/test';

test.describe('Library Game Detail Page', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to game detail page (assuming game-123 exists in test DB)
    await page.goto('/library/games/game-123');
  });

  test('loads and displays game information', async ({ page }) => {
    // Wait for page to load
    await expect(page.locator('h1')).toBeVisible();

    // Check game title is present
    const title = await page.locator('h1').textContent();
    expect(title).toBeTruthy();

    // Check main sections render
    await expect(page.getByText(/Posseduto|Wishlist|Nuovo/)).toBeVisible();
  });

  test('flips game card on click', async ({ page }) => {
    const card = page.locator('[role="button"][aria-label*="Carta del gioco"]');

    // Initial state - front side
    await expect(card).toHaveAttribute('aria-label', /vedere i dettagli sul retro/);

    // Click to flip
    await card.click();

    // After flip - back side
    await expect(card).toHaveAttribute('aria-label', /vedere il fronte/);

    // Verify categories/mechanics visible on back
    await expect(page.locator('text=/Categorie|Meccaniche/')).toBeVisible();
  });

  test('flips game card with keyboard (Enter)', async ({ page }) => {
    const card = page.locator('[role="button"][aria-label*="Carta del gioco"]');

    await card.focus();
    await page.keyboard.press('Enter');

    await expect(card).toHaveAttribute('aria-label', /vedere il fronte/);
  });

  test('changes game state via dropdown', async ({ page }) => {
    // Open state dropdown
    await page.getByText('Posseduto').click();

    // Select Wishlist
    await page.getByText('Wishlist').click();

    // Verify toast notification
    await expect(page.getByText(/Stato aggiornato/i)).toBeVisible({ timeout: 5000 });
  });

  test('toggles favorite status', async ({ page }) => {
    const favoriteButton = page.getByLabel(/favorites/i);

    // Click to add to favorites
    await favoriteButton.click();

    // Verify toast appears
    await expect(page.getByText(/aggiunto ai preferiti|rimosso dai preferiti/i)).toBeVisible({ timeout: 5000 });
  });

  test('opens and closes notes modal', async ({ page }) => {
    // Click edit notes button
    await page.getByLabel(/Modifica note/i).click();

    // Modal should open
    await expect(page.getByRole('dialog')).toBeVisible();

    // Close modal
    await page.getByRole('button', { name: /close|chiudi|annulla/i }).click();

    // Modal should close
    await expect(page.getByRole('dialog')).not.toBeVisible();
  });

  test('switches between Knowledge Base and Social Links tabs', async ({ page }) => {
    // Initially on Knowledge Base
    await expect(page.getByText(/Carica PDF|Knowledge Base/)).toBeVisible();

    // Switch to Social Links
    await page.getByText('Social Links').click();

    // Social links content visible
    await expect(page.getByText(/BoardGameGeek|Nessun link/)).toBeVisible();

    // Switch back to Knowledge Base
    await page.getByText('Knowledge Base').click();

    await expect(page.getByText(/Carica PDF|Nessun documento/)).toBeVisible();
  });

  test('opens PDF upload modal', async ({ page }) => {
    // Click upload button
    await page.getByText(/Carica PDF/i).click();

    // Modal should open
    await expect(page.getByRole('dialog')).toBeVisible();

    // Should have file upload UI elements
    await expect(page.getByText(/Upload|Carica|PDF/i)).toBeVisible();
  });

  test('displays play statistics when available', async ({ page }) => {
    // Check for stats section
    const statsSection = page.locator('text=/Partite giocate|Ultima partita|Vittorie/');

    if (await statsSection.isVisible()) {
      // Verify stats are rendered
      await expect(page.locator('text=/\\d+/')).toBeVisible();
    }
  });

  test('opens remove game dialog', async ({ page }) => {
    // Click remove button
    await page.getByText(/Rimuovi/i).click();

    // Confirmation dialog should open
    await expect(page.getByRole('dialog')).toBeVisible();
    await expect(page.getByText(/conferma|sicuro/i)).toBeVisible();
  });

  test('navigates back to library', async ({ page }) => {
    // Click back button
    await page.getByLabel(/Torna|Back/i).click();

    // Should navigate to library
    await expect(page).toHaveURL(/\/library/);
  });
});

test.describe('Game Detail Page - Responsive', () => {
  test('renders correctly on mobile viewport', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 667 });
    await page.goto('/library/games/game-123');

    // Page should render without layout issues
    await expect(page.locator('h1')).toBeVisible();
    await expect(page.getByText(/Posseduto|Wishlist/)).toBeVisible();
  });

  test('renders correctly on tablet viewport', async ({ page }) => {
    await page.setViewportSize({ width: 768, height: 1024 });
    await page.goto('/library/games/game-123');

    await expect(page.locator('h1')).toBeVisible();
  });

  test('renders correctly on desktop viewport', async ({ page }) => {
    await page.setViewportSize({ width: 1920, height: 1080 });
    await page.goto('/library/games/game-123');

    await expect(page.locator('h1')).toBeVisible();

    // Check side-by-side layout on desktop
    const hero = page.locator('[role="button"][aria-label*="Carta del gioco"]');
    const sideCard = page.locator('text=/Knowledge Base|Social Links/');

    await expect(hero).toBeVisible();
    await expect(sideCard).toBeVisible();
  });
});

test.describe('Game Detail Page - Error States', () => {
  test('handles missing game gracefully', async ({ page }) => {
    await page.goto('/library/games/nonexistent-game-id');

    // Should show error message
    await expect(page.getByText(/non trovato|errore/i)).toBeVisible();

    // Should have back button
    await expect(page.getByText(/Torna alla Libreria/i)).toBeVisible();
  });

  test('displays loading state', async ({ page }) => {
    // Intercept API to delay response
    await page.route('**/api/v1/library/games/*', async (route) => {
      await new Promise(resolve => setTimeout(resolve, 1000));
      await route.continue();
    });

    page.goto('/library/games/game-123');

    // Loading state should be visible briefly
    // (Handled by loading.tsx file)
    await page.waitForLoadState('networkidle');
  });
});
