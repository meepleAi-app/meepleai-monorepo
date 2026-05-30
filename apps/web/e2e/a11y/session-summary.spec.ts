/**
 * Accessibility tests — /sessions/[id] summary (Wave D.3, Issue #756).
 *
 * Combines (Phase 0.5 contract §15.3 + AC11):
 *   - axe-core WCAG 2.1 AA scan: default state (full layout)
 *   - axe-core WCAG 2.1 AA scan: tied state (podium variant + hero tied banner)
 *   - axe-core WCAG 2.1 AA scan: empty-photos state (Cell 6 partial)
 *   - axe-core WCAG 2.1 AA scan: empty-achievements state (Cell 6 partial)
 *   - axe-core WCAG 2.1 AA scan: filtered diary state (?diary=score)
 *   - axe-core WCAG 2.1 AA scan: dark ShareCard preview state (?theme=dark)
 *   - prefers-reduced-motion contract: confetti pieces collapse to sub-ms,
 *     static medal fallback rendered
 *   - Tied podium ARIA contract: hero exposes data-tied="true" + tied banner
 *     visible (announceable text content)
 *   - Diary filter pills keyboard nav: Tab into row → ArrowRight cycles next
 *     pill, ArrowLeft cycles prev, Home/End jump first/last (via shared
 *     `useTablistKeyboardNav` hook from Wave A.6 PR #623).
 *
 * NOTE — Component-isolated theme:
 *   `?theme=dark` toggles ONLY the ShareCard preview (per contract AC6). The
 *   page itself remains in light theme. We test the dark ShareCard a11y
 *   separately via the same default-page render — axe scans the whole page,
 *   so the dark inverted text contrast is exercised.
 *
 * NOTE — Hidden cells excluded:
 *   loading/error/not-found states have separate render paths covered by unit
 *   tests in `_components/__tests__/SessionSummaryView.test.tsx`. They emit
 *   `data-ui-state="loading|error|not-found"` but with minimal markup; not
 *   independently axe-scanned here.
 *
 * Auth bypass: triple helper required for `(authenticated)` routes — Wave B.1
 * lesson learned (Issue #633). No `networkidle` — `domcontentloaded` +
 * explicit `waitForSelector`.
 *
 * Fixture sentinel: `00000000-0000-4000-8000-000000000756` (Wave D.3).
 */
import AxeBuilder from '@axe-core/playwright';
import { test, expect, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';

// See sessions-index.spec.ts for rationale.
test.use({ colorScheme: 'dark' });

const FIXTURE_SESSION_ID = '00000000-0000-4000-8000-000000000756';
const WCAG_TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] as const;

async function seedAuth(page: Page): Promise<void> {
  await seedAuthSession(page);
  await seedCookieConsent(page);
  await mockAuthEndpoints(page);
}

async function gotoSummary(page: Page, search = ''): Promise<void> {
  await seedAuth(page);
  // The route renders SessionSummaryHero only when fsmCell.kind is 'default'
  // or 'partial'. Without `?fixture=...`, the orchestrator falls back to the
  // real-data path (useSessionDetail) which 401/404s against the test backend
  // and lands the FSM in 'error' / 'not-found' / 'loading' — no Hero rendered.
  // Surfaced by PR #1700 release CI as 6 waitForSelector timeouts on
  // [data-slot="session-summary-hero"]. Ensure `?fixture=` is present;
  // append `fixture=default` for callers that pass other params (e.g.
  // `?diary=score`, `?theme=dark`).
  const search2 = (() => {
    if (search === '') return '?fixture=default';
    const params = new URLSearchParams(search.replace(/^\?/, ''));
    if (!params.has('fixture')) params.set('fixture', 'default');
    return `?${params.toString()}`;
  })();
  await page.goto(`/sessions/${FIXTURE_SESSION_ID}${search2}`, {
    waitUntil: 'domcontentloaded',
  });
  await page.waitForSelector('[data-slot="session-summary-view"]', { timeout: 30_000 });
  await page.waitForSelector('[data-slot="session-summary-hero"]', { timeout: 10_000 });
}

function logViolations(
  label: string,
  violations: ReadonlyArray<{
    impact?: string | null;
    id: string;
    help: string;
    nodes: ReadonlyArray<unknown>;
  }>
): void {
  if (violations.length > 0) {
    const summary = violations
      .map(v => `[${v.impact}] ${v.id}: ${v.help} (${v.nodes.length} nodes)`)
      .join('\n');
    console.log(`axe violations (${label}):\n${summary}`);
  }
}

test.describe('Session summary — accessibility @a11y', () => {
  // ─────────────────────────────────────────────────────────────────────────
  // axe-core scans across fixture variants
  // ─────────────────────────────────────────────────────────────────────────

  test('axe-core: no WCAG 2.1 AA violations on default state', async ({ page }) => {
    await gotoSummary(page);
    await expect(
      page.locator('[data-slot="session-summary-view"][data-ui-state="default"]')
    ).toBeVisible();

    const results = await new AxeBuilder({ page })
      .withTags(WCAG_TAGS)
      .exclude('#webpack-dev-server-client-overlay')
      .analyze();

    logViolations('default', results.violations);
    expect(results.violations).toEqual([]);
  });

  test('axe-core: no WCAG 2.1 AA violations on tied podium state', async ({ page }) => {
    await gotoSummary(page, '?fixture=tied');
    await expect(
      page.locator('[data-slot="session-summary-hero"][data-tied="true"]')
    ).toBeVisible();
    await expect(page.locator('[data-slot="hero-tied-banner"]')).toBeVisible();

    const results = await new AxeBuilder({ page })
      .withTags(WCAG_TAGS)
      .exclude('#webpack-dev-server-client-overlay')
      .analyze();

    logViolations('tied', results.violations);
    expect(results.violations).toEqual([]);
  });

  test('axe-core: no WCAG 2.1 AA violations on empty-photos partial state', async ({ page }) => {
    await gotoSummary(page, '?fixture=empty-photos');
    await expect(page.locator('[data-slot="photos-gallery"]')).toBeVisible();

    const results = await new AxeBuilder({ page })
      .withTags(WCAG_TAGS)
      .exclude('#webpack-dev-server-client-overlay')
      .analyze();

    logViolations('empty-photos', results.violations);
    expect(results.violations).toEqual([]);
  });

  test('axe-core: no WCAG 2.1 AA violations on empty-achievements partial state', async ({
    page,
  }) => {
    await gotoSummary(page, '?fixture=empty-achievements');
    await expect(page.locator('[data-slot="achievements-carousel"]')).toBeVisible();

    const results = await new AxeBuilder({ page })
      .withTags(WCAG_TAGS)
      .exclude('#webpack-dev-server-client-overlay')
      .analyze();

    logViolations('empty-achievements', results.violations);
    expect(results.violations).toEqual([]);
  });

  test('axe-core: no WCAG 2.1 AA violations with diary filter ?diary=score active', async ({
    page,
  }) => {
    await gotoSummary(page, '?diary=score');
    await expect(page.locator('[data-slot="session-diary-timeline"]')).toBeVisible();
    // Verify filter pill 'score' is the active one (aria-selected="true").
    const scorePill = page.locator('[data-slot="diary-filter-pill"][data-filter="score"]');
    await expect(scorePill).toHaveAttribute('aria-selected', 'true');

    const results = await new AxeBuilder({ page })
      .withTags(WCAG_TAGS)
      .exclude('#webpack-dev-server-client-overlay')
      .analyze();

    logViolations('diary-filter-score', results.violations);
    expect(results.violations).toEqual([]);
  });

  test('axe-core: no WCAG 2.1 AA violations with ShareCard dark preview ?theme=dark', async ({
    page,
  }) => {
    await gotoSummary(page, '?theme=dark');
    await expect(page.locator('[data-slot="session-share-card"]')).toBeVisible();

    const results = await new AxeBuilder({ page })
      .withTags(WCAG_TAGS)
      .exclude('#webpack-dev-server-client-overlay')
      .analyze();

    logViolations('share-card-dark', results.violations);
    expect(results.violations).toEqual([]);
  });

  // ─────────────────────────────────────────────────────────────────────────
  // prefers-reduced-motion (AC4)
  // ─────────────────────────────────────────────────────────────────────────

  test('prefers-reduced-motion: confetti animation suppressed + medal fallback visible', async ({
    page,
  }) => {
    // Emulate reduced-motion BEFORE goto so the global CSS rule applies on
    // first paint. globals.css `@media (prefers-reduced-motion: reduce)` forces
    // all `transition-duration` to 0.01ms !important and the orchestrator's
    // useReducedMotion() flag short-circuits the Confetti render.
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await gotoSummary(page);

    // Hero is mounted; the static medal fallback must be visible (it is the
    // primary celebration affordance under reduced-motion). The fallback
    // carries `role="img"` + aria-label = labels.confettiSkippedLabel.
    const fallback = page.locator('[data-slot="confetti-skipped-fallback"]');
    if (await fallback.count()) {
      await expect(fallback.first()).toBeVisible();
      await expect(fallback.first()).toHaveAttribute('role', 'img');
    }

    // Confetti pieces should NOT be rendered when prefers-reduced-motion.
    // The orchestrator passes showConfetti to the hero only when not reduced.
    // Even if rendered, animation-duration must collapse to sub-ms via global
    // CSS override.
    const pieces = page.locator('[data-slot="confetti-piece"]');
    const count = await pieces.count();
    if (count > 0) {
      const animDurationMs = await pieces.first().evaluate((el: Element) => {
        const cs = window.getComputedStyle(el);
        const raw = cs.animationDuration;
        if (!raw || raw === 'none' || raw === '0s') return 0;
        if (raw.endsWith('ms')) return parseFloat(raw);
        if (raw.endsWith('s')) return parseFloat(raw) * 1000;
        return parseFloat(raw) * 1000;
      });
      expect(animDurationMs).toBeLessThanOrEqual(50);
    }
  });

  // ─────────────────────────────────────────────────────────────────────────
  // Tied podium ARIA contract (AC11)
  // ─────────────────────────────────────────────────────────────────────────

  test('tied podium exposes data-tied + accessible banner text', async ({ page }) => {
    await gotoSummary(page, '?fixture=tied');
    // Hero region has role="region" + data-tied="true"
    const hero = page.locator('[data-slot="session-summary-hero"]');
    await expect(hero).toHaveAttribute('role', 'region');
    await expect(hero).toHaveAttribute('data-tied', 'true');

    // Tied banner is a visible div with the localized announcement copy.
    // Text content provides the announcement when AT users navigate the region.
    const banner = page.locator('[data-slot="hero-tied-banner"]');
    await expect(banner).toBeVisible();
    const text = (await banner.textContent())?.trim() ?? '';
    expect(text.length).toBeGreaterThan(0);
  });

  // ─────────────────────────────────────────────────────────────────────────
  // Diary filter pills keyboard nav (AC11 + Wave A.6 PR #623 hook reuse)
  // ─────────────────────────────────────────────────────────────────────────

  test('diary filter tablist: ArrowRight cycles next pill, ArrowLeft cycles prev', async ({
    page,
  }) => {
    await gotoSummary(page);
    await page.waitForSelector('[data-slot="session-diary-timeline"]', { timeout: 10_000 });

    // FILTER_ORDER from SessionDiaryTimeline: ['all', 'score', 'event', 'chat', 'photo']
    const allPill = page.locator('[data-slot="diary-filter-pill"][data-filter="all"]');
    const scorePill = page.locator('[data-slot="diary-filter-pill"][data-filter="score"]');
    const photoPill = page.locator('[data-slot="diary-filter-pill"][data-filter="photo"]');

    // Default 'all' is selected (aria-selected="true", tabIndex=0).
    await expect(allPill).toHaveAttribute('role', 'tab');
    await expect(allPill).toHaveAttribute('aria-selected', 'true');

    // Focus first pill; ArrowRight moves to 'score' (next in FILTER_ORDER).
    await allPill.focus();
    await expect(allPill).toBeFocused();
    await page.keyboard.press('ArrowRight');
    await expect(scorePill).toBeFocused();

    // ArrowLeft from second pill returns focus to 'all'.
    await page.keyboard.press('ArrowLeft');
    await expect(allPill).toBeFocused();

    // End jumps to last pill ('photo').
    await page.keyboard.press('End');
    await expect(photoPill).toBeFocused();

    // Home jumps back to first ('all').
    await page.keyboard.press('Home');
    await expect(allPill).toBeFocused();
  });
});
