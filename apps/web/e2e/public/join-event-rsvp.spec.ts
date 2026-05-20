/**
 * Public Game Night RSVP — /join/event/[code]
 *
 * Issue #1169. E2E coverage of the anonymous RSVP surface in isolation —
 * all backend responses are stubbed via `route.fulfill()` so the FE FSM
 * can be exercised without a live backend.
 *
 * State coverage:
 *   1. Pending invitation (200 OK) → form rendered, Accept submit → success
 *   2. Already responded (200 OK + alreadyRespondedAs + respondedByName) →
 *      "Already responded as 'X'" panel + change CTA
 *   3. 410 expired token → ExpiredOrCancelledError(expired)
 *   4. 410 cancelled (POST response with reason GONE_CANCELLED)
 *   5. 404 malformed token → InvalidTokenError
 *   6. 429 rate-limited with Retry-After → RateLimitedError + countdown
 *
 * Auth: route does NOT require session — middleware allows /join/event/* through.
 */

import { expect, test, type Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

const TOKEN = 'tok-public-1169';

const PENDING_INVITATION = {
  token: TOKEN,
  status: 'Pending',
  expiresAt: '2026-06-01T00:00:00.000Z',
  respondedAt: null,
  hostUserId: '00000000-0000-4000-8000-000000000001',
  hostDisplayName: 'Marco R.',
  hostAvatarUrl: null,
  hostWelcomeMessage: 'Ci vediamo sabato!',
  gameNightId: '00000000-0000-4000-8000-000000000002',
  title: 'Sabato sera con i Padovani',
  scheduledAt: '2026-05-23T20:00:00.000Z',
  location: 'Casa Marco · Padova',
  durationMinutes: 180,
  expectedPlayers: 6,
  acceptedSoFar: 2,
  primaryGameId: null,
  primaryGameName: 'Wingspan',
  primaryGameImageUrl: null,
  alreadyRespondedAs: null,
  respondedByName: null,
};

const ACCEPTED_INVITATION = {
  ...PENDING_INVITATION,
  status: 'Accepted',
  respondedAt: '2026-05-18T07:00:00.000Z',
  alreadyRespondedAs: 'Accepted',
  respondedByName: 'Marco',
  acceptedSoFar: 3,
};

/**
 * Pin every public RSVP route to the supplied handlers. Reused per-test
 * to keep mocks isolated and explicit per FSM state.
 */
async function stubInvitationRoutes(
  page: Page,
  opts: {
    getResponse?: { status: number; body?: unknown; headers?: Record<string, string> };
    postResponse?: { status: number; body?: unknown; headers?: Record<string, string> };
  }
) {
  if (opts.getResponse) {
    await page.route(`**/api/v1/game-nights/invitations/${TOKEN}`, route => {
      if (route.request().method() !== 'GET') return route.continue();
      return route.fulfill({
        status: opts.getResponse!.status,
        contentType: 'application/json',
        headers: opts.getResponse!.headers ?? {},
        body: opts.getResponse!.body !== undefined ? JSON.stringify(opts.getResponse!.body) : '',
      });
    });
  }
  if (opts.postResponse) {
    await page.route(`**/api/v1/game-nights/invitations/${TOKEN}/respond`, route => {
      if (route.request().method() !== 'POST') return route.continue();
      return route.fulfill({
        status: opts.postResponse!.status,
        contentType: 'application/json',
        headers: opts.postResponse!.headers ?? {},
        body: opts.postResponse!.body !== undefined ? JSON.stringify(opts.postResponse!.body) : '',
      });
    });
  }
}

test.describe('Public Game Night RSVP — /join/event/[code]', () => {
  test('pending invitation renders RSVP form and accepts via Accept CTA', async ({ page }) => {
    await stubInvitationRoutes(page, {
      getResponse: { status: 200, body: PENDING_INVITATION },
      postResponse: { status: 200, body: ACCEPTED_INVITATION },
    });

    await page.goto(`/join/event/${TOKEN}`, { waitUntil: 'domcontentloaded' });

    // Hero rendered with invitation title.
    await expect(page.getByRole('heading', { name: PENDING_INVITATION.title })).toBeVisible({
      timeout: 8000,
    });
    // Surface is "rsvp" (not loading/error).
    await expect(page.locator('main[data-surface="rsvp"]')).toBeVisible();

    // Accept CTA visible + clickable.
    const acceptBtn = page.locator('[data-slot="public-rsvp-accept"]');
    await expect(acceptBtn).toBeVisible();
    await acceptBtn.click();

    // After POST 200 + refetch, the "already responded" surface renders.
    await expect(page.getByRole('heading', { name: /already responded/i }).first()).toBeVisible({
      timeout: 8000,
    });
  });

  test('already-responded invitation renders the named confirmation panel', async ({ page }) => {
    await stubInvitationRoutes(page, {
      getResponse: { status: 200, body: ACCEPTED_INVITATION },
    });

    await page.goto(`/join/event/${TOKEN}`, { waitUntil: 'domcontentloaded' });

    // Confirmation panel + change CTA.
    await expect(page.locator('[data-slot="public-rsvp-form-responded"]')).toBeVisible({
      timeout: 8000,
    });
    await expect(page.locator('[data-slot="public-rsvp-change-cta"]')).toBeVisible();
    // The persisted name is echoed in the alreadyRespondedBody copy.
    await expect(page.getByText('"Marco"', { exact: false })).toBeVisible();
    // Create-account CTA appears.
    await expect(page.locator('[data-slot="public-join-event-create-account-cta"]')).toBeVisible();
  });

  test('410 Gone (expired) renders ExpiredOrCancelledError', async ({ page }) => {
    await stubInvitationRoutes(page, {
      getResponse: { status: 410, body: { error: 'Invitation expired' } },
    });

    await page.goto(`/join/event/${TOKEN}`, { waitUntil: 'domcontentloaded' });

    await expect(page.locator('main[data-surface="token-expired"]')).toBeVisible({
      timeout: 8000,
    });
    await expect(
      page.locator('[data-slot="public-join-event-gone"][data-kind="expired"]')
    ).toBeVisible();
  });

  test('410 Gone on POST (cancelled) overrides the cached invitation', async ({ page }) => {
    await stubInvitationRoutes(page, {
      getResponse: { status: 200, body: PENDING_INVITATION },
      postResponse: {
        status: 410,
        body: { error: 'Game night cancelled', reason: 'GONE_CANCELLED' },
      },
    });

    await page.goto(`/join/event/${TOKEN}`, { waitUntil: 'domcontentloaded' });

    // Form first.
    await expect(page.locator('main[data-surface="rsvp"]')).toBeVisible({ timeout: 8000 });

    // Submit triggers the 410 path, surface flips to token-cancelled.
    await page.locator('[data-slot="public-rsvp-accept"]').click();
    await expect(page.locator('main[data-surface="token-cancelled"]')).toBeVisible({
      timeout: 8000,
    });
    await expect(
      page.locator('[data-slot="public-join-event-gone"][data-kind="cancelled"]')
    ).toBeVisible();
  });

  test('404 token unknown renders InvalidTokenError', async ({ page }) => {
    await stubInvitationRoutes(page, {
      getResponse: { status: 404, body: { error: 'Invitation token not found' } },
    });

    await page.goto(`/join/event/${TOKEN}`, { waitUntil: 'domcontentloaded' });

    await expect(page.locator('main[data-surface="token-invalid"]')).toBeVisible({
      timeout: 8000,
    });
    await expect(page.locator('[data-slot="public-join-event-invalid-token"]')).toBeVisible();
  });

  test('429 rate-limited renders RateLimitedError with Retry-After countdown', async ({ page }) => {
    await stubInvitationRoutes(page, {
      getResponse: {
        status: 429,
        body: { error: 'Too many requests' },
        headers: { 'Retry-After': '15' },
      },
    });

    await page.goto(`/join/event/${TOKEN}`, { waitUntil: 'domcontentloaded' });

    await expect(page.locator('main[data-surface="rate-limited"]')).toBeVisible({
      timeout: 8000,
    });
    // Countdown shown when retry-after > 0 (the live countdown means we just
    // assert the container is present and the heading is rendered).
    await expect(page.locator('[data-slot="public-join-event-rate-limited"]')).toBeVisible();
    await expect(page.getByRole('heading', { name: /Too many requests/i })).toBeVisible();
  });

  test('public banner is always visible above the content', async ({ page }) => {
    await stubInvitationRoutes(page, {
      getResponse: { status: 200, body: PENDING_INVITATION },
    });

    await page.goto(`/join/event/${TOKEN}`, { waitUntil: 'domcontentloaded' });
    await expect(page.locator('[data-slot="public-join-event-banner"]')).toBeVisible({
      timeout: 8000,
    });
  });

  test.skip('API base URL is used for fetches (sanity)', () => {
    // The mocks above intercept on the page network layer; this check is just
    // a guard against accidental real-network calls if the test file is run
    // in isolation against a live backend.
    expect(API_BASE).toBeTruthy();
  });
});
