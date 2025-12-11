/**
 * Admin Infrastructure Monitoring E2E - Issue #902
 *
 * Tests complete infrastructure monitoring dashboard with:
 * - Real-time service health matrix (30s polling)
 * - Prometheus metrics charts (CPU, Memory, API requests)
 * - Grafana dashboard embeds (4 dashboards with tab selector)
 * - Service filtering (all/healthy/unhealthy)
 * - Search and sort functionality
 * - Auto-refresh toggle and interval control
 * - Export functionality (CSV/JSON)
 * - Circuit breaker behavior (5 failures = pause)
 * - Error handling and graceful degradation
 * - Responsive design (mobile/tablet/desktop)
 *
 * Dependencies:
 * - Issue #899: Infrastructure page implementation
 * - Issue #901: Grafana embed implementation
 */

import { test as base, expect } from './fixtures/chromatic';
import type { Page } from '@playwright/test';
import { AdminHelper } from './pages';
import { WaitHelper } from './helpers/WaitHelper';

// Mock data generators
function createMockInfrastructureData(
  overrides: {
    healthyCount?: number;
    degradedCount?: number;
    unhealthyCount?: number;
  } = {}
) {
  const healthyCount = overrides.healthyCount ?? 7;
  const degradedCount = overrides.degradedCount ?? 1;
  const unhealthyCount = overrides.unhealthyCount ?? 0;

  const services = [];

  // Healthy services
  for (let i = 0; i < healthyCount; i++) {
    services.push({
      serviceName: `service-healthy-${i}`,
      state: 'Healthy',
      errorMessage: null,
      checkedAt: new Date().toISOString(),
      responseTime: '00:00:00.0150000', // 15ms
    });
  }

  // Degraded services
  for (let i = 0; i < degradedCount; i++) {
    services.push({
      serviceName: `service-degraded-${i}`,
      state: 'Degraded',
      errorMessage: 'High latency detected',
      checkedAt: new Date().toISOString(),
      responseTime: '00:00:02.5000000', // 2.5s
    });
  }

  // Unhealthy services
  for (let i = 0; i < unhealthyCount; i++) {
    services.push({
      serviceName: `service-unhealthy-${i}`,
      state: 'Unhealthy',
      errorMessage: 'Connection timeout',
      checkedAt: new Date().toISOString(),
      responseTime: '00:00:05.0000000', // 5s
    });
  }

  return {
    overall: {
      state: unhealthyCount > 0 ? 'Unhealthy' : degradedCount > 0 ? 'Degraded' : 'Healthy',
      totalServices: healthyCount + degradedCount + unhealthyCount,
      healthyServices: healthyCount,
      degradedServices: degradedCount,
      unhealthyServices: unhealthyCount,
      checkedAt: new Date().toISOString(),
    },
    services,
    prometheusMetrics: {
      apiRequestsLast24h: 45678,
      avgLatencyMs: 245.5,
      errorRate: 0.015,
      llmCostLast24h: 12.45,
    },
  };
}

const test = base.extend<{ adminPage: Page; waitHelper: WaitHelper }>({
  adminPage: async ({ page }: { page: Page }, use: (page: Page) => Promise<void>) => {
    const adminHelper = new AdminHelper(page);

    // Setup admin auth
    await adminHelper.setupAdminAuth(true);

    // Mock infrastructure API with realistic data
    await page.route('**/api/v1/admin/infrastructure/details*', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(createMockInfrastructureData()),
      });
    });

    // Mock Grafana iframe (prevent real iframe loading in tests)
    await page.route('**/grafana/**', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'text/html',
        body: '<html><body><h1>Grafana Dashboard Mock</h1></body></html>',
      });
    });

    await use(page);
  },
  waitHelper: async ({ page }: { page: Page }, use: (helper: WaitHelper) => Promise<void>) => {
    await use(new WaitHelper(page));
  },
});

test.describe('Admin Infrastructure Monitoring - Issue #902', () => {
  test.describe('Page Load and Authentication', () => {
    test('should load infrastructure page with admin access', async ({ adminPage }) => {
      await adminPage.goto('/admin/infrastructure');

      // Verify page loaded
      await expect(adminPage).toHaveTitle(/Infrastructure/i);

      // Verify main sections present
      await expect(
        adminPage.getByRole('heading', { name: /Monitoraggio Infrastruttura/i })
      ).toBeVisible();
      await expect(adminPage.getByText(/Stato Generale/i)).toBeVisible();
    });

    test('should redirect non-admin users to login', async ({ page }) => {
      // Navigate without admin auth
      await page.goto('/admin/infrastructure');

      // Should redirect to login
      await expect(page).toHaveURL(/\/login/);
    });

    test('should display loading state initially', async ({ page }) => {
      const adminHelper = new AdminHelper(page);
      await adminHelper.setupAdminAuth(true);

      // Delay API response to capture loading state
      await page.route('**/api/v1/admin/infrastructure/details*', async route => {
        await new Promise(resolve => setTimeout(resolve, 1000));
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(createMockInfrastructureData()),
        });
      });

      await page.goto('/admin/infrastructure');

      // Verify loading indicator present
      await expect(
        page.locator('[data-testid="loading-spinner"]').or(page.getByText(/Caricamento/i))
      ).toBeVisible();
    });
  });

  test.describe('Service Health Matrix', () => {
    test('should display all services with correct status badges', async ({ adminPage }) => {
      await adminPage.goto('/admin/infrastructure');

      // Wait for data to load
      await expect(adminPage.getByText(/service-healthy-0/i)).toBeVisible({ timeout: 5000 });

      // Verify healthy services have green badge
      const healthyBadge = adminPage
        .locator('[data-service="service-healthy-0"]')
        .locator('[data-status="Healthy"]');
      await expect(healthyBadge).toBeVisible();

      // Verify degraded services have yellow badge
      const degradedBadge = adminPage
        .locator('[data-service="service-degraded-0"]')
        .locator('[data-status="Degraded"]');
      await expect(degradedBadge).toBeVisible();
    });

    test('should display response times for each service', async ({ adminPage }) => {
      await adminPage.goto('/admin/infrastructure');

      await expect(adminPage.getByText(/service-healthy-0/i)).toBeVisible({ timeout: 5000 });

      // Verify response time displayed (e.g., "15ms")
      await expect(adminPage.getByText(/15ms/i)).toBeVisible();
    });

    test('should show error messages for degraded/unhealthy services', async ({
      adminPage,
      page,
    }) => {
      // Override mock with unhealthy service
      await page.route('**/api/v1/admin/infrastructure/details*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(createMockInfrastructureData({ unhealthyCount: 1 })),
        });
      });

      await adminPage.goto('/admin/infrastructure');

      await expect(adminPage.getByText(/service-unhealthy-0/i)).toBeVisible({ timeout: 5000 });

      // Verify error message displayed
      await expect(adminPage.getByText(/Connection timeout/i)).toBeVisible();
    });

    test('should update overall health status based on services', async ({ adminPage, page }) => {
      await page.route('**/api/v1/admin/infrastructure/details*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(
            createMockInfrastructureData({ healthyCount: 5, degradedCount: 2, unhealthyCount: 1 })
          ),
        });
      });

      await adminPage.goto('/admin/infrastructure');

      await expect(adminPage.getByText(/Stato Generale/i)).toBeVisible();

      // Verify overall status shows "Unhealthy" (due to 1 unhealthy service)
      await expect(adminPage.getByText(/Unhealthy/i)).toBeVisible();

      // Verify counts displayed
      await expect(adminPage.getByText(/5.*healthy/i)).toBeVisible();
      await expect(adminPage.getByText(/2.*degraded/i)).toBeVisible();
      await expect(adminPage.getByText(/1.*unhealthy/i)).toBeVisible();
    });
  });

  test.describe('Metrics Charts', () => {
    test('should display Prometheus metrics charts', async ({ adminPage }) => {
      await adminPage.goto('/admin/infrastructure');

      await expect(adminPage.getByText(/service-healthy-0/i)).toBeVisible({ timeout: 5000 });

      // Verify metrics cards present
      await expect(adminPage.getByText(/API Requests/i)).toBeVisible();
      await expect(adminPage.getByText(/Average Latency/i)).toBeVisible();
      await expect(adminPage.getByText(/Error Rate/i)).toBeVisible();
      await expect(adminPage.getByText(/LLM Cost/i)).toBeVisible();
    });

    test('should display correct metric values', async ({ adminPage }) => {
      await adminPage.goto('/admin/infrastructure');

      await expect(adminPage.getByText(/service-healthy-0/i)).toBeVisible({ timeout: 5000 });

      // Verify metric values from mock data
      await expect(adminPage.getByText(/45,?678/)).toBeVisible(); // API requests
      await expect(adminPage.getByText(/245\.5.*ms/i)).toBeVisible(); // Latency
      await expect(adminPage.getByText(/1\.5%/i)).toBeVisible(); // Error rate
      await expect(adminPage.getByText(/\$12\.45/i)).toBeVisible(); // LLM cost
    });

    test('should render charts with Recharts library', async ({ adminPage }) => {
      await adminPage.goto('/admin/infrastructure');

      await expect(adminPage.getByText(/service-healthy-0/i)).toBeVisible({ timeout: 5000 });

      // Verify chart SVG elements present
      const chartSvg = adminPage.locator('svg.recharts-surface').first();
      await expect(chartSvg).toBeVisible();
    });
  });

  test.describe('Grafana Dashboard Embeds', () => {
    test('should display Grafana tab selector', async ({ adminPage }) => {
      await adminPage.goto('/admin/infrastructure');

      await expect(adminPage.getByText(/service-healthy-0/i)).toBeVisible({ timeout: 5000 });

      // Verify Grafana section present
      await expect(adminPage.getByRole('heading', { name: /Grafana Dashboards/i })).toBeVisible();

      // Verify tabs for 4 dashboards
      await expect(adminPage.getByRole('tab', { name: /Overview/i })).toBeVisible();
      await expect(adminPage.getByRole('tab', { name: /API/i })).toBeVisible();
      await expect(adminPage.getByRole('tab', { name: /RAG/i })).toBeVisible();
      await expect(adminPage.getByRole('tab', { name: /Infrastructure/i })).toBeVisible();
    });

    test('should switch between Grafana dashboards', async ({ adminPage }) => {
      await adminPage.goto('/admin/infrastructure');

      await expect(adminPage.getByText(/service-healthy-0/i)).toBeVisible({ timeout: 5000 });

      // Click on API tab
      await adminPage.getByRole('tab', { name: /API/i }).click();

      // Verify iframe src changes (check data-dashboard attribute)
      const iframe = adminPage.locator('iframe[data-testid="grafana-embed"]');
      await expect(iframe).toHaveAttribute('data-dashboard', /api/i);

      // Click on RAG tab
      await adminPage.getByRole('tab', { name: /RAG/i }).click();
      await expect(iframe).toHaveAttribute('data-dashboard', /rag/i);
    });

    test('should display Grafana iframe with correct parameters', async ({ adminPage }) => {
      await adminPage.goto('/admin/infrastructure');

      await expect(adminPage.getByText(/service-healthy-0/i)).toBeVisible({ timeout: 5000 });

      // Verify iframe has kiosk mode and other parameters
      const iframe = adminPage.locator('iframe[data-testid="grafana-embed"]');
      await expect(iframe).toBeVisible();

      // Check iframe src includes Grafana parameters (kiosk, theme, refresh)
      const src = await iframe.getAttribute('src');
      expect(src).toContain('kiosk');
    });

    test('should show refresh button for Grafana embed', async ({ adminPage }) => {
      await adminPage.goto('/admin/infrastructure');

      await expect(adminPage.getByText(/service-healthy-0/i)).toBeVisible({ timeout: 5000 });

      // Verify refresh button present
      const refreshButton = adminPage.getByRole('button', { name: /Refresh.*Dashboard/i });
      await expect(refreshButton).toBeVisible();

      // Click refresh (should reload iframe)
      await refreshButton.click();
    });

    test('should display external link button for Grafana', async ({ adminPage }) => {
      await adminPage.goto('/admin/infrastructure');

      await expect(adminPage.getByText(/service-healthy-0/i)).toBeVisible({ timeout: 5000 });

      // Verify external link button present
      const externalLinkButton = adminPage.getByRole('link', { name: /Open.*Grafana/i });
      await expect(externalLinkButton).toBeVisible();

      // Verify link has correct href
      await expect(externalLinkButton).toHaveAttribute('href', /grafana/i);
      await expect(externalLinkButton).toHaveAttribute('target', '_blank');
    });
  });

  test.describe('Filtering and Search', () => {
    test('should filter services by status (all/healthy/unhealthy)', async ({
      adminPage,
      page,
    }) => {
      await page.route('**/api/v1/admin/infrastructure/details*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(
            createMockInfrastructureData({ healthyCount: 5, degradedCount: 2, unhealthyCount: 1 })
          ),
        });
      });

      await adminPage.goto('/admin/infrastructure');

      await expect(adminPage.getByText(/service-healthy-0/i)).toBeVisible({ timeout: 5000 });

      // All services visible initially (8 total)
      await expect(adminPage.getByText(/service-healthy-0/i)).toBeVisible();
      await expect(adminPage.getByText(/service-degraded-0/i)).toBeVisible();
      await expect(adminPage.getByText(/service-unhealthy-0/i)).toBeVisible();

      // Filter to show only healthy
      await adminPage.getByRole('button', { name: /Filter/i }).click();
      await adminPage.getByRole('option', { name: /Healthy/i }).click();

      // Unhealthy service should be hidden
      await expect(adminPage.getByText(/service-unhealthy-0/i)).not.toBeVisible();
      await expect(adminPage.getByText(/service-healthy-0/i)).toBeVisible();

      // Filter to show only unhealthy
      await adminPage.getByRole('button', { name: /Filter/i }).click();
      await adminPage.getByRole('option', { name: /Unhealthy/i }).click();

      // Only unhealthy services visible
      await expect(adminPage.getByText(/service-unhealthy-0/i)).toBeVisible();
      await expect(adminPage.getByText(/service-healthy-0/i)).not.toBeVisible();
    });

    test('should search services by name', async ({ adminPage }) => {
      await adminPage.goto('/admin/infrastructure');

      await expect(adminPage.getByText(/service-healthy-0/i)).toBeVisible({ timeout: 5000 });

      // Type in search box
      const searchInput = adminPage.getByPlaceholder(/Search.*services/i);
      await searchInput.fill('healthy-1');

      // Only matching service visible
      await expect(adminPage.getByText(/service-healthy-1/i)).toBeVisible();
      await expect(adminPage.getByText(/service-healthy-0/i)).not.toBeVisible();
      await expect(adminPage.getByText(/service-degraded-0/i)).not.toBeVisible();
    });

    test('should clear search on input clear', async ({ adminPage }) => {
      await adminPage.goto('/admin/infrastructure');

      await expect(adminPage.getByText(/service-healthy-0/i)).toBeVisible({ timeout: 5000 });

      const searchInput = adminPage.getByPlaceholder(/Search.*services/i);
      await searchInput.fill('healthy-1');

      await expect(adminPage.getByText(/service-healthy-1/i)).toBeVisible();
      await expect(adminPage.getByText(/service-healthy-0/i)).not.toBeVisible();

      // Clear search
      await searchInput.clear();

      // All services visible again
      await expect(adminPage.getByText(/service-healthy-0/i)).toBeVisible();
      await expect(adminPage.getByText(/service-healthy-1/i)).toBeVisible();
    });
  });

  test.describe('Sorting', () => {
    test('should sort services by name', async ({ adminPage, page }) => {
      await page.route('**/api/v1/admin/infrastructure/details*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            overall: {
              state: 'Healthy',
              totalServices: 3,
              healthyServices: 3,
              degradedServices: 0,
              unhealthyServices: 0,
              checkedAt: new Date().toISOString(),
            },
            services: [
              {
                serviceName: 'zebra-service',
                state: 'Healthy',
                errorMessage: null,
                checkedAt: new Date().toISOString(),
                responseTime: '00:00:00.0150000',
              },
              {
                serviceName: 'alpha-service',
                state: 'Healthy',
                errorMessage: null,
                checkedAt: new Date().toISOString(),
                responseTime: '00:00:00.0150000',
              },
              {
                serviceName: 'beta-service',
                state: 'Healthy',
                errorMessage: null,
                checkedAt: new Date().toISOString(),
                responseTime: '00:00:00.0150000',
              },
            ],
            prometheusMetrics: {
              apiRequestsLast24h: 1000,
              avgLatencyMs: 100,
              errorRate: 0.01,
              llmCostLast24h: 5.0,
            },
          }),
        });
      });

      await adminPage.goto('/admin/infrastructure');

      await expect(adminPage.getByText(/zebra-service/i)).toBeVisible({ timeout: 5000 });

      // Open sort dropdown
      await adminPage.getByRole('button', { name: /Sort/i }).click();
      await adminPage.getByRole('option', { name: /Name/i }).click();

      // Verify alpha-service appears before zebra-service
      const serviceNames = await adminPage.locator('[data-service-name]').allTextContents();
      expect(serviceNames.indexOf('alpha-service')).toBeLessThan(
        serviceNames.indexOf('zebra-service')
      );
    });

    test('should sort services by status', async ({ adminPage, page }) => {
      await page.route('**/api/v1/admin/infrastructure/details*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(
            createMockInfrastructureData({ healthyCount: 2, degradedCount: 1, unhealthyCount: 1 })
          ),
        });
      });

      await adminPage.goto('/admin/infrastructure');

      await expect(adminPage.getByText(/service-healthy-0/i)).toBeVisible({ timeout: 5000 });

      // Sort by status
      await adminPage.getByRole('button', { name: /Sort/i }).click();
      await adminPage.getByRole('option', { name: /Status/i }).click();

      // Unhealthy should appear first (worst status first)
      const firstService = adminPage.locator('[data-service]').first();
      await expect(firstService).toHaveAttribute('data-service', /unhealthy/i);
    });

    test('should sort services by response time', async ({ adminPage, page }) => {
      await page.route('**/api/v1/admin/infrastructure/details*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            overall: {
              state: 'Healthy',
              totalServices: 3,
              healthyServices: 3,
              degradedServices: 0,
              unhealthyServices: 0,
              checkedAt: new Date().toISOString(),
            },
            services: [
              {
                serviceName: 'fast-service',
                state: 'Healthy',
                errorMessage: null,
                checkedAt: new Date().toISOString(),
                responseTime: '00:00:00.0050000',
              },
              {
                serviceName: 'slow-service',
                state: 'Healthy',
                errorMessage: null,
                checkedAt: new Date().toISOString(),
                responseTime: '00:00:02.0000000',
              },
              {
                serviceName: 'medium-service',
                state: 'Healthy',
                errorMessage: null,
                checkedAt: new Date().toISOString(),
                responseTime: '00:00:00.5000000',
              },
            ],
            prometheusMetrics: {
              apiRequestsLast24h: 1000,
              avgLatencyMs: 100,
              errorRate: 0.01,
              llmCostLast24h: 5.0,
            },
          }),
        });
      });

      await adminPage.goto('/admin/infrastructure');

      await expect(adminPage.getByText(/fast-service/i)).toBeVisible({ timeout: 5000 });

      // Sort by response time
      await adminPage.getByRole('button', { name: /Sort/i }).click();
      await adminPage.getByRole('option', { name: /Response.*Time/i }).click();

      // slow-service should appear first (slowest first for visibility)
      const firstService = adminPage.locator('[data-service]').first();
      await expect(firstService).toHaveAttribute('data-service', /slow/i);
    });
  });

  test.describe('Auto-refresh and Polling', () => {
    test('should auto-refresh data every 30 seconds by default', async ({
      adminPage,
      page,
      waitHelper,
    }) => {
      let requestCount = 0;

      await page.route('**/api/v1/admin/infrastructure/details*', async route => {
        requestCount++;
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(createMockInfrastructureData()),
        });
      });

      await adminPage.goto('/admin/infrastructure');

      await expect(adminPage.getByText(/service-healthy-0/i)).toBeVisible({ timeout: 5000 });

      // Initial request
      expect(requestCount).toBe(1);

      // Wait for auto-refresh (30s + buffer)
      await adminPage.waitForTimeout(32000);

      // Should have made 2nd request
      expect(requestCount).toBeGreaterThanOrEqual(2);
    });

    test('should toggle auto-refresh on/off', async ({ adminPage, page }) => {
      let requestCount = 0;

      await page.route('**/api/v1/admin/infrastructure/details*', async route => {
        requestCount++;
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(createMockInfrastructureData()),
        });
      });

      await adminPage.goto('/admin/infrastructure');

      await expect(adminPage.getByText(/service-healthy-0/i)).toBeVisible({ timeout: 5000 });

      // Disable auto-refresh
      const autoRefreshToggle = adminPage.getByRole('switch', { name: /Auto.*Refresh/i });
      await autoRefreshToggle.click();

      const initialCount = requestCount;

      // Wait 32s (longer than refresh interval)
      await adminPage.waitForTimeout(32000);

      // Request count should not increase (auto-refresh disabled)
      expect(requestCount).toBe(initialCount);
    });

    test('should change refresh interval', async ({ adminPage }) => {
      await adminPage.goto('/admin/infrastructure');

      await expect(adminPage.getByText(/service-healthy-0/i)).toBeVisible({ timeout: 5000 });

      // Open interval selector
      await adminPage.getByRole('button', { name: /30.*seconds/i }).click();
      await adminPage.getByRole('option', { name: /60.*seconds/i }).click();

      // Verify interval changed (check displayed text)
      await expect(adminPage.getByText(/60.*seconds/i)).toBeVisible();
    });

    test('should display last updated timestamp', async ({ adminPage }) => {
      await adminPage.goto('/admin/infrastructure');

      await expect(adminPage.getByText(/service-healthy-0/i)).toBeVisible({ timeout: 5000 });

      // Verify "Last updated" timestamp present
      await expect(adminPage.getByText(/Last.*updated/i)).toBeVisible();
      await expect(adminPage.getByText(/ago/i)).toBeVisible(); // Relative time format
    });

    test('should show manual refresh button', async ({ adminPage, page }) => {
      let requestCount = 0;

      await page.route('**/api/v1/admin/infrastructure/details*', async route => {
        requestCount++;
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(createMockInfrastructureData()),
        });
      });

      await adminPage.goto('/admin/infrastructure');

      await expect(adminPage.getByText(/service-healthy-0/i)).toBeVisible({ timeout: 5000 });

      const initialCount = requestCount;

      // Click manual refresh button
      const refreshButton = adminPage.getByRole('button', { name: /Refresh/i }).first();
      await refreshButton.click();

      // Wait for request
      await adminPage.waitForTimeout(1000);

      // Request count should increase
      expect(requestCount).toBeGreaterThan(initialCount);
    });
  });

  test.describe('Export Functionality', () => {
    test('should export service data as CSV', async ({ adminPage, page }) => {
      await adminPage.goto('/admin/infrastructure');

      await expect(adminPage.getByText(/service-healthy-0/i)).toBeVisible({ timeout: 5000 });

      // Set up download listener
      const downloadPromise = page.waitForEvent('download');

      // Click export button and select CSV
      await adminPage.getByRole('button', { name: /Export/i }).click();
      await adminPage.getByRole('option', { name: /CSV/i }).click();

      // Wait for download
      const download = await downloadPromise;

      // Verify filename
      expect(download.suggestedFilename()).toMatch(/infrastructure.*\.csv/i);
    });

    test('should export service data as JSON', async ({ adminPage, page }) => {
      await adminPage.goto('/admin/infrastructure');

      await expect(adminPage.getByText(/service-healthy-0/i)).toBeVisible({ timeout: 5000 });

      // Set up download listener
      const downloadPromise = page.waitForEvent('download');

      // Click export button and select JSON
      await adminPage.getByRole('button', { name: /Export/i }).click();
      await adminPage.getByRole('option', { name: /JSON/i }).click();

      // Wait for download
      const download = await downloadPromise;

      // Verify filename
      expect(download.suggestedFilename()).toMatch(/infrastructure.*\.json/i);
    });

    test('should include all service data in export', async ({ adminPage, page }) => {
      await adminPage.goto('/admin/infrastructure');

      await expect(adminPage.getByText(/service-healthy-0/i)).toBeVisible({ timeout: 5000 });

      // Set up download listener
      const downloadPromise = page.waitForEvent('download');

      await adminPage.getByRole('button', { name: /Export/i }).click();
      await adminPage.getByRole('option', { name: /JSON/i }).click();

      const download = await downloadPromise;

      // Read download content
      const path = await download.path();
      const fs = require('fs');
      const content = fs.readFileSync(path!, 'utf-8');
      const data = JSON.parse(content);

      // Verify structure
      expect(data).toHaveProperty('overall');
      expect(data).toHaveProperty('services');
      expect(data).toHaveProperty('prometheusMetrics');
      expect(data.services.length).toBeGreaterThan(0);
    });
  });

  test.describe('Circuit Breaker', () => {
    test('should pause polling after 5 consecutive failures', async ({ adminPage, page }) => {
      let requestCount = 0;

      await page.route('**/api/v1/admin/infrastructure/details*', async route => {
        requestCount++;
        // Fail all requests
        await route.fulfill({
          status: 500,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Internal Server Error' }),
        });
      });

      await adminPage.goto('/admin/infrastructure');

      // Wait for 5 failed requests + some buffer
      await adminPage.waitForTimeout(10000);

      // Verify error message about circuit breaker
      await expect(
        adminPage.getByText(/Circuit breaker.*open/i).or(adminPage.getByText(/Too many failures/i))
      ).toBeVisible();

      const failedRequestCount = requestCount;

      // Wait longer - should NOT make more requests (circuit open)
      await adminPage.waitForTimeout(35000);

      // Request count should not significantly increase
      expect(requestCount).toBeLessThanOrEqual(failedRequestCount + 1);
    });

    test('should reset circuit breaker on successful request', async ({ adminPage, page }) => {
      let requestCount = 0;
      let shouldFail = true;

      await page.route('**/api/v1/admin/infrastructure/details*', async route => {
        requestCount++;

        if (shouldFail && requestCount <= 3) {
          await route.fulfill({
            status: 500,
            contentType: 'application/json',
            body: JSON.stringify({ error: 'Internal Server Error' }),
          });
        } else {
          // Start succeeding after 3 failures
          shouldFail = false;
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify(createMockInfrastructureData()),
          });
        }
      });

      await adminPage.goto('/admin/infrastructure');

      // Wait for recovery
      await adminPage.waitForTimeout(5000);

      // Should show data (circuit breaker reset)
      await expect(adminPage.getByText(/service-healthy-0/i)).toBeVisible({ timeout: 10000 });
    });
  });

  test.describe('Error Handling', () => {
    test('should display error message on API failure', async ({ adminPage, page }) => {
      await page.route('**/api/v1/admin/infrastructure/details*', async route => {
        await route.fulfill({
          status: 500,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Database connection failed' }),
        });
      });

      await adminPage.goto('/admin/infrastructure');

      // Verify error message displayed
      await expect(
        adminPage.getByText(/Error.*loading/i).or(adminPage.getByText(/Failed.*fetch/i))
      ).toBeVisible({ timeout: 5000 });
    });

    test('should handle network timeout gracefully', async ({ adminPage, page }) => {
      await page.route('**/api/v1/admin/infrastructure/details*', async route => {
        // Simulate timeout
        await new Promise(resolve => setTimeout(resolve, 65000));
        await route.abort('timedout');
      });

      await adminPage.goto('/admin/infrastructure');

      // Verify timeout error displayed
      await expect(
        adminPage.getByText(/Timeout/i).or(adminPage.getByText(/Network.*error/i))
      ).toBeVisible({ timeout: 70000 });
    });

    test('should show retry button on error', async ({ adminPage, page }) => {
      let failFirst = true;

      await page.route('**/api/v1/admin/infrastructure/details*', async route => {
        if (failFirst) {
          failFirst = false;
          await route.fulfill({
            status: 500,
            contentType: 'application/json',
            body: JSON.stringify({ error: 'Server error' }),
          });
        } else {
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify(createMockInfrastructureData()),
          });
        }
      });

      await adminPage.goto('/admin/infrastructure');

      // Verify error displayed
      await expect(adminPage.getByText(/Error/i)).toBeVisible({ timeout: 5000 });

      // Click retry button
      const retryButton = adminPage.getByRole('button', { name: /Retry/i });
      await retryButton.click();

      // Should now show data
      await expect(adminPage.getByText(/service-healthy-0/i)).toBeVisible({ timeout: 5000 });
    });
  });

  test.describe('Responsive Design', () => {
    test('should adapt layout for mobile viewport', async ({ adminPage, page }) => {
      await adminPage.goto('/admin/infrastructure');

      await expect(adminPage.getByText(/service-healthy-0/i)).toBeVisible({ timeout: 5000 });

      // Switch to mobile viewport
      await page.setViewportSize({ width: 390, height: 844 });

      // Verify mobile layout
      await expect(
        adminPage
          .locator('[data-testid="mobile-nav"]')
          .or(adminPage.locator('button[aria-label="Menu"]'))
      ).toBeVisible();

      // Metrics should stack vertically
      const metricsContainer = adminPage.locator('[data-testid="metrics-container"]');
      const boundingBox = await metricsContainer.boundingBox();

      if (boundingBox) {
        // Height should be greater than width (stacked layout)
        expect(boundingBox.height).toBeGreaterThan(boundingBox.width / 2);
      }
    });

    test('should adapt layout for tablet viewport', async ({ adminPage, page }) => {
      await page.setViewportSize({ width: 1024, height: 1366 });

      await adminPage.goto('/admin/infrastructure');

      await expect(adminPage.getByText(/service-healthy-0/i)).toBeVisible({ timeout: 5000 });

      // Verify tablet layout (2-column grid)
      const servicesGrid = adminPage.locator('[data-testid="services-grid"]');
      await expect(servicesGrid).toHaveCSS('grid-template-columns', /repeat\(2,/);
    });

    test('should maintain full desktop layout', async ({ adminPage, page }) => {
      await page.setViewportSize({ width: 1920, height: 1080 });

      await adminPage.goto('/admin/infrastructure');

      await expect(adminPage.getByText(/service-healthy-0/i)).toBeVisible({ timeout: 5000 });

      // Verify desktop layout (3+ column grid or flex)
      await expect(adminPage.locator('[data-testid="desktop-sidebar"]')).toBeVisible();
    });
  });

  test.describe('Performance', () => {
    test('should load page within 2 seconds', async ({ adminPage, page }) => {
      const startTime = Date.now();

      await adminPage.goto('/admin/infrastructure');

      await expect(adminPage.getByText(/service-healthy-0/i)).toBeVisible({ timeout: 5000 });

      const loadTime = Date.now() - startTime;

      // Verify load time < 2s
      expect(loadTime).toBeLessThan(2000);
    });

    test('should achieve TTI within 3 seconds', async ({ adminPage, page }) => {
      await adminPage.goto('/admin/infrastructure');

      const startTime = Date.now();

      // Wait for interactive elements
      await adminPage
        .getByRole('button', { name: /Refresh/i })
        .first()
        .isEnabled();

      const ttiTime = Date.now() - startTime;

      // Verify TTI < 3s
      expect(ttiTime).toBeLessThan(3000);
    });
  });

  test.describe('Accessibility', () => {
    test('should have no critical accessibility violations', async ({ adminPage }) => {
      await adminPage.goto('/admin/infrastructure');

      await expect(adminPage.getByText(/service-healthy-0/i)).toBeVisible({ timeout: 5000 });

      // Run axe accessibility scan
      const { AxePuppeteer } = require('@axe-core/puppeteer');
      const results = await new AxePuppeteer(adminPage).analyze();

      // Check for critical violations
      const criticalViolations = results.violations.filter(
        (v: { impact: string }) => v.impact === 'critical' || v.impact === 'serious'
      );

      expect(criticalViolations.length).toBe(0);
    });

    test('should support keyboard navigation', async ({ adminPage }) => {
      await adminPage.goto('/admin/infrastructure');

      await expect(adminPage.getByText(/service-healthy-0/i)).toBeVisible({ timeout: 5000 });

      // Tab through interactive elements
      await adminPage.keyboard.press('Tab');

      // Verify focus visible
      const focusedElement = adminPage.locator(':focus');
      await expect(focusedElement).toBeVisible();

      // Navigate to filter button via keyboard
      let tabCount = 0;
      while (tabCount < 10) {
        await adminPage.keyboard.press('Tab');
        const isFilterButton = await adminPage
          .getByRole('button', { name: /Filter/i })
          .evaluate(
            (el, focused) => el === focused,
            await adminPage.evaluateHandle(() => document.activeElement)
          );

        if (isFilterButton) break;
        tabCount++;
      }

      // Press Enter to activate
      await adminPage.keyboard.press('Enter');
    });

    test('should have proper ARIA labels', async ({ adminPage }) => {
      await adminPage.goto('/admin/infrastructure');

      await expect(adminPage.getByText(/service-healthy-0/i)).toBeVisible({ timeout: 5000 });

      // Verify main regions have labels
      await expect(adminPage.getByRole('region', { name: /Infrastructure/i })).toBeVisible();
      await expect(adminPage.getByRole('button', { name: /Refresh/i })).toHaveAttribute(
        'aria-label'
      );
    });
  });
});
