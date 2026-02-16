/**
 * Issue #4204: E2E Workflow Tests - Admin Actions
 *
 * Tests 8 admin workflows on the dashboard page using mock API responses:
 * 1. Approve game (single)
 * 2. Reject game (single)
 * 3. Bulk operations (select all → batch approve/reject)
 * 4. Suspend user
 * 5. View user detail (Sheet panel)
 * 6. Search/filters (debounced search, status/role/tier filters)
 * 7. Grid/list toggle
 * 8. Cross-browser (Playwright projects handle this automatically)
 *
 * Uses glob-based route mocking (**\/api/v1/**) to intercept browser-level
 * requests that go through the Next.js proxy, not directly to the backend.
 *
 * Run: pnpm exec playwright test e2e/admin/admin-workflow-actions.spec.ts
 */

import { test, expect, Page } from '@playwright/test';

// =============================================================================
// Mock Data Factories
// =============================================================================

function makeApprovalItem(overrides: Partial<ApprovalQueueItem> = {}): ApprovalQueueItem {
  const id = overrides.gameId ?? `game-${Math.random().toString(36).slice(2, 8)}`;
  return {
    gameId: id,
    title: `Test Game ${id.slice(-4)}`,
    submittedBy: 'submitter-001',
    submittedByName: 'Test User',
    submittedByEmail: 'user@test.com',
    submittedAt: new Date(Date.now() - 3 * 86400000).toISOString(),
    daysPending: 3,
    pdfCount: 2,
    ...overrides,
  };
}

interface ApprovalQueueItem {
  gameId: string;
  title: string;
  submittedBy: string;
  submittedByName: string;
  submittedByEmail: string;
  submittedAt: string;
  daysPending: number;
  pdfCount: number;
}

/** Mock user matching RawUserDto shape from backend (mapped via mapRawUserToUser) */
function makeUser(overrides: Partial<MockUser> = {}): MockUser {
  const id = overrides.id ?? `user-${Math.random().toString(36).slice(2, 8)}`;
  return {
    id,
    displayName: `User ${id.slice(-4)}`,
    email: `${id}@test.com`,
    role: 'User',
    tier: 'free',
    level: 5,
    experiencePoints: 2500,
    isSuspended: false,
    suspendReason: null,
    createdAt: new Date().toISOString(),
    lastSeenAt: new Date().toISOString(),
    ...overrides,
  };
}

/** Matches RawUserDto from admin-client.ts */
interface MockUser {
  id: string;
  displayName: string;
  email: string;
  role: string;
  tier: string;
  level: number;
  experiencePoints: number;
  isSuspended: boolean;
  suspendReason: string | null;
  createdAt: string;
  lastSeenAt: string | null;
}

function makePagedResult<T>(items: T[], totalCount?: number) {
  return {
    items,
    totalCount: totalCount ?? items.length,
    page: 1,
    pageSize: 10,
    totalPages: 1,
  };
}

// =============================================================================
// Admin Auth & Mock Setup
// =============================================================================

/**
 * Sets up admin authentication using cookies + glob-based route mocks.
 *
 * Browser API calls go to /api/v1/... → http://localhost:3000/api/v1/...
 * (Next.js proxy), NOT directly to http://localhost:8080.
 * We use ** glob patterns to match regardless of origin.
 */
async function setupAdminAuth(page: Page): Promise<void> {
  const context = page.context();

  // =========================================================================
  // Universal API catch-all (LOWEST priority - registered FIRST).
  // Prevents unmocked API requests from reaching the real backend.
  // =========================================================================
  await context.route('**/api/**', async (route) => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
  });

  // Set session cookies so Next.js middleware passes through
  await context.addCookies([
    {
      name: 'meepleai_session',
      value: 'mock-admin-session',
      domain: 'localhost',
      path: '/',
      httpOnly: true,
      secure: false,
      sameSite: 'Lax',
    },
    {
      name: 'meepleai_user_role',
      value: 'admin',
      domain: 'localhost',
      path: '/',
      httpOnly: false,
      secure: false,
      sameSite: 'Lax',
    },
  ]);

  // Mock auth/me (used by RequireRole component)
  await context.route('**/api/v1/auth/me', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: {
          id: '00000000-0000-4000-a000-000000000001',
          email: 'admin@meepleai.dev',
          displayName: 'Test Admin',
          role: 'Admin',
        },
      }),
    });
  });

  // Mock session/status (prevents redirect to /login)
  await context.route('**/api/v1/auth/session/status', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        expiresAt: new Date(Date.now() + 3600000).toISOString(),
        lastSeenAt: new Date().toISOString(),
        remainingMinutes: 60,
      }),
    });
  });
}

/**
 * Mocks admin dashboard API endpoints using context-level routes.
 *
 * CRITICAL: Uses page.context().route() instead of page.route() because
 * page-level route handlers registered before page.goto() do NOT reliably
 * intercept subsequent client-side fetch() calls in Playwright 1.57+ with
 * Next.js dev server. Context-level routes intercept ALL requests regardless
 * of when they were registered relative to navigation.
 *
 * Per-test overrides can still use page.route() (page routes take priority
 * over context routes for the same URL pattern via LIFO).
 */
async function setupDashboardMocks(
  page: Page,
  options: {
    approvalItems?: ApprovalQueueItem[];
    users?: MockUser[];
  } = {}
): Promise<void> {
  const context = page.context();
  const approvalItems = options.approvalItems ?? [
    makeApprovalItem({ gameId: 'game-001', title: 'Catan', daysPending: 3 }),
    makeApprovalItem({ gameId: 'game-002', title: 'Ticket to Ride', daysPending: 8 }),
    makeApprovalItem({ gameId: 'game-003', title: 'Pandemic', daysPending: 1 }),
  ];
  const users = options.users ?? [
    makeUser({ id: 'user-001', displayName: 'Alice Smith', email: 'alice@test.com', role: 'User', tier: 'premium' }),
    makeUser({ id: 'user-002', displayName: 'Bob Jones', email: 'bob@test.com', role: 'Admin', tier: 'normal' }),
    makeUser({ id: 'user-003', displayName: 'Carol Brown', email: 'carol@test.com', role: 'User', tier: 'free', isSuspended: true, suspendReason: 'Policy violation' }),
  ];

  // Admin Overview Stats (used by StatsOverview block)
  await context.route('**/api/v1/admin/overview-stats*', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        totalGames: 50,
        publishedGames: 30,
        totalUsers: 100,
        activeUsers: 25,
        approvalRate: 0.85,
        pendingApprovals: 3,
        recentSubmissions: 5,
      }),
    });
  });

  // =========================================================================
  // Unified User API handler — single handler for ALL /admin/users/* endpoints.
  // Dispatches internally based on path segments and HTTP method.
  // =========================================================================
  await context.route(
    (url) => url.pathname.startsWith('/api/v1/admin/users'),
    async (route) => {
    const url = new URL(route.request().url());
    const path = url.pathname;
    const method = route.request().method();

    const userPathMatch = path.match(/\/api\/v1\/admin\/users\/(.+)/);
    const subPath = userPathMatch ? userPathMatch[1] : '';
    const segments = subPath.split('/').filter(Boolean);

    // Users List: /admin/users (no sub-path)
    if (segments.length === 0) {
      if (method !== 'GET') {
        await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
        return;
      }
      const search = url.searchParams.get('search')?.toLowerCase();
      const role = url.searchParams.get('role');
      const tier = url.searchParams.get('tier');

      let filtered = [...users];
      if (search) {
        filtered = filtered.filter(
          (u) => u.displayName.toLowerCase().includes(search) || u.email.toLowerCase().includes(search)
        );
      }
      if (role && role !== 'all') {
        filtered = filtered.filter((u) => u.role.toLowerCase() === role.toLowerCase());
      }
      if (tier && tier !== 'all') {
        filtered = filtered.filter((u) => u.tier === tier);
      }
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(makePagedResult(filtered)),
      });
      return;
    }

    const userId = segments[0];
    const action = segments[1];

    // User sub-resource endpoints
    if (action === 'suspend' && method === 'POST') {
      await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
      return;
    }
    if (action === 'unsuspend' && method === 'POST') {
      await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
      return;
    }
    if (action === 'impersonate' && method === 'POST') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ sessionToken: 'mock-impersonation-token-12345678' }),
      });
      return;
    }
    if (action === 'tier' && method === 'PUT') {
      await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
      return;
    }
    if (action === 'badges' && method === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'badge-1', code: 'early-adopter', name: 'Early Adopter', description: 'Joined in the first month', iconUrl: null, tier: 'bronze', earnedAt: new Date().toISOString(), isDisplayed: true },
        ]),
      });
      return;
    }
    if (action === 'library' && segments[2] === 'stats' && method === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          totalGames: 42,
          sessionsPlayed: 128,
          favoriteGames: 8,
          oldestAddedAt: null,
          newestAddedAt: new Date().toISOString(),
        }),
      });
      return;
    }
    if (action === 'reset-password' && method === 'POST') {
      await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
      return;
    }

    // User Detail: /admin/users/{id} (no action, GET only)
    if (!action && method === 'GET') {
      const user = users.find((u) => u.id === userId);
      if (user) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(user),
        });
      } else {
        await route.fulfill({ status: 404, contentType: 'application/json', body: '{"error":"Not found"}' });
      }
      return;
    }

    // Fallback: unknown user endpoint
    await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
  });

  // Batch Reject (POST)
  await context.route('**/api/v1/admin/shared-games/batch-reject', async (route) => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
  });

  // Batch Approve (POST)
  await context.route('**/api/v1/admin/shared-games/batch-approve', async (route) => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
  });

  // Approval Queue (GET)
  await context.route('**/api/v1/admin/shared-games/approval-queue*', async (route) => {
    if (route.request().method() !== 'GET') {
      await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
      return;
    }
    const url = new URL(route.request().url());
    const search = url.searchParams.get('search')?.toLowerCase();
    const status = url.searchParams.get('status');

    let filtered = [...approvalItems];
    if (search) {
      filtered = filtered.filter((item) => item.title.toLowerCase().includes(search));
    }
    if (status && status !== 'all') {
      if (status === 'urgent') {
        filtered = filtered.filter((item) => item.daysPending > 7);
      }
    }

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(makePagedResult(filtered)),
    });
  });
}

// Helper to navigate to admin dashboard and wait for load
async function gotoAdminDashboard(page: Page): Promise<void> {
  await page.goto('/admin/dashboard');
  await page.getByRole('heading', { name: 'Approval Queue' }).waitFor({ timeout: 15000 });
}

/**
 * Helper to open user detail panel and wait for data to load.
 * Clicks the user card, then waits for the dialog to show actual user data
 * (not just the static "User Profile" heading which renders before data loads).
 */
async function openUserDetailPanel(page: Page, userName: string): Promise<void> {
  await page.getByRole('button', { name: `Player: ${userName}` }).click();
  // Wait for the dialog AND actual user data to render.
  // The "Quick Actions" section only renders when the React Query `user` data is loaded.
  await expect(page.getByText('Quick Actions')).toBeVisible({ timeout: 10000 });
}

// =============================================================================
// Test Suite
// =============================================================================

test.describe('Admin Workflow Actions', () => {
  // Force serial execution: parallel runs cause the Next.js dev server to
  // become overloaded, leading to slower page loads and flaky API mock timing.
  test.describe.configure({ mode: 'serial' });

  test.beforeEach(async ({ page }) => {
    await setupAdminAuth(page);
    await setupDashboardMocks(page);
    await gotoAdminDashboard(page);
  });

  // =========================================================================
  // 1. Approve Game (Single)
  // =========================================================================
  test.describe('Approve Game', () => {
    test('should display approval queue with game cards', async ({ page }) => {
      await expect(page.getByRole('heading', { name: 'Approval Queue' })).toBeVisible();
      await expect(page.getByText('Catan')).toBeVisible();
      await expect(page.getByText('Ticket to Ride')).toBeVisible();
      await expect(page.getByText('Pandemic')).toBeVisible();
    });

    test('should show pending count badge', async ({ page }) => {
      // "3 pending" appears in both stats overview and approval queue heading badge
      await expect(page.getByText('3 pending').first()).toBeVisible();
    });

    test('should approve a single game via selection and batch approve', async ({ page }) => {
      const approveRequests: string[] = [];
      await page.route('**/api/v1/admin/shared-games/batch-approve', async (route) => {
        const body = route.request().postDataJSON();
        approveRequests.push(...(body?.gameIds ?? []));
        await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
      });

      // Select a single game via its checkbox, then batch approve
      await page.getByRole('button', { name: 'Select Catan' }).click();
      await expect(page.getByTestId('bulk-actions')).toBeVisible();
      await page.getByRole('button', { name: 'Batch Approve' }).click();

      // Verify the API was called
      await expect.poll(() => approveRequests.length).toBeGreaterThan(0);
    });

    test('should show toast notification after approval', async ({ page }) => {
      // useToast hook is a stub that logs via console.warn('[Toast]', title, description)
      // Capture console messages to verify the toast fires
      const toastMessages: string[] = [];
      page.on('console', (msg) => {
        if (msg.type() === 'warning' && msg.text().includes('[Toast]')) {
          toastMessages.push(msg.text());
        }
      });

      // Select and approve, waiting for the API response
      await page.getByRole('button', { name: 'Select Catan' }).click();
      const approveResponse = page.waitForResponse(
        (resp) => resp.url().includes('batch-approve')
      );
      await page.getByRole('button', { name: 'Batch Approve' }).click();
      await approveResponse;

      // Toast fires via console.warn after successful mutation
      await expect.poll(() => toastMessages.some((m) => /game approved/i.test(m)), {
        timeout: 5000,
        message: 'Expected console.warn with "Game approved" toast',
      }).toBe(true);
    });
  });

  // =========================================================================
  // 2. Reject Game (Single)
  // =========================================================================
  test.describe('Reject Game', () => {
    test('should reject a single game via selection and batch reject', async ({ page }) => {
      const rejectRequests: string[] = [];
      await page.route('**/api/v1/admin/shared-games/batch-reject', async (route) => {
        const body = route.request().postDataJSON();
        rejectRequests.push(...(body?.gameIds ?? []));
        await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
      });

      // Select a single game via its checkbox, then batch reject
      await page.getByRole('button', { name: 'Select Catan' }).click();
      await expect(page.getByTestId('bulk-actions')).toBeVisible();
      await page.getByRole('button', { name: 'Batch Reject' }).click();

      await expect.poll(() => rejectRequests.length).toBeGreaterThan(0);
    });

    test('should show error toast on reject failure', async ({ page }) => {
      const toastMessages: string[] = [];
      page.on('console', (msg) => {
        if (msg.type() === 'warning' && msg.text().includes('[Toast]')) {
          toastMessages.push(msg.text());
        }
      });

      // Use 400 (not 500) to avoid httpClient's retry mechanism (retries 5xx with exponential backoff)
      await page.route('**/api/v1/admin/shared-games/batch-reject', async (route) => {
        await route.fulfill({ status: 400, contentType: 'application/json', body: '{"error":"Bad request"}' });
      });

      // Select and attempt to reject, waiting for the error response
      await page.getByRole('button', { name: 'Select Catan' }).click();
      const rejectResponse = page.waitForResponse(
        (resp) => resp.url().includes('batch-reject')
      );
      await page.getByRole('button', { name: 'Batch Reject' }).click();
      await rejectResponse;

      // Error toast fires via console.warn after mutation failure (400 = no retries)
      await expect.poll(() => toastMessages.some((m) => /failed to reject/i.test(m)), {
        timeout: 5000,
        message: 'Expected console.warn with "Failed to reject" toast',
      }).toBe(true);
    });
  });

  // =========================================================================
  // 3. Bulk Operations
  // =========================================================================
  test.describe('Bulk Operations', () => {
    test('should show select all toggle', async ({ page }) => {
      await expect(page.getByRole('button', { name: /select all|deselect all/i })).toBeVisible();
    });

    test('should show bulk actions bar when items are selected', async ({ page }) => {
      // Click select all button
      await page.getByRole('button', { name: /select all/i }).click();

      // Bulk actions bar should appear
      await expect(page.getByTestId('bulk-actions')).toBeVisible();
      await expect(page.getByText('3 selected')).toBeVisible();
    });

    test('should batch approve selected games', async ({ page }) => {
      let batchApproveIds: string[] = [];
      await page.route('**/api/v1/admin/shared-games/batch-approve', async (route) => {
        const body = route.request().postDataJSON();
        batchApproveIds = body?.gameIds ?? [];
        await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
      });

      // Select all then batch approve
      await page.getByRole('button', { name: /select all/i }).click();
      await page.getByRole('button', { name: 'Batch Approve' }).click();

      await expect.poll(() => batchApproveIds.length).toBe(3);
    });

    test('should batch reject selected games', async ({ page }) => {
      let batchRejectIds: string[] = [];
      await page.route('**/api/v1/admin/shared-games/batch-reject', async (route) => {
        const body = route.request().postDataJSON();
        batchRejectIds = body?.gameIds ?? [];
        await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
      });

      // Select all then batch reject
      await page.getByRole('button', { name: /select all/i }).click();
      await page.getByRole('button', { name: 'Batch Reject' }).click();

      await expect.poll(() => batchRejectIds.length).toBe(3);
    });

    test('should deselect all when clicking select all again', async ({ page }) => {
      // Select all
      await page.getByRole('button', { name: /select all/i }).click();
      await expect(page.getByTestId('bulk-actions')).toBeVisible();

      // Deselect all
      await page.getByRole('button', { name: /deselect all/i }).click();
      await expect(page.getByTestId('bulk-actions')).not.toBeVisible();
    });
  });

  // =========================================================================
  // 4. Suspend User
  // =========================================================================
  test.describe('Suspend User', () => {
    test('should display user management section', async ({ page }) => {
      await expect(page.getByRole('heading', { name: 'User Management' })).toBeVisible();
      await expect(page.getByText('Alice Smith')).toBeVisible();
      await expect(page.getByText('Bob Jones')).toBeVisible();
    });

    test('should show user count badge', async ({ page }) => {
      await expect(page.getByText('3 users')).toBeVisible();
    });

    test('should call suspend API via user detail panel', async ({ page }) => {
      // Register user detail routes AFTER page load to ensure interception of
      // React Query requests triggered by panel open (see registerUserDetailRoutes jsdoc)

      let suspendCalled = false;
      await page.route('**/api/v1/admin/users/*/suspend', async (route) => {
        suspendCalled = true;
        await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
      });

      await openUserDetailPanel(page, 'Alice Smith');
      await page.getByRole('button', { name: 'Suspend' }).click();

      await expect.poll(() => suspendCalled).toBe(true);
    });

    test('should show Unsuspend button for suspended users in detail panel', async ({ page }) => {

      // Carol Brown is suspended - open her detail panel
      await openUserDetailPanel(page, 'Carol Brown');

      // Should show Unsuspend in Quick Actions
      await expect(page.getByRole('button', { name: 'Unsuspend' })).toBeVisible({ timeout: 5000 });
    });

    test('should show toast on successful suspend via detail panel', async ({ page }) => {

      const toastMessages: string[] = [];
      page.on('console', (msg) => {
        if (msg.type() === 'warning' && msg.text().includes('[Toast]')) {
          toastMessages.push(msg.text());
        }
      });

      // Open Alice's detail panel and wait for data
      await openUserDetailPanel(page, 'Alice Smith');

      // Click Suspend
      await page.getByRole('button', { name: 'Suspend' }).click();

      // Toast fires via console.warn
      await expect.poll(() => toastMessages.some((m) => /user suspended/i.test(m)), {
        timeout: 5000,
        message: 'Expected console.warn with "User suspended" toast',
      }).toBe(true);
    });
  });

  // =========================================================================
  // 5. View User Detail
  // =========================================================================
  test.describe('View User Detail', () => {
    test('should open user detail panel when clicking user card', async ({ page }) => {

      // MeepleCard renders as a button with onClick → opens Sheet detail panel
      await openUserDetailPanel(page, 'Alice Smith');

      // Verify user info is rendered in the dialog (scoped to the sheet panel
      // to avoid strict mode violation with email shown in both card and panel)
      const panel = page.getByLabel('User Profile');
      await expect(panel.getByText('alice@test.com')).toBeVisible({ timeout: 5000 });
    });

    test('should display library statistics in detail panel', async ({ page }) => {

      await openUserDetailPanel(page, 'Alice Smith');

      await expect(page.getByText('Library Statistics')).toBeVisible({ timeout: 5000 });
      await expect(page.getByText('42')).toBeVisible(); // totalGames
      await expect(page.getByText('128')).toBeVisible(); // sessionsPlayed
    });

    test('should display user badges in detail panel', async ({ page }) => {

      await openUserDetailPanel(page, 'Alice Smith');

      await expect(page.getByText('Achievements')).toBeVisible({ timeout: 10000 });
      await expect(page.getByText('Early Adopter')).toBeVisible();
    });

    test('should show quick actions in detail panel', async ({ page }) => {

      await openUserDetailPanel(page, 'Alice Smith');

      await expect(page.getByText('Quick Actions')).toBeVisible({ timeout: 5000 });
      await expect(page.getByRole('button', { name: 'Email' })).toBeVisible();
      await expect(page.getByRole('button', { name: 'Impersonate' })).toBeVisible();
    });
  });

  // =========================================================================
  // 6. Search and Filters
  // =========================================================================
  test.describe('Search and Filters', () => {
    test('should filter approval queue by search text', async ({ page }) => {
      // Verify all games visible initially
      await expect(page.getByText('Pandemic')).toBeVisible();

      // Type in the games search input
      const gameSearch = page.locator('input[placeholder="Search games..."]');
      await gameSearch.fill('Catan');

      // Wait for debounce + refetch: Pandemic should disappear, Catan stays
      await expect(page.getByText('Pandemic')).not.toBeVisible({ timeout: 10000 });
      await expect(page.getByText('Catan')).toBeVisible();
    });

    test('should filter users by search text', async ({ page }) => {
      // Verify initial state
      await expect(page.getByText('Bob Jones')).toBeVisible();

      const userSearch = page.locator('input[placeholder="Search by name or email..."]');
      await userSearch.fill('Alice');

      // Wait for debounce + refetch: Bob should disappear, Alice stays
      await expect(page.getByText('Bob Jones')).not.toBeVisible({ timeout: 10000 });
      await expect(page.getByText('Alice Smith')).toBeVisible();
    });

    test('should show empty state when search has no results', async ({ page }) => {
      const gameSearch = page.locator('input[placeholder="Search games..."]');
      await gameSearch.fill('nonexistent game xyz');

      // Wait for debounce + refetch: empty state should appear
      await expect(page.getByText('No games in approval queue')).toBeVisible({ timeout: 10000 });
    });

    test('should show View All link for approval queue', async ({ page }) => {
      const viewAllLink = page.getByRole('link', { name: /view all/i }).first();
      await expect(viewAllLink).toBeVisible();
      await expect(viewAllLink).toHaveAttribute('href', '/admin/shared-games/approvals');
    });

    test('should show View All link for user management', async ({ page }) => {
      // Scroll to user management section
      await page.getByRole('heading', { name: 'User Management' }).scrollIntoViewIfNeeded();
      const viewAllLinks = page.getByRole('link', { name: /view all/i });
      // The second "View All" is for user management
      const userViewAll = viewAllLinks.nth(1);
      await expect(userViewAll).toHaveAttribute('href', '/admin/users/management');
    });
  });

  // =========================================================================
  // 7. Grid/List Toggle
  // =========================================================================
  test.describe('Grid/List Toggle', () => {
    test('should default to grid view for approval queue', async ({ page }) => {
      // Grid view: cards in a grid layout (multiple columns)
      const gridContainer = page.locator('.grid.grid-cols-1.md\\:grid-cols-2.lg\\:grid-cols-3');
      await expect(gridContainer.first()).toBeVisible();
    });

    test('should switch to list view when clicking list toggle', async ({ page }) => {
      // Click the list view toggle button (second button in the toggle group)
      // There are two toggle groups: one for games, one for users
      const listButtons = page.locator('button:has(svg.lucide-list)');
      await listButtons.first().click();

      // In list mode the container switches to space-y-4 (vertical stack)
      const listContainer = page.locator('.space-y-4');
      await expect(listContainer.first()).toBeVisible();
    });

    test('should switch back to grid view', async ({ page }) => {
      // Switch to list first
      const listButtons = page.locator('button:has(svg.lucide-list)');
      await listButtons.first().click();

      // Switch back to grid
      const gridButtons = page.locator('button:has(svg.lucide-grid-3x3)');
      await gridButtons.first().click();

      const gridContainer = page.locator('.grid.grid-cols-1.md\\:grid-cols-2.lg\\:grid-cols-3');
      await expect(gridContainer.first()).toBeVisible();
    });

    test('should independently toggle user management view', async ({ page }) => {
      // Scroll to user management
      await page.getByRole('heading', { name: 'User Management' }).scrollIntoViewIfNeeded();

      // Toggle user management to list view (the second toggle group)
      const listButtons = page.locator('button:has(svg.lucide-list)');
      await listButtons.nth(1).click();

      // User section should be in list view
      // Approval queue should still be in grid view
      const gridContainers = page.locator('.grid.grid-cols-1.md\\:grid-cols-2.lg\\:grid-cols-3');
      // At least the approval queue grid should still exist
      await expect(gridContainers.first()).toBeVisible();
    });
  });

  // =========================================================================
  // 8. Cross-browser coverage
  // =========================================================================
  test.describe('Cross-browser basics', () => {
    test('should render admin dashboard layout correctly', async ({ page }) => {
      // Basic layout checks that work across browsers
      await expect(page.getByRole('heading', { name: 'Approval Queue' })).toBeVisible();
      await expect(page.getByRole('heading', { name: 'User Management' })).toBeVisible();

      // Check game cards render
      await expect(page.getByText('Catan')).toBeVisible();
      await expect(page.getByText('Ticket to Ride')).toBeVisible();

      // Check user cards render
      await expect(page.getByText('Alice Smith')).toBeVisible();
    });

    test('should have accessible approval queue controls', async ({ page }) => {
      // Verify ARIA labels
      await expect(page.getByRole('button', { name: /select all/i })).toBeVisible();
      await expect(page.locator('input[placeholder="Search games..."]')).toBeVisible();
    });

    test('should have accessible user management controls', async ({ page }) => {
      await expect(page.locator('input[placeholder="Search by name or email..."]')).toBeVisible();
      // User cards are clickable buttons with accessible names
      await expect(page.getByRole('button', { name: 'Player: Alice Smith' })).toBeVisible();
    });
  });
});
