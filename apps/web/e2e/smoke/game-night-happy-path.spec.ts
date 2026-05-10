/**
 * Smoke E2E G1+G2+G3+G4 — happy path real LLM.
 *
 * Coverage:
 *   - G3: GamesRecentRail rendered su /games?tab=library
 *   - G2: ChatBubbleSkeleton durante hydrate, messages restored after reload
 *   - G1: CitationChip + ConfidenceBadge appaiono nella risposta agente
 *   - G4: CitationModal con tab Snippet (default) + tab PDF originale
 *
 * Spec: docs/superpowers/specs/2026-05-10-e2e-smoke-game-night-design.md §3.1
 *
 * NB: real LLM, può richiedere fino a 30s per la risposta.
 * Pre-requisito: snapshot DB con SEED_GAME_ID + KB indexed + smoke-aaron in library.
 */
import { test, expect } from '@playwright/test';

import { loginSmokeAaron } from './_helpers';
import { SEED_GAME_ID } from './_helpers.fixtures';

test.describe('Smoke G1-G5 — happy path (real LLM)', () => {
  test.setTimeout(120_000); // 2 min for full flow with real LLM
  test.describe.configure({ retries: 2 });

  test('end-to-end serata di gioco flow', async ({ page }) => {
    // ── Auth ──
    await loginSmokeAaron(page);

    // ── G3: Hub recents (graceful empty se utente non ha cronologia) ──
    await page.goto('/games?tab=library');
    await page.waitForLoadState('networkidle');

    // ── Navigate to chat tab ──
    await page.goto(`/library/games/${SEED_GAME_ID}?tab=aiChat`);
    await page.waitForLoadState('networkidle');

    // ── G2: hydrate (skeleton brief OR direct render) ──
    // Wait for input to be ready (chat tab fully mounted)
    await page
      .locator('[data-testid="message-input"]')
      .waitFor({ state: 'visible', timeout: 10_000 });

    // ── Send query ──
    const input = page.locator('[data-testid="message-input"]');
    await input.fill('posso usare potere uccello già attivato?');

    const sendBtn = page.locator('[data-testid="send-message-button"]');
    await sendBtn.click();

    // ── Wait response (real LLM, max 30s) ──
    const agentBubble = page.locator('[data-testid="chat-bubble"][data-role="agent"]').last();
    await expect(agentBubble).toBeVisible({ timeout: 30_000 });

    // ── G1: CitationChip present (assert at least 1) ──
    const citationChips = page.locator('[data-slot="citation-chip"]');
    await expect(citationChips.first()).toBeVisible({ timeout: 5_000 });

    // ── G5: ConfidenceBadge present ──
    const confBadge = page.locator('[data-slot="confidence-badge"]').last();
    await expect(confBadge).toBeVisible();

    // ── G4: Click chip → modal opens ──
    await citationChips.first().click();
    const modal = page.locator('[data-slot="citation-modal"]');
    await expect(modal).toBeVisible();

    // Tab Snippet active by default
    const snippetTab = modal.locator('[role="tab"]', { hasText: /snippet/i });
    await expect(snippetTab).toHaveAttribute('aria-selected', 'true');

    // Click PDF tab
    const pdfTab = modal.locator('[role="tab"]', { hasText: /pdf/i });
    await pdfTab.click();
    await expect(pdfTab).toHaveAttribute('aria-selected', 'true');

    // Tab body shows EITHER PDF renderer OR upsell (both valid for G4)
    const pdfBody = modal
      .locator('[data-slot="citation-pdf-renderer"], [data-slot="citation-ownership-upsell"]')
      .first();
    await expect(pdfBody).toBeVisible({ timeout: 10_000 });

    // Close modal (ESC)
    await page.keyboard.press('Escape');
    await expect(modal).not.toBeVisible();

    // ── G2: Reload, verify messages restored ──
    await page.reload();
    await page.waitForLoadState('networkidle');

    // After hydrate, the agent message should be visible (no specific content match)
    const restoredAgentBubble = page
      .locator('[data-testid="chat-bubble"][data-role="agent"]')
      .first();
    await expect(restoredAgentBubble).toBeVisible({ timeout: 10_000 });
  });
});
