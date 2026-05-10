/**
 * Smoke E2E G5 — low confidence disclaimer.
 *
 * Mock SSE V2: confidence=0.42 < 0.70 threshold → useGameChat deriva isLowQuality=true
 * → LowConfidenceDisclaimer rendered + ConfidenceBadge data-tier="bassa"
 *
 * Spec: docs/superpowers/specs/2026-05-10-e2e-smoke-game-night-design.md §3.2
 */
import { test, expect } from '@playwright/test';

import { loginSmokeAaron, mockNoChatHistory, mockQaStreamV2 } from './_helpers';
import { SEED_DOCUMENT_ID, SEED_GAME_ID } from './_helpers.fixtures';

test.describe('Smoke G5 — low confidence (mocked)', () => {
  test('shows LowConfidenceDisclaimer + bassa badge when confidence < 0.50', async ({ page }) => {
    await loginSmokeAaron(page);
    await mockNoChatHistory(page, SEED_GAME_ID);
    await mockQaStreamV2(page, {
      tokens: ['Non sono certo: ', 'questa è una risposta a confidenza bassa.'],
      citations: [
        {
          documentId: SEED_DOCUMENT_ID,
          pageNumber: 6,
          snippet: 'Edge case mock',
          relevanceScore: 0.5,
          copyrightTier: 'full',
        },
      ],
      confidence: 0.42,
    });

    await page.goto(`/library/games/${SEED_GAME_ID}?tab=aiChat`);
    await page.waitForLoadState('networkidle');

    const input = page.locator('[data-testid="message-input"]');
    await input.fill('edge case query');
    await page.locator('[data-testid="send-message-button"]').click();

    // G5: LowConfidenceDisclaimer rendered
    const disclaimer = page.locator('[data-slot="low-confidence-disclaimer"]');
    await expect(disclaimer).toBeVisible({ timeout: 10_000 });

    // G5: ConfidenceBadge tier=bassa (< 0.50)
    const badge = page.locator('[data-slot="confidence-badge"][data-tier="bassa"]');
    await expect(badge).toBeVisible();

    // G1: citation chip preserved (clickable)
    const chip = page.locator('[data-slot="citation-chip"]').first();
    await expect(chip).toBeVisible();
  });
});
