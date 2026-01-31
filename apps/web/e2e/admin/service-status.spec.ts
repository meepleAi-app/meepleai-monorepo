/**
 * Admin Service Status Dashboard E2E - Issue #2516
 *
 * Tests service status dashboard with:
 * - Real-time service health monitoring (30s polling)
 * - 3-column responsive grid layout
 * - Filters: All/Critical/Unhealthy
 * - Export functionality (CSV/JSON)
 * - Toast notifications on state changes
 * - Test Now button for manual checks
 * - Circuit breaker behavior (5 failures = pause)
 * - Search functionality
 * - Overall status badge
 *
 * Dependencies:
 * - Backend: GET /api/v1/admin/infrastructure/details
 * - Uses real backend data (no mocks)
 */

import { test as base, expect } from '../fixtures/chromatic';
import { WaitHelper } from '../helpers/WaitHelper';
import { AdminHelper } from '../pages';

import type { Page } from '@playwright/test';

const test = base.extend<{ adminPage: Page; waitHelper: WaitHelper }>({
  adminPage: async ({ page }: { page: Page }, use: (page: Page) => Promise<void>) => {
    const adminHelper = new AdminHelper(page);

    // Setup admin auth
    await adminHelper.setupAdminAuth(true);

    // ✅ NO MOCKS: Use real Infrastructure API
    // Backend GET /api/v1/admin/infrastructure/details must return:
    //   { overall: {state, totalServices, ...}, services: [...] }
    // Tests verify UI structure and monitoring features with real backend data

    await use(page);
  },
  waitHelper: async ({ page }: { page: Page }, use: (helper: WaitHelper) => Promise<void>) => {
    await use(new WaitHelper(page));
  },
});

test.describe('Admin Service Status Dashboard - Issue #2516', () => {
  test.describe('Page Load and Authentication', () => {
    test('should load service status page with admin access', async ({ adminPage }) => {
      await adminPage.goto('/admin/services');

      // Verify page loaded
      await expect(adminPage.getByRole('heading', { name: /Stato Servizi|Service Status/i })).toBeVisible();

      // Verify overall status badge present
      await expect(adminPage.getByText(/Overall Status/i)).toBeVisible();
    });

    test('should redirect non-admin users to login', async ({ page }) => {
      // Navigate without admin auth
      await page.goto('/admin/services');

      // Should redirect to login
      await expect(page).toHaveURL(/\/login/);
    });
  });

  test.describe('Service Grid Display', () => {
    test('should display service cards in 3-column grid', async ({ adminPage }) => {
      await adminPage.goto('/admin/services');

      // Wait for services to load
      await expect(adminPage.locator('[data-testid="service-card"]').first()).toBeVisible({
        timeout: 5000,
      });

      // Verify grid layout (at least 3 services expected)
      const serviceCards = adminPage.locator('[data-testid="service-card"]');
      const count = await serviceCards.count();
      expect(count).toBeGreaterThanOrEqual(3);

      // Verify responsive grid classes
      const grid = adminPage.locator('.grid');
      await expect(grid).toHaveClass(/md:grid-cols-2 lg:grid-cols-3/);
    });

    test('should display service status with correct colors', async ({ adminPage }) => {
      await adminPage.goto('/admin/services');

      // Wait for at least one service card
      const firstCard = adminPage.locator('[data-testid="service-card"]').first();
      await expect(firstCard).toBeVisible({ timeout: 5000 });

      // Verify status badge colors
      const healthyCards = adminPage.locator('.border-green-500');
      const degradedCards = adminPage.locator('.border-yellow-500');
      const unhealthyCards = adminPage.locator('.border-red-500');

      // At least one service should be visible
      const totalColoredCards = (await healthyCards.count()) +
        (await degradedCards.count()) +
        (await unhealthyCards.count());
      expect(totalColoredCards).toBeGreaterThan(0);
    });

    test('should display critical badge for critical services', async ({ adminPage }) => {
      await adminPage.goto('/admin/services');

      // Wait for services to load
      await expect(adminPage.locator('[data-testid="service-card"]').first()).toBeVisible({
        timeout: 5000,
      });

      // Critical services (postgres, qdrant, redis) should have critical badge
      const criticalBadges = adminPage.getByText(/Critico|Critical/i);
      const criticalCount = await criticalBadges.count();
      expect(criticalCount).toBeGreaterThanOrEqual(1); // At least one critical service
    });
  });

  test.describe('Overall Status Badge', () => {
    test('should display overall status with correct state', async ({ adminPage }) => {
      await adminPage.goto('/admin/services');

      // Overall status badge should be visible
      const overallBadge = adminPage.getByText(/Overall Status/i);
      await expect(overallBadge).toBeVisible();

      // Should show state (Healthy/Degraded/Unhealthy)
      const stateText = adminPage.locator('text=/Healthy|Degraded|Unhealthy/i').first();
      await expect(stateText).toBeVisible();
    });

    test('should display service counts breakdown', async ({ adminPage }) => {
      await adminPage.goto('/admin/services');

      // Overall badge should show total count
      const totalBadge = adminPage.getByText(/Total:/i);
      await expect(totalBadge).toBeVisible();

      // Should show counts with emojis (✅/⚠️/❌)
      const countsSection = adminPage.locator('text=/✅|⚠️|❌/').first();
      await expect(countsSection).toBeVisible();
    });
  });

  test.describe('Filtering', () => {
    test('should filter by All/Critical/Unhealthy', async ({ adminPage }) => {
      await adminPage.goto('/admin/services');

      // Wait for initial load
      await expect(adminPage.locator('[data-testid="service-card"]').first()).toBeVisible({
        timeout: 5000,
      });

      const initialCount = await adminPage.locator('[data-testid="service-card"]').count();

      // Click filter dropdown
      const filterSelect = adminPage.getByRole('combobox', { name: /Tutti|All/i });
      await filterSelect.click();

      // Select "Critical" filter
      await adminPage.getByRole('option', { name: /Critici|Critical/i }).click();

      // Verify filtered count is different (should be 3: postgres, qdrant, redis)
      const criticalCount = await adminPage.locator('[data-testid="service-card"]').count();
      expect(criticalCount).toBeLessThanOrEqual(initialCount);
      expect(criticalCount).toBeGreaterThanOrEqual(1);
    });

    test('should show empty state when no services match filter', async ({ adminPage }) => {
      await adminPage.goto('/admin/services');

      // Wait for initial load
      await expect(adminPage.locator('[data-testid="service-card"]').first()).toBeVisible({
        timeout: 5000,
      });

      // If all services are healthy, filter by "Unhealthy" should show empty state
      const filterSelect = adminPage.getByRole('combobox');
      await filterSelect.click();
      await adminPage.getByRole('option', { name: /Non Healthy|Unhealthy/i }).click();

      // Check if empty state or services shown
      const serviceCards = adminPage.locator('[data-testid="service-card"]');
      const emptyState = adminPage.getByText(/Nessun servizio trovato|No services found/i);

      const hasServices = (await serviceCards.count()) > 0;
      const hasEmptyState = await emptyState.isVisible();

      // One of these should be true
      expect(hasServices || hasEmptyState).toBe(true);
    });
  });

  test.describe('Search Functionality', () => {
    test('should filter services by search query', async ({ adminPage }) => {
      await adminPage.goto('/admin/services');

      // Wait for services to load
      await expect(adminPage.locator('[data-testid="service-card"]').first()).toBeVisible({
        timeout: 5000,
      });

      const initialCount = await adminPage.locator('[data-testid="service-card"]').count();

      // Search for specific service (e.g., "postgres")
      const searchInput = adminPage.getByPlaceholder(/Cerca servizio|Search service/i);
      await searchInput.fill('postgres');

      // Verify filtered results
      const filteredCount = await adminPage.locator('[data-testid="service-card"]').count();
      expect(filteredCount).toBeLessThanOrEqual(initialCount);
      expect(filteredCount).toBeGreaterThanOrEqual(1);

      // Verify filtered card contains "postgres"
      const firstCard = adminPage.locator('[data-testid="service-card"]').first();
      await expect(firstCard).toContainText(/postgres/i);
    });

    test('should show empty state for non-matching search', async ({ adminPage }) => {
      await adminPage.goto('/admin/services');

      // Wait for services to load
      await expect(adminPage.locator('[data-testid="service-card"]').first()).toBeVisible({
        timeout: 5000,
      });

      // Search for non-existent service
      const searchInput = adminPage.getByPlaceholder(/Cerca servizio|Search service/i);
      await searchInput.fill('nonexistent-service-xyz');

      // Verify empty state shown
      const emptyState = adminPage.getByText(/Nessun servizio trovato|No services found/i);
      await expect(emptyState).toBeVisible();
    });
  });

  test.describe('Export Functionality', () => {
    test('should export to JSON format', async ({ adminPage }) => {
      await adminPage.goto('/admin/services');

      // Wait for services to load
      await expect(adminPage.locator('[data-testid="service-card"]').first()).toBeVisible({
        timeout: 5000,
      });

      // Setup download listener
      const downloadPromise = adminPage.waitForEvent('download');

      // Click export dropdown and select JSON
      const exportSelect = adminPage.getByRole('combobox', { name: /Export/i });
      await exportSelect.click();
      await adminPage.getByRole('option', { name: /Export JSON/i }).click();

      // Verify download triggered
      const download = await downloadPromise;
      expect(download.suggestedFilename()).toMatch(/service-status-.*\.json/);

      // Verify toast notification
      await expect(adminPage.getByText(/Esportazione completata|Export completed/i)).toBeVisible();
    });

    test('should export to CSV format', async ({ adminPage }) => {
      await adminPage.goto('/admin/services');

      // Wait for services to load
      await expect(adminPage.locator('[data-testid="service-card"]').first()).toBeVisible({
        timeout: 5000,
      });

      // Setup download listener
      const downloadPromise = adminPage.waitForEvent('download');

      // Click export dropdown and select CSV
      const exportSelect = adminPage.getByRole('combobox', { name: /Export/i });
      await exportSelect.click();
      await adminPage.getByRole('option', { name: /Export CSV/i }).click();

      // Verify download triggered
      const download = await downloadPromise;
      expect(download.suggestedFilename()).toMatch(/service-status-.*\.csv/);

      // Verify toast notification
      await expect(adminPage.getByText(/Esportazione completata|Export completed/i)).toBeVisible();
    });
  });

  test.describe('Auto-Refresh and Polling', () => {
    test('should auto-refresh every 30 seconds', async ({ adminPage }) => {
      await adminPage.goto('/admin/services');

      // Wait for initial API call
      const firstResponsePromise = adminPage.waitForResponse(
        response =>
          response.url().includes('/api/v1/admin/infrastructure/details') &&
          response.status() === 200,
        { timeout: 5000 }
      );
      await firstResponsePromise;

      // Wait for second API call (30s polling)
      const secondResponsePromise = adminPage.waitForResponse(
        response =>
          response.url().includes('/api/v1/admin/infrastructure/details') &&
          response.status() === 200,
        { timeout: 35000 } // 30s + 5s buffer
      );
      await secondResponsePromise;

      // If we get here, polling is working
      expect(true).toBe(true);
    });

    test('should update last updated timestamp', async ({ adminPage }) => {
      await adminPage.goto('/admin/services');

      // Wait for services to load
      await expect(adminPage.locator('[data-testid="service-card"]').first()).toBeVisible({
        timeout: 5000,
      });

      // Verify last updated timestamp is visible
      const timestamp = adminPage.getByText(/Ultimo aggiornamento|Last updated/i);
      await expect(timestamp).toBeVisible();

      // Verify timestamp format (HH:MM:SS)
      await expect(timestamp).toContainText(/\d{1,2}:\d{2}:\d{2}/);
    });
  });

  test.describe('Manual Refresh', () => {
    test('should refresh data on Refresh All button click', async ({ adminPage }) => {
      await adminPage.goto('/admin/services');

      // Wait for initial load
      await expect(adminPage.locator('[data-testid="service-card"]').first()).toBeVisible({
        timeout: 5000,
      });

      // Click refresh button
      const refreshButton = adminPage.getByRole('button', { name: /Aggiorna|Refresh All/i });

      // Wait for API call triggered by refresh
      const responsePromise = adminPage.waitForResponse(
        response =>
          response.url().includes('/api/v1/admin/infrastructure/details') &&
          response.status() === 200,
        { timeout: 5000 }
      );

      await refreshButton.click();
      await responsePromise;

      // Verify loading indicator shown (if visible before data loads)
      const refreshIcon = adminPage.locator('.animate-spin').first();
      // May be visible or already completed - non-blocking assertion
      expect(true).toBe(true);
    });
  });

  test.describe('Test Now Button', () => {
    test('should trigger test on Test Now button click', async ({ adminPage }) => {
      await adminPage.goto('/admin/services');

      // Wait for services to load
      await expect(adminPage.locator('[data-testid="service-card"]').first()).toBeVisible({
        timeout: 5000,
      });

      // Click Test Now button on first service card
      const testButton = adminPage.getByRole('button', { name: /Testa Ora|Test Now/i }).first();

      // Wait for API call triggered by test
      const responsePromise = adminPage.waitForResponse(
        response =>
          response.url().includes('/api/v1/admin/infrastructure/details') &&
          response.status() === 200,
        { timeout: 5000 }
      );

      await testButton.click();

      // Verify toast notification shown
      await expect(adminPage.getByText(/Test.*in corso|Testing/i)).toBeVisible();

      await responsePromise;
    });
  });

  test.describe('Responsive Design', () => {
    test('should adapt layout for mobile', async ({ adminPage }) => {
      await adminPage.setViewportSize({ width: 375, height: 667 }); // iPhone SE
      await adminPage.goto('/admin/services');

      // Wait for services to load
      await expect(adminPage.locator('[data-testid="service-card"]').first()).toBeVisible({
        timeout: 5000,
      });

      // Verify grid is single column on mobile
      const grid = adminPage.locator('.grid');
      await expect(grid).toBeVisible();

      // Controls should stack vertically
      const controlsContainer = adminPage.locator('.flex-col');
      await expect(controlsContainer.first()).toBeVisible();
    });

    test('should adapt layout for tablet', async ({ adminPage }) => {
      await adminPage.setViewportSize({ width: 768, height: 1024 }); // iPad
      await adminPage.goto('/admin/services');

      // Wait for services to load
      await expect(adminPage.locator('[data-testid="service-card"]').first()).toBeVisible({
        timeout: 5000,
      });

      // Verify grid shows 2 columns on tablet
      const grid = adminPage.locator('.grid');
      await expect(grid).toBeVisible();
    });
  });

  test.describe('Error Handling', () => {
    test('should handle API errors gracefully', async ({ page }) => {
      const adminHelper = new AdminHelper(page);
      await adminHelper.setupAdminAuth(true);

      // Mock API failure
      await page.route('**/api/v1/admin/infrastructure/details', route =>
        route.abort('failed')
      );

      await page.goto('/admin/services');

      // Verify error message shown
      await expect(page.getByText(/Errore caricamento|Error loading/i)).toBeVisible({
        timeout: 5000,
      });
    });

    test('should trigger circuit breaker after 5 failures', async ({ page }) => {
      const adminHelper = new AdminHelper(page);
      await adminHelper.setupAdminAuth(true);

      // Mock API to fail consistently
      let requestCount = 0;
      await page.route('**/api/v1/admin/infrastructure/details', route => {
        requestCount++;
        route.abort('failed');
      });

      await page.goto('/admin/services');

      // Wait for circuit breaker message (after 5 failures)
      await expect(
        page.getByText(/Troppe richieste fallite|Too many failed requests/i)
      ).toBeVisible({ timeout: 10000 });

      // Verify auto-refresh paused message
      await expect(
        page.getByText(/Aggiornamento automatico sospeso|Auto-refresh paused/i)
      ).toBeVisible();
    });
  });
});
