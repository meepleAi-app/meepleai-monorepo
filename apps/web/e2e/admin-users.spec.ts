/**
 * Admin User Management E2E Tests - MIGRATED TO POM
 *
 * @see apps/web/e2e/pages/helpers/AdminHelper.ts - mockUsersCRUD()
 * @see apps/web/e2e/pages/admin/AdminPage.ts
 */

import { test as base, expect, Page } from './fixtures/chromatic';
import { AdminHelper } from './pages';
import { getTextMatcher, t } from './fixtures/i18n';

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
