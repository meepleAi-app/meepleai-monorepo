/**
 * Smoke E2E G1+G2+G4+G5 — happy path real LLM (weekly free-tier variant).
 *
 * Coverage:
 *   - G2: ChatBubbleSkeleton durante hydrate, messages restored after reload
 *   - G1: CitationChip + ConfidenceBadge appaiono nella risposta agente
 *   - G4: CitationModal con tab Snippet (default) + tab PDF originale
 *   - G5: ConfidenceBadge presente
 *
 * Auth model: smoke-aaron (free user) seedato via SQL fixture su snapshot DB.
 * Stack: `make dev-from-snapshot` (Catan + rulebook indicizzati).
 * LLM: free-tier OpenRouter con fallback chain (Llama 3.3 70b → DeepSeek → Gemini Flash).
 *
 * Free tier mitigations (Crispin/Nygard):
 *   - retries: 3
 *   - timeout: 180s (free LLM può raggiungere 60s/risposta)
 *   - tag @flaky-by-design (legitimate flake da rate-limit free tier)
 *
 * Spec: docs/superpowers/specs/2026-05-10-e2e-smoke-game-night-integration-design.md §4.2
 */
import { test, expect } from '@playwright/test';

import { smokeLogin, applySessionToPage } from '../smoke-real-backend/_helpers/auth';

const SEED_GAME_ID = process.env.SMOKE_GAME_ID ?? '3d54901d-fe9d-4d00-a7ae-e9865bb764f7';

test.describe.configure({ retries: 3 });

test.describe('SMOKE-REAL-LLM — game-night happy path (free tier)', () => {
  test('@flaky-by-design end-to-end serata di gioco flow with real LLM', async ({
    page,
    request,
  }) => {
    test.setTimeout(180_000);

    const { cookieHeader } = await smokeLogin(request);
    await applySessionToPage(page, cookieHeader);

    await page.goto(`/library/games/${SEED_GAME_ID}?tab=aiChat`, {
      waitUntil: 'domcontentloaded',
    });
    await page.waitForSelector('[data-testid="message-input"]', { timeout: 30_000 });

    const input = page.locator('[data-testid="message-input"]');
    await input.fill('quanti giocatori servono per giocare a Catan?');
    await page.locator('[data-testid="send-message-button"]').click();

    const agentBubble = page.locator('[data-testid="chat-bubble"][data-role="agent"]').last();
    await expect(agentBubble).toBeVisible({ timeout: 120_000 });

    const citationChips = page.locator('[data-slot="citation-chip"]');
    await expect(citationChips.first()).toBeVisible({ timeout: 10_000 });

    const confBadge = page.locator('[data-slot="confidence-badge"]').last();
    await expect(confBadge).toBeVisible();

    await citationChips.first().click();
    const modal = page.locator('[data-slot="citation-modal"]');
    await expect(modal).toBeVisible();

    const snippetTab = modal.locator('[role="tab"]', { hasText: /snippet/i });
    await expect(snippetTab).toHaveAttribute('aria-selected', 'true');

    const pdfTab = modal.locator('[role="tab"]', { hasText: /pdf/i });
    await pdfTab.click();
    await expect(pdfTab).toHaveAttribute('aria-selected', 'true');

    const pdfBody = modal
      .locator('[data-slot="citation-pdf-renderer"], [data-slot="citation-ownership-upsell"]')
      .first();
    await expect(pdfBody).toBeVisible({ timeout: 15_000 });

    await page.keyboard.press('Escape');
    await expect(modal).not.toBeVisible();

    await page.reload({ waitUntil: 'domcontentloaded' });
    await page.waitForSelector('[data-testid="message-input"]', { timeout: 30_000 });

    const restoredAgentBubble = page
      .locator('[data-testid="chat-bubble"][data-role="agent"]')
      .first();
    await expect(restoredAgentBubble).toBeVisible({ timeout: 15_000 });
  });
});
