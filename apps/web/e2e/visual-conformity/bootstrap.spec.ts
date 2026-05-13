/**
 * WS-C Phase 2 — Mockup baseline bootstrap.
 *
 * Run ONLY by Phase 3 `bootstrap-mockup-baselines.yml` workflow (manual dispatch
 * + auto-trigger on `admin-mockups/design_files/**` change) and from local dev
 * via `pnpm test:visual:conformity:bootstrap:update`.
 *
 * Iterates each entry in `mockup-ownership.bootstrap.json`, navigates the
 * corresponding mockup HTML on the static server (:5174), waits for React + JSX
 * + fonts to settle, then captures `__mockup__/{route.id}.{viewport}.png` as the
 * canonical reference for `conformity.spec.ts`.
 *
 * Refs: #1069 (WS-C), #1066 (umbrella), AC-C.6 baseline storage.
 */
import { join } from 'node:path';

import { test, expect, type Page } from '@playwright/test';

import { loadOwnership } from '../../scripts/conformity-ownership';

const ownershipPath = join(__dirname, 'mockup-ownership.bootstrap.json');
const ownership = loadOwnership(ownershipPath);

/**
 * Wait for the mockup page to be visually ready.
 *
 * Mirrors the contract used by `e2e/visual-mockups/baseline.spec.ts`:
 *   1. React mount point (one of #root / #desktop-root / #mobile-root) has children
 *   2. document.fonts.ready resolves (Inter + system stack)
 *   3. Two requestAnimationFrames flush layout/paint
 *
 * The standalone server uses Babel in-browser → first paint can take 8–12s on
 * heavier mockups. 30s timeout accommodates this.
 */
async function waitForMockupReady(page: Page): Promise<void> {
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
    test(`mockup baseline: ${route.id} (${route.mockup})`, async ({ page }) => {
      const url = `/${route.mockup}`;
      await page.goto(url, { waitUntil: 'networkidle' });
      await waitForMockupReady(page);

      // Snapshot lands at __mockup__/{route.id}.{viewport}.png via project-level
      // snapshotPathTemplate. Bootstrap mode (Phase 3 workflow) passes
      // --update-snapshots to (re)generate; verify mode asserts existence.
      await expect(page).toHaveScreenshot(route.id, {
        fullPage: true,
      });
    });
  }
});
