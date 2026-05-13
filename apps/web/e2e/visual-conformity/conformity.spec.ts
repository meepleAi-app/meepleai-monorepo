/**
 * WS-C Phase 2 — Conformity verify (scaffold).
 *
 * For each entry in `mockup-ownership.bootstrap.json`, navigate the live Next.js
 * route (:3000) and assert the rendered screenshot matches the committed mockup
 * baseline `__mockup__/{route.id}.{viewport}.png` within the per-route
 * `threshold` (per-pixel YIQ) and `maxDiffPixelRatio` (aggregate) budgets.
 *
 * **Phase 2 status**: every spec is currently `test.fixme()`'d. The route
 * cannot match the mockup pixel-for-pixel until backend data is mocked to mirror
 * the mockup's curated dataset (e.g. Nanolith game, 8 library cards). That work
 * lands in Phase 3, alongside the workflow that runs this spec.
 *
 * Why ship the scaffold now: it documents the consumer contract for the
 * ownership loader, exercises `snapshotPathTemplate` in CI (typecheck only,
 * no execution), and provides a single editing surface when Phase 3 mocks land.
 *
 * Refs: #1069 (WS-C), #1066 (umbrella), AC-C.2 + AC-C.6.
 */
import { existsSync } from 'node:fs';
import { join } from 'node:path';

import { test, expect } from '@playwright/test';

import { loadOwnership, resolveLivePath } from '../../scripts/conformity-ownership';

const ownershipPath = join(__dirname, 'mockup-ownership.bootstrap.json');
const ownership = loadOwnership(ownershipPath);
const baselineDir = join(__dirname, '__mockup__');

test.describe('WS-C Conformity — route vs mockup baseline', () => {
  // Conformity assertions are deterministic; flake should fail-fast.
  // Project-level `retries: 1` covers transient infra flake only.

  for (const route of ownership.routes) {
    test(`conformity: ${route.livePath} (id=${route.id})`, async ({ page }, testInfo) => {
      const viewport = testInfo.project.name.includes('mobile') ? 'mobile' : 'desktop';
      const baselinePath = join(baselineDir, `${route.id}.${viewport}.png`);

      // Phase 2 gate: stub Phase 3 prerequisites.
      // Two independent reasons can fixme this spec; check both for clearer
      // CI output so we know exactly what to fix when unblocking.
      const missingBaseline = !existsSync(baselinePath);
      const missingDataMock = true; // flipped per-route to false in Phase 3 as mocks land

      if (missingBaseline) {
        test.fixme(
          true,
          `Baseline missing at ${baselinePath}. Run Phase 3 bootstrap workflow ` +
            `(or \`pnpm test:visual:conformity:bootstrap:update\` locally).`
        );
      } else if (missingDataMock) {
        test.fixme(
          true,
          `Route data parity not yet mocked for ${route.id}. ` +
            `Phase 3 will add page.route() stubs matching the mockup dataset.`
        );
      }

      const target = resolveLivePath(route.livePath, route.liveFixture);
      await page.goto(target, { waitUntil: 'domcontentloaded' });

      // Per-route override the project-level expect defaults.
      await expect(page).toHaveScreenshot(route.id, {
        fullPage: true,
        threshold: route.threshold,
        maxDiffPixelRatio: route.conformityRatio,
      });
    });
  }
});
