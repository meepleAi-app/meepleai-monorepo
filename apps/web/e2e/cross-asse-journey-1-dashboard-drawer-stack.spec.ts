import { test, expect, type Page } from '@playwright/test';

import { ANNA_PERSONA, buildAnnaInitialState } from './_helpers/annaPersona';
import { assertExactStackDepth, assertExactUrl } from './_helpers/dataAssertionUtils';
import { withRetry } from './_helpers/resilienceWrappers';
import { mockAuthEndpoints, seedAuthSession } from './_helpers/seedAuthSession';
import { seedCookieConsent } from './_helpers/seedCookieConsent';
import {
  cleanupTestEntities,
  newTestRunId,
  seedGameNight,
  seedPlayer,
} from './_helpers/seedEntities';

/**
 * Cross-Asse Journey #1 — Dashboard drawer stack flow (Issue #1929).
 *
 * Anna (host) lands on /dashboard, opens her seeded GameNight via the
 * Prossimi card, pushes the Player drawer from inside the Giocatori tab,
 * then ESCs back down the stack. Verifies cascade-navigation-store
 * push/pop semantics, focus management, and prefers-reduced-motion compliance.
 *
 * **Initial state** (DEC-C-1 journey1):
 *   - 1 GN Published
 *   - 2 player roster (1 player + 1 guest)
 *
 * **testid contract** (WP3 GameNightEventDrawerContent.tsx line 185):
 *   - GN card: `data-testid="prossimi-card-{gameNightId}"` (ProssimiSection.tsx)
 *   - Player button inside GN drawer: `data-testid="gamenight-player-{rsvp.userId}"`
 *
 * **Spec ref**: DEC-C-1 + DEC-C-2 + DEC-C-6 + DEC-C-7 Journey #1 matrix.
 * **Replaces**: Task B demo spec (#1928 T7) pre-flight smoke — overwritten with
 *   full data-driven Journey #1 mandated by spec consolidato.
 *
 * **CI gate**: non-blocking on main-dev (DEC-C-4). Blocking on main-staging.
 */
test.describe('Cross-Asse Journey #1 — Dashboard drawer stack', () => {
  test.skip(({ browserName }) => browserName !== 'chromium', 'Chromium-only for speed');

  let testRunId: string;
  let gameNightId: string;
  // player1Id = the userId stored in RSVP rows (SeedPlayerResponse.playerId maps to userId)
  let player1Id: string;

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

    // ② BE entity seeding — journey1 initial state via Task B factory
    const initial = buildAnnaInitialState('journey1');
    expect(initial.gameNightCount).toBe(1);
    expect(initial.playerRosterCount).toBe(2);

    const gn = await withRetry(
      () =>
        seedGameNight(page, {
          testRunId,
          status: 'Published',
          ownerEmail: ANNA_PERSONA.email,
        }),
      { reason: 'seedGameNight journey1 beforeEach' }
    );
    gameNightId = gn.gameNightId;

    const p1 = await withRetry(
      () =>
        seedPlayer(page, {
          testRunId,
          gameNightId: gn.gameNightId,
          role: 'player',
          displayName: 'E2E Player 1',
        }),
      { reason: 'seedPlayer journey1 beforeEach (player)' }
    );
    // p1.playerId is the userId that appears in rsvp rows and in the
    // data-testid="gamenight-player-{rsvp.userId}" buttons (WP3 line 185).
    player1Id = p1.playerId;

    await withRetry(
      () =>
        seedPlayer(page, {
          testRunId,
          gameNightId: gn.gameNightId,
          role: 'guest',
          displayName: 'E2E Guest 2',
        }),
      { reason: 'seedPlayer journey1 beforeEach (guest)' }
    );
  });

  test.afterEach(async ({ page }) => {
    if (testRunId) {
      await cleanupTestEntities(page, { testRunId });
    }
  });

  // ── T2.1: Happy path — full drawer cascade ────────────────────────────────
  test('opens GN drawer from Prossimi → pushes Player drawer → ESC back-step → ESC close', async ({
    page,
  }) => {
    // ─── Step 1: Navigate to /dashboard
    await page.goto('/dashboard');
    await expect(page).not.toHaveURL(/\/(login|auth|sign-in)/);

    // ─── Step 2: Seeded GN visible in Prossimi (functional assertion)
    const prossimiCards = page.locator('[data-testid="prossimi-cards"]');
    await expect(prossimiCards).toBeVisible({ timeout: 10_000 });

    const gnCard = page.locator(`[data-testid="prossimi-card-${gameNightId}"]`);
    await expect(gnCard).toBeVisible({ timeout: 5_000 });

    // ─── Step 3: Click GN card → GN drawer opens (cascade-store stack depth = 0)
    await gnCard.click();

    // Radix Dialog marks open state with data-state="open" on the content element.
    const gnDrawer = page.locator('[data-state="open"][role="dialog"]').first();
    await expect(gnDrawer).toBeVisible({ timeout: 5_000 });

    // Read cascade store via Zustand devtools window hook (WP1 bridge).
    const storeAfterOpen = await readCascadeStore(page);
    // WP4 changed openDrawer call: 'event' → 'gameNightEvent'
    expect(storeAfterOpen.state).toBe('drawer');
    expect(storeAfterOpen.activeEntityType).toBe('gameNightEvent');
    expect(storeAfterOpen.activeEntityId).toBe(gameNightId);
    assertExactStackDepth(storeAfterOpen.drawerStack.length, 0);

    // ─── Step 4: Click Giocatori tab to reveal player buttons
    const giocatoriTab = gnDrawer.locator('[role="tab"]', { hasText: /Giocatori/i });
    await giocatoriTab.click();

    // ─── Step 5: Inside GN drawer Giocatori tab, click Player button → Player drawer pushes
    // WP3 GameNightEventDrawerContent.tsx line 185:
    // data-testid={`gamenight-player-${rsvp.userId}`}
    const playerBtn = page.locator(`[data-testid="gamenight-player-${player1Id}"]`);
    await expect(playerBtn).toBeVisible({ timeout: 5_000 });

    await withRetry(() => playerBtn.click(), {
      reason: 'click player button to push player drawer',
    });

    // Stack depth now = 1 (GN entry pushed on the stack, Player is active).
    const storeAfterPush = await readCascadeStore(page);
    expect(storeAfterPush.state).toBe('drawer');
    expect(storeAfterPush.activeEntityType).toBe('player');
    expect(storeAfterPush.activeEntityId).toBe(player1Id);
    assertExactStackDepth(storeAfterPush.drawerStack.length, 1);

    // ─── Step 6: ESC → Player drawer pops, GN restored
    await page.keyboard.press('Escape');

    // popDrawer restores GN as active, stack depth back to 0.
    const storeAfterEscOnce = await readCascadeStore(page);
    expect(storeAfterEscOnce.state).toBe('drawer');
    expect(storeAfterEscOnce.activeEntityType).toBe('gameNightEvent');
    expect(storeAfterEscOnce.activeEntityId).toBe(gameNightId);
    assertExactStackDepth(storeAfterEscOnce.drawerStack.length, 0);

    // ─── Step 7: ESC again → GN drawer closes entirely
    await page.keyboard.press('Escape');

    const storeAfterEscTwice = await readCascadeStore(page);
    expect(storeAfterEscTwice.state).toBe('closed');

    // Drawer DOM hidden — Radix removes or hides content on close
    await expect(gnDrawer).toBeHidden({ timeout: 2_000 });

    // ─── Step 8: URL unchanged throughout drawer flow (strict)
    assertExactUrl(page.url(), /\/dashboard(\?.*)?$/);
  });

  // ── T2.2: Edge case — prefers-reduced-motion variant ─────────────────────
  test('respects prefers-reduced-motion: ESC cascade still works with transitions disabled', async ({
    page,
  }) => {
    await page.emulateMedia({ reducedMotion: 'reduce' });

    await page.goto('/dashboard');
    await expect(page).not.toHaveURL(/\/(login|auth|sign-in)/);

    const gnCard = page.locator(`[data-testid="prossimi-card-${gameNightId}"]`);
    await expect(gnCard).toBeVisible({ timeout: 5_000 });

    await gnCard.click();
    const gnDrawer = page.locator('[data-state="open"][role="dialog"]').first();
    await expect(gnDrawer).toBeVisible({ timeout: 5_000 });

    // Navigate to Giocatori tab
    const giocatoriTab = gnDrawer.locator('[role="tab"]', { hasText: /Giocatori/i });
    await giocatoriTab.click();

    // No animation = no need to wait for settle; transition: none applies
    const playerBtn = page.locator(`[data-testid="gamenight-player-${player1Id}"]`);
    await expect(playerBtn).toBeVisible({ timeout: 5_000 });
    await playerBtn.click();

    await page.keyboard.press('Escape'); // pop Player → GN restored
    await page.keyboard.press('Escape'); // close GN

    const storeAfter = await readCascadeStore(page);
    expect(storeAfter.state).toBe('closed');
  });

  // ── T2.3: Edge case — backdrop click closes current level only ────────────
  test('backdrop click closes only top drawer (same semantics as ESC, NOT closeAll)', async ({
    page,
  }) => {
    await page.goto('/dashboard');

    const gnCard = page.locator(`[data-testid="prossimi-card-${gameNightId}"]`);
    await gnCard.click();

    const gnDrawer = page.locator('[data-state="open"][role="dialog"]').first();
    await expect(gnDrawer).toBeVisible({ timeout: 5_000 });

    // Navigate to Giocatori tab to reveal player buttons
    const giocatoriTab = gnDrawer.locator('[role="tab"]', { hasText: /Giocatori/i });
    await giocatoriTab.click();

    // Open Player drawer on top
    const playerBtn = page.locator(`[data-testid="gamenight-player-${player1Id}"]`);
    await expect(playerBtn).toBeVisible({ timeout: 5_000 });
    await playerBtn.click();

    const storeBeforeBackdrop = await readCascadeStore(page);
    assertExactStackDepth(storeBeforeBackdrop.drawerStack.length, 1);

    // Backdrop click: Radix Dialog overlay handles pointerdown-outside.
    // Locator targets the first fixed overlay region (z-40 backdrop).
    const overlay = page.locator('.fixed.inset-0.z-40').first();
    await expect(overlay).toBeVisible({ timeout: 2_000 });

    await overlay.click({ position: { x: 20, y: 20 }, force: true });

    // After backdrop: Player drawer pops, GN restored — same as ESC (DEC-C-7)
    const storeAfter = await readCascadeStore(page);
    expect(storeAfter.state).toBe('drawer');
    // WP4: 'event' → 'gameNightEvent'
    expect(storeAfter.activeEntityType).toBe('gameNightEvent');
    expect(storeAfter.activeEntityId).toBe(gameNightId);
    assertExactStackDepth(storeAfter.drawerStack.length, 0);
  });

  // ── T2.4: Edge case — ESC on empty stack is a no-op ──────────────────────
  test('ESC on empty drawer stack is a no-op (no error toast, store unchanged)', async ({
    page,
  }) => {
    await page.goto('/dashboard');

    // Verify initial state is closed (no drawers open)
    const storeBefore = await readCascadeStore(page);
    expect(storeBefore.state).toBe('closed');

    // ESC pressed with no drawer open
    await page.keyboard.press('Escape');

    // Store unchanged — no popDrawer attempted, no crash
    const storeAfter = await readCascadeStore(page);
    expect(storeAfter.state).toBe('closed');
    expect(storeAfter.activeEntityType).toBe(null);
    expect(storeAfter.activeEntityId).toBe(null);

    // No error toast surfaced (functional: toast region empty)
    const toastRegion = page.locator('[role="status"][data-sonner-toaster]').first();
    const toastCount = await toastRegion.locator('[data-sonner-toast]').count();
    expect(toastCount).toBe(0);
  });
});

// ============================================================================
// readCascadeStore — read Zustand store via WP1 window bridge
// ============================================================================

/**
 * Reads the cascade-navigation-store state via the `window.__cascadeStoreForE2E`
 * bridge registered in WP1 (`cascade-navigation-store.ts`, NODE_ENV-gated).
 *
 * If the bridge is missing, throws with a clear diagnostic. Do NOT fall back to
 * DOM heuristics — that would violate DEC-C-2 strict assertion policy.
 */
async function readCascadeStore(page: Page): Promise<{
  state: string;
  activeEntityType: string | null;
  activeEntityId: string | null;
  drawerStack: Array<{ entityType: string; entityId: string }>;
}> {
  return await page.evaluate(() => {
    const store = (
      window as unknown as {
        __cascadeStoreForE2E?: {
          getState: () => {
            state: string;
            activeEntityType: string | null;
            activeEntityId: string | null;
            drawerStack: Array<{ entityType: string; entityId: string }>;
          };
        };
      }
    ).__cascadeStoreForE2E;

    if (!store) {
      throw new Error(
        'cascade-navigation-store not exposed as window.__cascadeStoreForE2E. ' +
          'Verify WP1 bridge is registered in `src/lib/stores/cascade-navigation-store.ts` ' +
          'and that NODE_ENV !== "production" in this build.'
      );
    }

    return store.getState();
  });
}
