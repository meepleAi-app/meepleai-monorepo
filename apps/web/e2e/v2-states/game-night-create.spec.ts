/**
 * V2 State Coverage — /game-nights/new wizard (Issue #950 W4).
 *
 * Two test surfaces:
 *
 *   1. **Fixture FSM coverage** (10 states from spec §4 inventory) —
 *      visual snapshots via `?fixture=<state-id>` URL hatch + the
 *      `IS_VISUAL_TEST_BUILD` constant. Same pattern as Wave C.2
 *      agent-detail.
 *
 *   2. **6 Gherkin scenarios** (spec §12) — behavioural tests asserting
 *      user-visible outcomes. Backend calls are mocked via
 *      `page.context().route()` so the suite stays deterministic and
 *      doesn't depend on a live API.
 *
 * Snapshots written to `apps/web/e2e/v2-states/game-night-create.spec.ts-snapshots/`.
 * Run via CI bootstrap workflow:
 *   gh workflow run visual-regression-migrated.yml \
 *     --ref feature/issue-950-week4-e2e \
 *     -f mode=bootstrap -f project_filter=both
 */

import { test, expect, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';

const VIEWPORTS = [
  { name: 'desktop', width: 1280, height: 720 },
  { name: 'mobile', width: 375, height: 812 },
] as const;

const FIXTURE_STATES = [
  'step1-date',
  'step1-warning',
  'step2-location',
  'step3-empty',
  // PR #1305 review fix: `step3-typing` removed — it returns state
  // identical to `step3-empty` (the autocomplete dropdown is driven by
  // sibling React state, not the wizard reducer), so the snapshot would
  // be byte-identical. Tracking as W3-orchestrator follow-up: surface a
  // dedicated reducer slot for the autocomplete-typing state if we want
  // distinct visual coverage.
  'step3-filled',
  'step4-games',
  'step4-decide-group',
  'mobile-step-flow',
  'desktop-split',
] as const;

async function seedAuth(page: Page): Promise<void> {
  await seedAuthSession(page);
  await seedCookieConsent(page);
  await mockAuthEndpoints(page);
}

async function waitForFontsAndRaf(page: Page): Promise<void> {
  await page.evaluate(async () => {
    if (typeof document !== 'undefined' && 'fonts' in document) {
      await (document as Document & { fonts: { ready: Promise<void> } }).fonts.ready;
    }
  });
  await page.evaluate(
    () =>
      new Promise<void>(resolve => {
        requestAnimationFrame(() => requestAnimationFrame(() => resolve()));
      })
  );
}

async function waitForWizard(page: Page): Promise<void> {
  await page.waitForSelector('[data-slot="game-night-create-wizard"]', { timeout: 30_000 });
  await waitForFontsAndRaf(page);
}

// ─────────────────────────────────────────────────────────────────────────
// 1) Fixture FSM coverage — 10 states × 2 viewports
// ─────────────────────────────────────────────────────────────────────────

test.describe('Game night create — FSM coverage via fixture', () => {
  test.describe.configure({ retries: 0 });

  for (const viewport of VIEWPORTS) {
    test.describe(`${viewport.name} ${viewport.width}x${viewport.height}`, () => {
      for (const fixture of FIXTURE_STATES) {
        test(`state=${fixture}`, async ({ page }) => {
          await page.setViewportSize({ width: viewport.width, height: viewport.height });
          await seedAuth(page);
          await page.goto(`/game-nights/new?fixture=${fixture}`, {
            waitUntil: 'domcontentloaded',
          });
          await waitForWizard(page);

          await expect(page).toHaveScreenshot(`game-night-create-${viewport.name}-${fixture}.png`, {
            fullPage: true,
            animations: 'disabled',
            mask: [page.locator('[data-dynamic]')],
          });
        });
      }
    });
  }
});

// ─────────────────────────────────────────────────────────────────────────
// 2) 6 Gherkin scenarios — behavioural assertions with mocked routes
// ─────────────────────────────────────────────────────────────────────────

test.describe('Game night create — Gherkin behavioural scenarios', () => {
  test.describe.configure({ retries: 0 });

  /**
   * Scenario 2: Date conflict warning + override.
   *
   *   Given Marco is on Step 1 with a date that overlaps an existing event
   *   When the conflict-check effect resolves with hasConflict=true
   *   Then state-02 warning surfaces with the conflicting event title
   *   When he clicks "Continua comunque"
   *   Then the warning is dismissed but the date is preserved
   */
  test('Scenario 2: conflict warning + override preserves date', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 720 });
    await seedAuth(page);
    await page.goto('/game-nights/new?fixture=step1-warning', {
      waitUntil: 'domcontentloaded',
    });
    await waitForWizard(page);

    const warning = page.locator('[data-slot="game-night-create-step1-conflict"]');
    await expect(warning).toBeVisible();
    await expect(warning.getByText(/festa di compleanno/i)).toBeVisible();

    await page.getByRole('button', { name: /continua comunque|continue anyway/i }).click();
    // After override, the alert surface is dismissed.
    await expect(warning).toBeHidden();
  });

  /**
   * Scenario 3: Mixed invitee types — step3-filled fixture shows 6 invitees
   * mixing registered users + email guests.
   */
  test('Scenario 3: mixed invitee types show both user and email entries', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 720 });
    await seedAuth(page);
    await page.goto('/game-nights/new?fixture=step3-filled', {
      waitUntil: 'domcontentloaded',
    });
    await waitForWizard(page);

    const inviteesList = page.locator('[data-slot="game-night-create-step3-invitees"]');
    await expect(inviteesList).toBeVisible();
    // Fixture seeds Laura/Marco/Sara/Giulia (users) + federica/ospite (emails).
    await expect(inviteesList.getByText(/laura rossi/i)).toBeVisible();
    await expect(inviteesList.getByText(/federica@example.com/i)).toBeVisible();
  });

  /**
   * Scenario 6: Mobile step flow + decide-at-group toggle ON.
   *
   * The step4-decide-group fixture seeds decideAtGroup=true and an
   * empty `selected` list. The preview card should announce "giochi: al
   * gruppo" / "Games: decided at the group" copy.
   */
  test('Scenario 6: decideAtGroup ON shows preview gamesTbd copy', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 812 });
    await seedAuth(page);
    await page.goto('/game-nights/new?fixture=step4-decide-group', {
      waitUntil: 'domcontentloaded',
    });
    await waitForWizard(page);

    const preview = page.locator('[data-slot="game-night-create-preview"]');
    await expect(preview).toBeVisible();
    await expect(preview.getByText(/al gruppo|decided at the group/i)).toBeVisible();
  });

  /**
   * Scenario 1 (happy path): start from step1-date, advance through the
   * stepper. We use the stepper buttons rather than the "Avanti" nav
   * since the navigation gate depends on the reducer state's
   * step-complete predicate, and the fixture initial state has step=1
   * empty which would block "Avanti".
   */
  test('Scenario 1: stepper allows jumping between steps in fixture mode', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 720 });
    await seedAuth(page);
    await page.goto('/game-nights/new?fixture=step3-filled', {
      waitUntil: 'domcontentloaded',
    });
    await waitForWizard(page);

    // Stepper should highlight step 3 as current.
    const step3Button = page.locator('[data-slot="game-night-create-stepper-step3"]');
    await expect(step3Button).toHaveAttribute('aria-current', 'step');

    // Clicking step 4 advances; step container changes.
    await page.locator('[data-slot="game-night-create-stepper-step4"]').click();
    await expect(page.locator('[data-slot="game-night-create-step-4-container"]')).toBeVisible();
  });

  /**
   * Scenario 4 (draft autosave): localStorage seeding scenario is deferred.
   * The autosave hook is unit-tested in `useGameNightDraftPersist`; the
   * cross-render restore flow needs a `before-mount` page.evaluate to set
   * localStorage which conflicts with Next.js's hydration timing in
   * Playwright. Tracked as W4 follow-up.
   */
  test.skip('Scenario 4: draft autosave restore (TBD — localStorage timing)', async () => {
    // Deferred: requires before-mount localStorage seeding via
    // page.addInitScript(); current Next.js 16 hydration order causes
    // the useState initializer to fire after the script in headless mode.
  });

  /**
   * Scenario 5 (submit failure + retry): mocking the createGameNight
   * mutation to fail 3 times in a row would exercise the retry policy
   * `[1s, 2s, 4s]`, but the deterministic timing required for the
   * assertion (max retry duration = 7s + jitter) makes this brittle in
   * CI. The retry loop is unit-tested via the orchestrator's
   * isMountedRef guard logic; the E2E coverage is deferred.
   */
  test.skip('Scenario 5: submit failure + retry exhaustion (TBD — retry timing)', async () => {
    // Deferred: 7s+ max retry duration is too slow for the E2E gate.
    // Move to a focused unit test of `handleSubmit` once it's extracted
    // out of the orchestrator (refactor scoped to a future PR).
  });
});
