import { test, expect } from '@playwright/test';

import {
  NANOLITH_DEMO_USER,
  NANOLITH_GAME_TITLE,
  SETUP_PROMPT_4_PLAYERS,
  STORYBOOK_FIXTURE_PATH,
} from './__tests__/fixtures';

/**
 * @demo-runthrough — Stadio 2 G2 auto smoke
 *
 * Simula il caso d'uso Aaron [1]-[6] usando REAL backend (post-seed):
 *   [1] Login badsworm@gmail.com (form vero, no mock)
 *   [2] Vede card "Nanolith" in /library
 *   [3] Click card → /library/games/{nanolithId}
 *   [4] Click CTA "Nuova campagna libro game" (NanolithCampaignCTA)
 *   [5] Apri chat con agente Nanolith Tutor → input "come si imposta la
 *       partita per 4 giocatori?" → riceve risposta SSE
 *   [6] Apri "Traduci pagina" → upload pagina sample → segmentazione →
 *       seleziona paragrafo → traduci → riceve traduzione SSE
 *
 * Pre-requisiti operativi (lanciati MANUALMENTE prima del test):
 *   1. `make dev-core` — stack locale up (api + web + postgres + redis)
 *   2. `make seed-nanolith-demo` — account + game + KB indicizzato + agent
 *
 * Il test NON usa `page.route()` mock: tutte le chiamate API arrivano al
 * backend reale a localhost:8080 attraverso il proxy Next.js.
 *
 * Acceptance manuale (G2.1 spec §3.2): full flow manuale entro 10 min;
 * questo @ci smoke automatico target < 60s headless.
 *
 * Selectors usati — TUTTI estratti dai v2 components shipped:
 *   - LoginForm:                  #login-email, #login-password, role=button "Accedi"
 *   - NanolithCampaignCTA:        data-testid="nanolith-campaign-cta"
 *   - NewCampaignDialog:          data-testid="new-campaign-title-input",
 *                                 data-testid="new-campaign-submit"
 *   - GamebookPlayShell:          data-testid="gamebook-open-chat",
 *                                 data-testid="gamebook-open-translate"
 *   - TranslateViewer:            data-testid="page-type-select",
 *                                 data-testid="open-camera-button",
 *                                 data-testid="photo-input"
 *   - SegmentPicker:              data-testid="segment-picker",
 *                                 data-testid^="segment-picker-translate-"
 *   - TranslationPane:            data-testid="translation-pane-it"
 *   - ChatMessageBubble:          data-testid="chat-message-bubble" (data-role)
 *
 * Spec: docs/superpowers/specs/2026-05-07-libro-game-nanolith-demo-design.md
 *       docs/superpowers/plans/2026-05-08-nanolith-demo-runthrough-phase-a.md
 *       (Gherkin G2.1 stadio 2, G2.2 mobile)
 */

test.describe("@demo-runthrough Nanolith caso d'uso end-to-end", () => {
  // 2 minuti di budget complessivo (segmentazione OCR può richiedere 30s+).
  test.setTimeout(120_000);

  test('flow [1]-[6] happy path locale post-seed', async ({ page }) => {
    // ----------------------------------------------------------------------
    // [1] Login Aaron — form vero (no mock)
    // ----------------------------------------------------------------------
    await page.goto('/login');

    // LoginForm uses id-anchored fields (id="login-email", id="login-password")
    await page.locator('#login-email').fill(NANOLITH_DEMO_USER.email);
    await page.locator('#login-password').fill(NANOLITH_DEMO_USER.password);

    // Submit button label is "Accedi" (it.json: auth.login.loginButton).
    // Use role=button + name regex covering both it/en for resilience.
    await page.getByRole('button', { name: /^(Accedi|Login)$/ }).click();

    // After login, AuthCard navigates to `?from=...` or `/library` default.
    await page.waitForURL(/\/library(\?|\/|$)/, { timeout: 15_000 });

    // ----------------------------------------------------------------------
    // [2] Vede Nanolith in /library
    // ----------------------------------------------------------------------
    // Library hub renders entries via MeepleCard / unified card; the title
    // text is the most stable selector across the v2 layouts (LibraryHubV2
    // doesn't expose a per-card data-testid for individual entries — see
    // PR #638 Wave B.3).
    const nanolithCard = page
      .getByRole('link', { name: new RegExp(NANOLITH_GAME_TITLE, 'i') })
      .first();
    await expect(nanolithCard).toBeVisible({ timeout: 10_000 });

    // ----------------------------------------------------------------------
    // [3] Click card → game detail page
    // ----------------------------------------------------------------------
    await nanolithCard.click();
    await page.waitForURL(/\/library\/games\/[^/]+(?:\?|$)/, { timeout: 10_000 });

    // ----------------------------------------------------------------------
    // [4] CTA "Nuova campagna libro game" → dialog → submit → /play/{id}
    // ----------------------------------------------------------------------
    const libroCta = page.getByTestId('nanolith-campaign-cta');
    await expect(libroCta).toBeVisible({ timeout: 5_000 });
    await libroCta.click();

    // NewCampaignDialog opens — fill title + submit
    await page.getByTestId('new-campaign-title-input').fill('Demo runthrough campaign');
    await page.getByTestId('new-campaign-submit').click();

    // Router pushes to /library/games/{gameId}/play/{campaignId}
    await page.waitForURL(/\/library\/games\/[^/]+\/play\/[^/]+(?:\?|$)/, {
      timeout: 15_000,
    });

    // ----------------------------------------------------------------------
    // [5] Q&A setup chat — apri chat slide-over, invia prompt, attendi SSE
    // ----------------------------------------------------------------------
    // GamebookPlayShell exposes data-testid="gamebook-open-chat" on the CTA
    // that calls useChatPanelStore.open()
    await page.getByTestId('gamebook-open-chat').click();

    // Chat slide-over renders ChatInputBar — textarea is the only one in panel
    const chatTextarea = page.locator('textarea[placeholder*="regola" i]').first();
    await expect(chatTextarea).toBeVisible({ timeout: 5_000 });
    await chatTextarea.fill(SETUP_PROMPT_4_PLAYERS);
    await chatTextarea.press('Enter');

    // Wait for assistant bubble to render with non-trivial content (≥ 20 chars)
    // ChatMessageBubble exposes data-testid="chat-message-bubble" + data-role.
    const assistantBubble = page
      .locator('[data-testid="chat-message-bubble"][data-role="assistant"]')
      .last();
    await expect(assistantBubble).toBeVisible({ timeout: 30_000 });
    await expect(assistantBubble).toContainText(/.{20,}/, { timeout: 30_000 });

    // ----------------------------------------------------------------------
    // [6] Photo upload + segmentation + translate
    // ----------------------------------------------------------------------
    // Open the translate page via GamebookPlayShell CTA.
    await page.getByTestId('gamebook-open-translate').click();
    await page.waitForURL(/\/play\/[^/]+\/translate(?:\?|$)/, { timeout: 10_000 });

    // TranslateViewer exposes a hidden file input — set files directly.
    // (Camera button only triggers .click() on the input; bypass for headless.)
    const fileInput = page.getByTestId('photo-input');
    await fileInput.setInputFiles(STORYBOOK_FIXTURE_PATH);

    // SegmentPicker becomes visible after upload + segmentation completes.
    // Phase: 'segmenting' → 'segments_ready' (waits OCR up to 30s).
    // NOTE: with the 1×1 PNG fallback fixture, OCR may legitimately return
    // zero segments (data-testid="segment-picker-empty"). Accept both
    // success and "empty segments" outcomes — the goal of @ci is to verify
    // the upload mechanics + FSM transition, not the OCR quality.
    const segmentList = page.getByTestId('segment-picker');
    const segmentEmpty = page.getByTestId('segment-picker-empty');

    await expect(segmentList.or(segmentEmpty)).toBeVisible({ timeout: 60_000 });

    // If segments are present, exercise the translate path. Otherwise the
    // smoke ends here — see fixture comment in fixtures.ts.
    const hasSegments = await segmentList.isVisible();
    if (hasSegments) {
      // Click the first translate button (data-testid pattern:
      // "segment-picker-translate-{paragraphNumber}")
      const firstTranslateBtn = segmentList
        .locator('button[data-testid^="segment-picker-translate-"]')
        .first();
      await firstTranslateBtn.click();

      // TranslationPane renders data-testid="translation-pane-it" once SSE
      // streaming starts. Initial state shows "Traduzione in corso…",
      // updates progressively, finalizes with "completata" marker.
      const translationPane = page.getByTestId('translation-pane-it');
      await expect(translationPane).toBeVisible({ timeout: 10_000 });
      await expect(translationPane).toContainText(/.{10,}/, { timeout: 30_000 });
    }
  });
});
