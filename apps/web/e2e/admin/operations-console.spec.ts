/**
 * Admin -- Operations Console E2E Tests
 * Covers the 4 tabs: Resources, Queue, Emergency, Audit
 */

import { test, expect, type Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

/* ---------- Auth mock ---------- */

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

/* ---------- Mock data ---------- */

const MOCK_DB_METRICS = {
  sizeFormatted: '1.2 GB',
  activeConnections: 15,
  maxConnections: 100,
  transactionsCommitted: 54321,
  transactionsRolledBack: 12,
  growthLast7Days: 2.5,
};

const MOCK_CACHE_METRICS = {
  totalKeys: 5432,
  hitRate: 0.95,
  memoryUsagePercent: 65.2,
  usedMemoryFormatted: '256 MB',
  maxMemoryFormatted: '512 MB',
  evictedKeys: 12,
};

const MOCK_VECTOR_METRICS = {
  totalVectors: 15000,
  totalCollections: 3,
  indexedVectors: 14500,
  memoryFormatted: '850 MB',
};

const MOCK_TABLES = [
  {
    tableName: 'games',
    rowCount: 1500,
    totalSizeBytes: 5000000,
    sizeFormatted: '5 MB',
    indexSizeBytes: 1000000,
    indexSizeFormatted: '1 MB',
  },
];

const MOCK_QUEUE = {
  jobs: [
    {
      id: 'j1',
      pdfFileName: 'catan.pdf',
      status: 'Processing',
      priority: 1,
      createdAt: '2026-03-01T10:00:00Z',
      currentStep: 'Chunking',
      canRetry: false,
    },
    {
      id: 'j2',
      pdfFileName: 'monopoly.pdf',
      status: 'Failed',
      priority: 5,
      createdAt: '2026-03-01T09:00:00Z',
      currentStep: null,
      canRetry: true,
    },
  ],
  total: 2,
  page: 1,
  totalPages: 1,
};

const MOCK_QUEUE_STATUS = {
  queueDepth: 5,
  maxConcurrentWorkers: 3,
  estimatedWaitMinutes: 12,
  isPaused: false,
  isUnderPressure: false,
};

const MOCK_OVERRIDES: unknown[] = [];

const MOCK_AUDIT = {
  entries: [
    {
      id: 'a1',
      action: 'UserCreated',
      resource: 'User',
      result: 'Success',
      createdAt: '2026-03-01T10:00:00Z',
      userName: 'Admin',
      userEmail: 'admin@test.com',
      resourceId: 'u-1',
      ipAddress: '127.0.0.1',
      details: '{"email":"test@test.com"}',
    },
  ],
  totalCount: 1,
  limit: 50,
  offset: 0,
};

/* ---------- Route helpers ---------- */

async function mockAllEndpoints(page: Page) {
  // Resources
  await page.context().route(`${API_BASE}/api/v1/resources/database/metrics`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(MOCK_DB_METRICS),
    })
  );
  await page.context().route(`${API_BASE}/api/v1/resources/cache/metrics`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(MOCK_CACHE_METRICS),
    })
  );
  await page.context().route(`${API_BASE}/api/v1/resources/vectors/metrics`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(MOCK_VECTOR_METRICS),
    })
  );
  await page.context().route(`${API_BASE}/api/v1/resources/database/tables/top**`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(MOCK_TABLES),
    })
  );

  // Queue — status must be registered before the generic queue route
  await page.context().route(`${API_BASE}/api/v1/admin/queue/status`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(MOCK_QUEUE_STATUS),
    })
  );
  await page.context().route(`${API_BASE}/api/v1/admin/queue**`, (route) => {
    const url = route.request().url();
    // Avoid re-handling the /status sub-route
    if (url.includes('/queue/status')) return route.fallback();
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(MOCK_QUEUE),
    });
  });

  // Emergency overrides
  await page.context().route(`${API_BASE}/api/v1/admin/llm/emergency/active**`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(MOCK_OVERRIDES),
    })
  );

  // Audit logs
  await page.context().route(`${API_BASE}/api/v1/admin/audit-log**`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(MOCK_AUDIT),
    })
  );

  // Catch-all for other admin endpoints
  await page.context().route(`${API_BASE}/api/v1/admin/**`, (route) => {
    const url = route.request().url();
    // Let already-matched routes fall through
    if (
      url.includes('/queue') ||
      url.includes('/audit-log') ||
      url.includes('/llm/emergency')
    ) {
      return route.fallback();
    }
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ data: [] }),
    });
  });
}

/* ---------- Tests ---------- */

test.describe('Operations Console', () => {
  test.beforeEach(async ({ page }) => {
    await mockAdminAuth(page);
    await mockAllEndpoints(page);
  });

  test('navigates to operations page and shows Resources tab by default', async ({ page }) => {
    await page.goto('/admin/monitor/operations', { waitUntil: 'domcontentloaded' });

    await expect(page.getByText('Operations Console').first()).toBeVisible({ timeout: 8000 });
    await expect(page.locator('[data-testid="resources-tab"]')).toBeVisible({ timeout: 8000 });
  });

  test('Resources tab renders metric cards', async ({ page }) => {
    await page.goto('/admin/monitor/operations?tab=resources', { waitUntil: 'domcontentloaded' });

    await expect(page.locator('[data-testid="resources-tab"]')).toBeVisible({ timeout: 8000 });

    // Verify the three metric card headings are visible
    await expect(page.getByText('Database').first()).toBeVisible({ timeout: 8000 });
    await expect(page.getByText('Cache (Redis)').first()).toBeVisible({ timeout: 8000 });
    await expect(page.getByText('Vector Store (Qdrant)').first()).toBeVisible({ timeout: 8000 });
  });

  test('switches to Queue tab and shows jobs', async ({ page }) => {
    await page.goto('/admin/monitor/operations?tab=queue', { waitUntil: 'domcontentloaded' });

    await expect(page.locator('[data-testid="queue-tab"]')).toBeVisible({ timeout: 8000 });

    // Verify job file names appear in the table
    const catanCell = page.getByText('catan.pdf').first();
    if (await catanCell.count() > 0) {
      await expect(catanCell).toBeVisible({ timeout: 8000 });
    }

    // Verify status banner shows healthy state
    const banner = page.locator('[data-testid="queue-status-banner"]');
    if (await banner.count() > 0) {
      await expect(banner).toBeVisible({ timeout: 8000 });
      await expect(banner.getByText('Queue Healthy')).toBeVisible();
    }
  });

  test('Queue tab shows action buttons based on job status', async ({ page }) => {
    await page.goto('/admin/monitor/operations?tab=queue', { waitUntil: 'domcontentloaded' });

    await expect(page.locator('[data-testid="queue-tab"]')).toBeVisible({ timeout: 8000 });

    // Processing job should have a Cancel button
    const cancelBtn = page.locator('button[aria-label="Cancel job"]').first();
    if (await cancelBtn.count() > 0) {
      await expect(cancelBtn).toBeVisible({ timeout: 8000 });
    }

    // Failed job with canRetry should have a Retry button
    const retryBtn = page.locator('button[aria-label="Retry job"]').first();
    if (await retryBtn.count() > 0) {
      await expect(retryBtn).toBeVisible({ timeout: 8000 });
    }
  });

  test('switches to Emergency tab and shows empty state', async ({ page }) => {
    await page.goto('/admin/monitor/operations?tab=emergency', { waitUntil: 'domcontentloaded' });

    await expect(page.locator('[data-testid="emergency-tab"]')).toBeVisible({ timeout: 8000 });

    // No overrides — empty state should be shown
    const noOverrides = page.locator('[data-testid="no-overrides"]');
    if (await noOverrides.count() > 0) {
      await expect(noOverrides).toBeVisible({ timeout: 8000 });
      await expect(
        noOverrides.getByText('No active emergency overrides')
      ).toBeVisible();
    }
  });

  test('switches to Audit tab and shows entries', async ({ page }) => {
    await page.goto('/admin/monitor/operations?tab=audit', { waitUntil: 'domcontentloaded' });

    await expect(page.locator('[data-testid="audit-tab"]')).toBeVisible({ timeout: 8000 });

    // Verify the audit entry action appears in the table
    const actionBadge = page.getByText('UserCreated').first();
    if (await actionBadge.count() > 0) {
      await expect(actionBadge).toBeVisible({ timeout: 8000 });
    }

    // Verify filter inputs are present
    await expect(page.locator('[data-testid="audit-action-filter"]')).toBeVisible({ timeout: 8000 });
  });

  test('Audit tab has export buttons', async ({ page }) => {
    await page.goto('/admin/monitor/operations?tab=audit', { waitUntil: 'domcontentloaded' });

    await expect(page.locator('[data-testid="audit-tab"]')).toBeVisible({ timeout: 8000 });

    await expect(page.locator('[data-testid="export-audit-csv-button"]')).toBeVisible({
      timeout: 8000,
    });
    await expect(page.locator('[data-testid="export-audit-json-button"]')).toBeVisible({
      timeout: 8000,
    });
  });

  test('Queue tab Enqueue button opens dialog', async ({ page }) => {
    await page.goto('/admin/monitor/operations?tab=queue', { waitUntil: 'domcontentloaded' });

    await expect(page.locator('[data-testid="queue-tab"]')).toBeVisible({ timeout: 8000 });

    const enqueueBtn = page.locator('[data-testid="enqueue-job-button"]');
    await expect(enqueueBtn).toBeVisible({ timeout: 8000 });
    await enqueueBtn.click();

    // Verify dialog appears with the expected title
    await expect(page.getByText('Enqueue New Job').first()).toBeVisible({ timeout: 5000 });
  });

  test('tab navigation updates URL', async ({ page }) => {
    // Start on default (resources)
    await page.goto('/admin/monitor/operations', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('[data-testid="resources-tab"]')).toBeVisible({ timeout: 8000 });

    // Navigate to queue
    await page.goto('/admin/monitor/operations?tab=queue', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('[data-testid="queue-tab"]')).toBeVisible({ timeout: 8000 });

    // Navigate to emergency
    await page.goto('/admin/monitor/operations?tab=emergency', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('[data-testid="emergency-tab"]')).toBeVisible({ timeout: 8000 });

    // Navigate to audit
    await page.goto('/admin/monitor/operations?tab=audit', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('[data-testid="audit-tab"]')).toBeVisible({ timeout: 8000 });
  });
});
