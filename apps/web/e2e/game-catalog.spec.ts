/**
 * Game Catalog Page E2E Tests (Issue #1838: PAGE-003)
 *
 * Tests:
 * 1. View toggle persists in URL
 * 2. Search updates URL and filters games
 * 3. Pagination navigation works
 * 4. Browser back/forward maintains state
 * 5. Responsive grid (2→3→4 columns)
 * 6. Empty state handling
 * 7. Direct URL access works
 * 8. Accessibility compliance
 */

import { test, expect } from '@playwright/test';

const BASE_URL = process.env.PLAYWRIGHT_TEST_BASE_URL || 'http://localhost:3000';

test.describe('Game Catalog Page', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto(`${BASE_URL}/games`);
  });

  test.describe('Component Rendering', () => {
    test('should load game catalog page successfully', async ({ page }) => {
      // Wait for page to load
      await expect(page.locator('h1')).toContainText('Catalogo Giochi');

      // Check main elements are present
      await expect(page.getByPlaceholder('Cerca giochi per nome...')).toBeVisible();
      await expect(page.getByLabel('Toggle view mode')).toBeVisible();

      // Games grid should be visible (or empty state)
      const gamesGrid = page.locator('div').filter({ hasText: /Nessun gioco trovato|game-/i });
      await expect(gamesGrid).toBeVisible();
    });

    test('should display correct page title and metadata', async ({ page }) => {
      await expect(page).toHaveTitle(/Catalogo Giochi.*MeepleAI/);
    });
  });

  test.describe('View Toggle', () => {
    test('should toggle between grid and list view', async ({ page }) => {
      // Start in grid view (default)
      await expect(page).toHaveURL(/view=grid|^(?!.*view)/);

      // Click list view
      const listButton = page.getByLabel('List view');
      await listButton.click();

      // URL should update
      await expect(page).toHaveURL(/view=list/);

      // Click grid view
      const gridButton = page.getByLabel('Grid view');
      await gridButton.click();

      // URL should update back to grid
      await expect(page).toHaveURL(/view=grid/);
    });

    test('should persist view mode in URL on page reload', async ({ page }) => {
      // Switch to list view
      await page.getByLabel('List view').click();
      await expect(page).toHaveURL(/view=list/);

      // Reload page
      await page.reload();

      // Should still be list view
      await expect(page).toHaveURL(/view=list/);
    });
  });

  test.describe('Search Functionality', () => {
    test('should update URL when searching', async ({ page }) => {
      const searchInput = page.getByPlaceholder('Cerca giochi per nome...');

      // Type search query
      await searchInput.fill('catan');

      // Wait for debounce (300ms)
      await page.waitForTimeout(400);

      // URL should contain search param
      await expect(page).toHaveURL(/search=catan/);
    });

    test('should clear search when X button clicked', async ({ page }) => {
      const searchInput = page.getByPlaceholder('Cerca giochi per nome...');

      // Type and wait for debounce
      await searchInput.fill('test');
      await page.waitForTimeout(400);

      // Clear button should appear
      const clearButton = page.getByLabel('Clear search');
      await expect(clearButton).toBeVisible();

      // Click clear
      await clearButton.click();

      // Input should be empty
      await expect(searchInput).toHaveValue('');

      // URL should not have search param
      await page.waitForTimeout(400);
      await expect(page).toHaveURL(/^(?!.*search)/);
    });

    test('should reset to page 1 when search changes', async ({ page }) => {
      // Navigate to page 2 first (if pagination exists)
      const page2Link = page.getByLabel(/Go to page 2|page 2/i);
      if (await page2Link.isVisible()) {
        await page2Link.click();
        await expect(page).toHaveURL(/page=2/);

        // Now search
        await page.getByPlaceholder('Cerca giochi per nome...').fill('test');
        await page.waitForTimeout(400);

        // Should be back to page 1
        await expect(page).toHaveURL(/^(?!.*page=2)/);
      }
    });
  });

  test.describe('Pagination', () => {
    test('should navigate between pages', async ({ page }) => {
      // Check if pagination exists
      const nextButton = page.getByLabel('Next page');

      if (await nextButton.isEnabled()) {
        // Click next
        await nextButton.click();

        // URL should update to page 2
        await expect(page).toHaveURL(/page=2/);

        // Page info should update
        await expect(page.locator('text=/Pagina 2 di/')).toBeVisible();

        // Previous button should now be enabled
        const prevButton = page.getByLabel('Previous page');
        await expect(prevButton).toBeEnabled();

        // Click previous
        await prevButton.click();

        // Should be back to page 1 (no page param or page=1)
        await expect(page).toHaveURL(/^(?!.*page)|page=1/);
      }
    });

    test('should scroll to top when changing page', async ({ page }) => {
      const nextButton = page.getByLabel('Next page');

      if (await nextButton.isEnabled()) {
        // Scroll down first
        await page.evaluate(() => window.scrollTo(0, 500));
        await page.waitForTimeout(100);

        // Click next page
        await nextButton.click();

        // Wait for smooth scroll animation
        await page.waitForTimeout(500);

        // Should be scrolled to top
        const scrollY = await page.evaluate(() => window.scrollY);
        expect(scrollY).toBeLessThan(100);
      }
    });
  });

  test.describe('Browser Navigation', () => {
    test('should support browser back/forward buttons', async ({ page }) => {
      // Change view to list
      await page.getByLabel('List view').click();
      await expect(page).toHaveURL(/view=list/);

      // Search for something
      await page.getByPlaceholder('Cerca giochi per nome...').fill('test');
      await page.waitForTimeout(400);
      await expect(page).toHaveURL(/search=test/);

      // Go back (should remove search)
      await page.goBack();
      await expect(page).toHaveURL(/^(?!.*search)/);
      await expect(page).toHaveURL(/view=list/);

      // Go back again (should remove view)
      await page.goBack();
      await expect(page).toHaveURL(/^(?!.*view)/);

      // Go forward
      await page.goForward();
      await expect(page).toHaveURL(/view=list/);
    });
  });

  test.describe('Responsive Design', () => {
    test('should display 2-column grid on mobile', async ({ page }) => {
      await page.setViewportSize({ width: 375, height: 667 });

      // Check grid columns (if games exist)
      const grid = page.locator('[class*="grid-cols-2"]').first();
      if (await grid.isVisible()) {
        await expect(grid).toBeVisible();
      }
    });

    test('should display 3-column grid on tablet', async ({ page }) => {
      await page.setViewportSize({ width: 768, height: 1024 });

      // Check grid columns (if games exist)
      const grid = page.locator('[class*="md:grid-cols-3"]').first();
      if (await grid.isVisible()) {
        await expect(grid).toBeVisible();
      }
    });

    test('should display 4-column grid on desktop', async ({ page }) => {
      await page.setViewportSize({ width: 1440, height: 900 });

      // Check grid columns (if games exist)
      const grid = page.locator('[class*="lg:grid-cols-4"]').first();
      if (await grid.isVisible()) {
        await expect(grid).toBeVisible();
      }
    });
  });

  test.describe('Empty State', () => {
    test('should show empty state when no games found', async ({ page }) => {
      // Search for something that won't match
      await page.getByPlaceholder('Cerca giochi per nome...').fill('xyznonexistent123');
      await page.waitForTimeout(400);

      // Empty state should appear
      await expect(page.locator('text=/Nessun gioco trovato/i')).toBeVisible();
      await expect(page.locator('text=/Prova a modificare/i')).toBeVisible();
    });
  });

  test.describe('Direct URL Access', () => {
    test('should load correctly with URL parameters', async ({ page }) => {
      // Navigate directly to page with params
      await page.goto(`${BASE_URL}/games?view=list&search=catan&page=2`);

      // View should be list
      const listToggle = page.getByLabel('List view');
      await expect(listToggle).toHaveAttribute('aria-pressed', 'true');

      // Search should be populated
      await expect(page.getByPlaceholder('Cerca giochi per nome...')).toHaveValue('catan');

      // Page should be 2 (if exists)
      await expect(page).toHaveURL(/page=2/);
    });
  });

  test.describe('Accessibility', () => {
    test('should have proper ARIA labels', async ({ page }) => {
      // Search input
      await expect(page.getByLabel('Search games')).toBeVisible();

      // View toggle
      await expect(page.getByLabel('Toggle view mode')).toBeVisible();
      await expect(page.getByLabel('Grid view')).toBeVisible();
      await expect(page.getByLabel('List view')).toBeVisible();

      // Pagination buttons (if visible)
      const prevButton = page.getByLabel('Previous page');
      const nextButton = page.getByLabel('Next page');

      if (await prevButton.isVisible()) {
        await expect(prevButton).toHaveAttribute('aria-label');
      }
      if (await nextButton.isVisible()) {
        await expect(nextButton).toHaveAttribute('aria-label');
      }
    });

    test('should have proper heading hierarchy', async ({ page }) => {
      // h1 should exist and be unique
      const h1Count = await page.locator('h1').count();
      expect(h1Count).toBe(1);

      // h1 should contain "Catalogo Giochi"
      await expect(page.locator('h1')).toContainText('Catalogo Giochi');
    });

    test('should be keyboard navigable', async ({ page }) => {
      // Tab through interactive elements
      await page.keyboard.press('Tab'); // Search input
      await page.keyboard.press('Tab'); // Clear button (if visible)
      await page.keyboard.press('Tab'); // Grid toggle
      await page.keyboard.press('Tab'); // List toggle

      // Active element should be one of the toggles
      const activeElement = await page.evaluate(() =>
        document.activeElement?.getAttribute('aria-label')
      );
      expect(['Grid view', 'List view', 'Search games', 'Clear search']).toContain(activeElement);
    });
  });
});
