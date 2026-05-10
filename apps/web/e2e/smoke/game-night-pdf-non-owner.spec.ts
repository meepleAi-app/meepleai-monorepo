/**
 * Smoke E2E G4 — PDF tab non-owner upsell.
 *
 * Mock SSE V2 happy + mock getGameDocuments → empty array.
 * useCanViewPdf ritorna canView=false → CitationOwnershipUpsell rendered.
 *
 * Spec: docs/superpowers/specs/2026-05-10-e2e-smoke-game-night-design.md §3.4
 */
import { test, expect } from '@playwright/test';

import {
  loginSmokeAaron,
  mockNoChatHistory,
  mockNoDocuments,
  mockQaStreamV2,
} from './_helpers';
import { SEED_DOCUMENT_ID, SEED_GAME_ID } from './_helpers.fixtures';

test.describe('Smoke G4 — PDF non-owner upsell (mocked)', () => {
  test('shows CitationOwnershipUpsell when user does not own the PDF', async ({ page }) => {
    await loginSmokeAaron(page);
    await mockNoChatHistory(page, SEED_GAME_ID);
    await mockNoDocuments(page, SEED_GAME_ID); // forces canView=false
    await mockQaStreamV2(page, {
      tokens: ['Risposta con citazione.'],
      citations: [
        {
          documentId: SEED_DOCUMENT_ID,
          pageNumber: 12,
          snippet: 'Citazione mock',
          relevanceScore: 0.95,
          copyrightTier: 'full',
        },
      ],
      confidence: 0.9,
    });

    await page.goto(`/library/games/${SEED_GAME_ID}?tab=aiChat`);
    await page.waitForLoadState('networkidle');

    const input = page.locator('[data-testid="message-input"]');
    await input.fill('test citazione');
    await page.locator('[data-testid="send-message-button"]').click();

    // Wait response + click chip
    const chip = page.locator('[data-slot="citation-chip"]').first();
    await expect(chip).toBeVisible({ timeout: 10_000 });
    await chip.click();

    const modal = page.locator('[data-slot="citation-modal"]');
    await expect(modal).toBeVisible();

    // Click tab PDF
    const pdfTab = modal.locator('[role="tab"]', { hasText: /pdf/i });
    await pdfTab.click();

    // G4: upsell card visible (NOT viewer)
    const upsell = modal.locator('[data-slot="citation-ownership-upsell"]');
    await expect(upsell).toBeVisible();

    // CTA href contains gameId
    const cta = upsell.locator('a', { hasText: /carica.*pdf/i });
    const href = await cta.getAttribute('href');
    expect(href).toContain(`gameId=${SEED_GAME_ID}`);

    // Anti-leak: NO download button anywhere in modal
    await expect(modal.locator('button', { hasText: /download|scarica/i })).toHaveCount(0);
  });
});
