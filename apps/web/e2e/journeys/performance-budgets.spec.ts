/**
 * Performance Budget Validation for User Journeys
 *
 * Validates page load performance across all journey pages:
 * - Budget: Page load < 2000ms (2 seconds)
 * - Measures: DOMContentLoaded, Load Complete, Network Idle
 * - Scope: All critical journey pages
 *
 * Pattern: Lightweight performance assertions (NOT full Lighthouse)
 * Related Issue: #2843 - AC #7 "Performance budgets (page load < 2s)"
 * Epic: #2823
 *
 * Note: Full Lighthouse audits are in performance.spec.ts
 * This file focuses on journey-specific page load budgets
 */

import { expect, test } from '../fixtures';
import { AuthHelper, USER_FIXTURES } from '../pages';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';
const BUDGET_MS = 2000; // 2 seconds (AC requirement)
const WARNING_MS = 1500; // Warning threshold

const MOCK_GAME = {
  id: 'test-game-perf-1',
  title: 'Catan',
  bggId: 13,
};

/**
 * Measure page load time with detailed metrics
 */
async function measurePageLoad(page: any) {
  const metrics = await page.evaluate(() => {
    const navigation = performance.getEntriesByType('navigation')[0] as PerformanceNavigationTiming;
    const paint = performance.getEntriesByType('paint');

    const fcp = paint.find(entry => entry.name === 'first-contentful-paint');

    return {
      domContentLoaded:
        navigation.domContentLoadedEventEnd - navigation.domContentLoadedEventStart,
      loadComplete: navigation.loadEventEnd - navigation.loadEventStart,
      totalLoad: navigation.loadEventEnd - navigation.fetchStart,
      fcp: fcp?.startTime || 0,
      domInteractive: navigation.domInteractive - navigation.fetchStart,
    };
  });

  return metrics;
}

test.describe('Performance Budgets: Journey Pages', () => {
  test.beforeEach(async ({ page }) => {
    const authHelper = new AuthHelper(page);
    await page.emulateMedia({ reducedMotion: 'reduce' });

    // Auth: Mock authenticated session
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);

    // Mock: Lightweight API responses for fast loading
    await page.route(`${API_BASE}/api/v1/games/${MOCK_GAME.id}`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_GAME),
      });
    });

    await page.route(`${API_BASE}/api/v1/games*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([MOCK_GAME]),
      });
    });
  });

  test('Homepage (/) should load under budget', async ({ page }) => {
    const startTime = Date.now();

    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const loadTime = Date.now() - startTime;
    const metrics = await measurePageLoad(page);

    console.log('Homepage Performance:', {
      totalLoad: `${loadTime}ms`,
      fcp: `${metrics.fcp.toFixed(0)}ms`,
      domInteractive: `${metrics.domInteractive.toFixed(0)}ms`,
    });

    // Enforce budget
    expect(loadTime).toBeLessThan(BUDGET_MS);

    // Warn if approaching budget
    if (loadTime > WARNING_MS) {
      console.warn(`⚠️  Homepage load time (${loadTime}ms) approaching budget (${BUDGET_MS}ms)`);
    }
  });

  test('AI Chat Page (/board-game-ai/ask) should load under budget', async ({ page }) => {
    const startTime = Date.now();

    await page.goto('/board-game-ai/ask');
    await page.waitForLoadState('networkidle');

    const loadTime = Date.now() - startTime;
    const metrics = await measurePageLoad(page);

    console.log('AI Chat Performance:', {
      totalLoad: `${loadTime}ms`,
      fcp: `${metrics.fcp.toFixed(0)}ms`,
      domInteractive: `${metrics.domInteractive.toFixed(0)}ms`,
    });

    expect(loadTime).toBeLessThan(BUDGET_MS);

    if (loadTime > WARNING_MS) {
      console.warn(`⚠️  AI Chat load time (${loadTime}ms) approaching budget`);
    }
  });

  test('Game Detail Page (/games/:id) should load under budget', async ({ page }) => {
    const startTime = Date.now();

    await page.goto(`/games/${MOCK_GAME.id}`);
    await page.waitForLoadState('networkidle');

    const loadTime = Date.now() - startTime;
    const metrics = await measurePageLoad(page);

    console.log('Game Detail Performance:', {
      totalLoad: `${loadTime}ms`,
      fcp: `${metrics.fcp.toFixed(0)}ms`,
      domInteractive: `${metrics.domInteractive.toFixed(0)}ms`,
    });

    expect(loadTime).toBeLessThan(BUDGET_MS);

    if (loadTime > WARNING_MS) {
      console.warn(`⚠️  Game Detail load time (${loadTime}ms) approaching budget`);
    }
  });

  test('Sessions History (/sessions/history) should load under budget', async ({ page }) => {
    // Mock: Sessions API
    await page.route(`${API_BASE}/api/v1/sessions/history*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: [], totalCount: 0 }),
      });
    });

    const startTime = Date.now();

    await page.goto('/sessions/history');
    await page.waitForLoadState('networkidle');

    const loadTime = Date.now() - startTime;
    const metrics = await measurePageLoad(page);

    console.log('Sessions History Performance:', {
      totalLoad: `${loadTime}ms`,
      fcp: `${metrics.fcp.toFixed(0)}ms`,
      domInteractive: `${metrics.domInteractive.toFixed(0)}ms`,
    });

    expect(loadTime).toBeLessThan(BUDGET_MS);

    if (loadTime > WARNING_MS) {
      console.warn(`⚠️  Sessions History load time (${loadTime}ms) approaching budget`);
    }
  });

  test('Games Catalog (/games) should load under budget', async ({ page }) => {
    const startTime = Date.now();

    await page.goto('/games');
    await page.waitForLoadState('networkidle');

    const loadTime = Date.now() - startTime;
    const metrics = await measurePageLoad(page);

    console.log('Games Catalog Performance:', {
      totalLoad: `${loadTime}ms`,
      fcp: `${metrics.fcp.toFixed(0)}ms`,
      domInteractive: `${metrics.domInteractive.toFixed(0)}ms`,
    });

    expect(loadTime).toBeLessThan(BUDGET_MS);

    if (loadTime > WARNING_MS) {
      console.warn(`⚠️  Games Catalog load time (${loadTime}ms) approaching budget`);
    }
  });
});

test.describe('Performance Budgets: Critical Interactions', () => {
  test.beforeEach(async ({ page }) => {
    const authHelper = new AuthHelper(page);
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);

    // Mock APIs
    await page.route(`${API_BASE}/api/v1/games/${MOCK_GAME.id}`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_GAME),
      });
    });
  });

  test('Tab switching should be instant (<100ms)', async ({ page }) => {
    await page.goto(`/games/${MOCK_GAME.id}`);
    await page.waitForLoadState('networkidle');

    // Measure tab switch performance
    const tabs = ['overview', 'rules', 'sessions', 'notes'];

    for (const tabName of tabs) {
      const tab = page.getByRole('tab', { name: new RegExp(tabName, 'i') });

      if (await tab.isVisible({ timeout: 2000 })) {
        const startTime = Date.now();
        await tab.click();
        await page.waitForLoadState('domcontentloaded');
        const switchTime = Date.now() - startTime;

        console.log(`Tab switch (${tabName}): ${switchTime}ms`);

        // Tab switches should be near-instant
        expect(switchTime).toBeLessThan(100);
      }
    }
  });

  test('Search should respond quickly (<500ms)', async ({ page }) => {
    await page.goto('/games');
    await page.waitForLoadState('networkidle');

    // Mock: Search API
    await page.route(`${API_BASE}/api/v1/games/search*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([MOCK_GAME]),
      });
    });

    const searchInput = page.locator('input[type="search"], input[placeholder*="Search"]').first();

    if (await searchInput.isVisible({ timeout: 3000 })) {
      const startTime = Date.now();
      await searchInput.fill('Catan');
      await page.waitForLoadState('networkidle');
      const searchTime = Date.now() - startTime;

      console.log(`Search response time: ${searchTime}ms`);

      // Search should be fast
      expect(searchTime).toBeLessThan(500);
    }
  });

  test('Modal open/close should be smooth (<200ms)', async ({ page }) => {
    await page.goto(`/games/${MOCK_GAME.id}`);
    await page.waitForLoadState('networkidle');

    // Find any modal trigger button
    const modalButton = page.getByRole('button', { name: /start session|upload|add/i }).first();

    if (await modalButton.isVisible({ timeout: 3000 })) {
      // Measure modal open time
      const openStart = Date.now();
      await modalButton.click();
      await page.waitForSelector('[role="dialog"]', { state: 'visible' });
      const openTime = Date.now() - openStart;

      console.log(`Modal open time: ${openTime}ms`);
      expect(openTime).toBeLessThan(200);

      // Measure modal close time
      const closeButton = page.getByRole('button', { name: /close|cancel|×/i }).first();
      const closeStart = Date.now();
      await closeButton.click();
      await page.waitForSelector('[role="dialog"]', { state: 'hidden' });
      const closeTime = Date.now() - closeStart;

      console.log(`Modal close time: ${closeTime}ms`);
      expect(closeTime).toBeLessThan(200);
    }
  });
});

test.describe('Performance Budget Monitoring', () => {
  test('should log performance regression warnings', async ({ page }) => {
    // This test tracks performance trends over time
    // Useful for detecting gradual performance degradation

    const pages = [
      { url: '/', name: 'Homepage' },
      { url: '/games', name: 'Games Catalog' },
      { url: '/board-game-ai/ask', name: 'AI Chat' },
    ];

    const results: { page: string; loadTime: number; budget: number; margin: number }[] = [];

    for (const { url, name } of pages) {
      const startTime = Date.now();
      await page.goto(url);
      await page.waitForLoadState('networkidle');
      const loadTime = Date.now() - startTime;

      const margin = BUDGET_MS - loadTime;
      const marginPercent = ((margin / BUDGET_MS) * 100).toFixed(0);

      results.push({ page: name, loadTime, budget: BUDGET_MS, margin });

      console.log(`${name}: ${loadTime}ms (margin: ${margin}ms / ${marginPercent}%)`);
    }

    // Log summary
    console.log('\n📊 Performance Budget Summary:');
    results.forEach(r => {
      const status = r.loadTime < BUDGET_MS ? '✅' : '❌';
      console.log(`  ${status} ${r.page}: ${r.loadTime}ms / ${r.budget}ms`);
    });

    // All pages must meet budget
    results.forEach(r => {
      expect(r.loadTime).toBeLessThan(r.budget);
    });
  });
});
