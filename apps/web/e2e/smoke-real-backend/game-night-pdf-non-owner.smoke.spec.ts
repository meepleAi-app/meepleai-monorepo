/**
 * Smoke E2E G4 — PDF tab non-owner upsell (nightly real-backend variant).
 *
 * Mock SSE V2 happy + mock getGameDocuments → empty array.
 * useCanViewPdf ritorna canView=false → CitationOwnershipUpsell rendered.
 *
 * Auth model: smoke-user (admin) + inline seedGameForChat — no snapshot DB needed,
 * compatible with `make dev-core` stack used by nightly cron.
 *
 * Spec: docs/superpowers/specs/2026-05-10-e2e-smoke-game-night-integration-design.md §4.1
 */
import { test, expect } from '@playwright/test';

import { smokeLogin, applySessionToPage } from './_helpers/auth';
import {
  mockHasIndexedDocument,
  mockNoChatHistory,
  mockQaStreamV2,
  seedGameForChat,
} from './_helpers/game-chat';

test.describe('SMOKE — game-chat PDF non-owner upsell (G4)', () => {
  test('shows CitationOwnershipUpsell when user does not own the PDF', async ({
    page,
    request,
  }) => {
    const { cookieHeader } = await smokeLogin(request);
    await applySessionToPage(page, cookieHeader);
    const gameId = await seedGameForChat(request, cookieHeader);
    await mockNoChatHistory(page, gameId);
    // Mock 1 indexed doc with id 053edd. The citation below uses id 0001
    // (different) → useCanViewPdf cerca doc 0001 nei kbDocs returned (053edd)
    // → not found → canView=false → CitationOwnershipUpsell mostrato.
    // Same single mock satisfies both: GameAiChatTab gate (kbDocs > 0) + non-owner state.
    await mockHasIndexedDocument(page, gameId);
    await mockQaStreamV2(page, {
      tokens: ['Risposta con citazione.'],
      citations: [
        {
          documentId: '00000000-0000-4000-8000-000000000001',
          pageNumber: 12,
          snippet: 'Citazione mock',
          relevanceScore: 0.95,
          copyrightTier: 'full',
        },
      ],
      confidence: 0.9,
    });

    await page.goto(`/library/${gameId}?tab=aiChat`, { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('[data-testid="message-input"]', { timeout: 30_000 });

    const input = page.locator('[data-testid="message-input"]');
    await input.fill('test citazione');
    await page.locator('[data-testid="send-message-button"]').click();

    const chip = page.locator('[data-slot="citation-chip"]').first();
    await expect(chip).toBeVisible({ timeout: 10_000 });
    await chip.click();

    const modal = page.locator('[data-slot="citation-modal"]');
    await expect(modal).toBeVisible();

    const pdfTab = modal.locator('[role="tab"]', { hasText: /pdf/i });
    await pdfTab.click();

    const upsell = modal.locator('[data-slot="citation-ownership-upsell"]');
    await expect(upsell).toBeVisible();

    const cta = upsell.locator('a', { hasText: /carica.*pdf/i });
    const href = await cta.getAttribute('href');
    expect(href).toContain(`gameId=${gameId}`);

    await expect(modal.locator('button', { hasText: /download|scarica/i })).toHaveCount(0);
  });
});
