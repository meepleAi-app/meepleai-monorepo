/**
 * Smoke E2E G5 — low confidence disclaimer (nightly real-backend variant).
 *
 * Mock SSE V2: confidence=0.42 < 0.70 threshold → useGameChat deriva isLowQuality=true
 * → LowConfidenceDisclaimer rendered + ConfidenceBadge data-tier="bassa"
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

test.describe('SMOKE — game-chat low confidence (G5)', () => {
  test('shows LowConfidenceDisclaimer + bassa badge when confidence < 0.50', async ({
    page,
    request,
  }) => {
    const { cookieHeader } = await smokeLogin(request);
    await applySessionToPage(page, cookieHeader);
    const gameId = await seedGameForChat(request, cookieHeader);
    // Mock at least 1 indexed PDF so GameAiChatTab renders GameChatTabV2
    // (and therefore ChatInputBar with data-testid=message-input). Without
    // this the tab shows the "Carica un PDF" placeholder instead.
    await mockHasIndexedDocument(page, gameId);
    await mockNoChatHistory(page, gameId);
    await mockQaStreamV2(page, {
      tokens: ['Non sono certo: ', 'questa è una risposta a confidenza bassa.'],
      citations: [
        {
          documentId: '00000000-0000-4000-8000-000000000001',
          pageNumber: 6,
          snippet: 'Edge case mock',
          relevanceScore: 0.5,
          copyrightTier: 'full',
        },
      ],
      confidence: 0.42,
    });

    await page.goto(`/library/${gameId}?tab=aiChat`, { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('[data-testid="message-input"]', { timeout: 30_000 });

    const input = page.locator('[data-testid="message-input"]');
    await input.fill('edge case query');
    await page.locator('[data-testid="send-message-button"]').click();

    const disclaimer = page.locator('[data-slot="low-confidence-disclaimer"]');
    await expect(disclaimer).toBeVisible({ timeout: 10_000 });

    const badge = page.locator('[data-slot="confidence-badge"][data-tier="bassa"]');
    await expect(badge).toBeVisible();

    const chip = page.locator('[data-slot="citation-chip"]').first();
    await expect(chip).toBeVisible();
  });
});
