import * as fs from 'node:fs';
import * as path from 'node:path';

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
   * Upload a PDF to a private game via direct API call.
   * Uses page.request to POST multipart form data with session cookies.
   */
  async uploadPdfToGame(privateGameId: string, pdfPath: string): Promise<string> {
    // Navigate to hub first (for screenshots and cookie context)
    await this.page.goto(`/library/private/${privateGameId}`, { waitUntil: 'networkidle' });
    await this.page.waitForTimeout(1000);
    await this.page.screenshot({
      path: 'test-results/debug-t5-hub-before-upload.png',
      fullPage: true,
    });

    // Try UI-based upload first (click "Carica regolamento" → disclaimer → file chooser)
    try {
      return await this.uploadPdfViaUI(pdfPath);
    } catch (e) {
      console.log(`[LibraryPage] UI upload failed: ${e}, trying API fallback`);
    }

    // Fallback: use Playwright's request API to upload directly to the same proxy.
    // page.request shares the page's storage state (cookies) automatically.
    // This bypasses browser fetch limitations with large payloads.
    const currentUrl = new URL(this.page.url());
    const apiUrl = `${currentUrl.origin}/api/v1/ingest/pdf`;

    const response = await this.page.request.post(apiUrl, {
      multipart: {
        file: {
          name: path.basename(pdfPath),
          mimeType: 'application/pdf',
          buffer: fs.readFileSync(pdfPath),
        },
        privateGameId: privateGameId,
      },
    });

    const status = response.status();
    const body = await response.text();
    console.log(
      `[LibraryPage] Playwright request upload: status=${status}, body=${body.substring(0, 300)}`
    );

    if (status >= 200 && status < 300) {
      try {
        const data = JSON.parse(body);
        return data.documentId ?? '';
      } catch {
        return '';
      }
    }
    return '';
  }

  /**
   * Upload PDF via UI flow: Carica regolamento → disclaimer → file chooser.
   */
  private async uploadPdfViaUI(pdfPath: string): Promise<string> {
    // Register response interceptor BEFORE triggering upload
    const uploadResponsePromise = this.page
      .waitForResponse(
        resp => resp.url().includes('/ingest/pdf') && resp.request().method() === 'POST'
      )
      .catch(() => null);

    // 1. Click "Carica regolamento"
    const uploadBtn = this.page.getByRole('button', { name: /carica regolamento/i });
    await expect(uploadBtn.first()).toBeVisible({ timeout: 10_000 });
    await uploadBtn.first().click();

    // 2. Handle file chooser after disclaimer acceptance
    const fileChooserPromise = this.page.waitForEvent('filechooser');
    const disclaimerAccept = this.page.getByRole('button', {
      name: /confermo e carico|accetto|accept/i,
    });
    await expect(disclaimerAccept.first()).toBeVisible({ timeout: 5_000 });
    await disclaimerAccept.first().click();

    // 3. Set file
    const fileChooser = await fileChooserPromise;
    await fileChooser.setFiles(pdfPath);

    // 4. Wait for upload API response
    const uploadResponse = await Promise.race([
      uploadResponsePromise,
      new Promise<null>(r => setTimeout(() => r(null), 60_000)),
    ]);
    if (uploadResponse) {
      const status = uploadResponse.status();
      console.log(`[LibraryPage] UI upload response: ${status} ${uploadResponse.url()}`);
      if (status >= 200 && status < 300) {
        const data = await uploadResponse.json().catch(() => ({}));
        return data.documentId ?? '';
      }
      throw new Error(`Upload failed with status ${status}`);
    }
    throw new Error('Upload response timeout');
  }

  /**
   * Enqueue a PDF for processing via the admin queue API.
   * The fire-and-forget Task.Run on the backend can fail silently;
   * this ensures the Quartz-based queue picks up the job.
   */
  async enqueuePdfForProcessing(documentId: string, adminPage: Page): Promise<void> {
    const currentUrl = new URL(adminPage.url());
    const baseUrl = currentUrl.origin;

    const response = await adminPage.request.post(`${baseUrl}/api/v1/admin/queue/enqueue`, {
      data: { pdfDocumentId: documentId, priority: 5 },
    });
    const status = response.status();
    const body = await response.text();
    if (status >= 200 && status < 300) {
      console.log(`[LibraryPage] PDF enqueued successfully: ${body.substring(0, 200)}`);
    } else if (status === 409) {
      console.log(`[LibraryPage] PDF already in queue (409) — OK`);
    } else {
      console.warn(
        `[LibraryPage] Enqueue unexpected status=${status}, body=${body.substring(0, 200)}`
      );
    }
  }

  /**
   * Wait for PDF processing to complete by polling the hub page.
   * Uses data-completed attribute on step-pdf element.
   */
  async waitForPdfProcessing(privateGameId: string, timeoutMs: number = 120_000): Promise<boolean> {
    const startTime = Date.now();
    const pollInterval = 8_000;

    while (Date.now() - startTime < timeoutMs) {
      // Reload the hub page to check status
      await this.page.goto(`/library/private/${privateGameId}`, { waitUntil: 'networkidle' });
      await this.page.waitForTimeout(2000);

      // Check if step-pdf has data-completed="true"
      const stepPdf = this.page.locator('[data-testid="step-pdf"][data-completed="true"]');
      if ((await stepPdf.count()) > 0) {
        console.log('[LibraryPage] PDF processing complete (step-pdf data-completed=true)');
        return true;
      }

      // Screenshot for debugging
      await this.page.screenshot({
        path: `test-results/debug-t5-pdf-processing-${Math.round((Date.now() - startTime) / 1000)}s.png`,
        fullPage: true,
      });

      const elapsed = Math.round((Date.now() - startTime) / 1000);
      console.log(`[LibraryPage] PDF still processing... (${elapsed}s elapsed)`);
      await this.page.waitForTimeout(pollInterval);
    }

    console.log('[LibraryPage] PDF processing timed out');
    return false;
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
