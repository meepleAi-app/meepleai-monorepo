import { test, expect, type Page } from '@playwright/test';

import { ANNA_PERSONA, buildAnnaInitialState } from './_helpers/annaPersona';
import { assertExactUrl } from './_helpers/dataAssertionUtils';
import { withRetry } from './_helpers/resilienceWrappers';
import { mockAuthEndpoints, seedAuthSession } from './_helpers/seedAuthSession';
import { seedCookieConsent } from './_helpers/seedCookieConsent';
import { cleanupTestEntities, newTestRunId, seedLibraryGame } from './_helpers/seedEntities';

/**
 * Cross-Asse Journey #2 — Empty CTA → wizard 4-step → publish → live opt-in flow.
 *
 * Anna (host) lands on /dashboard with **0 GameNights** + **1 library game**
 * (DEC-C-8 Real BE seedLibraryGame factory shipped Macro 3a). She clicks the
 * Prossimi empty-state CTA to navigate /game-nights/new, fills the wizard
 * 4-step (Quando/Dove/Chi/Cosa), submits → redirects /game-nights/{newId} in
 * Draft state, publishes → "Aggiungi partita" → GamePickerDialog opens with
 * seeded library game visible.
 *
 * **Initial state** (DEC-C-1 journey2):
 *   - 0 GameNight
 *   - 0 player roster
 *   - 1 library game (Catan E2E Test) seeded via Real BE factory
 *
 * **testid contract**:
 *   - Prossimi empty: `data-testid="prossimi-empty"` (ProssimiSection.tsx:73)
 *   - Wizard title input: `data-slot="game-night-create-title-input"` (_content.tsx:342)
 *   - Wizard stepper steps: `data-slot="game-night-create-stepper-stepN"` (GameNightCreateWizard:159)
 *   - Wizard nav next/submit: `data-slot="game-night-create-nav-next"` (GameNightCreateWizard:251)
 *   - Step 2 location kind: `data-slot="game-night-create-step2-kind-{kind}"` (GameNightLocationToggle:86)
 *   - Step 4 library card: `data-slot="game-night-create-step4-game-{id}"` (GameCandidatesPicker:113)
 *   - Publish button: `data-testid="publish-game-night"` (GameNightDetailView:294, T3b.0 ADDITIVE)
 *   - "Aggiungi partita" button: `data-testid="game-night-add-partita"` (GameNightActions:72, T3b.0 ADDITIVE)
 *   - GamePickerDialog: `data-testid="game-picker-dialog"` (GamePickerDialog:99)
 *   - GamePicker game items: `data-testid="game-picker-item-{gameId}"` (GamePickerItem:84)
 *
 * **Spec ref**: DEC-C-1 + DEC-C-2 + DEC-C-6 + DEC-C-7 Journey #2 matrix
 *               + DEC-C-8 Real BE seedLibraryGame factory (Macro 3a `789b3a301`)
 *               + DEC-C-9 Full live opt-in flow (publish + GamePickerDialog).
 *
 * **Architectural scope limit** (P177 pragmatic-deferral-with-mitigation):
 *   Real BE seedLibraryGame creates a SharedGame + UserLibraryEntry but does
 *   NOT seed PDFs / vector index. Consequently `GET /api/v1/games/{gameId}/kb-readiness`
 *   returns `isReady: false`, which disables the `GamePickerItem` button. This
 *   spec MOCKS the kb-readiness endpoint with `isReady: true` to allow click-
 *   through (targeted deviation from DEC-C-8 Real BE for THIS specific probe
 *   only — `seedLibraryGame` remains real BE).
 *
 *   Reaching `/sessions/{id}/live` requires extensive additional mocks for
 *   session detail + SSE streams + live state wire. Per DEC-C-9 "live opt-in
 *   flow", this spec verifies the WIRE INITIATES (URL transitions toward
 *   `/sessions/{id}`) but does NOT verify the live session view fully renders.
 *   Full /live render is tracked as follow-up.
 *
 * **CI gate**: non-blocking on main-dev (DEC-C-4). Blocking on main-staging.
 */
test.describe('Cross-Asse Journey #2 — Empty CTA → wizard 4-step → live opt-in', () => {
  test.skip(({ browserName }) => browserName !== 'chromium', 'Chromium-only for speed');

  let testRunId: string;
  let libraryGameId: string;

  test.beforeEach(async ({ page }, testInfo) => {
    testRunId = newTestRunId(testInfo.testId);

    // ① FE auth seeding — Anna as authenticated user
    await seedCookieConsent(page);
    await seedAuthSession(page, { role: ANNA_PERSONA.role });
    await mockAuthEndpoints(page, {
      role: ANNA_PERSONA.role,
      userId: ANNA_PERSONA.userId,
      email: ANNA_PERSONA.email,
      onboardingCompleted: ANNA_PERSONA.onboardingCompleted,
    });

    // ② BE entity seeding — journey2 initial state via DEC-C-8 Real BE factory
    const initial = buildAnnaInitialState('journey2');
    expect(initial.gameNightCount).toBe(0);
    expect(initial.libraryGameCount).toBe(1);

    const libGame = await withRetry(
      () =>
        seedLibraryGame(page, {
          testRunId,
          ownerEmail: ANNA_PERSONA.email,
          title: 'Catan E2E Test',
          publisher: 'KOSMOS E2E',
          minPlayers: 3,
          maxPlayers: 4,
        }),
      { reason: 'seedLibraryGame journey2 beforeEach' }
    );
    libraryGameId = libGame.gameId;
  });

  test.afterEach(async ({ page }) => {
    if (testRunId) {
      await cleanupTestEntities(page, { testRunId });
    }
  });

  // ── T3b.1: Happy path — empty CTA → wizard mount ─────────────────────────
  test('empty dashboard CTA navigates → wizard step 1 mount', async ({ page }) => {
    // ─── Step 1: Navigate to /dashboard
    await page.goto('/dashboard');
    await expect(page).not.toHaveURL(/\/(login|auth|sign-in)/);

    // ─── Step 2: Prossimi empty state visible (journey2 has 0 GN)
    const empty = page.locator('[data-testid="prossimi-empty"]');
    await expect(empty).toBeVisible({ timeout: 10_000 });

    // ─── Step 3: Click CTA → navigate /game-nights/new
    // EmptySection renders the action as a button OR a link wrapping a button.
    // Use role=link to disambiguate from the parent button stack.
    const cta = empty.getByRole('link').first();
    await expect(cta).toBeVisible({ timeout: 5_000 });
    await cta.click();

    // ─── Step 4: Wizard mounted on /game-nights/new (strict URL)
    await page.waitForURL(/\/game-nights\/new/, { timeout: 10_000 });
    assertExactUrl(page.url(), /\/game-nights\/new(\?.*)?$/);

    // Wizard skeleton visible
    await expect(page.locator('[data-slot="game-night-create-wizard"]')).toBeVisible({
      timeout: 10_000,
    });

    // Stepper visible with step 1 active
    const step1 = page.locator('[data-slot="game-night-create-stepper-step1"]');
    await expect(step1).toBeVisible({ timeout: 5_000 });
    await expect(step1).toHaveAttribute('aria-current', 'step');
  });

  // ── T3b.2-3b.5: Wizard 4-step fill + submit + redirect ────────────────────
  test('fills wizard 4-step + submits → redirects /game-nights/{newId}', async ({ page }) => {
    await page.goto('/game-nights/new');
    await expect(page.locator('[data-slot="game-night-create-wizard"]')).toBeVisible({
      timeout: 10_000,
    });

    // ─── Title (above wizard, persisted into payload)
    await page
      .locator('[data-slot="game-night-create-title-input"]')
      .fill('Anna E2E Test GameNight');

    // ─── Step 1 (Quando): fill date input → at least min hours ahead
    // SCHEDULED_AT_MIN_HOURS_AHEAD is enforced by step1DateSchema.
    // Tomorrow at 20:00 is safely > 1 hour ahead.
    const tomorrow = new Date();
    tomorrow.setDate(tomorrow.getDate() + 1);
    tomorrow.setHours(20, 0, 0, 0);
    // datetime-local accepts 'YYYY-MM-DDTHH:mm' (16 chars).
    const localValue = formatDateTimeLocal(tomorrow);

    const dateInput = page.locator('input[type="datetime-local"]').first();
    await dateInput.fill(localValue);

    // Wait a tick so conflict check debounce settles + reducer dispatches.
    await page.waitForTimeout(700);

    // Wait for Next button to become enabled (canAdvance gated by step1 schema).
    const navNext = page.locator('[data-slot="game-night-create-nav-next"]');
    await expect(navNext).toBeEnabled({ timeout: 5_000 });

    await withRetry(() => navNext.click(), {
      reason: 'wizard step 1 → next',
    });

    // ─── Step 2 (Dove): select 'home' radio (initial default but click to confirm intent)
    const step2Container = page.locator('[data-slot="game-night-create-step2"]');
    await expect(step2Container).toBeVisible({ timeout: 5_000 });

    const homeBtn = page.locator('[data-slot="game-night-create-step2-kind-home"]');
    await expect(homeBtn).toBeVisible({ timeout: 5_000 });
    await homeBtn.click();

    // Click Next → step 3
    await expect(navNext).toBeEnabled();
    await withRetry(() => navNext.click(), {
      reason: 'wizard step 2 → next',
    });

    // ─── Step 3 (Chi): skip with no invitees (allowed by step3InviteesSchema)
    const step3Container = page.locator('[data-slot="game-night-create-step3"]');
    await expect(step3Container).toBeVisible({ timeout: 5_000 });

    await expect(navNext).toBeEnabled();
    await withRetry(() => navNext.click(), {
      reason: 'wizard step 3 → next (no invitees)',
    });

    // ─── Step 4 (Cosa): select seeded library game
    const step4Container = page.locator('[data-slot="game-night-create-step4"]');
    await expect(step4Container).toBeVisible({ timeout: 10_000 });

    // The library list mounts after `useLibrary` query resolves. Wait for
    // the seeded card to appear (testRunId-scoped Catan E2E Test).
    const libraryCard = page.locator(`[data-slot="game-night-create-step4-game-${libraryGameId}"]`);
    await expect(libraryCard).toBeVisible({ timeout: 15_000 });
    await libraryCard.click();

    // Verify card now marked as selected (aria-pressed=true)
    await expect(libraryCard).toHaveAttribute('aria-pressed', 'true');

    // ─── Submit: nav button now labelled "Crea evento" (i18n submit key)
    await expect(navNext).toBeEnabled();
    await withRetry(() => navNext.click(), {
      reason: 'wizard step 4 → submit',
    });

    // ─── Wait for redirect to /game-nights/{newId}
    // Submit goes through useCreateGameNight with retry [1s, 2s, 4s] backoff
    // so allow generous timeout for first attempt + first retry.
    await page.waitForURL(/\/game-nights\/[a-f0-9-]+$/, { timeout: 15_000 });

    // ─── Extract newId from URL + verify it's a UUID-ish string
    const url = page.url();
    const match = url.match(/\/game-nights\/([a-f0-9-]+)$/);
    expect(match).toBeTruthy();
    const createdGnId = match![1];
    expect(createdGnId).toMatch(/^[0-9a-f-]{36}$/i);
  });

  // ── T3b.6: Publish GN + "Aggiungi partita" → GamePickerDialog opens ─────
  test('publishes GN + clicks "Aggiungi partita" → GamePickerDialog opens', async ({ page }) => {
    // Reuse the wizard flow to create a Draft GN, then publish + open dialog.
    const createdGnId = await runWizardAndCreateDraftGn(page, libraryGameId);
    expect(createdGnId).toBeTruthy();

    // ─── On /game-nights/{id} as Draft → host publish button visible
    // Status transition: Draft → Published is host-only.
    const publishBtn = page.locator('[data-testid="publish-game-night"]');
    await expect(publishBtn).toBeVisible({ timeout: 10_000 });

    // Click publish → BE mutation usePublishGameNight → status transitions
    await withRetry(() => publishBtn.click(), {
      reason: 'click publish-game-night',
    });

    // ─── After publish: GameNightActions renders → "Aggiungi partita" visible
    // Detection: data-testid="game-night-add-partita" only mounts when
    // event.status === 'Published'.
    const addPartitaBtn = page.locator('[data-testid="game-night-add-partita"]');
    await expect(addPartitaBtn).toBeVisible({ timeout: 10_000 });

    // Click → GamePickerDialog opens
    await addPartitaBtn.click();

    // GamePickerDialog visible (testid existed pre-spec)
    const dialog = page.locator('[data-testid="game-picker-dialog"]');
    await expect(dialog).toBeVisible({ timeout: 5_000 });

    // Search input present (sanity check the dialog is fully mounted)
    const search = page.locator('[data-testid="game-picker-search"]');
    await expect(search).toBeVisible({ timeout: 2_000 });

    // List container present
    const list = page.locator('[data-testid="game-picker-list"]');
    await expect(list).toBeVisible({ timeout: 2_000 });
  });

  // ── T3b.7: Select game in dialog + initiate session navigate ────────────
  // Scope limit (P177): asserts that startSession initiates navigation away
  // from the game-night detail page toward /sessions/{id}. Does NOT verify
  // /live page renders (requires deeper session detail + SSE mocks beyond
  // Macro 3b scope — tracked follow-up).
  test('selects game in dialog + clicks → URL initiates navigation away from /game-nights/{id}', async ({
    page,
  }) => {
    // ─── Mock KB readiness endpoint — seedLibraryGame doesn't seed PDFs so
    // the real BE returns isReady=false, which disables the picker button.
    // This is a TARGETED deviation from DEC-C-8 Real BE for THIS probe only.
    await page.context().route(/\/api\/v1\/games\/[^/]+\/kb-readiness/, async route => {
      if (route.request().method() !== 'GET') {
        await route.continue();
        return;
      }
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          isReady: true,
          state: 'Ready',
          readyPdfCount: 1,
          failedPdfCount: 0,
          warnings: [],
        }),
      });
    });

    // ─── Run full wizard + publish + open dialog
    const createdGnId = await runWizardAndCreateDraftGn(page, libraryGameId);

    const publishBtn = page.locator('[data-testid="publish-game-night"]');
    await expect(publishBtn).toBeVisible({ timeout: 10_000 });
    await withRetry(() => publishBtn.click(), { reason: 'click publish-game-night' });

    const addPartitaBtn = page.locator('[data-testid="game-night-add-partita"]');
    await expect(addPartitaBtn).toBeVisible({ timeout: 10_000 });
    await addPartitaBtn.click();

    const dialog = page.locator('[data-testid="game-picker-dialog"]');
    await expect(dialog).toBeVisible({ timeout: 5_000 });

    // ─── Wait for KB readiness probe to resolve (button becomes enabled)
    const gameItem = page.locator(`[data-testid="game-picker-item-${libraryGameId}"]`);
    await expect(gameItem).toBeVisible({ timeout: 10_000 });
    await expect(gameItem).toBeEnabled({ timeout: 10_000 });

    // Click game → triggers startSession → router.push('/sessions/{id}')
    await withRetry(() => gameItem.click(), { reason: 'click game-picker-item' });

    // ─── Assertion: URL transitions AWAY from /game-nights/{id}.
    // We don't strictly assert the final URL is /sessions/{id} or /sessions/{id}/live
    // because that depends on session detail mocks beyond scope.
    // The wire intent: navigation initiates.
    await page.waitForURL(
      url => !url.toString().match(new RegExp(`/game-nights/${createdGnId}$`)),
      {
        timeout: 10_000,
      }
    );

    // Verify we're no longer on the GN detail page
    expect(page.url()).not.toMatch(new RegExp(`/game-nights/${createdGnId}$`));
  });
});

// ============================================================================
// Helpers
// ============================================================================

/**
 * datetime-local input value format: 'YYYY-MM-DDTHH:mm' (16 chars).
 * Browser .toISOString() includes seconds + Z which datetime-local rejects.
 */
function formatDateTimeLocal(d: Date): string {
  const pad = (n: number): string => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/**
 * Runs the wizard 4-step flow end-to-end. Returns the created GN id from URL.
 * Extracted for reuse across T3b.6 + T3b.7 tests.
 */
async function runWizardAndCreateDraftGn(page: Page, libraryGameId: string): Promise<string> {
  await page.goto('/game-nights/new');
  await expect(page.locator('[data-slot="game-night-create-wizard"]')).toBeVisible({
    timeout: 10_000,
  });

  await page.locator('[data-slot="game-night-create-title-input"]').fill('Anna E2E Test GameNight');

  // Step 1: date
  const tomorrow = new Date();
  tomorrow.setDate(tomorrow.getDate() + 1);
  tomorrow.setHours(20, 0, 0, 0);
  await page.locator('input[type="datetime-local"]').first().fill(formatDateTimeLocal(tomorrow));
  await page.waitForTimeout(700);

  const navNext = page.locator('[data-slot="game-night-create-nav-next"]');
  await expect(navNext).toBeEnabled({ timeout: 5_000 });
  await withRetry(() => navNext.click(), { reason: 'wizard step 1 → next' });

  // Step 2: location 'home'
  await expect(page.locator('[data-slot="game-night-create-step2"]')).toBeVisible({
    timeout: 5_000,
  });
  await page.locator('[data-slot="game-night-create-step2-kind-home"]').click();
  await expect(navNext).toBeEnabled();
  await withRetry(() => navNext.click(), { reason: 'wizard step 2 → next' });

  // Step 3: skip
  await expect(page.locator('[data-slot="game-night-create-step3"]')).toBeVisible({
    timeout: 5_000,
  });
  await expect(navNext).toBeEnabled();
  await withRetry(() => navNext.click(), { reason: 'wizard step 3 → next' });

  // Step 4: select seeded library game
  await expect(page.locator('[data-slot="game-night-create-step4"]')).toBeVisible({
    timeout: 10_000,
  });
  const libraryCard = page.locator(`[data-slot="game-night-create-step4-game-${libraryGameId}"]`);
  await expect(libraryCard).toBeVisible({ timeout: 15_000 });
  await libraryCard.click();
  await expect(libraryCard).toHaveAttribute('aria-pressed', 'true');

  // Submit
  await expect(navNext).toBeEnabled();
  await withRetry(() => navNext.click(), { reason: 'wizard step 4 → submit' });

  // Wait for redirect
  await page.waitForURL(/\/game-nights\/[a-f0-9-]+$/, { timeout: 15_000 });
  const url = page.url();
  const match = url.match(/\/game-nights\/([a-f0-9-]+)$/);
  if (!match) {
    throw new Error(`runWizardAndCreateDraftGn: URL did not match expected pattern: ${url}`);
  }
  return match[1];
}
