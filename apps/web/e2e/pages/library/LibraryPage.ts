import { type Page, expect } from '@playwright/test';

import { BasePage } from '../base/BasePage';

export class LibraryPage extends BasePage {
  constructor(page: Page) {
    super(page);
  }

  async goto(): Promise<void> {
    await this.page.goto('/library');
    await this.waitForLoad();
  }

  async clickAddGame(): Promise<void> {
    await this.click(
      this.page
        .locator('[data-testid="add-game-button"]')
        .or(this.page.getByRole('button', { name: /add game/i }))
    );
  }

  async searchGame(gameName: string): Promise<void> {
    const searchInput = this.page
      .locator('[data-testid="game-search-input"]')
      .or(this.page.getByPlaceholder(/search/i));
    await this.fill(searchInput, gameName);
    await this.waitForNetworkIdle();
  }

  async selectFirstSearchResult(): Promise<{ gameId: string; gameTitle: string }> {
    const result = this.page
      .locator('[data-testid="game-search-result"]')
      .or(this.page.locator('[data-testid^="game-result-"]'))
      .first();

    await expect(result).toBeVisible();

    const gameTitle =
      (await result.locator('h3, [data-testid="game-title"]').first().textContent()) ?? 'Unknown';
    const gameId = (await result.getAttribute('data-game-id')) ?? '';

    await result.click();
    return { gameId, gameTitle: gameTitle.trim() };
  }

  async confirmAddToCollection(): Promise<void> {
    await this.click(
      this.page
        .locator('[data-testid="confirm-add-game"]')
        .or(this.page.getByRole('button', { name: /add to collection|confirm|save/i }))
    );
    await this.waitForNetworkIdle();
  }

  async verifyGameInCollection(gameTitle: string): Promise<void> {
    await expect(this.page.getByText(gameTitle)).toBeVisible({ timeout: 10_000 });
  }
}
