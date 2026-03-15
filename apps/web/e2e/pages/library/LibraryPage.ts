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
    // Try: header button, empty state CTA, or URL-based trigger
    const addBtn = this.page
      .locator('[data-testid="add-private-game-btn"]')
      .or(this.page.locator('[data-testid="empty-state-primary-cta"]'))
      .or(this.page.getByRole('button', { name: /aggiungi|add game/i }));
    await addBtn.first().click();
  }

  async selectFromCatalog(): Promise<void> {
    // In the choice step, select "From shared catalog"
    const catalogChoice = this.page.locator('[data-testid="add-game-choice-catalog"]');
    if (await catalogChoice.isVisible({ timeout: 3_000 }).catch(() => false)) {
      await catalogChoice.click();
    }
  }

  async searchGame(gameName: string): Promise<void> {
    // Search input has Italian placeholder "Cerca un gioco..."
    const searchInput = this.page
      .locator('[data-testid="game-search-input"]')
      .or(this.page.getByPlaceholder(/cerca un gioco|search/i));
    await this.fill(searchInput, gameName);
    await this.waitForNetworkIdle();
  }

  async selectFirstSearchResult(): Promise<{ gameId: string; gameTitle: string }> {
    // Search results are buttons with game title text
    const results = this.page
      .locator('[data-testid="add-game-drawer"] button, [role="dialog"] button')
      .filter({ hasNotText: /annulla|indietro|avanti|cerca|chiudi/i });

    // Wait for results to appear
    await this.page.waitForTimeout(1000);

    // Find a result that looks like a game (has game-like content)
    const gameResult = this.page
      .locator('[data-testid^="game-result-"]')
      .or(this.page.locator('[data-testid="add-game-drawer"] [role="option"]'))
      .or(results.first());

    await expect(gameResult.first()).toBeVisible({ timeout: 10_000 });

    const gameTitle = (await gameResult.first().textContent()) ?? 'Unknown Game';
    const gameId = (await gameResult.first().getAttribute('data-game-id')) ?? '';

    await gameResult.first().click();
    return { gameId, gameTitle: gameTitle.trim().split('\n')[0].trim() };
  }

  async clickNext(): Promise<void> {
    // "Avanti" button to go to next step
    await this.click(this.page.getByRole('button', { name: /avanti|next/i }));
  }

  async confirmAddToCollection(): Promise<void> {
    // Final save button: "Salva in Collezione"
    const saveBtn = this.page
      .locator('[data-testid="save-button"]')
      .or(this.page.getByRole('button', { name: /salva in collezione|save|confirm/i }));
    await saveBtn.click();
    await this.waitForNetworkIdle();
  }

  async verifyGameInCollection(gameTitle: string): Promise<void> {
    // After save, verify game appears somewhere on the page
    await this.page.waitForTimeout(2000);
    // Navigate to library to verify
    await this.page.goto('/library');
    await this.waitForLoad();
    await expect(this.page.getByText(gameTitle, { exact: false }).first()).toBeVisible({
      timeout: 10_000,
    });
  }
}
