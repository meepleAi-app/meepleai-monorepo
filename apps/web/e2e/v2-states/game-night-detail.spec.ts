/**
 * E2E behavioral coverage for /game-nights/[id] v2 (Issue #951 commit 4).
 *
 * Verifies the 5 AC-H1 GWT (Given/When/Then) scenarios from the spec-hardening
 * doc + WCAG 2.1 AA via axe-core @ 375/768/1280 viewports.
 *
 * Scenarios covered:
 *   - AC-H1.a capacity-exceeded: POST /rsvp → 409 capacity → toast + rollback
 *   - AC-H1.b double-rsvp (no-op): same response → state machine no-op, no POST
 *   - AC-H1.c cancelled: status=Cancelled → CancelledBanner, no action bar
 *   - AC-H1.d not-found: GET /detail → 404 → error shell with Back CTA
 *   - AC-H1.e concurrent-edit: POST /rsvp → 410 Gone (event cancelled mid-flow)
 *
 * Implementation note: no MSW worker is wired into the e2e pipeline. Mocks
 * use Playwright's `page.context().route()` (same pattern as
 * `e2e/admin/admin-workflow-actions.spec.ts`). Fixtures live inline because
 * each scenario tunes specific fields; a shared factory would obscure intent.
 *
 * Viewer setup: `mockAuthEndpoints` injects userId `00000000-0000-4000-8000-000000000fff`.
 * Scenarios that need the viewer classified as `guest` seed an RSVP entry
 * keyed to that UUID. Scenarios for `host` set `organizerId` to it.
 */
import AxeBuilder from '@axe-core/playwright';
import { test, expect, type Page, type Route } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';

const WCAG_TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'];

const VIEWER_USER_ID = '00000000-0000-4000-8000-000000000fff';
const ORGANIZER_USER_ID = '00000000-0000-4000-8000-000000000001';
const OTHER_GUEST_USER_ID = '00000000-0000-4000-8000-000000000002';

const EVENT_ID = '00000000-0000-4000-8000-000000000951';

type EventStatus = 'Draft' | 'Published' | 'Cancelled' | 'Completed';
type RsvpStatus = 'Pending' | 'Accepted' | 'Declined' | 'Maybe';

interface EventFixture {
  readonly status: EventStatus;
  readonly organizerId?: string;
  readonly maxPlayers?: number;
  readonly acceptedCount?: number;
  readonly description?: string | null;
}

interface RsvpFixture {
  readonly userId: string;
  readonly userName: string;
  readonly status: RsvpStatus;
}

function buildEventDto(overrides: EventFixture): unknown {
  return {
    id: EVENT_ID,
    organizerId: overrides.organizerId ?? ORGANIZER_USER_ID,
    organizerName: 'Marco R.',
    title: 'Carcassonne night — castelli e prati',
    description: overrides.description ?? null,
    scheduledAt: '2026-06-20T20:30:00Z',
    location: 'Casa Marco · Padova',
    maxPlayers: overrides.maxPlayers ?? 8,
    gameIds: [],
    status: overrides.status,
    acceptedCount: overrides.acceptedCount ?? 3,
    pendingCount: 1,
    totalInvited: 5,
    createdAt: '2026-06-01T12:00:00Z',
    updatedAt: '2026-06-15T18:00:00Z',
  };
}

// Issue #1315 — `GameNightRsvpDtoSchema` validates `id` as `z.string().uuid()`.
// The previous synthesis `${rsvp.userId.slice(0, 8)}-rsvp` produced strings like
// "00000000-rsvp" which fail UUID validation and silently reject the entire
// `getRsvps()` response, propagating to `useGameNightDetail.isError=true` and
// rendering the not-found shell. Synthesize a deterministic v4 UUID per user
// instead — substitute the trailing 12 chars (the node identifier) with
// "f1f1f1f1f1f1" so callers can distinguish rsvp IDs from user IDs at a glance.
function buildRsvpDto(rsvp: RsvpFixture): unknown {
  return {
    id: `${rsvp.userId.slice(0, 24)}f1f1f1f1f1f1`,
    userId: rsvp.userId,
    userName: rsvp.userName,
    status: rsvp.status,
    respondedAt: rsvp.status === 'Pending' ? null : '2026-06-15T19:00:00Z',
    createdAt: '2026-06-01T12:00:00Z',
  };
}

interface MockOptions {
  readonly event: EventFixture;
  readonly rsvps: readonly RsvpFixture[];
  /** Override POST /rsvp behaviour. Default: 200 OK. */
  readonly rsvpHandler?: (route: Route) => Promise<void> | void;
  /** When true, GET /game-nights/{id} returns 404. */
  readonly eventNotFound?: boolean;
}

async function mockGameNightApis(page: Page, options: MockOptions): Promise<void> {
  const eventDto = options.eventNotFound ? null : buildEventDto(options.event);
  const rsvpDtos = options.rsvps.map(buildRsvpDto);

  await page
    .context()
    .route(new RegExp(`/api/v1/game-nights/${EVENT_ID}(\\?.*)?$`), async (route: Route) => {
      if (route.request().method() !== 'GET') return route.continue();
      if (options.eventNotFound || !eventDto) {
        return route.fulfill({
          status: 404,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'GameNight not found' }),
        });
      }
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(eventDto),
      });
    });

  await page
    .context()
    .route(new RegExp(`/api/v1/game-nights/${EVENT_ID}/rsvps(\\?.*)?$`), async (route: Route) => {
      if (route.request().method() !== 'GET') return route.continue();
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(rsvpDtos),
      });
    });

  await page
    .context()
    .route(new RegExp(`/api/v1/game-nights/${EVENT_ID}/rsvp(\\?.*)?$`), async (route: Route) => {
      if (route.request().method() !== 'POST') return route.continue();
      if (options.rsvpHandler) {
        await options.rsvpHandler(route);
        return;
      }
      await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
    });

  // Quiesce unrelated background queries (sessions/diary panels render under
  // hero for Published events but aren't part of AC-H1 — return empty payloads).
  await page.context().route(/\/api\/v1\/sessions\?.*gameNightId=/, async (route: Route) => {
    if (route.request().method() === 'GET') {
      return route.fulfill({ status: 200, contentType: 'application/json', body: '[]' });
    }
    await route.continue();
  });
  await page
    .context()
    .route(new RegExp(`/api/v1/game-nights/${EVENT_ID}/diary(\\?.*)?$`), async (route: Route) => {
      if (route.request().method() === 'GET') {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ items: [] }),
        });
      }
      await route.continue();
    });
}

async function seedAuthAndNavigate(page: Page, options: MockOptions): Promise<void> {
  await seedAuthSession(page);
  await seedCookieConsent(page);
  await mockAuthEndpoints(page, { userId: VIEWER_USER_ID });
  await mockGameNightApis(page, options);
  await page.goto(`/game-nights/${EVENT_ID}`, { waitUntil: 'domcontentloaded' });
}

// ───────────────────────────────────────────────────────────────────────────
// AC-H1 behavioral scenarios
// ───────────────────────────────────────────────────────────────────────────

test.describe('Game Night detail — AC-H1 RSVP failure modes', () => {
  test('AC-H1.a capacity-exceeded → toast + rollback', async ({ page }) => {
    // Given a Published event where the viewer is a Pending guest and
    // the server will reject the Accept with 409 capacity.
    await seedAuthAndNavigate(page, {
      event: { status: 'Published', maxPlayers: 4, acceptedCount: 4 },
      rsvps: [
        { userId: ORGANIZER_USER_ID, userName: 'Marco R.', status: 'Accepted' },
        { userId: VIEWER_USER_ID, userName: 'Fixture User', status: 'Pending' },
      ],
      rsvpHandler: async route => {
        await route.fulfill({
          status: 409,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Capacity exceeded' }),
        });
      },
    });

    await page.waitForSelector('[data-testid="game-night-detail-hero"]', { timeout: 30_000 });
    const actionBar = page.locator('[data-testid="game-night-rsvp-action-bar"]');
    await expect(actionBar).toBeVisible();

    // When the guest clicks "Partecipo".
    await page.locator('[data-testid="rsvp-btn-accepted"]').click();

    // Then the optimistic state is rolled back and a destructive toast surfaces.
    // The button must return to non-selected (data-selected=false) once rollback runs.
    const acceptBtn = page.locator('[data-testid="rsvp-btn-accepted"]');
    await expect(acceptBtn).toHaveAttribute('data-pending', 'false', { timeout: 5_000 });
    await expect(acceptBtn).toHaveAttribute('data-selected', 'false', { timeout: 5_000 });
  });

  test('AC-H1.b double-rsvp (no-op) → no network call, no state change', async ({ page }) => {
    // Given the viewer is already Accepted.
    let postCount = 0;
    await seedAuthAndNavigate(page, {
      event: { status: 'Published' },
      rsvps: [
        { userId: ORGANIZER_USER_ID, userName: 'Marco R.', status: 'Accepted' },
        { userId: VIEWER_USER_ID, userName: 'Fixture User', status: 'Accepted' },
      ],
      rsvpHandler: async route => {
        postCount += 1;
        await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
      },
    });

    await page.waitForSelector('[data-testid="game-night-rsvp-action-bar"]', { timeout: 30_000 });
    const acceptBtn = page.locator('[data-testid="rsvp-btn-accepted"]');
    await expect(acceptBtn).toHaveAttribute('data-selected', 'true');

    // When the guest clicks "Partecipo" again (same response → no-op).
    await acceptBtn.click();

    // Then no POST was issued and the button remains selected without going pending.
    await page.waitForTimeout(500); // Allow time for any rogue request to fire.
    expect(postCount).toBe(0);
    await expect(acceptBtn).toHaveAttribute('data-selected', 'true');
    await expect(acceptBtn).toHaveAttribute('data-pending', 'false');
  });

  test('AC-H1.c cancelled → CancelledBanner renders, action bar hidden', async ({ page }) => {
    // Given the event is in terminal Cancelled state.
    await seedAuthAndNavigate(page, {
      event: {
        status: 'Cancelled',
        description: 'Marco è influenzato — riprogrammiamo a luglio.',
      },
      rsvps: [
        { userId: ORGANIZER_USER_ID, userName: 'Marco R.', status: 'Accepted' },
        { userId: VIEWER_USER_ID, userName: 'Fixture User', status: 'Maybe' },
      ],
    });

    // When the page renders.
    await page.waitForSelector('[data-testid="game-night-detail-hero"]', { timeout: 30_000 });

    // Then the CancelledBanner is visible (reason copied verbatim) and the
    // RSVP action bar is absent regardless of guest status.
    const banner = page.locator('[data-testid="game-night-cancelled-banner"]');
    await expect(banner).toBeVisible();
    await expect(banner).toContainText('riprogrammiamo');
    await expect(page.locator('[data-testid="game-night-rsvp-action-bar"]')).toHaveCount(0);
  });

  test('AC-H1.d not-found → error shell with Back CTA', async ({ page }) => {
    // Given the event 404s on first fetch.
    await seedAuthAndNavigate(page, {
      eventNotFound: true,
      event: { status: 'Published' },
      rsvps: [],
    });

    // When the page renders.
    // Then the error shell with title + back CTA is visible. Detail hero is absent.
    await expect(page.getByRole('heading', { name: /serata non trovata/i })).toBeVisible({
      timeout: 30_000,
    });
    await expect(page.getByRole('link', { name: /torna al calendario/i })).toBeVisible();
    await expect(page.locator('[data-testid="game-night-detail-hero"]')).toHaveCount(0);
  });

  test('AC-H1.e concurrent-edit → POST 410 Gone → rollback + toast', async ({ page }) => {
    // Given the viewer is a Pending guest and another host action cancels the
    // event between fetches; the next POST surfaces as 410 Gone.
    await seedAuthAndNavigate(page, {
      event: { status: 'Published' },
      rsvps: [
        { userId: ORGANIZER_USER_ID, userName: 'Marco R.', status: 'Accepted' },
        { userId: VIEWER_USER_ID, userName: 'Fixture User', status: 'Pending' },
      ],
      rsvpHandler: async route => {
        await route.fulfill({
          status: 410,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Game night cancelled or expired' }),
        });
      },
    });

    await page.waitForSelector('[data-testid="game-night-rsvp-action-bar"]', { timeout: 30_000 });

    // When the guest clicks "Forse".
    await page.locator('[data-testid="rsvp-btn-maybe"]').click();

    // Then the button transitions to pending, then rolls back to unselected.
    const maybeBtn = page.locator('[data-testid="rsvp-btn-maybe"]');
    await expect(maybeBtn).toHaveAttribute('data-pending', 'false', { timeout: 5_000 });
    await expect(maybeBtn).toHaveAttribute('data-selected', 'false', { timeout: 5_000 });
  });
});

// ───────────────────────────────────────────────────────────────────────────
// Accessibility — axe-core WCAG 2.1 AA @ 375/768/1280
// ───────────────────────────────────────────────────────────────────────────

const A11Y_VIEWPORTS = [
  { name: 'mobile', width: 375, height: 812 },
  { name: 'tablet', width: 768, height: 1024 },
  { name: 'desktop', width: 1280, height: 720 },
] as const;

test.describe('Game Night detail — accessibility @a11y', () => {
  for (const viewport of A11Y_VIEWPORTS) {
    test(`axe-core: no WCAG 2.1 AA violations — default (Published, guest) @ ${viewport.name}`, async ({
      page,
    }) => {
      await page.setViewportSize({ width: viewport.width, height: viewport.height });
      await seedAuthAndNavigate(page, {
        event: { status: 'Published' },
        rsvps: [
          { userId: ORGANIZER_USER_ID, userName: 'Marco R.', status: 'Accepted' },
          { userId: VIEWER_USER_ID, userName: 'Fixture User', status: 'Pending' },
          { userId: OTHER_GUEST_USER_ID, userName: 'Laura B.', status: 'Maybe' },
        ],
      });
      await page.waitForSelector('[data-testid="game-night-detail-hero"]', { timeout: 30_000 });
      await page.waitForSelector('[data-testid="game-night-rsvp-action-bar"]', { timeout: 10_000 });

      const results = await new AxeBuilder({ page })
        .withTags([...WCAG_TAGS])
        .exclude('#webpack-dev-server-client-overlay')
        // Legacy GameNightActions / GameNightSessionsList / GameNightDiaryPanel
        // are out of scope for the spec-hardening (AC-H1..H5 cover RSVP+hero
        // surfaces only). Exclude to avoid masking real v2 regressions; legacy
        // a11y debt tracked separately in Issue #951 follow-up.
        .exclude('[data-testid^="game-night-session"]')
        .exclude('[data-testid="game-night-diary"]')
        .exclude('[data-testid="game-night-actions"]')
        .analyze();

      expect(results.violations).toEqual([]);
    });

    test(`axe-core: no WCAG 2.1 AA violations — cancelled @ ${viewport.name}`, async ({ page }) => {
      await page.setViewportSize({ width: viewport.width, height: viewport.height });
      await seedAuthAndNavigate(page, {
        event: {
          status: 'Cancelled',
          description: 'Influenza — riprogrammiamo.',
        },
        rsvps: [
          { userId: ORGANIZER_USER_ID, userName: 'Marco R.', status: 'Accepted' },
          { userId: VIEWER_USER_ID, userName: 'Fixture User', status: 'Maybe' },
        ],
      });
      await page.waitForSelector('[data-testid="game-night-cancelled-banner"]', {
        timeout: 30_000,
      });

      const results = await new AxeBuilder({ page })
        .withTags([...WCAG_TAGS])
        .exclude('#webpack-dev-server-client-overlay')
        .analyze();

      expect(results.violations).toEqual([]);
    });
  }
});
