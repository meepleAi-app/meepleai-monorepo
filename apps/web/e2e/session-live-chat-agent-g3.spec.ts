/**
 * E2E — #2375 G3 ChatAgent always-visible accordion FSM.
 *
 * Covers 8 scenarios from spec §7.3:
 *   1. Default render: ChatAgent expanded (no ?chat param)
 *   2. Click header → ?chat=collapsed URL written + body hidden
 *   3. Reload preserves collapsed state (URL is SSOT)
 *   4. Click again removes ?chat param (toggle back to expanded)
 *   5. Draft persists across reload (sessionStorage-backed)
 *   6. Send clears draft + input, draft absent after reload
 *   7. data-at-bottom attribute reflects scroll anchor state
 *   8. axe AA — 0 violations on expanded + collapsed states
 *
 * Uses the visual-test fixture (?fixture=host) so SSE is mocked and
 * the page renders deterministically without a running backend.
 * The fixture activates only when STATE_OVERRIDE_ENABLED=true
 * (NODE_ENV !== 'production'), which is always the case in E2E dev mode.
 *
 * Auth bypass: triple helper (seedAuthSession + seedCookieConsent +
 * mockAuthEndpoints) required for `(authenticated)` routes
 * (Wave B.1 lesson #633 — mirrors session-live-mobile.spec.ts pattern).
 */
import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from './_helpers/seedAuthSession';
import { seedCookieConsent } from './_helpers/seedCookieConsent';

// SessionLiveView hardcodes data-theme="dark" on its root div.
// next-themes also applies .dark class from colorScheme preference.
test.use({ colorScheme: 'dark' });

/** Sentinel UUID matching VISUAL_TEST_FIXTURE_SESSION.id. */
const FIXTURE_SESSION_ID = '00000000-0000-4000-8000-000000000d20';

const WCAG_TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] as const;

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

async function seedAuth(page: Page): Promise<void> {
  await seedAuthSession(page);
  await seedCookieConsent(page);
  await mockAuthEndpoints(page);
}

/**
 * Navigate to the session-live page with the host fixture active.
 * `search` is appended verbatim after `?fixture=host` (use `&foo=bar`).
 *
 * Waits for `data-ui-state="default"` — the fixture-loaded state.
 * Without this, the page may be stuck in `loading` waiting for
 * the backend (which is not running during E2E tests).
 * Mirrors the `?fixture=host` pattern in `a11y/session-live.spec.ts:438`.
 */
async function gotoSessionLive(page: Page, search = ''): Promise<void> {
  await seedAuth(page);
  await page.goto(`/sessions/${FIXTURE_SESSION_ID}/live?fixture=host${search}`, {
    waitUntil: 'domcontentloaded',
  });
  // Wait for the fixture to load and render the default shell.
  // data-ui-state="default" is set by SessionLiveView when the fixture
  // resolves — equivalent to the session being live and renderable.
  await page.waitForSelector('[data-slot="session-live-view"][data-ui-state="default"]', {
    timeout: 30_000,
  });
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

test.describe('#2375 G3 ChatAgent accordion + draft + smart scroll', () => {
  // ── 1. Default render ─────────────────────────────────────────────────────

  test('1. default render: ChatAgentPanel expanded (no ?chat param)', async ({ page }) => {
    await gotoSessionLive(page);

    const panel = page.locator('[data-slot="chat-agent-panel"]').first();
    await expect(panel).toBeVisible();

    // data-collapsed must be absent (not set to 'true') when expanded.
    await expect(panel).not.toHaveAttribute('data-collapsed', 'true');

    // Body (LiveAgentChat) is mounted when expanded.
    await expect(panel.locator('[data-slot="live-agent-chat"]')).toBeVisible();
  });

  // ── 2. Click header → collapsed URL + body hidden ─────────────────────────

  test('2. click header → URL becomes ?chat=collapsed, body hidden', async ({ page }) => {
    await gotoSessionLive(page);

    const panel = page.locator('[data-slot="chat-agent-panel"]').first();

    // Header is a <button> when onHeaderClick is wired (§5 contract).
    const header = panel.locator('button[aria-expanded]').first();
    await header.click();

    // URL must contain ?chat=collapsed (router.replace via handleChatCollapsedChange).
    await expect.poll(() => page.url(), { timeout: 5_000 }).toMatch(/[?&]chat=collapsed/);

    // data-collapsed="true" set on section root.
    await expect(panel).toHaveAttribute('data-collapsed', 'true');

    // Body unmounts when collapsed=true (§5: no hidden focus traps).
    await expect(panel.locator('[data-slot="live-agent-chat"]')).toHaveCount(0);
  });

  // ── 3. Reload preserves collapsed state ───────────────────────────────────

  test('3. reload preserves collapsed state (URL is SSOT)', async ({ page }) => {
    await gotoSessionLive(page, '&chat=collapsed');

    await page.reload({ waitUntil: 'domcontentloaded' });
    await page.waitForSelector('[data-slot="session-live-view"][data-ui-state="default"]', {
      timeout: 30_000,
    });

    const panel = page.locator('[data-slot="chat-agent-panel"]').first();
    await expect(panel).toHaveAttribute('data-collapsed', 'true');
    await expect(panel.locator('[data-slot="live-agent-chat"]')).toHaveCount(0);
  });

  // ── 4. Click again removes ?chat param ────────────────────────────────────

  test('4. click again removes ?chat=collapsed param (toggle back)', async ({ page }) => {
    await gotoSessionLive(page, '&chat=collapsed');

    const panel = page.locator('[data-slot="chat-agent-panel"]').first();
    await expect(panel).toHaveAttribute('data-collapsed', 'true');

    const header = panel.locator('button[aria-expanded]').first();
    await header.click();

    // ?chat=collapsed must be removed from URL.
    await expect.poll(() => page.url(), { timeout: 5_000 }).not.toMatch(/[?&]chat=collapsed/);

    await expect(panel).not.toHaveAttribute('data-collapsed', 'true');
    await expect(panel.locator('[data-slot="live-agent-chat"]')).toBeVisible();
  });

  // ── 5. Draft persists across reload (sessionStorage) ─────────────────────

  test('5. draft persists across reload (sessionStorage)', async ({ page }) => {
    await gotoSessionLive(page);

    const panel = page.locator('[data-slot="chat-agent-panel"]').first();
    await page.waitForSelector('[data-slot="chat-agent-panel"] [data-slot="live-agent-chat"]', {
      timeout: 15_000,
    });

    const input = panel.locator('input[type="text"]').first();
    await input.fill('borrador');

    // Wait for sessionStorage write (useChatDraft: onChange debounced).
    await expect(input).toHaveValue('borrador');

    await page.reload({ waitUntil: 'domcontentloaded' });
    await page.waitForSelector('[data-slot="session-live-view"][data-ui-state="default"]', {
      timeout: 30_000,
    });
    await page.waitForSelector('[data-slot="chat-agent-panel"] [data-slot="live-agent-chat"]', {
      timeout: 15_000,
    });

    const inputAfter = page
      .locator('[data-slot="chat-agent-panel"]')
      .first()
      .locator('input[type="text"]')
      .first();
    await expect(inputAfter).toHaveValue('borrador');
  });

  // ── 6. Send clears draft + input ──────────────────────────────────────────

  test('6. send clears draft + input; draft absent after reload', async ({ page }) => {
    await gotoSessionLive(page);

    const panel = page.locator('[data-slot="chat-agent-panel"]').first();
    await page.waitForSelector('[data-slot="chat-agent-panel"] [data-slot="live-agent-chat"]', {
      timeout: 15_000,
    });

    const input = panel.locator('input[type="text"]').first();
    await input.fill('hello');
    await expect(input).toHaveValue('hello');

    // Submit via button[type="submit"] inside the form.
    const sendBtn = panel.locator('button[type="submit"]').first();
    await sendBtn.click();

    // Input cleared immediately after send.
    await expect(input).toHaveValue('');

    // After reload, sessionStorage draft also cleared (clearDraft called).
    await page.reload({ waitUntil: 'domcontentloaded' });
    await page.waitForSelector('[data-slot="session-live-view"][data-ui-state="default"]', {
      timeout: 30_000,
    });
    await page.waitForSelector('[data-slot="chat-agent-panel"] [data-slot="live-agent-chat"]', {
      timeout: 15_000,
    });

    const inputAfter = page
      .locator('[data-slot="chat-agent-panel"]')
      .first()
      .locator('input[type="text"]')
      .first();
    await expect(inputAfter).toHaveValue('');
  });

  // ── 7. data-at-bottom reflects scroll anchor ──────────────────────────────

  test('7. data-at-bottom="true" on initial mount (no overflow)', async ({ page }) => {
    await gotoSessionLive(page);

    await page.waitForSelector('[data-slot="chat-agent-panel"] [data-slot="live-agent-chat"]', {
      timeout: 15_000,
    });

    const chat = page
      .locator('[data-slot="chat-agent-panel"]')
      .first()
      .locator('[data-slot="live-agent-chat"]')
      .first();

    // On initial mount with fixture messages, scroll anchor is at bottom.
    await expect(chat).toHaveAttribute('data-at-bottom', 'true');
  });

  // ── 8. axe AA — expanded + collapsed states ───────────────────────────────

  test('8. axe AA — 0 WCAG 2.1 AA violations on expanded + collapsed states', async ({ page }) => {
    // Disable animations so axe sees stable final colours
    // (mirrors session-live-mobile.spec.ts pattern).
    await page.emulateMedia({ reducedMotion: 'reduce' });

    // ── State 1: expanded (default) ──────────────────────────────────────────
    await gotoSessionLive(page);
    await page.waitForSelector('[data-slot="chat-agent-panel"] [data-slot="live-agent-chat"]', {
      timeout: 15_000,
    });

    const expandedResults = await new AxeBuilder({ page })
      .withTags(WCAG_TAGS)
      .exclude('#webpack-dev-server-client-overlay')
      .analyze();

    if (expandedResults.violations.length > 0) {
      const summary = expandedResults.violations
        .map(v => `[${v.impact}] ${v.id}: ${v.help} (${v.nodes.length} nodes)`)
        .join('\n');
      console.error('axe violations (expanded):\n' + summary);
    }
    expect(expandedResults.violations).toEqual([]);

    // ── State 2: collapsed ───────────────────────────────────────────────────
    const header = page
      .locator('[data-slot="chat-agent-panel"]')
      .first()
      .locator('button[aria-expanded]')
      .first();
    await header.click();

    await expect.poll(() => page.url(), { timeout: 5_000 }).toMatch(/[?&]chat=collapsed/);

    const collapsedResults = await new AxeBuilder({ page })
      .withTags(WCAG_TAGS)
      .exclude('#webpack-dev-server-client-overlay')
      .analyze();

    if (collapsedResults.violations.length > 0) {
      const summary = collapsedResults.violations
        .map(v => `[${v.impact}] ${v.id}: ${v.help} (${v.nodes.length} nodes)`)
        .join('\n');
      console.error('axe violations (collapsed):\n' + summary);
    }
    expect(collapsedResults.violations).toEqual([]);
  });
});
