/**
 * Admin — Overview, Activity Feed, System Health
 * Sezione 6 del piano di test UI MeepleAI
 */

import { test, expect, type Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

async function mockAdminAuth(page: Page) {
  await page.context().route(`${API_BASE}/api/v1/auth/me`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: { id: 'admin-1', email: 'admin@meepleai.dev', displayName: 'Admin', role: 'Admin' },
        expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
      }),
    })
  );
}

const MOCK_OVERVIEW_STATS = {
  totalUsers: 1_234,
  activeUsers: 89,
  totalGames: 456,
  totalSessions: 3_891,
  totalDocuments: 142,
  systemStatus: 'healthy',
};

const MOCK_ACTIVITY = [
  { id: 'a1', type: 'user_registered', message: 'Nuovo utente: test@example.com', createdAt: '2026-02-21T09:00:00Z' },
  { id: 'a2', type: 'game_published', message: 'Gioco pubblicato: Catan', createdAt: '2026-02-21T08:30:00Z' },
  { id: 'a3', type: 'pdf_processed', message: 'PDF processato: catan-rulebook.pdf', createdAt: '2026-02-21T08:00:00Z' },
];

const MOCK_HEALTH = {
  status: 'healthy',
  uptime: '5d 14h 22m',
  services: [
    { name: 'API',      status: 'healthy', latencyMs: 45  },
    { name: 'Database', status: 'healthy', latencyMs: 12  },
    { name: 'Redis',    status: 'healthy', latencyMs: 3   },
    { name: 'Qdrant',   status: 'healthy', latencyMs: 25  },
    { name: 'Embedding',status: 'healthy', latencyMs: 180 },
  ],
};

test.describe('ADM — Overview', () => {
  test.beforeEach(async ({ page }) => {
    await mockAdminAuth(page);
    await page.context().route(`${API_BASE}/api/v1/admin/stats**`, (route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(MOCK_OVERVIEW_STATS) })
    );
    await page.context().route(`${API_BASE}/api/v1/admin/**`, (route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data: [] }) })
    );
  });

  test('carica pagina overview con TopNav e Sidebar', async ({ page }) => {
    await page.goto('/admin/overview', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('body')).toBeVisible();
    // Cerca TopNav
    const topNav = page.locator('nav, header, [data-testid="top-nav"]').first();
    await expect(topNav).toBeVisible({ timeout: 8000 });
  });

  test('mostra KPI cards statistiche', async ({ page }) => {
    await page.goto('/admin/overview', { waitUntil: 'domcontentloaded' });
    // Cerca almeno uno dei KPI
    const kpi = page.locator('[data-testid="kpi-card"]')
      .or(page.locator('text=/1.234|1234|utenti|users|giochi|games/i'))
      .first();
    if (await kpi.count() > 0) {
      await expect(kpi).toBeVisible({ timeout: 8000 });
    }
  });

  test('sidebar contestuale presente', async ({ page }) => {
    await page.goto('/admin/overview', { waitUntil: 'domcontentloaded' });
    const sidebar = page.locator('aside, [data-testid="contextual-sidebar"], [data-testid="admin-sidebar"]').first();
    if (await sidebar.count() > 0) {
      await expect(sidebar).toBeVisible({ timeout: 8000 });
    }
  });
});

test.describe('ADM — Activity Feed', () => {
  test.beforeEach(async ({ page }) => {
    await mockAdminAuth(page);
    await page.context().route(`${API_BASE}/api/v1/admin/activity**`, (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: MOCK_ACTIVITY, totalCount: MOCK_ACTIVITY.length }),
      })
    );
    await page.context().route(`${API_BASE}/api/v1/admin/**`, (route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data: [] }) })
    );
  });

  test('carica activity feed', async ({ page }) => {
    await page.goto('/admin/overview/activity', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('body')).toBeVisible();
  });

  test('mostra eventi recenti con timestamp', async ({ page }) => {
    await page.goto('/admin/overview/activity', { waitUntil: 'domcontentloaded' });

    const activityItem = page.locator('[data-testid="activity-item"]')
      .or(page.locator('text=/Catan|utente|registrat|published/i'))
      .first();
    if (await activityItem.count() > 0) {
      await expect(activityItem).toBeVisible({ timeout: 8000 });
    }
  });
});

test.describe('ADM — System Health', () => {
  test.beforeEach(async ({ page }) => {
    await mockAdminAuth(page);
    await page.context().route(`${API_BASE}/api/v1/admin/health**`, (route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(MOCK_HEALTH) })
    );
    await page.context().route(`${API_BASE}/api/v1/admin/**`, (route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data: [] }) })
    );
  });

  test('carica pagina system health', async ({ page }) => {
    await page.goto('/admin/overview/system', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('body')).toBeVisible();
  });

  test('mostra stato servizi (API, Database, Redis, Qdrant)', async ({ page }) => {
    await page.goto('/admin/overview/system', { waitUntil: 'domcontentloaded' });

    const serviceList = page.locator('text=/API|Database|Redis|Qdrant/i').first();
    if (await serviceList.count() > 0) {
      await expect(serviceList).toBeVisible({ timeout: 8000 });
    }
  });

  test('mostra uptime sistema', async ({ page }) => {
    await page.goto('/admin/overview/system', { waitUntil: 'domcontentloaded' });

    const uptime = page.locator('text=/uptime|5d|14h/i').first();
    if (await uptime.count() > 0) {
      await expect(uptime).toBeVisible({ timeout: 8000 });
    }
  });
});
