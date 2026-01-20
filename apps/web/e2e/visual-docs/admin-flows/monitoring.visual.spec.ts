/**
 * Monitoring - Visual Documentation (Admin Role)
 *
 * Captures visual documentation for system monitoring:
 * - Dashboard overview
 * - System health
 * - Alert management
 * - Audit logs
 *
 * @see docs/08-user-flows/admin-role/04-monitoring.md
 */

import { test } from '../../fixtures/chromatic';
import { AuthHelper, USER_FIXTURES } from '../../pages';
import {
  ScreenshotHelper,
  ADMIN_FLOWS,
  disableAnimations,
  waitForStableState,
  ANNOTATION_COLORS,
} from '../fixtures/screenshot-helpers';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// Mock monitoring data
const MOCK_DASHBOARD_STATS = {
  activeUsers: 1250,
  totalGames: 487,
  documentsProcessed: 2341,
  chatSessions: 8923,
  systemHealth: 'healthy',
  uptime: '99.9%',
};

const MOCK_ALERTS = [
  {
    id: 'alert-1',
    type: 'warning',
    message: 'PDF processing queue backlog exceeding threshold',
    createdAt: '2026-01-19T14:30:00Z',
    acknowledged: false,
  },
  {
    id: 'alert-2',
    type: 'info',
    message: 'New user registration spike detected',
    createdAt: '2026-01-19T12:00:00Z',
    acknowledged: true,
  },
];

const MOCK_AUDIT_LOGS = [
  {
    id: 'log-1',
    action: 'user.tier.changed',
    actor: 'admin@example.com',
    target: 'user@example.com',
    details: 'Tier changed from Free to Premium',
    timestamp: '2026-01-19T15:00:00Z',
  },
  {
    id: 'log-2',
    action: 'game.approved',
    actor: 'admin@example.com',
    target: 'Wingspan',
    details: 'Game approved for publication',
    timestamp: '2026-01-19T14:30:00Z',
  },
];

test.describe('Monitoring - Visual Documentation (Admin)', () => {
  let helper: ScreenshotHelper;
  let authHelper: AuthHelper;

  test.beforeEach(async ({ page }) => {
    helper = new ScreenshotHelper({
      outputDir: ADMIN_FLOWS.monitoring.outputDir,
      flow: ADMIN_FLOWS.monitoring.name,
      role: ADMIN_FLOWS.monitoring.role,
    });
    authHelper = new AuthHelper(page);
    await disableAnimations(page);

    // Setup admin session
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.admin);

    // Mock monitoring endpoints
    await page.route(`${API_BASE}/api/v1/admin/dashboard/stats`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_DASHBOARD_STATS),
      });
    });

    await page.route(`${API_BASE}/api/v1/admin/alerts*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: MOCK_ALERTS, total: 2 }),
      });
    });

    await page.route(`${API_BASE}/api/v1/admin/audit-logs*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: MOCK_AUDIT_LOGS, total: 2 }),
      });
    });

    await page.route(`${API_BASE}/api/v1/admin/system/health`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          status: 'healthy',
          services: {
            database: 'healthy',
            redis: 'healthy',
            qdrant: 'healthy',
            embedding: 'healthy',
          },
        }),
      });
    });
  });

  test('dashboard overview - key metrics', async ({ page }) => {
    // Step 1: Navigate to dashboard
    await page.goto('/admin/dashboard');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'Admin Dashboard',
      description: 'Overview of system metrics and health',
      annotations: [
        { selector: 'h1, [data-testid="dashboard-heading"]', label: 'Dashboard', color: ANNOTATION_COLORS.primary },
      ],
      nextAction: 'Review metrics',
    });

    // Step 2: Stats cards
    const statsSection = page.locator('[data-testid="stats-cards"], .stats-grid, .metrics').first();
    if (await statsSection.isVisible({ timeout: 3000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 2,
        title: 'Key Metrics',
        description: 'User count, games, documents, and sessions',
        annotations: [
          { selector: 'text=/\\d+.*user/i, [data-metric="users"]', label: 'Users', color: ANNOTATION_COLORS.info },
          { selector: 'text=/\\d+.*game/i, [data-metric="games"]', label: 'Games', color: ANNOTATION_COLORS.info },
        ],
        previousAction: 'View dashboard',
        nextAction: 'Check health',
      });
    }

    // Step 3: Health indicator
    const healthStatus = page.locator('text=/healthy|status/i, [data-testid="health-status"]').first();
    if (await healthStatus.isVisible({ timeout: 2000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 3,
        title: 'System Health',
        description: 'Overall system status indicator',
        annotations: [
          { selector: 'text=/healthy/i, [data-testid="health-status"]', label: 'Health', color: ANNOTATION_COLORS.success },
          { selector: 'text=/99/i, [data-metric="uptime"]', label: 'Uptime', color: ANNOTATION_COLORS.success },
        ],
        previousAction: 'View metrics',
        nextAction: 'Check services',
      });
    }

    helper.setTotalSteps(3);
    console.log(`\n✅ Dashboard captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('system health - service status', async ({ page }) => {
    // Step 1: Navigate to health
    await page.goto('/admin/health');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'System Health',
      description: 'Detailed health status of all services',
      nextAction: 'Review services',
    });

    // Step 2: Service status list
    const serviceList = page.locator('[data-testid="service-list"], .services-status, table').first();
    if (await serviceList.isVisible({ timeout: 3000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 2,
        title: 'Service Status',
        description: 'Health status of each system component',
        annotations: [
          { selector: 'text=/database/i', label: 'Database', color: ANNOTATION_COLORS.success },
          { selector: 'text=/redis/i', label: 'Redis', color: ANNOTATION_COLORS.success },
          { selector: 'text=/qdrant/i', label: 'Qdrant', color: ANNOTATION_COLORS.success },
        ],
        previousAction: 'View health',
        nextAction: 'Monitor status',
      });
    }

    helper.setTotalSteps(2);
    console.log(`\n✅ System health captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('alert management - view and acknowledge', async ({ page }) => {
    // Step 1: Navigate to alerts
    await page.goto('/admin/alerts');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'System Alerts',
      description: 'View active and past system alerts',
      annotations: [
        { selector: 'text=/\\d+ alert/i, [data-testid="alert-count"]', label: 'Count', color: ANNOTATION_COLORS.warning },
      ],
      nextAction: 'Review alerts',
    });

    // Step 2: Alert list
    const alertList = page.locator('[data-testid="alert-list"], .alerts-list, ul').first();
    if (await alertList.isVisible({ timeout: 3000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 2,
        title: 'Alert List',
        description: 'Active alerts requiring attention',
        annotations: [
          { selector: '[data-type="warning"], .alert-warning', label: 'Warning', color: ANNOTATION_COLORS.warning },
          { selector: '[data-type="info"], .alert-info', label: 'Info', color: ANNOTATION_COLORS.info },
        ],
        previousAction: 'View alerts',
        nextAction: 'Acknowledge alert',
      });
    }

    // Step 3: Acknowledge button
    const ackBtn = page.locator('button:has-text("Acknowledge"), [data-testid="ack-alert"]').first();
    if (await ackBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 3,
        title: 'Acknowledge Alert',
        description: 'Mark alert as acknowledged',
        annotations: [
          { selector: 'button:has-text("Acknowledge")', label: 'Acknowledge', color: ANNOTATION_COLORS.primary },
        ],
        previousAction: 'View alert',
        nextAction: 'Click acknowledge',
      });
    }

    helper.setTotalSteps(3);
    console.log(`\n✅ Alerts captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('audit logs - activity history', async ({ page }) => {
    // Step 1: Navigate to audit logs
    await page.goto('/admin/audit-logs');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'Audit Logs',
      description: 'Complete history of administrative actions',
      annotations: [
        { selector: 'input[type="search"], [data-testid="log-search"]', label: 'Search', color: ANNOTATION_COLORS.primary },
      ],
      nextAction: 'Browse logs',
    });

    // Step 2: Log entries
    const logTable = page.locator('table, [data-testid="log-table"], .audit-logs').first();
    if (await logTable.isVisible({ timeout: 3000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 2,
        title: 'Log Entries',
        description: 'Detailed log of each administrative action',
        annotations: [
          { selector: 'th, [data-column="action"]', label: 'Action', color: ANNOTATION_COLORS.info },
          { selector: 'th, [data-column="actor"]', label: 'Actor', color: ANNOTATION_COLORS.info },
          { selector: 'th, [data-column="timestamp"]', label: 'Time', color: ANNOTATION_COLORS.info },
        ],
        previousAction: 'View logs',
        nextAction: 'Filter or search',
      });
    }

    // Step 3: Log detail
    const logRow = page.locator('tr, [data-testid="log-entry"]').nth(1);
    if (await logRow.isVisible({ timeout: 2000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 3,
        title: 'Log Entry Detail',
        description: 'Individual audit log entry with full details',
        annotations: [
          { selector: 'text=/tier.*changed|approved/i', label: 'Action', color: ANNOTATION_COLORS.primary },
          { selector: 'text=/admin@/i', label: 'Actor', color: ANNOTATION_COLORS.info },
        ],
        previousAction: 'View entries',
        nextAction: 'View details',
      });
    }

    helper.setTotalSteps(3);
    console.log(`\n✅ Audit logs captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('real-time monitoring - live updates', async ({ page }) => {
    // Step 1: Navigate to monitoring
    await page.goto('/admin/monitoring');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'Real-time Monitoring',
      description: 'Live system metrics and activity',
      nextAction: 'Watch updates',
    });

    // Step 2: Live charts/graphs
    const charts = page.locator('[data-testid="metrics-chart"], .chart, canvas, svg').first();
    if (await charts.isVisible({ timeout: 3000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 2,
        title: 'Live Metrics',
        description: 'Real-time graphs of system performance',
        annotations: [
          { selector: '[data-testid="metrics-chart"], .chart', label: 'Chart', color: ANNOTATION_COLORS.info },
        ],
        previousAction: 'View monitoring',
        nextAction: 'Analyze trends',
      });
    }

    // Step 3: Activity feed
    const activityFeed = page.locator('[data-testid="activity-feed"], .activity-feed').first();
    if (await activityFeed.isVisible({ timeout: 2000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 3,
        title: 'Activity Feed',
        description: 'Live stream of system events',
        annotations: [
          { selector: '[data-testid="activity-feed"]', label: 'Live Feed', color: ANNOTATION_COLORS.primary },
        ],
        previousAction: 'View charts',
        nextAction: 'Monitor activity',
      });
    }

    helper.setTotalSteps(3);
    console.log(`\n✅ Real-time monitoring captured: ${helper.getCapturedSteps().length} screenshots`);
  });
});
