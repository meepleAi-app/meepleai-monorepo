/**
 * User Management - Visual Documentation (Admin Role)
 *
 * Captures visual documentation for user management:
 * - View user list
 * - Change user tier
 * - Manage roles
 * - Suspend/unsuspend users
 *
 * @see docs/08-user-flows/admin-role/02-user-management.md
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

// Mock users data
const MOCK_USERS = [
  {
    id: 'user-1',
    email: 'user@example.com',
    displayName: 'Regular User',
    role: 'User',
    tier: 'Free',
    status: 'active',
    createdAt: '2026-01-01T10:00:00Z',
    lastLoginAt: '2026-01-19T08:00:00Z',
  },
  {
    id: 'user-2',
    email: 'premium@example.com',
    displayName: 'Premium User',
    role: 'User',
    tier: 'Premium',
    status: 'active',
    createdAt: '2025-12-15T10:00:00Z',
    lastLoginAt: '2026-01-19T12:00:00Z',
  },
  {
    id: 'user-3',
    email: 'suspended@example.com',
    displayName: 'Suspended User',
    role: 'User',
    tier: 'Normal',
    status: 'suspended',
    suspendedReason: 'Violation of terms',
    createdAt: '2025-11-01T10:00:00Z',
  },
];

test.describe('User Management - Visual Documentation (Admin)', () => {
  let helper: ScreenshotHelper;
  let authHelper: AuthHelper;

  test.beforeEach(async ({ page }) => {
    helper = new ScreenshotHelper({
      outputDir: ADMIN_FLOWS.userManagement.outputDir,
      flow: ADMIN_FLOWS.userManagement.name,
      role: ADMIN_FLOWS.userManagement.role,
    });
    authHelper = new AuthHelper(page);
    await disableAnimations(page);

    // Setup admin session
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.admin);

    // Mock users endpoint
    await page.route(`${API_BASE}/api/v1/admin/users*`, async route => {
      const url = route.request().url();
      if (url.includes('/users/user-1')) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(MOCK_USERS[0]),
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ items: MOCK_USERS, total: 3, page: 1, pageSize: 10 }),
        });
      }
    });
  });

  test('user list - view all users', async ({ page }) => {
    // Step 1: Navigate to user management
    await page.goto('/admin/users');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'User Management',
      description: 'View and manage all users',
      annotations: [
        { selector: 'input[type="search"], [data-testid="user-search"]', label: 'Search', color: ANNOTATION_COLORS.primary },
      ],
      nextAction: 'Search or browse users',
    });

    // Step 2: User table
    const userTable = page.locator('table, [data-testid="user-table"], .user-list').first();
    if (await userTable.isVisible({ timeout: 3000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 2,
        title: 'User List',
        description: 'All users with role, tier, and status',
        annotations: [
          { selector: 'th, [data-testid="column-email"]', label: 'Email', color: ANNOTATION_COLORS.info },
          { selector: 'th, [data-testid="column-tier"]', label: 'Tier', color: ANNOTATION_COLORS.info },
          { selector: 'th, [data-testid="column-status"]', label: 'Status', color: ANNOTATION_COLORS.info },
        ],
        previousAction: 'View list',
        nextAction: 'Select user',
      });
    }

    // Step 3: User row with actions
    const userRow = page.locator('tr, [data-testid="user-row"]').nth(1);
    if (await userRow.isVisible({ timeout: 2000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 3,
        title: 'User Row',
        description: 'Individual user with quick actions',
        annotations: [
          { selector: 'button:has-text("Edit"), [data-testid="edit-user"]', label: 'Edit', color: ANNOTATION_COLORS.primary },
        ],
        previousAction: 'View users',
        nextAction: 'Edit user',
      });
    }

    helper.setTotalSteps(3);
    console.log(`\n✅ User list captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('change user tier - upgrade/downgrade', async ({ page }) => {
    // Mock tier update
    await page.route(`${API_BASE}/api/v1/admin/users/user-1/tier`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ tier: 'Premium' }),
      });
    });

    // Step 1: Navigate to user
    await page.goto('/admin/users/user-1');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'User Details',
      description: 'View user details to change tier',
      annotations: [
        { selector: 'text=/Free|Normal|Premium/i, [data-testid="current-tier"]', label: 'Current Tier', color: ANNOTATION_COLORS.info },
      ],
      nextAction: 'Change tier',
    });

    // Step 2: Tier change button/selector
    const changeTierBtn = page.locator('button:has-text("Change Tier"), [data-testid="change-tier"]').first();
    if (await changeTierBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await changeTierBtn.click();
      await waitForStableState(page);

      // Step 3: Tier selection
      const tierSelect = page.locator('[role="dialog"], .tier-selector, select').first();
      if (await tierSelect.isVisible({ timeout: 2000 }).catch(() => false)) {
        await helper.capture(page, {
          step: 2,
          title: 'Select Tier',
          description: 'Choose new tier for user (Free, Normal, Premium)',
          annotations: [
            { selector: 'text=Premium, [data-tier="premium"]', label: 'Premium', color: ANNOTATION_COLORS.success },
            { selector: 'text=Normal, [data-tier="normal"]', label: 'Normal', color: ANNOTATION_COLORS.info },
            { selector: 'text=Free, [data-tier="free"]', label: 'Free', color: ANNOTATION_COLORS.neutral },
          ],
          previousAction: 'Open selector',
          nextAction: 'Select tier',
        });
      }
    }

    helper.setTotalSteps(2);
    console.log(`\n✅ Change tier captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('change user role - promote/demote', async ({ page }) => {
    // Mock role update
    await page.route(`${API_BASE}/api/v1/admin/users/user-1/role`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ role: 'Editor' }),
      });
    });

    // Step 1: Navigate to user
    await page.goto('/admin/users/user-1');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'User Role Management',
      description: 'View user to change role',
      annotations: [
        { selector: 'text=/User|Editor|Admin/i, [data-testid="current-role"]', label: 'Current Role', color: ANNOTATION_COLORS.info },
      ],
      nextAction: 'Change role',
    });

    // Step 2: Role change
    const changeRoleBtn = page.locator('button:has-text("Change Role"), [data-testid="change-role"]').first();
    if (await changeRoleBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await changeRoleBtn.click();
      await waitForStableState(page);

      const roleSelect = page.locator('[role="dialog"], .role-selector').first();
      if (await roleSelect.isVisible({ timeout: 2000 }).catch(() => false)) {
        await helper.capture(page, {
          step: 2,
          title: 'Select Role',
          description: 'Choose new role (User, Editor, Admin)',
          annotations: [
            { selector: 'text=Editor, [data-role="editor"]', label: 'Editor', color: ANNOTATION_COLORS.warning },
            { selector: 'text=Admin, [data-role="admin"]', label: 'Admin', color: ANNOTATION_COLORS.error },
          ],
          previousAction: 'Open selector',
          nextAction: 'Assign role',
        });
      }
    }

    helper.setTotalSteps(2);
    console.log(`\n✅ Change role captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('suspend user - moderation action', async ({ page }) => {
    // Mock suspend endpoint
    await page.route(`${API_BASE}/api/v1/admin/users/user-1/suspend`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ status: 'suspended' }),
      });
    });

    // Step 1: Navigate to user
    await page.goto('/admin/users/user-1');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'User to Suspend',
      description: 'Active user account',
      annotations: [
        { selector: 'button:has-text("Suspend"), [data-testid="suspend-user"]', label: 'Suspend', color: ANNOTATION_COLORS.error },
      ],
      nextAction: 'Suspend user',
    });

    // Step 2: Suspend dialog
    const suspendBtn = page.locator('button:has-text("Suspend"), [data-testid="suspend-user"]').first();
    if (await suspendBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await suspendBtn.click();
      await waitForStableState(page);

      const suspendDialog = page.locator('[role="dialog"], [role="alertdialog"]').first();
      if (await suspendDialog.isVisible({ timeout: 2000 }).catch(() => false)) {
        await helper.capture(page, {
          step: 2,
          title: 'Suspend Reason',
          description: 'Provide reason for suspending user',
          annotations: [
            { selector: 'textarea, [data-testid="suspend-reason"]', label: 'Reason', color: ANNOTATION_COLORS.warning },
            { selector: 'button:has-text("Confirm"), button:has-text("Suspend")', label: 'Confirm', color: ANNOTATION_COLORS.error },
          ],
          previousAction: 'Open suspend dialog',
          nextAction: 'Enter reason and suspend',
        });
      }
    }

    helper.setTotalSteps(2);
    console.log(`\n✅ Suspend user captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('unsuspend user - restore access', async ({ page }) => {
    // Mock unsuspend endpoint
    await page.route(`${API_BASE}/api/v1/admin/users/user-3*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_USERS[2]),
      });
    });

    await page.route(`${API_BASE}/api/v1/admin/users/user-3/unsuspend`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ status: 'active' }),
      });
    });

    // Step 1: Navigate to suspended user
    await page.goto('/admin/users/user-3');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'Suspended User',
      description: 'View suspended user details',
      annotations: [
        { selector: 'text=/suspended/i, [data-status="suspended"]', label: 'Suspended', color: ANNOTATION_COLORS.error },
        { selector: 'button:has-text("Unsuspend"), [data-testid="unsuspend"]', label: 'Unsuspend', color: ANNOTATION_COLORS.success },
      ],
      nextAction: 'Unsuspend user',
    });

    // Step 2: Unsuspend
    const unsuspendBtn = page.locator('button:has-text("Unsuspend"), [data-testid="unsuspend"]').first();
    if (await unsuspendBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 2,
        title: 'Restore Access',
        description: 'Unsuspend user to restore account access',
        annotations: [
          { selector: 'button:has-text("Unsuspend")', label: 'Unsuspend', color: ANNOTATION_COLORS.success },
        ],
        previousAction: 'View suspended',
        nextAction: 'Click unsuspend',
      });
    }

    helper.setTotalSteps(2);
    console.log(`\n✅ Unsuspend user captured: ${helper.getCapturedSteps().length} screenshots`);
  });
});
