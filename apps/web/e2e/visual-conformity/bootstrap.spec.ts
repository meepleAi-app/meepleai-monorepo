/**
 * WS-C Phase 2 — Mockup baseline bootstrap (single-screen mode, v2).
 *
 * Run ONLY by Phase 3 `bootstrap-mockup-baselines.yml` workflow (manual dispatch
 * + auto-trigger on `admin-mockups/design_files/**` change) and from local dev
 * via `pnpm test:visual:conformity:bootstrap:update`.
 *
 * Iterates each entry in `mockup-ownership.bootstrap.json`, navigates the
 * corresponding mockup HTML on the static server (:5174), waits for React + JSX
 * + fonts to settle, then captures the screenshot of the SINGLE element marked
 * with `data-conformity-screen="default-{viewport}"`.
 *
 * Hard-fails if the marker is missing or duplicated — the contract is documented
 * in the spec (Marker Contract section) and enforced by `conformity-marker-utils.ts`.
 *
 * Refs: #1269 (single-screen refactor), #1069 (WS-C), #1066 (umbrella), AC-C.6.
 * Spec: docs/superpowers/specs/2026-05-18-conformity-baseline-single-screen-design.md
 */
import { join } from 'node:path';

import { test, expect, type Page } from '@playwright/test';

import {
  buildMarkerSelector,
  getViewportFromProjectName,
  validateMarkerCount,
} from '../../scripts/conformity-marker-utils';
import { loadOwnership } from '../../scripts/conformity-ownership';

const ownershipPath = join(__dirname, 'mockup-ownership.bootstrap.json');
const ownership = loadOwnership(ownershipPath);

/**
 * Wait for the mockup page to be visually ready.
 *
 * Adaptive across two mockup styles in `admin-mockups/design_files/`:
 *
 *   (a) React-bootstrapped (e.g. sp4-library-desktop): loads React UMD + Babel
 *       standalone and renders into one of #root / #desktop-root / #mobile-root.
 *       JSX compilation can take 8–12s on heavier mockups.
 *   (b) Pure-CSS (e.g. nanolith-runthrough-game-detail): static HTML + tokens.css,
 *       body renders immediately, no mount point.
 *
 * Detection strategy: probe for known React mount points for up to 15s. If found,
 * wait for their children. If not found within the probe window, fall back to
 * asserting <body> has rendered content. Then settle fonts + 2× RAF in both cases.
 */
async function waitForMockupReady(page: Page): Promise<void> {
  // Probe React mount points; if absent → pure-CSS mockup, skip JSX wait.
  const hasReactMount = await page
    .waitForFunction(
      () => {
        const candidates = ['root', 'desktop-root', 'mobile-root'];
        return candidates.some(id => document.getElementById(id) !== null);
      },
      { timeout: 5_000 }
    )
    .then(() => true)
    .catch(() => false);

  if (hasReactMount) {
    // React/JSX path: wait for the mount point to have children.
    await page.waitForFunction(
      () => {
        const candidates = ['root', 'desktop-root', 'mobile-root'];
        return candidates.some(id => {
          const el = document.getElementById(id);
          return el !== null && el.children.length > 0;
        });
      },
      { timeout: 30_000 }
    );
  } else {
    // Pure-CSS path: body should have rendered content already.
    await page.waitForFunction(() => document.body.children.length > 0, {
      timeout: 5_000,
    });
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

test.describe('WS-C Conformity — mockup baseline bootstrap', () => {
  // Bootstrap snapshots are deterministic by design; retries would mask flake.
  test.describe.configure({ retries: 0 });

  for (const route of ownership.routes) {
    test(`mockup baseline: ${route.id} (${route.mockup})`, async ({ page }, testInfo) => {
      const viewport = getViewportFromProjectName(testInfo.project.name);
      const markerValue = `default-${viewport}`;
      const selector = buildMarkerSelector(viewport);

      const url = `/${route.mockup}`;
      await page.goto(url, { waitUntil: 'networkidle' });
      await waitForMockupReady(page);

      // Single-screen marker contract enforcement (spec §Marker Contract).
      // The mockup HTML must expose exactly ONE element matching the marker
      // for the current viewport. Hard-fail with actionable message otherwise.
      const target = page.locator(selector);
      const count = await target.count();
      validateMarkerCount(count, route.mockup, markerValue);

      // Snapshot lands at __mockup__/{route.id}.{viewport}.png via project-level
      // snapshotPathTemplate. Bootstrap mode (Phase 3 workflow) passes
      // --update-snapshots to (re)generate; verify mode asserts existence.
      // Capture ONLY the marked locator (not fullPage) so the baseline matches
      // the single-screen live render rather than the multi-state catalog.
      await expect(target).toHaveScreenshot(`${route.id}.png`);
    });
  }
});
