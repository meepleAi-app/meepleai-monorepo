/**
 * E2E Tests for User Library Page (Issue #2464)
 *
 * Tests complete user workflows:
 * - Add game to library from catalog
 * - View library and see added games
 * - Edit notes for library entry
 * - Toggle favorite status
 * - Remove game from library
 * - Filter and search library
 */

import { test, expect } from '@playwright/test';

test.describe('User Library Management', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to library page (assumes user is authenticated)
    await page.goto('/library');
  });

  test('should display empty state when library is empty', async ({ page }) => {
    await expect(page.locator('text=La tua libreria è vuota')).toBeVisible();
    await expect(page.locator('text=Esplora Catalogo Giochi')).toBeVisible();
  });

  test('should add game to library from catalog', async ({ page }) => {
    // Go to catalog
    await page.goto('/library');

    // Click "Add to Library" on first game
    const addButton = page.locator('button:has-text("Aggiungi alla Collezione")').first();
    await addButton.click();

    // Wait for success toast
    await expect(page.locator('text=/aggiunto alla tua libreria/')).toBeVisible({
      timeout: 5000,
    });

    // Navigate to library
    await page.goto('/library');

    // Verify game appears
    await expect(page.locator('[data-testid="game-card"]').first()).toBeVisible();
  });

  test('should filter library by favorites', async ({ page }) => {
    // Assume library has games
    await page.waitForSelector('[data-testid="game-card"]', { timeout: 5000 });

    // Toggle favorites filter
    const favoritesToggle = page.locator('label:has-text("Solo Preferiti")');
    await favoritesToggle.click();

    // Verify URL updated with filter
    await expect(page).toHaveURL(/favoritesOnly=true/);
  });

  test('should search library by game title', async ({ page }) => {
    // Type in search box
    const searchInput = page.getByPlaceholderText('Cerca per titolo...');
    await searchInput.fill('Catan');

    // Wait for debounce (300ms)
    await page.waitForTimeout(400);

    // Verify only matching games shown
    const gameCards = page.locator('[data-testid="game-card"]');
    const count = await gameCards.count();

    if (count > 0) {
      // All visible games should match search
      const firstGame = gameCards.first();
      await expect(firstGame.locator('text=/Catan/i')).toBeVisible();
    } else {
      // No results message
      await expect(page.locator('text=Nessun gioco trovato')).toBeVisible();
    }
  });

  test('should open edit notes modal', async ({ page }) => {
    // Wait for game cards
    await page.waitForSelector('[data-testid="game-card"]', { timeout: 5000 });

    // Click edit notes button
    const editButton = page.locator('button:has-text("Modifica Note")').first();
    await editButton.click();

    // Verify modal opened
    await expect(page.locator('text=Modifica Note')).toBeVisible();
    await expect(page.getByPlaceholderText('Inserisci le tue note qui...')).toBeVisible();
  });

  test('should save notes in edit modal', async ({ page }) => {
    // Wait for game cards
    await page.waitForSelector('[data-testid="game-card"]', { timeout: 5000 });

    // Open edit notes
    await page.locator('button:has-text("Modifica Note")').first().click();

    // Type notes
    const textarea = page.getByPlaceholderText('Inserisci le tue note qui...');
    await textarea.fill('Great game for families!');

    // Save
    await page.locator('button:has-text("Salva")').click();

    // Verify success toast
    await expect(page.locator('text=Note aggiornate con successo')).toBeVisible({
      timeout: 5000,
    });
  });

  test('should open remove confirmation dialog', async ({ page }) => {
    // Wait for game cards
    await page.waitForSelector('[data-testid="game-card"]', { timeout: 5000 });

    // Click remove button (trash icon)
    const removeButton = page
      .locator('button')
      .filter({ has: page.locator('svg.lucide-trash-2') })
      .first();
    await removeButton.click();

    // Verify confirmation dialog
    await expect(page.locator('text=Rimuovi dalla Libreria?')).toBeVisible();
    await expect(page.locator('text=/Questa azione non può essere annullata/')).toBeVisible();
  });

  test('should remove game when confirmed', async ({ page }) => {
    // Wait for game cards
    await page.waitForSelector('[data-testid="game-card"]', { timeout: 5000 });

    const initialCount = await page.locator('[data-testid="game-card"]').count();

    // Click remove
    const removeButton = page
      .locator('button')
      .filter({ has: page.locator('svg.lucide-trash-2') })
      .first();
    await removeButton.click();

    // Confirm removal
    await page.locator('button:has-text("Rimuovi")').click();

    // Verify success toast
    await expect(page.locator('text=/rimosso dalla tua libreria/')).toBeVisible({
      timeout: 5000,
    });

    // Verify game count decreased or empty state shown
    const newCount = await page.locator('[data-testid="game-card"]').count();
    if (initialCount > 1) {
      expect(newCount).toBe(initialCount - 1);
    } else {
      await expect(page.locator('text=La tua libreria è vuota')).toBeVisible();
    }
  });

  test('should toggle favorite status', async ({ page }) => {
    // Wait for game cards
    await page.waitForSelector('[data-testid="game-card"]', { timeout: 5000 });

    // Click favorite toggle (heart icon)
    const favoriteButton = page.locator('button[aria-label*="favorite"]').first();
    await favoriteButton.click();

    // Verify success toast
    await expect(page.locator('text=/added to|removed from.*favorites/i')).toBeVisible({
      timeout: 5000,
    });
  });

  test('should change sort order', async ({ page }) => {
    // Wait for sort dropdown
    const sortDropdown = page.getByLabelText('Sort library');
    await sortDropdown.click();

    // Select "Titolo (A-Z)"
    await page.locator('text=Titolo (A-Z)').click();

    // Verify filter applied (URL or state change)
    // Note: Actual verification depends on implementation
  });

  test('should clear all filters', async ({ page }) => {
    // Apply filters
    await page.getByPlaceholderText('Cerca per titolo...').fill('test');
    await page.waitForTimeout(400); // Debounce

    await page.locator('label:has-text("Solo Preferiti")').click();

    // Clear filters
    await page.locator('button:has-text("Pulisci Filtri")').click();

    // Verify search cleared
    const searchInput = page.getByPlaceholderText('Cerca per titolo...');
    await expect(searchInput).toHaveValue('');

    // Verify favorites toggle unchecked
    const favoritesSwitch = page.locator('button[role="switch"]');
    await expect(favoritesSwitch).toHaveAttribute('aria-checked', 'false');
  });
});
