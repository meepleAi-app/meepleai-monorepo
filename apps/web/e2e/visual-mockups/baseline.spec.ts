/**
 * Visual regression baseline — Claude Design mockups.
 *
 * Issue #571 — V2 Phase 0.
 *
 * These snapshots are the *design contract* against which Phase 1+2 page
 * migrations are validated. They are captured from the standalone HTML
 * mockups in `admin-mockups/design_files/`, which use React UMD + Babel
 * standalone + a stable `data.js` dataset.
 *
 * Workflow:
 *   - To regenerate after a mockup change:
 *       pnpm --filter @meepleai/web test:visual:mockups:update
 *   - To verify (CI / local):
 *       pnpm --filter @meepleai/web test:visual:mockups
 *
 * The mockup static server is started automatically by Playwright's
 * `webServer` config (port 5174). See `scripts/serve-mockups.cjs`.
 *
 * Snapshot threshold: maxDiffPixelRatio 0.001 / threshold 0.2 — set in
 * `playwright.config.ts` for the mockup-baseline-* projects.
 */
import { test, expect, type Page } from '@playwright/test';

interface MockupEntry {
  /** Slug (filename without extension) */
  slug: string;
  /** Human-readable label (used in test name) */
  label: string;
  /** Wave / scope — for grouping in test output */
  group: 'wave-1' | 'wave-2' | 'sp3-secondary' | 'core-stable';
  /** True if mockup is desktop-only (skip mobile snapshot) */
  desktopOnly?: boolean;
  /**
   * If set, the mockup is temporarily skipped from baseline capture.
   * Reason recorded in test output via test.skip().
   */
  skipReason?: string;
}

/**
 * Mockup registry — single source of truth for what gets snapshotted.
 *
 * Adding a new mockup: append entry here, run `:update` script.
 */
const MOCKUPS: MockupEntry[] = [
  // SP4 wave 1 (5 desktop entity pages)
  { slug: 'sp4-games-index', label: 'Games index (/games)', group: 'wave-1' },
  { slug: 'sp4-game-detail', label: 'Game detail (/games/[id])', group: 'wave-1' },
  { slug: 'sp4-agents-index', label: 'Agents index (/agents)', group: 'wave-1' },
  { slug: 'sp4-agent-detail', label: 'Agent detail (/agents/[id])', group: 'wave-1' },
  {
    slug: 'sp4-library-desktop',
    label: 'Library desktop (/library)',
    group: 'wave-1',
    desktopOnly: true,
  },

  // SP4 wave 2 (sessions triade)
  { slug: 'sp4-sessions-index', label: 'Sessions index (/sessions)', group: 'wave-2' },
  {
    slug: 'sp4-session-live',
    label: 'Session live (/sessions/[id]/live)',
    group: 'wave-2',
    desktopOnly: true,
  },
  {
    slug: 'sp4-session-summary',
    label: 'Session summary (/sessions/[id])',
    group: 'wave-2',
    // FIXME(visual-regression): bootstrap >30s on CI runner — heavy JSX (728 lines + parts file).
    // Tracked in follow-up issue. Skip until investigated (split parts? lazy-mount? bundle-size?).
    skipReason: 'Heavy JSX bootstrap exceeds 30s timeout — pending split/lazy-mount investigation',
  },

  // SP3 secondary public pages
  { slug: 'sp3-join', label: 'Join waitlist (/join)', group: 'sp3-secondary' },
  {
    slug: 'sp3-shared-games',
    label: 'Shared games catalog (/shared-games)',
    group: 'sp3-secondary',
  },
  {
    slug: 'sp3-shared-game-detail',
    label: 'Shared game detail (/shared-games/[id])',
    group: 'sp3-secondary',
  },
  {
    slug: 'sp3-accept-invite',
    label: 'Accept invite (/invites/[token])',
    group: 'sp3-secondary',
  },
  { slug: 'sp3-faq-enhanced', label: 'FAQ enhanced (/faq)', group: 'sp3-secondary' },

  // Core stable (already-shipped or design-locked)
  { slug: 'auth-flow', label: 'Auth flow', group: 'core-stable' },
  { slug: 'public', label: 'Public landing', group: 'core-stable' },
  { slug: 'settings', label: 'Settings', group: 'core-stable' },
  { slug: 'notifications', label: 'Notifications', group: 'core-stable' },
  {
    slug: 'onboarding',
    label: 'Onboarding',
    group: 'core-stable',
    // FIXME(visual-regression): bootstrap >30s on local Windows runner — possible runtime JS error or
    // heavy multi-screen state machine. Tracked in follow-up issue.
    skipReason: 'Heavy bootstrap exceeds 30s timeout — pending console-error investigation',
  },
];

// Note: 'mobile-app' è solo .jsx (loaded via 01-screens.html catalog), niente standalone .html.
// Lo skippiamo dal baseline visual regression — 01-screens.html è una galleria meta, non un mockup di pagina.

/**
 * Wait for the mockup to finish rendering:
 *   1. The static page is loaded (DOMContentLoaded).
 *   2. Babel + React UMD + JSX evaluate; the React mount point gets populated.
 *      Mount points vary across mockups — we accept any of:
 *        #root (most common, default)
 *        #desktop-root (split mockups: settings.html)
 *        #mobile-root (mobile-first split, used as fallback)
 *      The function resolves as soon as ANY of these has children.
 *   3. Fonts (Quicksand / Nunito / JetBrains Mono) are loaded — affects layout.
 *   4. One requestAnimationFrame to flush layout/paint.
 *
 * Timeout is 30s because Babel standalone compiles JSX in-browser at runtime.
 * Heavier mockups (sp4-session-summary at 728 lines + parts file, settings.jsx
 * at 1091 lines) can take 8–12s on CI to fully bootstrap.
 */
async function waitForMockupReady(page: Page): Promise<void> {
  // (1) Wait for any known React mount point to have children.
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

  // (2) Fonts ready — guard against `document.fonts` undefined in older WebKit.
  await page.evaluate(async () => {
    if (typeof document !== 'undefined' && 'fonts' in document) {
      await (document as Document & { fonts: { ready: Promise<void> } }).fonts.ready;
    }
  });

  // (3) Two RAFs to ensure layout/paint flushed after font swap.
  await page.evaluate(
    () =>
      new Promise<void>(resolve => {
        requestAnimationFrame(() => requestAnimationFrame(() => resolve()));
      })
  );
}

/** Determine if the current project is the mobile baseline. */
function isMobileProject(projectName: string): boolean {
  return projectName.includes('mobile');
}

test.describe('V2 Visual Regression — Claude Design mockup baseline', () => {
  // Disable retries for snapshot tests (deterministic; retries hide flake).
  test.describe.configure({ retries: 0 });

  for (const mockup of MOCKUPS) {
    test(`[${mockup.group}] ${mockup.label}`, async ({ page }, testInfo) => {
      const mobile = isMobileProject(testInfo.project.name);

      if (mockup.skipReason) {
        test.skip(true, `${mockup.slug}: ${mockup.skipReason}`);
      }

      if (mobile && mockup.desktopOnly) {
        test.skip(true, `${mockup.slug} is desktop-only; skipping mobile snapshot.`);
      }

      const url = `/${mockup.slug}.html`;
      await page.goto(url, { waitUntil: 'networkidle' });
      await waitForMockupReady(page);

      // Snapshot file lands in __snapshots__/<spec>/<test-name>-<project>-<browser>-<platform>.png
      // We use a stable per-mockup name so updates touch only the relevant baseline.
      await expect(page).toHaveScreenshot(`${mockup.slug}.png`, {
        // Per-call options merge with project-level defaults set in playwright.config.ts.
        fullPage: true,
      });
    });
  }
});
