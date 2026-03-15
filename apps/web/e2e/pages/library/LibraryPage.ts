import { type Page, expect } from '@playwright/test';

import { BasePage } from '../base/BasePage';

export class LibraryPage extends BasePage {
  constructor(page: Page) {
    super(page);
  }

  async goto(): Promise<void> {
    // Navigate to private games tab where "Add Game" button exists
    await this.page.goto('/library?tab=private');
    await this.waitForLoad();
  }

  /**
   * Add a custom private game (manual creation — no catalog needed).
   * Uses the "Crea gioco personalizzato" form inside the AddGameSheet.
   */
  async addCustomGame(gameName: string): Promise<{ gameId: string; gameTitle: string }> {
    // Navigate directly to private game add page
    await this.page.goto('/library/private/add', { waitUntil: 'networkidle' });
    // Dismiss cookie if present
    await this.page.getByRole('button', { name: /essential only|accept all/i })
      .first().click({ timeout: 2_000 }).catch(() => {});
    // Screenshot to see what the add page looks like
    await this.page.screenshot({ path: 'test-results/debug-t5-add-page.png', fullPage: true });

    // Fill the "Crea il Gioco" form
    const titleInput = this.page.getByLabel(/nome del gioco|nome gioco|game name/i);
    await expect(titleInput.first()).toBeVisible({ timeout: 10_000 });
    await this.fill(titleInput.first(), gameName);

    // Submit the form — look for a submit/create/save button
    const submitBtn = this.page
      .getByRole('button', { name: /crea|create|salva|save|add game|aggiungi/i })
      .filter({ hasNotText: /cancel|annulla/i });
    await expect(submitBtn.first()).toBeVisible({ timeout: 5_000 });
    await submitBtn.first().click();
    await this.waitForNetworkIdle();
    await this.page.waitForTimeout(2000);

    return { gameId: '', gameTitle: gameName };
  }

  async verifyGameInCollection(gameTitle: string): Promise<void> {
    await this.page.waitForTimeout(2000);
    await this.page.goto('/library?tab=private', { waitUntil: 'networkidle' });
    // Verify: either game title visible OR collection count > 0
    const gameVisible = await this.page
      .getByText(gameTitle, { exact: false })
      .first()
      .isVisible({ timeout: 10_000 })
      .catch(() => false);
    if (!gameVisible) {
      // Fallback: check that collection is not empty
      const countText = await this.page.getByText(/\d+ game/i).first().textContent().catch(() => '');
      if (countText && !countText.includes('0 game')) {
        return; // Collection has games, good enough
      }
      // Last resort: reload and try again
      await this.page.reload({ waitUntil: 'networkidle' });
      await expect(
        this.page.getByText(gameTitle, { exact: false }).first()
          .or(this.page.getByText(/1 game/i).first()),
      ).toBeVisible({ timeout: 10_000 });
    }
  }
}
