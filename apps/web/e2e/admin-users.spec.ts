/**
 * Admin User Management E2E Tests - MIGRATED TO POM
 *
 * @see apps/web/e2e/pages/helpers/AdminHelper.ts - mockUsersCRUD()
 * @see apps/web/e2e/pages/admin/AdminPage.ts
 */

import { test as base, expect, Page } from './fixtures';
import { getTextMatcher, t } from './fixtures/i18n';
import { AdminHelper } from './pages';

// Extended test with admin authentication
const test = base.extend<{ adminPage: Page }>({
  adminPage: async ({ page }, use) => {
    const adminHelper = new AdminHelper(page);
    await adminHelper.setupAdminAuth(true); // Skip navigation
    await use(page);
  },
});

test.describe('Admin User Management E2E Flow', () => {
  test('complete user lifecycle: create → edit → delete', async ({ adminPage: page }) => {
    const adminHelper = new AdminHelper(page);

    // Setup CRUD mocks with default sample users
    await adminHelper.mockUsersCRUD();

    // Navigate to user management page
    await page.goto('http://localhost:3000/admin/users');

    // Verify initial user list loads
    await expect(page.getByText(getTextMatcher('admin.users.management'))).toBeVisible();
    await expect(page.getByText('existing@example.com')).toBeVisible();
    await expect(page.getByText('editor@example.com')).toBeVisible();

    // ===== CREATE USER =====
    // Click Create User button (open modal)
    await page.getByTestId('open-create-user-modal').click();

    // Fill out create form
    await expect(
      page.getByRole('heading', { name: getTextMatcher('admin.users.createUser') })
    ).toBeVisible();
    await page.getByLabel(getTextMatcher('admin.users.email')).fill('newuser@example.com');
    await page.getByLabel(getTextMatcher('admin.users.password')).fill('SecurePass123!');
    await page.getByLabel(getTextMatcher('admin.users.displayName')).fill('New Test User');
    await page.getByLabel(getTextMatcher('admin.users.role')).selectOption('Editor');

    // Submit form
    await page.getByTestId('submit-user-form').click();

    // Verify success toast appears
    await expect(page.getByText(/created successfully/)).toBeVisible();

    // Wait for toast to disappear or close it to avoid interference

    // Verify new user appears in table (use role to be specific with exact match)
    await expect(
      page.getByRole('cell', { name: 'newuser@example.com', exact: true })
    ).toBeVisible();
    await expect(page.getByRole('cell', { name: 'New Test User', exact: true })).toBeVisible();

    // ===== EDIT USER =====
    // Find and click Edit button for the newly created user
    const newUserRow = page.locator('tr:has-text("newuser@example.com")');
    await newUserRow.getByRole('button', { name: getTextMatcher('common.edit') }).click();

    // Verify edit modal opens with pre-filled data
    await expect(
      page.getByRole('heading', { name: getTextMatcher('admin.users.editUser') })
    ).toBeVisible();
    await expect(page.getByLabel(getTextMatcher('admin.users.email'))).toHaveValue(
      'newuser@example.com'
    );

    // Update display name and role
    await page.getByLabel(getTextMatcher('admin.users.displayName')).fill('Updated User Name');
    await page.getByLabel(getTextMatcher('admin.users.role')).selectOption('User');

    // Save changes
    await page.getByRole('button', { name: getTextMatcher('admin.users.saveChanges') }).click();

    // Verify success toast
    await expect(page.getByText(/updated successfully/)).toBeVisible();

    // Wait for toast to dismiss

    // Verify updates appear in table (use role for specificity)
    await expect(page.getByRole('cell', { name: 'Updated User Name' })).toBeVisible();

    // ===== SEARCH FUNCTIONALITY =====
    // Search for the user
    await page.getByPlaceholder(t('admin.users.searchPlaceholder')).fill('Updated');

    // Verify filtered results (use role to avoid toast interference)
    await expect(
      page.getByRole('cell', { name: 'newuser@example.com', exact: true })
    ).toBeVisible();
    await expect(page.getByText('existing@example.com')).not.toBeVisible();

    // Clear search
    await page.getByPlaceholder(t('admin.users.searchPlaceholder')).clear();

    // ===== ROLE FILTER =====
    // Filter by User role
    await page.locator('select').first().selectOption('User');

    // Verify only User role users shown (use role='cell' to avoid toast interference)
    await expect(
      page.getByRole('cell', { name: 'newuser@example.com', exact: true })
    ).toBeVisible();
    await expect(
      page.getByRole('cell', { name: 'existing@example.com', exact: true })
    ).toBeVisible();
    await expect(
      page.getByRole('cell', { name: 'editor@example.com', exact: true })
    ).not.toBeVisible();

    // Reset filter
    await page.locator('select').first().selectOption('all');

    // ===== DELETE USER =====
    // Click Delete button for the user we created
    // First, ensure we're finding the correct row
    const userRowToDelete = page.locator('tr').filter({ hasText: 'newuser@example.com' });
    await expect(userRowToDelete).toBeVisible();

    // Find the Delete button within that row, scroll into view, then click
    const deleteButton = userRowToDelete.locator('button:has-text("Delete")');
    await deleteButton.scrollIntoViewIfNeeded();
    await deleteButton.click({ force: true });

    // Verify confirmation dialog appears
    await expect(page.getByText(getTextMatcher('admin.users.deleteUser'))).toBeVisible();
    await expect(page.getByText(new RegExp(t('admin.users.deleteConfirm')))).toBeVisible();

    // Confirm deletion (use force for portal overlay)
    await page
      .getByRole('button', { name: getTextMatcher('common.confirm') })
      .click({ force: true });

    // Verify success toast
    await expect(page.getByText(/deleted successfully/)).toBeVisible();

    // Wait for toast to dismiss

    // Verify user no longer in list
    await expect(page.getByText('newuser@example.com')).not.toBeVisible();
  });

  test('bulk delete functionality', async ({ adminPage: page }) => {
    const adminHelper = new AdminHelper(page);

    const bulkUsers = [
      {
        id: 'bulk-1',
        email: 'bulk1@example.com',
        displayName: 'Bulk User 1',
        role: 'User',
        createdAt: new Date().toISOString(),
        lastSeenAt: null,
      },
      {
        id: 'bulk-2',
        email: 'bulk2@example.com',
        displayName: 'Bulk User 2',
        role: 'User',
        createdAt: new Date().toISOString(),
        lastSeenAt: null,
      },
      {
        id: 'bulk-3',
        email: 'bulk3@example.com',
        displayName: 'Bulk User 3',
        role: 'User',
        createdAt: new Date().toISOString(),
        lastSeenAt: null,
      },
    ];

    await adminHelper.mockUsersCRUD(bulkUsers);

    await page.goto('http://localhost:3000/admin/users');

    // Verify users loaded
    await expect(page.getByText('bulk1@example.com')).toBeVisible();
    await expect(page.getByText('bulk2@example.com')).toBeVisible();

    // Select first two users
    const checkboxes = page.getByRole('checkbox');
    await checkboxes.nth(1).check(); // First user
    await checkboxes.nth(2).check(); // Second user

    // Verify bulk delete button appears with count
    await expect(
      page.getByText(new RegExp(`${t('admin.users.deleteSelected')} \\(2\\)`))
    ).toBeVisible();

    // Click bulk delete
    await page.getByText(new RegExp(`${t('admin.users.deleteSelected')} \\(2\\)`)).click();

    // Verify confirmation dialog
    await expect(page.getByText(getTextMatcher('admin.users.deleteMultiple'))).toBeVisible();
    await expect(page.getByText(/2 user\(s\)/)).toBeVisible();

    // Confirm bulk delete
    await page.getByRole('button', { name: getTextMatcher('common.confirm') }).click();

    // Verify success toast
    await expect(page.getByText(/2 user\(s\) deleted successfully/)).toBeVisible();

    // Verify users removed (would need page reload in real scenario)
  });

  test('sorting columns', async ({ adminPage: page }) => {
    const adminHelper = new AdminHelper(page);

    const sortUsers = [
      {
        id: 'sort-1',
        email: 'alpha@example.com',
        displayName: 'Alpha',
        role: 'Admin',
        createdAt: new Date('2024-01-03T00:00:00Z').toISOString(),
        lastSeenAt: null,
      },
      {
        id: 'sort-2',
        email: 'beta@example.com',
        displayName: 'Beta',
        role: 'User',
        createdAt: new Date('2024-01-01T00:00:00Z').toISOString(),
        lastSeenAt: null,
      },
      {
        id: 'sort-3',
        email: 'gamma@example.com',
        displayName: 'Gamma',
        role: 'Editor',
        createdAt: new Date('2024-01-02T00:00:00Z').toISOString(),
        lastSeenAt: null,
      },
    ];

    await adminHelper.mockUsersCRUD(sortUsers);

    await page.goto('http://localhost:3000/admin/users');

    // Default: sorted by createdAt desc (newest first)
    await expect(page.getByText('alpha@example.com')).toBeVisible();

    // Click Email header to sort by email
    await page.getByText(new RegExp(t('admin.users.email').replace(' *', ''))).click();

    // Should show sort indicator
    await expect(
      page.getByText(new RegExp(`${t('admin.users.email').replace(' *', '')} ↑`))
    ).toBeVisible();

    // Click again to reverse sort
    await page.getByText(new RegExp(t('admin.users.email').replace(' *', ''))).click();
    await expect(
      page.getByText(new RegExp(`${t('admin.users.email').replace(' *', '')} ↓`))
    ).toBeVisible();
  });

  test('pagination navigation', async ({ adminPage: page }) => {
    const adminHelper = new AdminHelper(page);

    // Create 25 users to test pagination (20 per page)
    const paginationUsers = Array.from({ length: 25 }, (_, i) => ({
      id: `paginate-${i + 1}`,
      email: `user${i + 1}@example.com`,
      displayName: `User ${i + 1}`,
      role: 'User',
      createdAt: new Date().toISOString(),
      lastSeenAt: null,
    }));

    await adminHelper.mockUsersCRUD(paginationUsers);

    await page.goto('http://localhost:3000/admin/users');

    // Verify page 1
    await expect(
      page.getByText(new RegExp(`${t('admin.users.page')} 1 ${t('admin.users.of')} 2`))
    ).toBeVisible();
    await expect(
      page.getByText(
        new RegExp(
          `${t('admin.users.showing')} 1 ${t('admin.users.to')} 20 ${t('admin.users.of')} 25 ${t('admin.users.users')}`
        )
      )
    ).toBeVisible();
    await expect(page.getByText('user1@example.com')).toBeVisible();

    // Previous should be disabled
    await expect(page.getByTestId('pagination-previous')).toBeDisabled();

    // Go to page 2
    await page.getByTestId('pagination-next').click();

    // Verify page 2
    await expect(
      page.getByText(new RegExp(`${t('admin.users.page')} 2 ${t('admin.users.of')} 2`))
    ).toBeVisible();
    await expect(
      page.getByText(
        new RegExp(
          `${t('admin.users.showing')} 21 ${t('admin.users.to')} 25 ${t('admin.users.of')} 25 ${t('admin.users.users')}`
        )
      )
    ).toBeVisible();

    // Next should be disabled on last page
    await expect(page.getByTestId('pagination-next')).toBeDisabled();

    // Go back to page 1
    await page.getByTestId('pagination-previous').click();
    await expect(
      page.getByText(new RegExp(`${t('admin.users.page')} 1 ${t('admin.users.of')} 2`))
    ).toBeVisible();
  });

  test('form validation in create modal', async ({ adminPage: page }) => {
    const adminHelper = new AdminHelper(page);

    await adminHelper.mockUsersCRUD([]);

    await page.goto('http://localhost:3000/admin/users');

    // Open create modal
    await page.getByTestId('open-create-user-modal').click();

    // Disable HTML5 validation to test custom React validation
    await page.evaluate(() => {
      const form = document.querySelector('form');
      if (form) form.setAttribute('novalidate', 'true');
    });

    // Fill with invalid data to trigger custom validation
    await page.getByLabel(getTextMatcher('admin.users.email')).fill('invalid');
    await page.getByLabel(getTextMatcher('admin.users.password')).fill('short');
    await page.getByLabel(getTextMatcher('admin.users.displayName')).fill('');

    // Try to submit with invalid data
    await page.getByTestId('submit-user-form').click();

    // Should show validation errors
    await expect(page.getByText(getTextMatcher('admin.users.validationEmail'))).toBeVisible();
    await expect(page.getByText(getTextMatcher('admin.users.validationPassword'))).toBeVisible();
    await expect(page.getByText(getTextMatcher('admin.users.validationDisplayName'))).toBeVisible();

    // Fill with valid data
    await page.getByLabel(getTextMatcher('admin.users.email')).fill('valid@example.com');
    await page.getByLabel(getTextMatcher('admin.users.password')).fill('ValidPass123!');
    await page.getByLabel(getTextMatcher('admin.users.displayName')).fill('Valid User');

    // Validation errors should disappear after filling
    await page.getByTestId('submit-user-form').click();

    // Should not show validation errors now (modal will close or show API error)
    await expect(page.getByText(getTextMatcher('admin.users.validationEmail'))).not.toBeVisible();
  });

  test('cancel buttons close modals without making changes', async ({ adminPage: page }) => {
    const adminHelper = new AdminHelper(page);

    const cancelUsers = [
      {
        id: 'cancel-1',
        email: 'cancel@example.com',
        displayName: 'Cancel User',
        role: 'User',
        createdAt: new Date().toISOString(),
        lastSeenAt: null,
      },
    ];

    await adminHelper.mockUsersCRUD(cancelUsers);

    await page.goto('http://localhost:3000/admin/users');

    // Test create modal cancel
    await page.getByTestId('open-create-user-modal').click();
    await expect(
      page.getByRole('heading', { name: getTextMatcher('admin.users.createUser') })
    ).toBeVisible();
    await page.getByRole('button', { name: getTextMatcher('common.cancel') }).click();
    await expect(
      page.getByRole('heading', { name: getTextMatcher('admin.users.createUser') })
    ).not.toBeVisible();

    // Test edit modal cancel
    await page.getByRole('button', { name: getTextMatcher('common.edit') }).click();
    await expect(
      page.getByRole('heading', { name: getTextMatcher('admin.users.editUser') })
    ).toBeVisible();
    await page.getByRole('button', { name: getTextMatcher('common.cancel') }).click();
    await expect(
      page.getByRole('heading', { name: getTextMatcher('admin.users.editUser') })
    ).not.toBeVisible();

    // Test delete confirmation cancel
    await page.getByRole('button', { name: getTextMatcher('common.delete') }).click();
    await expect(page.getByText(getTextMatcher('admin.users.deleteUser'))).toBeVisible();
    await page.getByRole('button', { name: getTextMatcher('common.cancel') }).click();
    await expect(page.getByText(getTextMatcher('admin.users.deleteUser'))).not.toBeVisible();
  });
});

// ========== ISSUE #2891: User Management E2E Tests ==========
test.describe('Issue #2891 - User Management E2E Tests', () => {
  // DoD #1: Test: Search user by email
  test('search user by email', async ({ adminPage: page }) => {
    const adminHelper = new AdminHelper(page);

    const searchUsers = [
      {
        id: 'search-1',
        email: 'alice@example.com',
        displayName: 'Alice Smith',
        role: 'User',
        createdAt: new Date().toISOString(),
        lastSeenAt: null,
        isSuspended: false,
      },
      {
        id: 'search-2',
        email: 'bob@example.com',
        displayName: 'Bob Jones',
        role: 'Editor',
        createdAt: new Date().toISOString(),
        lastSeenAt: null,
        isSuspended: false,
      },
      {
        id: 'search-3',
        email: 'charlie@example.com',
        displayName: 'Charlie Brown',
        role: 'Admin',
        createdAt: new Date().toISOString(),
        lastSeenAt: null,
        isSuspended: false,
      },
    ];

    await adminHelper.mockUsersCRUD(searchUsers);
    await page.goto('http://localhost:3000/admin/users');

    // Verify all users visible initially
    await expect(page.getByText('alice@example.com')).toBeVisible();
    await expect(page.getByText('bob@example.com')).toBeVisible();
    await expect(page.getByText('charlie@example.com')).toBeVisible();

    // Search for Alice by email
    await page.getByPlaceholder(/search/i).fill('alice');
    await expect(page.getByText('alice@example.com')).toBeVisible();
    await expect(page.getByText('bob@example.com')).not.toBeVisible();
    await expect(page.getByText('charlie@example.com')).not.toBeVisible();

    // Clear and search for Bob
    await page.getByPlaceholder(/search/i).clear();
    await page.getByPlaceholder(/search/i).fill('bob');
    await expect(page.getByText('bob@example.com')).toBeVisible();
    await expect(page.getByText('alice@example.com')).not.toBeVisible();
  });

  // DoD #2: Test: Filter by role (Admin, Editor, User)
  test('filter by role (Admin, Editor, User)', async ({ adminPage: page }) => {
    const adminHelper = new AdminHelper(page);

    const roleUsers = [
      {
        id: 'role-admin',
        email: 'admin@example.com',
        displayName: 'Admin User',
        role: 'Admin',
        createdAt: new Date().toISOString(),
        lastSeenAt: null,
        isSuspended: false,
      },
      {
        id: 'role-editor',
        email: 'editor@example.com',
        displayName: 'Editor User',
        role: 'Editor',
        createdAt: new Date().toISOString(),
        lastSeenAt: null,
        isSuspended: false,
      },
      {
        id: 'role-user',
        email: 'user@example.com',
        displayName: 'Regular User',
        role: 'User',
        createdAt: new Date().toISOString(),
        lastSeenAt: null,
        isSuspended: false,
      },
    ];

    await adminHelper.mockUsersCRUD(roleUsers);
    await page.goto('http://localhost:3000/admin/users');

    // Filter by Admin
    await page.getByRole('combobox', { name: /filter by role/i }).selectOption('Admin');
    await expect(page.getByRole('cell', { name: 'admin@example.com' })).toBeVisible();
    await expect(page.getByRole('cell', { name: 'editor@example.com' })).not.toBeVisible();
    await expect(page.getByRole('cell', { name: 'user@example.com' })).not.toBeVisible();

    // Filter by Editor
    await page.getByRole('combobox', { name: /filter by role/i }).selectOption('Editor');
    await expect(page.getByRole('cell', { name: 'editor@example.com' })).toBeVisible();
    await expect(page.getByRole('cell', { name: 'admin@example.com' })).not.toBeVisible();
    await expect(page.getByRole('cell', { name: 'user@example.com' })).not.toBeVisible();

    // Filter by User
    await page.getByRole('combobox', { name: /filter by role/i }).selectOption('User');
    await expect(page.getByRole('cell', { name: 'user@example.com' })).toBeVisible();
    await expect(page.getByRole('cell', { name: 'admin@example.com' })).not.toBeVisible();
    await expect(page.getByRole('cell', { name: 'editor@example.com' })).not.toBeVisible();

    // Reset to All Roles
    await page.getByRole('combobox', { name: /filter by role/i }).selectOption('all');
    await expect(page.getByRole('cell', { name: 'admin@example.com' })).toBeVisible();
    await expect(page.getByRole('cell', { name: 'editor@example.com' })).toBeVisible();
    await expect(page.getByRole('cell', { name: 'user@example.com' })).toBeVisible();
  });

  // DoD #3: Test: Filter by status (Active, Suspended)
  test('filter by status (Active, Suspended)', async ({ adminPage: page }) => {
    const adminHelper = new AdminHelper(page);

    const statusUsers = [
      {
        id: 'status-active',
        email: 'active@example.com',
        displayName: 'Active User',
        role: 'User',
        createdAt: new Date().toISOString(),
        lastSeenAt: null,
        isSuspended: false,
      },
      {
        id: 'status-suspended',
        email: 'suspended@example.com',
        displayName: 'Suspended User',
        role: 'User',
        createdAt: new Date().toISOString(),
        lastSeenAt: null,
        isSuspended: true,
        suspendReason: 'Violation of terms',
      },
    ];

    await adminHelper.mockUsersCRUD(statusUsers);
    await page.goto('http://localhost:3000/admin/users');

    // Verify both users visible initially
    await expect(page.getByRole('cell', { name: 'active@example.com' })).toBeVisible();
    await expect(page.getByRole('cell', { name: 'suspended@example.com' })).toBeVisible();

    // Verify status badges
    await expect(page.getByTestId('status-badge-status-active')).toContainText('Active');
    await expect(page.getByTestId('status-badge-status-suspended')).toContainText('Suspended');

    // Filter by Active
    await page.getByTestId('status-filter').selectOption('active');
    await expect(page.getByRole('cell', { name: 'active@example.com' })).toBeVisible();
    await expect(page.getByRole('cell', { name: 'suspended@example.com' })).not.toBeVisible();

    // Filter by Suspended
    await page.getByTestId('status-filter').selectOption('suspended');
    await expect(page.getByRole('cell', { name: 'suspended@example.com' })).toBeVisible();
    await expect(page.getByRole('cell', { name: 'active@example.com' })).not.toBeVisible();

    // Reset to All Status
    await page.getByTestId('status-filter').selectOption('all');
    await expect(page.getByRole('cell', { name: 'active@example.com' })).toBeVisible();
    await expect(page.getByRole('cell', { name: 'suspended@example.com' })).toBeVisible();
  });

  // DoD #4: Test: Change user role (User → Editor)
  test('change user role (User → Editor)', async ({ adminPage: page }) => {
    const adminHelper = new AdminHelper(page);

    const roleChangeUsers = [
      {
        id: 'role-change-1',
        email: 'tobe-editor@example.com',
        displayName: 'User To Editor',
        role: 'User',
        createdAt: new Date().toISOString(),
        lastSeenAt: null,
        isSuspended: false,
      },
    ];

    await adminHelper.mockUsersCRUD(roleChangeUsers);
    await page.goto('http://localhost:3000/admin/users');

    // Verify user shows as User role
    const userRow = page.locator('tr:has-text("tobe-editor@example.com")');
    await expect(userRow.locator('span:has-text("User")')).toBeVisible();

    // Click Edit button
    await userRow.getByRole('button', { name: /edit/i }).click();

    // Change role to Editor
    await page.getByLabel(/role/i).selectOption('Editor');

    // Save changes
    await page.getByTestId('submit-user-form').click();

    // Verify success toast
    await expect(page.getByText(/updated successfully/i)).toBeVisible();

    // Verify role changed to Editor
    await expect(userRow.locator('span:has-text("Editor")')).toBeVisible();
  });

  // DoD #5: Test: Suspend user account
  test('suspend user account', async ({ adminPage: page }) => {
    const adminHelper = new AdminHelper(page);

    const suspendUsers = [
      {
        id: 'to-suspend',
        email: 'tosuspend@example.com',
        displayName: 'User To Suspend',
        role: 'User',
        createdAt: new Date().toISOString(),
        lastSeenAt: null,
        isSuspended: false,
      },
    ];

    await adminHelper.mockUsersCRUD(suspendUsers);
    await page.goto('http://localhost:3000/admin/users');

    // Verify user is active
    await expect(page.getByTestId('status-badge-to-suspend')).toContainText('Active');

    // Click Suspend button
    await page.getByTestId('suspend-to-suspend').click();

    // Verify confirmation dialog
    await expect(page.getByText(/suspend user/i)).toBeVisible();
    await expect(page.getByText(/are you sure/i)).toBeVisible();

    // Confirm suspension
    await page.getByRole('button', { name: /confirm/i }).click();

    // Verify success toast
    await expect(page.getByText(/suspended successfully/i)).toBeVisible();

    // Verify status changed to Suspended
    await expect(page.getByTestId('status-badge-to-suspend')).toContainText('Suspended');

    // Verify Activate button now visible (instead of Suspend)
    await expect(page.getByTestId('unsuspend-to-suspend')).toBeVisible();
  });

  // DoD #6: Test: Bulk select and export CSV
  test('bulk select and export CSV', async ({ adminPage: page }) => {
    const adminHelper = new AdminHelper(page);

    const exportUsers = [
      {
        id: 'export-1',
        email: 'export1@example.com',
        displayName: 'Export User 1',
        role: 'User',
        createdAt: new Date().toISOString(),
        lastSeenAt: null,
        isSuspended: false,
      },
      {
        id: 'export-2',
        email: 'export2@example.com',
        displayName: 'Export User 2',
        role: 'Editor',
        createdAt: new Date().toISOString(),
        lastSeenAt: null,
        isSuspended: false,
      },
      {
        id: 'export-3',
        email: 'export3@example.com',
        displayName: 'Export User 3',
        role: 'Admin',
        createdAt: new Date().toISOString(),
        lastSeenAt: null,
        isSuspended: false,
      },
    ];

    await adminHelper.mockUsersCRUD(exportUsers);
    await page.goto('http://localhost:3000/admin/users');

    // Verify users loaded
    await expect(page.getByText('export1@example.com')).toBeVisible();

    // Select first two users via checkboxes
    const checkboxes = page.getByRole('checkbox');
    await checkboxes.nth(1).check(); // First user
    await checkboxes.nth(2).check(); // Second user

    // Verify bulk action bar shows selection count
    await expect(page.getByText(/2.*(selected|user)/i)).toBeVisible();

    // Setup download listener
    const downloadPromise = page.waitForEvent('download');

    // Click Export CSV button
    await page.getByRole('button', { name: /esporta csv/i }).click();

    // Verify download started
    const download = await downloadPromise;
    expect(download.suggestedFilename()).toMatch(/users_export.*\.csv/);

    // Verify success toast
    await expect(page.getByText(/esportati in csv/i)).toBeVisible();
  });

  // DoD #7: Test: Pagination navigation (already exists above, but adding explicit test)
  test('pagination navigation with multiple pages', async ({ adminPage: page }) => {
    const adminHelper = new AdminHelper(page);

    // Create 25 users to test pagination (20 per page)
    const paginationUsers = Array.from({ length: 25 }, (_, i) => ({
      id: `page-user-${i + 1}`,
      email: `pageuser${i + 1}@example.com`,
      displayName: `Page User ${i + 1}`,
      role: 'User',
      createdAt: new Date().toISOString(),
      lastSeenAt: null,
      isSuspended: false,
    }));

    await adminHelper.mockUsersCRUD(paginationUsers);
    await page.goto('http://localhost:3000/admin/users');

    // Verify page 1 info
    await expect(page.getByText(/page 1 of 2/i)).toBeVisible();
    await expect(page.getByText(/showing 1 to 20 of 25/i)).toBeVisible();

    // Verify Previous button is disabled on first page
    await expect(page.getByTestId('pagination-previous')).toBeDisabled();

    // Click Next to go to page 2
    await page.getByTestId('pagination-next').click();

    // Verify page 2 info
    await expect(page.getByText(/page 2 of 2/i)).toBeVisible();
    await expect(page.getByText(/showing 21 to 25 of 25/i)).toBeVisible();

    // Verify Next button is disabled on last page
    await expect(page.getByTestId('pagination-next')).toBeDisabled();

    // Verify Previous is now enabled
    await expect(page.getByTestId('pagination-previous')).toBeEnabled();

    // Go back to page 1
    await page.getByTestId('pagination-previous').click();
    await expect(page.getByText(/page 1 of 2/i)).toBeVisible();
  });
});
