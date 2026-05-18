/**
 * WS-C Phase 3b — Conformity verify (real).
 *
 * For each entry in `mockup-ownership.bootstrap.json`, navigate the live Next.js
 * route (:3000) and assert the rendered screenshot matches the committed mockup
 * baseline `__mockup__/{route.id}.{viewport}.png` within the per-route
 * `threshold` (per-pixel YIQ) and `maxDiffPixelRatio` (aggregate) budgets.
 *
 * Determinism strategy per route (no backend dependency):
 *   - `/library` → `IS_VISUAL_TEST_BUILD` constant (set via
 *     `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` at build time) short-circuits
 *     `LibraryHubV2` to a curated 12-entry fixture (Wave B.3 pattern).
 *   - `/library/{gameId}` → `?state=default` URL override consumes
 *     `useStateOverride()` (WS-D Exemplar, PR #1093) to surface the fixture
 *     detail without hitting any API.
 *
 * Auth bypass per `(authenticated)/` layout: triple-helper pattern
 * (`seedAuthSession` + `seedCookieConsent` + `mockAuthEndpoints`) from
 * `visual-migrated/sp4-library-desktop.spec.ts`.
 *
 * Baselines absence is still tolerated: `test.fixme()` on missing PNG documents
 * that the bootstrap workflow has not yet been dispatched. Once Phase 3 workflow
 * runs and the auto-PR lands committed baselines, the fixme self-resolves.
 *
 * Refs: #1069 (WS-C), #1066 (umbrella), AC-C.2 + AC-C.6.
 */
import { existsSync } from 'node:fs';
import { join } from 'node:path';

import { test, expect, type Page } from '@playwright/test';

import {
  loadOwnership,
  resolveLivePath,
  type RouteOwnership,
} from '../../scripts/conformity-ownership';
import { mockAuthEndpoints, seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';

const ownershipPath = join(__dirname, 'mockup-ownership.bootstrap.json');
const ownership = loadOwnership(ownershipPath);
const baselineDir = join(__dirname, '__mockup__');

/**
 * Per-route ready-state selector + URL strategy.
 *
 * Each route declares HOW to make the live render deterministic without
 * hitting the backend. Add a new entry when extending the ownership map.
 */
interface RouteRunbook {
  /** URL appended to resolveLivePath() output (e.g. '?state=default'). */
  search?: string;
  /** Wait for this selector before screenshot. */
  readySelector: string;
  /** Optional secondary selector for slower-rendering content. */
  contentSelector?: string;
}

const RUNBOOKS: Record<string, RouteRunbook> = {
  library: {
    readySelector: '[data-slot="library-hub-v2"]',
    contentSelector: '[data-slot="library-grid-card"]',
  },
  'library-game-detail': {
    search: '?state=default',
    readySelector: 'main',
  },
  'player-detail': {
    // PlayerDetailView short-circuits to Sara Rossi fixture when
    // NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1 (baked into CI prod build).
    // ?state=default is the explicit no-op override that confirms fixture path.
    search: '?state=default',
    readySelector: '[data-slot="player-detail-view"]',
  },
  'game-nights-index': {
    // No build-time fixture for game-nights (tracked as Open Issue §1 in spec).
    // Live route in CI without backend renders empty state — matched by
    // mockup marker on DesktopFrame #06 Empty (spec Risk R6 mitigation path a).
    readySelector: '[data-testid="game-nights-empty"]',
  },
};

async function waitForRouteReady(page: Page, runbook: RouteRunbook): Promise<void> {
  await page.waitForSelector(runbook.readySelector, { timeout: 30_000 });
  if (runbook.contentSelector) {
    await page.waitForSelector(runbook.contentSelector, { timeout: 10_000 });
  }

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

test.describe('WS-C Conformity — route vs mockup baseline', () => {
  // Conformity assertions are deterministic; flake should fail-fast.
  // Project-level `retries: 1` (playwright.config.ts) covers transient infra flake only.

  for (const route of ownership.routes as readonly RouteOwnership[]) {
    test(`conformity: ${route.livePath} (id=${route.id})`, async ({ page }, testInfo) => {
      const viewport = testInfo.project.name.includes('mobile') ? 'mobile' : 'desktop';
      const baselinePath = join(baselineDir, `${route.id}.${viewport}.png`);

      // Baseline materialization is performed by `bootstrap-mockup-baselines.yml`.
      // On a fresh checkout (or before the first workflow run), the PNG is absent;
      // tolerate that with a fixme so PRs that touch unrelated areas don't block.
      if (!existsSync(baselinePath)) {
        test.fixme(
          true,
          `Baseline missing at ${baselinePath}. Dispatch the ` +
            `\`Conformity — Bootstrap Mockup Baselines\` workflow (workflow_dispatch) ` +
            `then merge the auto-PR. Local equivalent: ` +
            `\`pnpm test:visual:conformity:bootstrap:update\`.`
        );
      }

      const runbook = RUNBOOKS[route.id];
      if (!runbook) {
        throw new Error(
          `No runbook for route id "${route.id}". Add an entry in RUNBOOKS ` +
            `before mapping a new route in mockup-ownership.bootstrap.json.`
        );
      }

      // Triple-helper auth bypass for `(authenticated)/` routes.
      await seedAuthSession(page);
      await seedCookieConsent(page);
      await mockAuthEndpoints(page);

      const target = resolveLivePath(route.livePath, route.liveFixture) + (runbook.search ?? '');
      await page.goto(target, { waitUntil: 'domcontentloaded' });
      await waitForRouteReady(page, runbook);

      // Per-route override the project-level expect defaults.
      await expect(page).toHaveScreenshot(`${route.id}.png`, {
        fullPage: true,
        threshold: route.threshold,
        maxDiffPixelRatio: route.conformityRatio,
        // Mask dynamic zones to avoid flake (timestamps, randomized counters).
        mask: [page.locator('[data-dynamic]')],
      });
    });
  }
});
