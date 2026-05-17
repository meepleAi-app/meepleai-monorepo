#!/usr/bin/env node
/**
 * Phase A.live runner — per-node color-contrast extraction (#1215, refs #1094)
 *
 * One-shot script that runs axe-core against the inventory of (route, state)
 * targets enumerated in docs/for-developers/audits/a11y-color-contrast-restoration.md §1.1,
 * filters the `color-contrast` rule only, and dumps per-node `ViolationRow`
 * records to apps/web/audits/phase-a-color-contrast-runtime.json.
 *
 * NOT a replacement for pnpm test:a11y:e2e (the per-PR regression gate).
 * This is a one-shot inventory tool for Phase A.live (#1215) to populate
 * §1.4 of the audit doc.
 *
 * Pre-condition: a Playwright-compatible dev server is reachable at
 * BASE_URL (default http://localhost:3000) with PLAYWRIGHT_AUTH_BYPASS=true
 * and NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1. The docker prod image does
 * NOT carry these flags, so the runner expects you to start `pnpm dev` (or
 * `next dev`) in a separate terminal AFTER stopping `docker stop meepleai-web`.
 *
 * Usage:
 *   # Run all targets (full matrix)
 *   node apps/web/scripts/phase-a-color-contrast-audit.mjs
 *
 *   # Run only public routes (no auth required) — PoC subset
 *   PHASE_A_SUBSET=public node apps/web/scripts/phase-a-color-contrast-audit.mjs
 *
 *   # Single target by name (matches `name` field in TARGETS)
 *   PHASE_A_ONLY=login-light node apps/web/scripts/phase-a-color-contrast-audit.mjs
 *
 * Output: apps/web/audits/phase-a-color-contrast-runtime.json (single source of truth).
 */

import { chromium, devices } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = path.resolve(__dirname, '..', '..', '..');
const OUTPUT_PATH = path.join(REPO_ROOT, 'apps', 'web', 'audits', 'phase-a-color-contrast-runtime.json');
const BASE_URL = process.env.BASE_URL || 'http://localhost:3000';
const SUBSET = process.env.PHASE_A_SUBSET || 'all'; // 'all' | 'public' | 'authenticated'
const ONLY = process.env.PHASE_A_ONLY || null; // exact name match

/**
 * Inventory targets — synced with audit doc §1.1.
 *
 * To add a new target:
 *   - public + no special setup → just add { name, route, state, theme, viewports }
 *   - requires auth → include `requiresAuth: true` + a `setup(page)` async function
 *   - requires viewport-specific layout → restrict `viewports`
 *
 * The viewport key matches Playwright `devices[...]` device names; we use
 * just 2: 'Desktop Chrome' (1280×720) and 'iPhone 13' (mobile-chrome).
 */
const TARGETS = [
  // ─── PUBLIC ROUTES (no auth required — safest PoC subset) ───
  {
    name: 'login-light',
    route: '/login',
    state: 'default',
    theme: 'light',
    viewports: ['Desktop Chrome', 'iPhone 13'],
    requiresAuth: false,
    waitFor: 'networkidle',
  },
  {
    name: 'login-dark',
    route: '/login',
    state: 'default',
    theme: 'dark',
    viewports: ['Desktop Chrome', 'iPhone 13'],
    requiresAuth: false,
    waitFor: 'networkidle',
  },
  {
    name: 'register-light',
    route: '/register',
    state: 'default',
    theme: 'light',
    viewports: ['Desktop Chrome', 'iPhone 13'],
    requiresAuth: false,
    waitFor: 'networkidle',
  },
  {
    name: 'register-dark',
    route: '/register',
    state: 'default',
    theme: 'dark',
    viewports: ['Desktop Chrome', 'iPhone 13'],
    requiresAuth: false,
    waitFor: 'networkidle',
  },
  {
    name: 'landing-light',
    route: '/',
    state: 'default',
    theme: 'light',
    viewports: ['Desktop Chrome', 'iPhone 13'],
    requiresAuth: false,
    waitFor: 'networkidle',
  },
  {
    name: 'landing-dark',
    route: '/',
    state: 'default',
    theme: 'dark',
    viewports: ['Desktop Chrome', 'iPhone 13'],
    requiresAuth: false,
    waitFor: 'networkidle',
  },

  // ─── AUTHENTICATED ROUTES (PLAYWRIGHT_AUTH_BYPASS=true on Next.js dev) ───
  // These mirror the inventory in audit doc §1.1. Each requires the test
  // fixture flags on the dev server. Full setup mirrors e2e/a11y/*.spec.ts
  // patterns but is intentionally minimal here (no fixture seeding beyond
  // navigation + wait) — the goal is data extraction, not behavior coverage.
  {
    name: 'library-default-light',
    route: '/library',
    state: 'default',
    theme: 'light',
    viewports: ['Desktop Chrome', 'iPhone 13'],
    requiresAuth: true,
    waitFor: '[data-slot="library-hub-v2"]',
  },
  {
    name: 'agents-index-light',
    route: '/agents',
    state: 'default',
    theme: 'light',
    viewports: ['Desktop Chrome', 'iPhone 13'],
    requiresAuth: true,
    waitFor: 'main',
  },
  {
    name: 'players-index-light',
    route: '/players',
    state: 'default',
    theme: 'light',
    viewports: ['Desktop Chrome', 'iPhone 13'],
    requiresAuth: true,
    waitFor: 'main',
  },
  {
    name: 'sessions-index-light',
    route: '/sessions',
    state: 'default',
    theme: 'light',
    viewports: ['Desktop Chrome', 'iPhone 13'],
    requiresAuth: true,
    waitFor: 'main',
  },
  // NOTE: The full inventory of 30 (route, state) targets enumerated in §1.1
  // of the audit doc is NOT yet wired here. Adding the remaining ~20 targets
  // (e.g. gamebook-* states, session-live dark, pause-overlay-open, etc.)
  // requires per-target setup() functions that mirror the e2e/a11y/*.spec.ts
  // patterns (seedAuthSession + state-specific navigation). That extension
  // is the follow-up work tracked under #1215 if this PoC validates the
  // pattern. See "Next steps" in the audit doc §1.4 closing notes.
];

/**
 * Filter TARGETS based on SUBSET / ONLY env vars.
 */
function selectTargets() {
  let selected = TARGETS;
  if (ONLY) {
    selected = TARGETS.filter((t) => t.name === ONLY);
  } else if (SUBSET === 'public') {
    selected = TARGETS.filter((t) => !t.requiresAuth);
  } else if (SUBSET === 'authenticated') {
    selected = TARGETS.filter((t) => t.requiresAuth);
  }
  return selected;
}

/**
 * Run axe-core color-contrast against a single (target, viewport) pair.
 *
 * Returns: { target, viewport, violations: ViolationRow[], runMs, error? }
 */
async function runOneTarget(browser, target, viewportName) {
  const device = devices[viewportName];
  if (!device) throw new Error(`Unknown viewport device: ${viewportName}`);

  const context = await browser.newContext({
    ...device,
    colorScheme: target.theme,
  });
  const page = await context.newPage();
  const startedAt = Date.now();

  try {
    // Auth seeding for (authenticated) routes — mirrors apps/web/e2e/_helpers/seedAuthSession.ts.
    if (target.requiresAuth) {
      await context.addCookies([
        {
          name: 'meepleai_session',
          value: 'playwright-fixture-session-token',
          domain: 'localhost',
          path: '/',
          httpOnly: true,
          secure: false,
          sameSite: 'Lax',
        },
        {
          name: 'meepleai_user_role',
          value: 'user',
          domain: 'localhost',
          path: '/',
          httpOnly: false,
          secure: false,
          sameSite: 'Lax',
        },
      ]);

      const authMeDto = {
        user: {
          id: '00000000-0000-4000-8000-000000000fff',
          email: 'fixture-user@meepleai.test',
          role: 'User',
          displayName: 'Fixture User',
          onboardingCompleted: true,
          onboardingSkipped: false,
        },
      };
      await context.route(/\/api\/v1\/auth\/me(\?.*)?$/, async (route) => {
        if (route.request().method() === 'GET') {
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify(authMeDto),
          });
          return;
        }
        await route.continue();
      });
      await context.route(/\/api\/v1\/auth\/session\/status(\?.*)?$/, async (route) => {
        if (route.request().method() === 'GET') {
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
              expiresAt: '2099-12-31T23:59:59Z',
              lastSeenAt: '2026-04-30T12:00:00Z',
              remainingMinutes: 60,
            }),
          });
          return;
        }
        await route.continue();
      });
    }

    await page.goto(`${BASE_URL}${target.route}`, {
      waitUntil: target.waitFor === 'networkidle' ? 'networkidle' : 'domcontentloaded',
      timeout: 30_000,
    });

    if (target.waitFor && target.waitFor !== 'networkidle') {
      await page.waitForSelector(target.waitFor, { timeout: 15_000 });
    }

    // Run axe-core with color-contrast (WCAG AA, 4.5:1) rule ONLY.
    // Do NOT include color-contrast-enhanced (AAA, 7:1) — #1094 scope is AA per WCAG 2.1 AA target.
    const results = await new AxeBuilder({ page })
      .options({
        runOnly: { type: 'rule', values: ['color-contrast'] },
      })
      .analyze();

    const colorContrastViolations = results.violations.filter(
      (v) => v.id === 'color-contrast',
    );

    /** @type {ViolationRow[]} */
    const rows = [];
    for (const v of colorContrastViolations) {
      for (const node of v.nodes) {
        // axe color-contrast stores fg/bg/ratio in the matching check's `data`.
        // Check can live in `any`, `all`, or `none` depending on rule semantics;
        // for color-contrast it's in `any` with id === rule.id.
        const ccCheck =
          (node.any || []).find((c) => c.id === 'color-contrast') ||
          (node.all || []).find((c) => c.id === 'color-contrast') ||
          (node.none || []).find((c) => c.id === 'color-contrast');
        const data = ccCheck?.data || {};
        rows.push({
          route: target.route,
          state: target.state,
          viewport: viewportName,
          theme: target.theme,
          selector: Array.isArray(node.target) ? node.target.join(' ') : String(node.target),
          fgColor: data.fgColor || '',
          bgColor: data.bgColor || '',
          contrastRatio: typeof data.contrastRatio === 'number' ? data.contrastRatio : null,
          expectedContrastRatio:
            typeof data.expectedContrastRatio === 'number' ? data.expectedContrastRatio : null,
          fontSize: data.fontSize || '',
          fontWeight: data.fontWeight || '',
          impact: node.impact || v.impact || 'serious',
          ruleId: v.id,
        });
      }
    }

    return {
      target: target.name,
      viewport: viewportName,
      runMs: Date.now() - startedAt,
      violationCount: rows.length,
      violations: rows,
    };
  } catch (err) {
    return {
      target: target.name,
      viewport: viewportName,
      runMs: Date.now() - startedAt,
      violationCount: null,
      error: err.message || String(err),
    };
  } finally {
    await context.close();
  }
}

async function main() {
  const targets = selectTargets();
  console.log(`Phase A.live runner — ${targets.length} target(s), BASE_URL=${BASE_URL}`);
  console.log(
    `Targets: ${targets
      .map((t) => `${t.name}(${t.viewports.length}vp)`)
      .join(', ')}`,
  );

  fs.mkdirSync(path.dirname(OUTPUT_PATH), { recursive: true });

  const browser = await chromium.launch({ headless: true });
  const startedAt = new Date();
  const results = [];

  try {
    for (const target of targets) {
      for (const viewport of target.viewports) {
        process.stdout.write(`  → ${target.name} [${viewport}] ... `);
        const result = await runOneTarget(browser, target, viewport);
        results.push(result);
        if (result.error) {
          process.stdout.write(`ERROR: ${result.error.substring(0, 80)}\n`);
        } else {
          process.stdout.write(
            `${result.violationCount} node(s), ${result.runMs}ms\n`,
          );
        }
      }
    }
  } finally {
    await browser.close();
  }

  const totalRunMs = results.reduce((sum, r) => sum + r.runMs, 0);
  const totalViolations = results
    .filter((r) => !r.error)
    .reduce((sum, r) => sum + (r.violationCount || 0), 0);
  const errorCount = results.filter((r) => r.error).length;

  const output = {
    meta: {
      runStartedAt: startedAt.toISOString(),
      runFinishedAt: new Date().toISOString(),
      baseUrl: BASE_URL,
      subset: SUBSET,
      only: ONLY,
      targetCount: targets.length,
      runCount: results.length,
      totalRunMs,
      totalViolations,
      errorCount,
      runnerVersion: '0.1.0',
      runnerScript: 'apps/web/scripts/phase-a-color-contrast-audit.mjs',
      sourceIssue: '#1215',
      parentIssue: '#1094',
    },
    results,
  };

  fs.writeFileSync(OUTPUT_PATH, JSON.stringify(output, null, 2) + '\n', 'utf-8');
  console.log(
    `\n✓ Output: ${path.relative(REPO_ROOT, OUTPUT_PATH)}\n` +
      `  ${results.length} runs, ${totalViolations} total color-contrast nodes, ${errorCount} errors, ${(totalRunMs / 1000).toFixed(1)}s\n`,
  );

  process.exit(errorCount > 0 && totalViolations === 0 ? 2 : 0);
}

main().catch((err) => {
  console.error('Runner crashed:', err);
  process.exit(1);
});
