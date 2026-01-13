import { Locator } from '@playwright/test';

import { BasePage } from '../base/BasePage';

/**
 * SharedGameCatalogPage - SharedGame search and add flow (Issue #2373)
 *
 * Handles the catalog-first game search flow:
 * - Search in SharedGameCatalog first
 * - BGG fallback when no catalog results
 * - Game detail modal
 * - Add game to collection
 *
 * Usage:
 *   const catalogPage = new SharedGameCatalogPage(page);
 *   await catalogPage.goto();
 *   await catalogPage.searchGames('Catan');
 *   await catalogPage.selectFirstResult();
 *   await catalogPage.confirmAddGame();
 */
export class SharedGameCatalogPage extends BasePage {
  // ========================================================================
  // Locators
  // ========================================================================

  /**
   * Search input field - uses role-based selector for reliability
   * The SharedGameSearch component renders input[type="search"] with aria-label="Cerca giochi"
   */
  private get searchInput(): Locator {
    // Use role-based selector which is more reliable
    return this.page.getByRole('searchbox');
  }

  /**
   * Search input (alternative selector using type attribute)
   */
  private get searchInputAlt(): Locator {
    return this.page.locator('input[type="search"]');
  }

  /**
   * Clear search button (X icon)
   */
  private get clearSearchButton(): Locator {
    return this.page.getByRole('button', { name: /cancella ricerca/i });
  }

  /**
   * Search results container
   */
  private get resultsContainer(): Locator {
    return this.page.locator('[class*="shadow-lg"]').filter({ hasText: /Catalogo|BGG/ });
  }

  /**
   * Individual result items
   */
  private get resultItems(): Locator {
    return this.page.locator('button').filter({ hasText: /Catalogo|BGG/ });
  }

  /**
   * Catalog badge on results
   */
  private get catalogBadges(): Locator {
    return this.page.locator('[class*="badge"]').filter({ hasText: 'Catalogo' });
  }

  /**
   * BGG badge on results
   */
  private get bggBadges(): Locator {
    return this.page.locator('[class*="badge"]').filter({ hasText: 'BGG' });
  }

  /**
   * "Cerca su BoardGameGeek" fallback button
   */
  private get bggFallbackButton(): Locator {
    return this.page.getByRole('button', { name: /cerca su boardgamegeek/i });
  }

  /**
   * No results message
   */
  private get noResultsMessage(): Locator {
    return this.page.locator('text=/Nessun risultato|Nessun gioco trovato/i');
  }

  /**
   * Loading skeleton
   */
  private get loadingSkeleton(): Locator {
    return this.page.locator('[class*="skeleton"]');
  }

  /**
   * Game detail modal
   */
  private get detailModal(): Locator {
    return this.page.locator('[role="dialog"]');
  }

  /**
   * Modal confirm button (Add to collection)
   */
  private get modalConfirmButton(): Locator {
    return this.page.getByRole('button', { name: /aggiungi alla collezione/i });
  }

  /**
   * Modal close button
   */
  private get modalCloseButton(): Locator {
    return this.detailModal.getByRole('button', { name: /chiudi|close|×/i });
  }

  /**
   * Success toast message
   */
  private get successToast(): Locator {
    return this.page.locator('[data-sonner-toast]').filter({ hasText: /successo|aggiunto/i });
  }

  /**
   * Filter toggles
   */
  private get filterToggles(): Locator {
    return this.page.locator('[class*="toggle"]');
  }

  /**
   * "Solo dal catalogo" filter toggle
   */
  private get catalogOnlyFilter(): Locator {
    return this.page.getByRole('button', { name: /solo dal catalogo/i });
  }

  // ========================================================================
  // Navigation
  // ========================================================================

  /**
   * Navigate to add game page
   */
  async goto(): Promise<void> {
    await this.page.goto('/games/add');
    await this.waitForLoad();
  }

  // ========================================================================
  // Search Actions
  // ========================================================================

  /**
   * Search for games
   * @param query - Search term
   */
  async searchGames(query: string): Promise<void> {
    const input = (await this.searchInput.count()) > 0 ? this.searchInput : this.searchInputAlt;
    await this.fill(input, query);
    await this.waitForSearchResults();
  }

  /**
   * Wait for search results (loading spinner disappears)
   */
  async waitForSearchResults(): Promise<void> {
    try {
      // Wait for skeleton to appear then disappear
      await this.waitForElement(this.loadingSkeleton, { timeout: 2000 });
      await this.waitForElementToDisappear(this.loadingSkeleton, { timeout: 10000 });
    } catch {
      // Loading may be too fast to catch
    }
    // Small delay for results to render
    await this.page.waitForTimeout(500);
  }

  /**
   * Clear search input
   */
  async clearSearch(): Promise<void> {
    if (await this.isVisible(this.clearSearchButton)) {
      await this.click(this.clearSearchButton);
    } else {
      const input = (await this.searchInput.count()) > 0 ? this.searchInput : this.searchInputAlt;
      await input.fill('');
    }
    await this.page.waitForTimeout(300);
  }

  /**
   * Click BGG fallback button when no catalog results
   */
  async clickBggFallback(): Promise<void> {
    await this.click(this.bggFallbackButton);
    await this.waitForSearchResults();
  }

  // ========================================================================
  // Filter Actions
  // ========================================================================

  /**
   * Toggle "Solo dal catalogo" filter
   */
  async toggleCatalogOnlyFilter(): Promise<void> {
    await this.click(this.catalogOnlyFilter);
    await this.waitForSearchResults();
  }

  // ========================================================================
  // Result Actions
  // ========================================================================

  /**
   * Get count of search results
   */
  async getResultCount(): Promise<number> {
    return await this.resultItems.count();
  }

  /**
   * Select first search result
   */
  async selectFirstResult(): Promise<void> {
    await this.click(this.resultItems.first());
    await this.waitForElement(this.detailModal);
  }

  /**
   * Select result by index
   * @param index - 0-based index
   */
  async selectResultByIndex(index: number): Promise<void> {
    await this.click(this.resultItems.nth(index));
    await this.waitForElement(this.detailModal);
  }

  /**
   * Get game title from result
   * @param index - 0-based index
   */
  async getResultTitle(index: number): Promise<string> {
    const result = this.resultItems.nth(index);
    const titleElement = result.locator('.font-medium');
    return (await titleElement.textContent())?.trim() || '';
  }

  /**
   * Check if result is from catalog
   * @param index - 0-based index
   */
  async isResultFromCatalog(index: number): Promise<boolean> {
    const result = this.resultItems.nth(index);
    const catalogBadge = result.locator('[class*="badge"]').filter({ hasText: 'Catalogo' });
    return await this.isVisible(catalogBadge);
  }

  // ========================================================================
  // Modal Actions
  // ========================================================================

  /**
   * Confirm add game from modal
   */
  async confirmAddGame(): Promise<void> {
    await this.click(this.modalConfirmButton);
    // Wait for navigation to games page or toast
    await Promise.race([this.waitForUrl('/games'), this.waitForElement(this.successToast)]).catch(
      () => {
        // Either navigation or toast is fine
      }
    );
  }

  /**
   * Close detail modal
   */
  async closeModal(): Promise<void> {
    await this.click(this.modalCloseButton);
    await this.waitForElementToDisappear(this.detailModal);
  }

  /**
   * Check if modal is open
   */
  async isModalOpen(): Promise<boolean> {
    return await this.isVisible(this.detailModal);
  }

  // ========================================================================
  // Assertions
  // ========================================================================

  /**
   * Assert search results are visible
   */
  async assertResultsVisible(): Promise<void> {
    await this.waitForElement(this.resultsContainer);
  }

  /**
   * Assert no results message is shown
   */
  async assertNoResults(): Promise<void> {
    await this.waitForElement(this.noResultsMessage);
  }

  /**
   * Assert BGG fallback button is visible
   */
  async assertBggFallbackVisible(): Promise<void> {
    await this.waitForElement(this.bggFallbackButton);
  }

  /**
   * Assert detail modal is visible
   */
  async assertModalVisible(): Promise<void> {
    await this.waitForElement(this.detailModal);
  }

  /**
   * Assert success message is shown
   */
  async assertSuccessMessage(): Promise<void> {
    await this.waitForElement(this.successToast);
  }

  /**
   * Assert we're on the games page after adding
   */
  async assertOnGamesPage(): Promise<void> {
    await this.waitForUrl(/\/games$/);
  }

  /**
   * Assert catalog results exist
   */
  async assertHasCatalogResults(): Promise<boolean> {
    return (await this.catalogBadges.count()) > 0;
  }

  /**
   * Assert BGG results exist
   */
  async assertHasBggResults(): Promise<boolean> {
    return (await this.bggBadges.count()) > 0;
  }
}
