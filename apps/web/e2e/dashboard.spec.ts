/**
 * Dashboard Page E2E Tests — Gaming Hub restyle
 *
 * Spec: docs/for-developers/specs/2026-05-12-dashboard-restyle-design.md
 *
 * Coverage:
 * - Authentication & middleware
 * - Hero, StatsRow, EntityZones, DiscoverCarousel, ToolkitGrid rendering
 * - Responsive (mobile/desktop)
 * - Accessibility (axe light + dark)
 */

import AxeBuilder from '@axe-core/playwright';
import { test, expect } from '@playwright/test';

const AUTH_COOKIE = {
  name: 'meepleai_session',
  value: 'mock-session-token',
  domain: 'localhost',
  path: '/',
  httpOnly: true,
  secure: false,
  sameSite: 'Lax' as const,
};

test.describe('Dashboard — Auth & middleware', () => {
  test('redirects unauthenticated users to /login', async ({ page, context }) => {
    await context.clearCookies();
    await page.goto('/dashboard');
    await expect(page).toHaveURL(/\/login\?from=%2Fdashboard/);
  });

  test('allows authenticated access', async ({ page, context }) => {
    await context.addCookies([AUTH_COOKIE]);
    await page.goto('/dashboard');
    await expect(page).toHaveURL('/dashboard');
  });
});

test.describe('Dashboard — Component rendering', () => {
  test.beforeEach(async ({ context }) => {
    await context.addCookies([AUTH_COOKIE]);
  });

  test('DashboardHero h1 with user name visible', async ({ page }) => {
    await page.goto('/dashboard');
    await expect(page.getByRole('heading', { level: 1 })).toContainText(/Ciao,/);
  });

  test('DashboardStatsRow renders 4 entity-tagged stat cards', async ({ page }) => {
    await page.goto('/dashboard');
    const nav = page.getByRole('navigation', { name: 'Statistiche personali' });
    await expect(nav).toBeVisible();
    const cards = nav.locator('[data-entity]');
    await expect(cards).toHaveCount(4);
  });

  test('each EntityZone has aria-labelledby on its section', async ({ page }) => {
    await page.goto('/dashboard');
    const sections = page.locator('section[aria-labelledby]');
    await expect(sections.first()).toBeVisible();
    expect(await sections.count()).toBeGreaterThanOrEqual(4);
  });

  test('Sessions zone renders DiscoverCarousel region when sessions exist', async ({ page }) => {
    await page.goto('/dashboard');
    const sessionEmpty = page.getByText(/Nessuna sessione/i);
    if (await sessionEmpty.isVisible()) {
      // empty state, no carousel expected
      return;
    }
    await expect(page.getByRole('region', { name: /Carosello sessioni/i })).toBeVisible();
  });
});

test.describe('Dashboard — Responsive', () => {
  test.beforeEach(async ({ context }) => {
    await context.addCookies([AUTH_COOKIE]);
  });

  test('mobile 375x667: stat-row visible', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 667 });
    await page.goto('/dashboard');
    await expect(page.getByRole('navigation', { name: 'Statistiche personali' })).toBeVisible();
  });

  test('desktop 1280x800: stat-row visible', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/dashboard');
    await expect(page.getByRole('navigation', { name: 'Statistiche personali' })).toBeVisible();
  });
});

test.describe('Dashboard — Accessibility', () => {
  test.beforeEach(async ({ context }) => {
    await context.addCookies([AUTH_COOKIE]);
  });

  test('axe: light theme has no critical/serious violations', async ({ page }) => {
    await page.goto('/dashboard');
    await page.waitForSelector('h1');
    const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze();
    const critical = results.violations.filter(v =>
      ['critical', 'serious'].includes(v.impact ?? '')
    );
    expect(critical).toEqual([]);
  });

  test('axe: dark theme has no critical/serious violations', async ({ page }) => {
    await page.goto('/dashboard');
    await page.waitForSelector('h1');
    await page.evaluate(() => {
      document.documentElement.setAttribute('data-theme', 'dark');
      document.documentElement.classList.add('dark');
    });
    await page.waitForTimeout(300);
    const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze();
    const critical = results.violations.filter(v =>
      ['critical', 'serious'].includes(v.impact ?? '')
    );
    expect(critical).toEqual([]);
  });

  test('keyboard: Tab traverses focusable controls', async ({ page }) => {
    await page.goto('/dashboard');
    await page.waitForSelector('h1');
    await page.keyboard.press('Tab');
    const focused1 = await page.evaluate(() => document.activeElement?.tagName);
    expect(['A', 'BUTTON']).toContain(focused1);
    await page.keyboard.press('Tab');
    await page.keyboard.press('Tab');
    const focused3 = await page.evaluate(() => document.activeElement?.tagName);
    expect(['A', 'BUTTON', 'DIV', 'INPUT']).toContain(focused3);
  });
});
