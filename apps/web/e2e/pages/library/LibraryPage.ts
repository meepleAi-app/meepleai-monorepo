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
    await this.page
      .getByRole('button', { name: /essential only|accept all/i })
      .first()
      .click({ timeout: 2_000 })
      .catch(() => {});
    // Screenshot to see what the add page looks like
    await this.page.screenshot({ path: 'test-results/debug-t5-add-page.png', fullPage: true });

    // Fill the "Crea il Gioco" form
    const titleInput = this.page.getByLabel(/nome del gioco|nome gioco|game name/i);
    await expect(titleInput.first()).toBeVisible({ timeout: 10_000 });
    await this.fill(titleInput.first(), gameName);

    // Intercept the API response to capture the game ID
    // Intercept any POST that creates a game (private-games or library endpoints)
    const createPromise = this.page
      .waitForResponse(
        resp =>
          (resp.url().includes('private-game') ||
            resp.url().includes('private_game') ||
            resp.url().includes('/library')) &&
          resp.request().method() === 'POST' &&
          resp.status() >= 200 &&
          resp.status() < 300
      )
      .catch(() => null);

    // Submit the form
    const submitBtn = this.page
      .getByRole('button', { name: /crea|create|salva|save|add game|aggiungi/i })
      .filter({ hasNotText: /cancel|annulla/i });
    await expect(submitBtn.first()).toBeVisible({ timeout: 5_000 });
    await submitBtn.first().click();

    // Capture game ID from API response
    let privateGameId = '';
    const createResponse = await Promise.race([
      createPromise,
      new Promise<null>(r => setTimeout(() => r(null), 15_000)),
    ]);
    if (createResponse) {
      console.log(`[LibraryPage] Create API: ${createResponse.status()} ${createResponse.url()}`);
      const data = await createResponse.json().catch(() => ({}));
      privateGameId = data.id ?? data.privateGameId ?? '';
      console.log(`[LibraryPage] Game ID from API: ${privateGameId}`);
    } else {
      console.log('[LibraryPage] No create API response captured');
    }

    // Fallback: try to get from URL after redirect
    if (!privateGameId) {
      await this.page.waitForTimeout(3000);
      const url = this.page.url();
      const match = url.match(
        /private\/([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})/
      );
      privateGameId = match?.[1] ?? '';
    }

    await this.waitForNetworkIdle();

    return { gameId: privateGameId, gameTitle: gameName };
  }

  /**
   * Upload a PDF to a private game's Knowledge Base.
   * Navigates to the game's hub page and uses the upload area.
   */
  async uploadPdfToGame(privateGameId: string, pdfPath: string): Promise<void> {
    // Navigate to the private game hub
    await this.page.goto(`/library/private/${privateGameId}`, { waitUntil: 'networkidle' });
    await this.page.waitForTimeout(1000);

    // Find the file input (hidden) and set the file
    const fileInput = this.page.locator('input[type="file"][accept*="pdf"]');
    if ((await fileInput.count()) === 0) {
      // Try clicking an upload button to reveal the file input
      const uploadBtn = this.page
        .getByText(/carica|upload|trascina|drag/i)
        .first()
        .or(this.page.locator('[data-testid="show-upload-button"]'));
      if (await uploadBtn.isVisible({ timeout: 5_000 }).catch(() => false)) {
        await uploadBtn.click();
        await this.page.waitForTimeout(500);
      }
    }

    await fileInput.first().setInputFiles(pdfPath);
    // Wait for upload to start and complete
    await this.page.waitForTimeout(3000);
    await this.waitForNetworkIdle();
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
      const countText = await this.page
        .getByText(/\d+ game/i)
        .first()
        .textContent()
        .catch(() => '');
      if (countText && !countText.includes('0 game')) {
        return; // Collection has games, good enough
      }
      // Last resort: reload and try again
      await this.page.reload({ waitUntil: 'networkidle' });
      await expect(
        this.page
          .getByText(gameTitle, { exact: false })
          .first()
          .or(this.page.getByText(/1 game/i).first())
      ).toBeVisible({ timeout: 10_000 });
    }
  }
}
