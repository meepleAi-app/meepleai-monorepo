/**
 * E2E state matrix + a11y for /game-nights index v2 (Issue #1170 Stage 3, commit 4).
 *
 * Covers the 5 orchestrator branches in
 * `apps/web/src/app/(authenticated)/game-nights/_content.tsx`:
 *   - default        — calendar view renders with mixed events (organizing + invited)
 *   - empty          — both endpoints return [] → game-nights-empty state
 *   - loading        — endpoints delayed → game-nights-loading skeleton
 *   - filtered-empty — list view + organizing filter with 0 organizing events
 *   - error          — both endpoints 500 → game-nights-error state with retry CTA
 *
 * Each scenario runs against 3 viewports (375 / 768 / 1280) with axe-core
 * WCAG 2.1 AA checks. Visual baselines are NOT captured here — the project's
 * `visual-conformity` framework (CI bootstrap workflow) owns visual baselines
 * for the /game-nights route (see mockup-ownership.bootstrap.json).
 *
 * Implementation note: mirrors the sibling
 * `e2e/v2-states/game-night-detail.spec.ts` pattern (page.context().route()
 * backend mocking, no `?fixture=` URL hatch).
 *
 * Auth pattern (Wave B.1 lesson, Issue #633): seedAuthSession +
 * seedCookieConsent + mockAuthEndpoints triple for `(authenticated)` routes.
 */
import AxeBuilder from '@axe-core/playwright';
import { test, expect, type Page, type Route } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';

const WCAG_TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'];

const VIEWER_USER_ID = '00000000-0000-4000-8000-000000000fff';
const OTHER_USER_ID = '00000000-0000-4000-8000-000000000001';

type GameNightStatus = 'Draft' | 'Published' | 'Cancelled' | 'Completed';

interface EventOverrides {
  readonly id?: string;
  readonly organizerId?: string;
  readonly title?: string;
  readonly scheduledAt?: string;
  readonly status?: GameNightStatus;
  readonly acceptedCount?: number;
}

function buildEventDto(overrides: EventOverrides): unknown {
  return {
    id: overrides.id ?? '00000000-0000-4000-8000-000000001170',
    organizerId: overrides.organizerId ?? OTHER_USER_ID,
    organizerName: 'Marco R.',
    title: overrides.title ?? 'Wingspan night',
    description: null,
    scheduledAt: overrides.scheduledAt ?? '2026-06-15T19:00:00Z',
    location: 'Casa Marco',
    maxPlayers: 5,
    gameIds: [],
    status: overrides.status ?? 'Published',
    acceptedCount: overrides.acceptedCount ?? 3,
    pendingCount: 1,
    totalInvited: 5,
    createdAt: '2026-06-01T12:00:00Z',
    updatedAt: null,
  };
}

interface MockOptions {
  /** Payload (or status-only override) for GET /upcoming. */
  readonly upcoming?: { status?: number; body?: readonly unknown[]; delayMs?: number };
  /** Payload (or status-only override) for GET /mine. */
  readonly mine?: { status?: number; body?: readonly unknown[]; delayMs?: number };
}

async function mockGameNightsApis(page: Page, options: MockOptions = {}): Promise<void> {
  const upcoming = options.upcoming ?? { body: [] };
  const mine = options.mine ?? { body: [] };

  await page.context().route(/\/api\/v1\/game-nights\/upcoming(\?.*)?$/, async (route: Route) => {
    if (route.request().method() !== 'GET') return route.continue();
    if (upcoming.delayMs) await new Promise(r => setTimeout(r, upcoming.delayMs));
    await route.fulfill({
      status: upcoming.status ?? 200,
      contentType: 'application/json',
      body: JSON.stringify(upcoming.body ?? []),
    });
  });

  await page.context().route(/\/api\/v1\/game-nights\/mine(\?.*)?$/, async (route: Route) => {
    if (route.request().method() !== 'GET') return route.continue();
    if (mine.delayMs) await new Promise(r => setTimeout(r, mine.delayMs));
    await route.fulfill({
      status: mine.status ?? 200,
      contentType: 'application/json',
      body: JSON.stringify(mine.body ?? []),
    });
  });
}

async function seedAuthAndNavigate(
  page: Page,
  options: MockOptions,
  url: string = '/game-nights'
): Promise<void> {
  await seedAuthSession(page);
  await seedCookieConsent(page);
  await mockAuthEndpoints(page, { userId: VIEWER_USER_ID });
  await mockGameNightsApis(page, options);
  await page.goto(url, { waitUntil: 'domcontentloaded' });
}

const VIEWPORTS = [
  { name: 'mobile', width: 375, height: 740 },
  { name: 'tablet', width: 768, height: 1024 },
  { name: 'desktop', width: 1280, height: 720 },
] as const;

// ───────────────────────────────────────────────────────────────────────────
// Fixtures for the "default" scenario — 3 mixed events (1 organizing, 2 invited),
// 1 of those echoed by /mine so the orchestrator's dedup-merge path is exercised.
// ───────────────────────────────────────────────────────────────────────────

const DEFAULT_UPCOMING = [
  buildEventDto({
    id: '00000000-0000-4000-8000-000000000a01',
    organizerId: VIEWER_USER_ID,
    title: 'Carcassonne — viewer organizes',
    scheduledAt: '2026-06-10T19:00:00Z',
    status: 'Published',
  }),
  buildEventDto({
    id: '00000000-0000-4000-8000-000000000a02',
    title: 'Wingspan @ Marco',
    scheduledAt: '2026-06-15T19:00:00Z',
    status: 'Published',
  }),
  buildEventDto({
    id: '00000000-0000-4000-8000-000000000a03',
    title: 'Brass: Birmingham',
    scheduledAt: '2026-06-22T20:00:00Z',
    status: 'Draft',
  }),
];

const DEFAULT_MINE = [DEFAULT_UPCOMING[1]];

// Invited-only fixtures for the filtered-empty scenario.
const INVITED_ONLY_UPCOMING = [
  buildEventDto({
    id: '00000000-0000-4000-8000-000000000b01',
    title: 'Wingspan @ Marco',
    scheduledAt: '2026-06-15T19:00:00Z',
  }),
  buildEventDto({
    id: '00000000-0000-4000-8000-000000000b02',
    title: 'Brass: Birmingham',
    scheduledAt: '2026-06-22T20:00:00Z',
  }),
];

// ───────────────────────────────────────────────────────────────────────────
// State-matrix tests — one describe block per scenario, parameterised by viewport.
// ───────────────────────────────────────────────────────────────────────────

test.describe('Game Nights index — state matrix', () => {
  for (const vp of VIEWPORTS) {
    test(`default (calendar + mixed events) @ ${vp.name}`, async ({ page }) => {
      await page.setViewportSize({ width: vp.width, height: vp.height });
      await seedAuthAndNavigate(page, {
        upcoming: { body: DEFAULT_UPCOMING },
        mine: { body: DEFAULT_MINE },
      });

      await expect(page.locator('[data-testid="game-nights-calendar-month-grid"]')).toBeVisible({
        timeout: 30_000,
      });

      const a11y = await new AxeBuilder({ page })
        .withTags([...WCAG_TAGS])
        .exclude('#webpack-dev-server-client-overlay')
        .analyze();
      expect(a11y.violations).toEqual([]);
    });

    test(`empty (no events) @ ${vp.name}`, async ({ page }) => {
      await page.setViewportSize({ width: vp.width, height: vp.height });
      await seedAuthAndNavigate(page, { upcoming: { body: [] }, mine: { body: [] } });

      await expect(page.locator('[data-testid="game-nights-empty"]')).toBeVisible({
        timeout: 30_000,
      });

      const a11y = await new AxeBuilder({ page })
        .withTags([...WCAG_TAGS])
        .exclude('#webpack-dev-server-client-overlay')
        .analyze();
      expect(a11y.violations).toEqual([]);
    });

    test(`loading (delayed endpoints) @ ${vp.name}`, async ({ page }) => {
      await page.setViewportSize({ width: vp.width, height: vp.height });
      // Delay both endpoints so the orchestrator stays in the loading branch
      // for the duration of the assertion + axe scan. We do NOT wait for the
      // responses to settle — the loading skeleton is the asserted state.
      await seedAuthAndNavigate(page, {
        upcoming: { body: [], delayMs: 10_000 },
        mine: { body: [], delayMs: 10_000 },
      });

      await expect(page.locator('[data-testid="game-nights-loading"]')).toBeVisible({
        timeout: 30_000,
      });

      const a11y = await new AxeBuilder({ page })
        .withTags([...WCAG_TAGS])
        .exclude('#webpack-dev-server-client-overlay')
        .analyze();
      expect(a11y.violations).toEqual([]);
    });

    test(`filtered-empty (list + organizing filter, no organizing events) @ ${vp.name}`, async ({
      page,
    }) => {
      await page.setViewportSize({ width: vp.width, height: vp.height });
      await seedAuthAndNavigate(
        page,
        { upcoming: { body: INVITED_ONLY_UPCOMING }, mine: { body: [] } },
        '/game-nights?view=list&filter=organizing'
      );

      await expect(page.locator('[data-testid="game-nights-list-empty"]')).toBeVisible({
        timeout: 30_000,
      });

      const a11y = await new AxeBuilder({ page })
        .withTags([...WCAG_TAGS])
        .exclude('#webpack-dev-server-client-overlay')
        .analyze();
      expect(a11y.violations).toEqual([]);
    });

    test(`error (both endpoints 500) @ ${vp.name}`, async ({ page }) => {
      await page.setViewportSize({ width: vp.width, height: vp.height });
      await seedAuthAndNavigate(page, {
        upcoming: { status: 500, body: { error: 'boom' } as unknown as readonly unknown[] },
        mine: { status: 500, body: { error: 'boom' } as unknown as readonly unknown[] },
      });

      const errorEl = page.locator('[data-testid="game-nights-error"]');
      await expect(errorEl).toBeVisible({ timeout: 30_000 });
      // Retry CTA is a <button> labelled by `gameNightsIndex.states.error.retry` (IT
      // "Riprova" / EN "Retry") — match either via role + name regex.
      await expect(errorEl.getByRole('button', { name: /riprova|retry/i })).toBeVisible();

      const a11y = await new AxeBuilder({ page })
        .withTags([...WCAG_TAGS])
        .exclude('#webpack-dev-server-client-overlay')
        .analyze();
      expect(a11y.violations).toEqual([]);
    });
  }
});
