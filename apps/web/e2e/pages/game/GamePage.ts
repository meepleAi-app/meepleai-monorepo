import { Page, Locator } from '@playwright/test';

import { BasePage } from '../base/BasePage';

/**
 * GamePage - Game search and browse interactions
 *
 * Handles all game discovery features:
 * - Game list browsing
 * - Search by name (exact, partial, case-insensitive)
 * - Filters (category, status)
 * - Sorting (name, date added)
 * - Pagination
 * - Game card interactions
 *
 * Usage:
 *   const gamePage = new GamePage(page);
 *   await gamePage.goto();
 *   await gamePage.searchGames('chess');
 *   await gamePage.clickGameCard(0);
 */
export class GamePage extends BasePage {
  // ========================================================================
  // Locators
  // ========================================================================

  /**
   * Game list container (contains all game cards)
   */
  private get gameListContainer(): Locator {
    return this.page.locator('[data-testid="game-list"]');
  }

  /**
   * Individual game cards
   */
  private get gameCards(): Locator {
    return this.page.locator('[data-testid="game-card"]');
  }

  /**
   * Search input field
   */
  private get searchInput(): Locator {
    return this.page.getByRole('textbox', { name: /search games/i });
  }

  /**
   * Search button
   */
  private get searchButton(): Locator {
    return this.page.getByRole('button', { name: /search/i });
  }

  /**
   * Clear search button
   */
  private get clearSearchButton(): Locator {
    return this.page.getByRole('button', { name: /clear|reset/i });
  }

  /**
   * Sort dropdown
   */
  private get sortDropdown(): Locator {
    return this.page.getByRole('combobox', { name: /sort by/i });
  }

  /**
   * Category filter dropdown
   */
  private get categoryFilter(): Locator {
    return this.page.getByRole('combobox', { name: /category/i });
  }

  /**
   * Games per page selector
   */
  private get perPageSelector(): Locator {
    return this.page.getByRole('combobox', { name: /items per page|games per page/i });
  }

  /**
   * Pagination next button
   */
  private get nextPageButton(): Locator {
    return this.page.getByRole('button', { name: /next/i });
  }

  /**
   * Pagination previous button
   */
  private get previousPageButton(): Locator {
    return this.page.getByRole('button', { name: /previous|prev/i });
  }

  /**
   * No results message
   */
  private get noResultsMessage(): Locator {
    return this.page.getByText(/no games found|no results/i);
  }

  /**
   * Loading spinner
   */
  private get loadingSpinner(): Locator {
    return this.page.locator('[data-testid="loading-spinner"]');
  }

  // ========================================================================
  // Navigation
  // ========================================================================

  /**
   * Navigate to games page (homepage or dedicated games page)
   */
  async goto(): Promise<void> {
    await this.page.goto('/');
    await this.waitForLoad();
  }

  // ========================================================================
  // Search Actions
  // ========================================================================

  /**
   * Search for games by name
   * @param query - Search query
   */
  async searchGames(query: string): Promise<void> {
    await this.fill(this.searchInput, query);
    await this.click(this.searchButton);
    await this.waitForSearchResults();
  }

  /**
   * Wait for search results to load (spinner disappears)
   */
  async waitForSearchResults(): Promise<void> {
    // Wait for loading spinner to disappear
    try {
      await this.waitForElementToDisappear(this.loadingSpinner, { timeout: 5000 });
    } catch {
      // Spinner may not appear for cached results
    }
  }

  /**
   * Clear search (reset to all games)
   */
  async clearSearch(): Promise<void> {
    await this.click(this.clearSearchButton);
    await this.waitForSearchResults();
  }

  // ========================================================================
  // Filter & Sort Actions
  // ========================================================================

  /**
   * Select sort option
   * @param option - Sort option (name-asc, name-desc, date-asc, date-desc)
   */
  async sortBy(option: 'name-asc' | 'name-desc' | 'date-asc' | 'date-desc'): Promise<void> {
    await this.selectOption(this.sortDropdown, option);
    await this.waitForSearchResults();
  }

  /**
   * Filter by category
   * @param category - Category name (or 'all' to clear filter)
   */
  async filterByCategory(category: string): Promise<void> {
    await this.selectOption(this.categoryFilter, category);
    await this.waitForSearchResults();
  }

  /**
   * Set games per page
   * @param count - Number of games per page (10, 25, 50)
   */
  async setGamesPerPage(count: 10 | 25 | 50): Promise<void> {
    await this.selectOption(this.perPageSelector, count.toString());
    await this.waitForSearchResults();
  }

  // ========================================================================
  // Pagination Actions
  // ========================================================================

  /**
   * Go to next page
   */
  async goToNextPage(): Promise<void> {
    await this.click(this.nextPageButton);
    await this.waitForSearchResults();
  }

  /**
   * Go to previous page
   */
  async goToPreviousPage(): Promise<void> {
    await this.click(this.previousPageButton);
    await this.waitForSearchResults();
  }

  /**
   * Go to specific page number
   * @param pageNumber - Page number (1-indexed)
   */
  async goToPage(pageNumber: number): Promise<void> {
    const pageButton = this.page.getByRole('button', {
      name: new RegExp(`^${pageNumber}$`),
    });
    await this.click(pageButton);
    await this.waitForSearchResults();
  }

  // ========================================================================
  // Game Card Actions
  // ========================================================================

  /**
   * Get specific game card by index
   * @param index - Card index (0-based)
   */
  private getGameCard(index: number): Locator {
    return this.gameCards.nth(index);
  }

  /**
   * Click game card to navigate to chat
   * @param index - Card index (0-based)
   */
  async clickGameCard(index: number): Promise<void> {
    await this.click(this.getGameCard(index));
  }

  /**
   * Get game name from card
   * @param index - Card index (0-based)
   */
  async getGameName(index: number): Promise<string> {
    const card = this.getGameCard(index);
    const name = await card.locator('[data-testid="game-name"]').textContent();
    return name?.trim() || '';
  }

  /**
   * Get game description from card
   * @param index - Card index (0-based)
   */
  async getGameDescription(index: number): Promise<string> {
    const card = this.getGameCard(index);
    const description = await card.locator('[data-testid="game-description"]').textContent();
    return description?.trim() || '';
  }

  /**
   * Check if game card has image
   * @param index - Card index (0-based)
   */
  async hasGameImage(index: number): Promise<boolean> {
    const card = this.getGameCard(index);
    const image = card.locator('img');
    return await this.isVisible(image);
  }

  // ========================================================================
  // Assertions
  // ========================================================================

  /**
   * Assert game list is visible
   */
  async assertGameListVisible(): Promise<void> {
    await this.waitForElement(this.gameListContainer);
  }

  /**
   * Assert number of visible game cards
   * @param expectedCount - Expected number of cards
   */
  async assertGameCount(expectedCount: number): Promise<void> {
    const count = await this.gameCards.count();
    if (count !== expectedCount) {
      throw new Error(`Expected ${expectedCount} games, found ${count}`);
    }
  }

  /**
   * Assert no results message is visible
   */
  async assertNoResults(): Promise<void> {
    await this.waitForElement(this.noResultsMessage);
  }

  /**
   * Assert pagination controls are visible
   */
  async assertPaginationVisible(): Promise<void> {
    await this.waitForElement(this.nextPageButton);
  }

  /**
   * Assert next button is disabled (last page)
   */
  async assertNextButtonDisabled(): Promise<void> {
    const isDisabled = await this.nextPageButton.isDisabled();
    if (!isDisabled) {
      throw new Error('Next button should be disabled on last page');
    }
  }

  /**
   * Assert previous button is disabled (first page)
   */
  async assertPreviousButtonDisabled(): Promise<void> {
    const isDisabled = await this.previousPageButton.isDisabled();
    if (!isDisabled) {
      throw new Error('Previous button should be disabled on first page');
    }
  }

  /**
   * Assert search input contains value
   * @param value - Expected search value
   */
  async assertSearchValue(value: string): Promise<void> {
    const inputValue = await this.searchInput.inputValue();
    if (inputValue !== value) {
      throw new Error(`Expected search value "${value}", found "${inputValue}"`);
    }
  }

  /**
   * Assert game card displays correctly
   * @param index - Card index (0-based)
   * @param expectedName - Expected game name
   */
  async assertGameCardDisplays(index: number, expectedName: string): Promise<void> {
    const card = this.getGameCard(index);
    await this.waitForElement(card);
    const actualName = await this.getGameName(index);
    if (!actualName.toLowerCase().includes(expectedName.toLowerCase())) {
      throw new Error(`Expected game name to include "${expectedName}", found "${actualName}"`);
    }
  }
}
