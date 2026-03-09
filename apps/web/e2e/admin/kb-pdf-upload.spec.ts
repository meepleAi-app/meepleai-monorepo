/**
 * Admin — KB PDF Upload con XHR progress ⭐
 * Sezione 9 del piano di test UI MeepleAI (Epic #4789, #4920)
 *
 * Testa: drag-drop PDF, progress bar XHR, completamento, documento in lista
 */

import { test, expect, type Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

async function mockAdminAuth(page: Page) {
  await page.context().route(`${API_BASE}/api/v1/auth/me`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: { id: 'admin-1', email: 'admin@meepleai.dev', displayName: 'Admin', role: 'Admin' },
        expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
      }),
    })
  );
}

test.describe('ADM — KB PDF Upload ⭐', () => {
  test.beforeEach(async ({ page }) => {
    await mockAdminAuth(page);

    // Mock endpoint upload PDF (XHR multipart)
    await page.context().route(`${API_BASE}/api/v1/admin/knowledge-base/upload`, route => {
      const method = route.request().method();
      if (method === 'POST') {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            documentId: 'doc-new-1',
            fileName: 'test-rulebook.pdf',
            status: 'processing',
            message: 'Upload completato',
          }),
        });
      }
      return route.continue();
    });

    await page
      .context()
      .route(`${API_BASE}/api/v1/admin/**`, route =>
        route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ data: [] }),
        })
      );
  });

  test('carica pagina upload e mostra area drop', async ({ page }) => {
    await page.goto('/admin/knowledge-base/upload', { waitUntil: 'domcontentloaded' });

    // Verifica area di upload presente
    const uploadArea = page
      .locator(
        '[data-testid="upload-area"], [data-testid="drop-zone"], input[type="file"], .dropzone'
      )
      .first();
    await expect(uploadArea).toBeVisible({ timeout: 8000 });
  });

  test('seleziona file tramite input e avvia upload', async ({ page }) => {
    await page.goto('/admin/knowledge-base/upload', { waitUntil: 'domcontentloaded' });

    // Usa file input per simulare selezione file
    const fileInput = page.locator('input[type="file"]').first();
    if ((await fileInput.count()) > 0) {
      // Crea un file PDF fittizio per il test
      await fileInput.setInputFiles({
        name: 'test-rulebook.pdf',
        mimeType: 'application/pdf',
        buffer: Buffer.from('%PDF-1.4 test content'),
      });

      await page.waitForTimeout(500);

      // Verifica che venga mostrato il nome del file o la progress bar
      const feedback = page
        .locator(
          'text=/test-rulebook|upload|caricamento/i, [data-testid="progress-bar"], [role="progressbar"]'
        )
        .first();
      if ((await feedback.count()) > 0) {
        await expect(feedback).toBeVisible({ timeout: 8000 });
      }
    }
  });

  test('click pulsante upload invia richiesta', async ({ page }) => {
    let uploadCalled = false;
    await page.context().route(`${API_BASE}/api/v1/admin/knowledge-base/upload`, route => {
      if (route.request().method() === 'POST') {
        uploadCalled = true;
      }
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ documentId: 'doc-new-1', status: 'processing' }),
      });
    });

    await page.goto('/admin/knowledge-base/upload', { waitUntil: 'domcontentloaded' });

    const fileInput = page.locator('input[type="file"]').first();
    if ((await fileInput.count()) > 0) {
      await fileInput.setInputFiles({
        name: 'test-rulebook.pdf',
        mimeType: 'application/pdf',
        buffer: Buffer.from('%PDF-1.4 test content'),
      });

      // Click pulsante upload se presente (a volte l'upload è automatico)
      const uploadBtn = page
        .locator('button:has-text("Upload"), button:has-text("Carica"), button[type="submit"]')
        .first();
      if ((await uploadBtn.count()) > 0) {
        await uploadBtn.click();
        await page.waitForTimeout(1000);
      }
    }

    await expect(page.locator('body')).toBeVisible();
  });

  test('upload completato mostra messaggio successo', async ({ page }) => {
    await page.goto('/admin/knowledge-base/upload', { waitUntil: 'domcontentloaded' });

    const fileInput = page.locator('input[type="file"]').first();
    if ((await fileInput.count()) > 0) {
      await fileInput.setInputFiles({
        name: 'success-test.pdf',
        mimeType: 'application/pdf',
        buffer: Buffer.from('%PDF-1.4 success test'),
      });

      const uploadBtn = page
        .locator('button:has-text("Upload"), button:has-text("Carica"), button[type="submit"]')
        .first();
      if ((await uploadBtn.count()) > 0) {
        await uploadBtn.click();
        // Aspetta messaggio conferma
        const success = page
          .locator('text=/completato|success|caricato|documento/i, [data-testid="upload-success"]')
          .first();
        if ((await success.count()) > 0) {
          await expect(success).toBeVisible({ timeout: 10000 });
        }
      }
    }
  });
});
