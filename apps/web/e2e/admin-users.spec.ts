import { test, expect, Page } from '@playwright/test';

const apiBase = 'http://localhost:8080';

async function mockAuthenticatedAdmin(page: Page) {
  await page.route(`${apiBase}/api/v1/auth/me`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: {
          id: 'admin-e2e',
          email: 'admin@e2e.com',
          displayName: 'E2E Admin',
          role: 'Admin'
        },
        expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString()
      })
    });
  });
}

test.describe('Admin User Management E2E Flow', () => {
  test('complete user lifecycle: create → edit → delete', async ({ page }) => {
    // Setup: Mock authenticated admin
    await mockAuthenticatedAdmin(page);

    const sampleUsers = [
      {
        id: 'user-1',
        email: 'existing@example.com',
        displayName: 'Existing User',
        role: 'User',
        createdAt: new Date('2024-01-01T10:00:00Z').toISOString(),
        lastSeenAt: new Date('2024-01-15T14:30:00Z').toISOString()
      },
      {
        id: 'user-2',
        email: 'editor@example.com',
        displayName: 'Editor User',
        role: 'Editor',
        createdAt: new Date('2024-01-02T10:00:00Z').toISOString(),
        lastSeenAt: null
      }
    ];

    let nextUserId = 3;
    let users = [...sampleUsers];

    // Mock GET /api/v1/admin/users
    await page.route(new RegExp(`${apiBase}/api/v1/admin/users\\??.*`), async (route) => {
      if (route.request().method() === 'GET') {
        const url = new URL(route.request().url());
        const search = url.searchParams.get('search');
        const role = url.searchParams.get('role');
        const page = parseInt(url.searchParams.get('page') || '1');
        const limit = parseInt(url.searchParams.get('limit') || '20');

        let filteredUsers = [...users];

        if (search) {
          const term = search.toLowerCase();
          filteredUsers = filteredUsers.filter(
            (u) =>
              u.email.toLowerCase().includes(term) ||
              u.displayName.toLowerCase().includes(term)
          );
        }

        if (role && role !== 'all') {
          filteredUsers = filteredUsers.filter((u) => u.role === role);
        }

        const start = (page - 1) * limit;
        const end = start + limit;
        const pagedUsers = filteredUsers.slice(start, end);

        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: pagedUsers,
            total: filteredUsers.length,
            page,
            pageSize: limit
          })
        });
      }
    });

    // Mock POST /api/v1/admin/users (Create)
    await page.route(`${apiBase}/api/v1/admin/users`, async (route) => {
      if (route.request().method() === 'POST') {
        const requestData = JSON.parse(route.request().postData() || '{}');
        const newUser = {
          id: `user-${nextUserId++}`,
          email: requestData.email,
          displayName: requestData.displayName,
          role: requestData.role || 'User',
          createdAt: new Date().toISOString(),
          lastSeenAt: null
        };
        users.push(newUser);

        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify(newUser)
        });
      }
    });

    // Mock PUT /api/v1/admin/users/{id} (Update)
    await page.route(new RegExp(`${apiBase}/api/v1/admin/users/user-\\d+`), async (route) => {
      if (route.request().method() === 'PUT') {
        const userId = route.request().url().split('/').pop();
        const requestData = JSON.parse(route.request().postData() || '{}');
        const userIndex = users.findIndex((u) => u.id === userId);

        if (userIndex !== -1) {
          users[userIndex] = {
            ...users[userIndex],
            ...requestData
          };

          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify(users[userIndex])
          });
        } else {
          await route.fulfill({
            status: 404,
            contentType: 'application/json',
            body: JSON.stringify({ error: 'User not found' })
          });
        }
      } else if (route.request().method() === 'DELETE') {
        const userId = route.request().url().split('/').pop();
        const userIndex = users.findIndex((u) => u.id === userId);

        if (userIndex !== -1) {
          users.splice(userIndex, 1);
          await route.fulfill({ status: 204 });
        } else {
          await route.fulfill({
            status: 404,
            contentType: 'application/json',
            body: JSON.stringify({ error: 'User not found' })
          });
        }
      }
    });

    // Navigate to user management page
    await page.goto('http://localhost:3000/admin/users');

    // Verify initial user list loads
    await expect(page.getByText('User Management')).toBeVisible();
    await expect(page.getByText('existing@example.com')).toBeVisible();
    await expect(page.getByText('editor@example.com')).toBeVisible();

    // ===== CREATE USER =====
    // Click Create User button (open modal)
    await page.getByTestId('open-create-user-modal').click();

    // Fill out create form
    await expect(page.getByRole('heading', { name: 'Create User' })).toBeVisible();
    await page.getByLabel('Email *').fill('newuser@example.com');
    await page.getByLabel('Password *').fill('SecurePass123!');
    await page.getByLabel('Display Name *').fill('New Test User');
    await page.getByLabel('Role *').selectOption('Editor');

    // Submit form
    await page.getByTestId('submit-user-form').click();

    // Verify success toast appears
    await expect(page.getByText(/created successfully/)).toBeVisible();

    // Wait for toast to disappear or close it to avoid interference
    await page.waitForTimeout(1000); // Give toast time to auto-dismiss

    // Verify new user appears in table (use role to be specific with exact match)
    await expect(page.getByRole('cell', { name: 'newuser@example.com', exact: true })).toBeVisible();
    await expect(page.getByRole('cell', { name: 'New Test User', exact: true })).toBeVisible();

    // ===== EDIT USER =====
    // Find and click Edit button for the newly created user
    const newUserRow = page.locator('tr:has-text("newuser@example.com")');
    await newUserRow.getByRole('button', { name: 'Edit' }).click();

    // Verify edit modal opens with pre-filled data
    await expect(page.getByRole('heading', { name: 'Edit User' })).toBeVisible();
    await expect(page.getByLabel('Email *')).toHaveValue('newuser@example.com');

    // Update display name and role
    await page.getByLabel('Display Name *').fill('Updated User Name');
    await page.getByLabel('Role *').selectOption('User');

    // Save changes
    await page.getByRole('button', { name: /Save Changes/i }).click();

    // Verify success toast
    await expect(page.getByText(/updated successfully/)).toBeVisible();

    // Wait for toast to dismiss
    await page.waitForTimeout(1000);

    // Verify updates appear in table (use role for specificity)
    await expect(page.getByRole('cell', { name: 'Updated User Name' })).toBeVisible();

    // ===== SEARCH FUNCTIONALITY =====
    // Search for the user
    await page.getByPlaceholder('Search by email or name...').fill('Updated');

    // Verify filtered results (use role to avoid toast interference)
    await expect(page.getByRole('cell', { name: 'newuser@example.com', exact: true })).toBeVisible();
    await expect(page.getByText('existing@example.com')).not.toBeVisible();

    // Clear search
    await page.getByPlaceholder('Search by email or name...').clear();

    // ===== ROLE FILTER =====
    // Filter by User role
    await page.locator('select').first().selectOption('User');

    // Verify only User role users shown
    await expect(page.getByText('newuser@example.com')).toBeVisible();
    await expect(page.getByText('existing@example.com')).toBeVisible();
    await expect(page.getByText('editor@example.com')).not.toBeVisible();

    // Reset filter
    await page.locator('select').first().selectOption('all');

    // ===== DELETE USER =====
    // Click Delete button for the user we created
    const userRowToDelete = page.locator('tr:has-text("newuser@example.com")');
    await userRowToDelete.getByRole('button', { name: 'Delete' }).click();

    // Verify confirmation dialog
    await expect(page.getByText('Delete User')).toBeVisible();
    await expect(page.getByText(/Are you sure you want to delete newuser@example.com/)).toBeVisible();

    // Confirm deletion
    await page.getByRole('button', { name: 'Confirm' }).click();

    // Verify success toast
    await expect(page.getByText(/deleted successfully/)).toBeVisible();

    // Wait for toast to dismiss
    await page.waitForTimeout(1000);

    // Verify user no longer in list
    await expect(page.getByText('newuser@example.com')).not.toBeVisible();
  });

  test('bulk delete functionality', async ({ page }) => {
    await mockAuthenticatedAdmin(page);

    const users = [
      {
        id: 'bulk-1',
        email: 'bulk1@example.com',
        displayName: 'Bulk User 1',
        role: 'User',
        createdAt: new Date().toISOString(),
        lastSeenAt: null
      },
      {
        id: 'bulk-2',
        email: 'bulk2@example.com',
        displayName: 'Bulk User 2',
        role: 'User',
        createdAt: new Date().toISOString(),
        lastSeenAt: null
      },
      {
        id: 'bulk-3',
        email: 'bulk3@example.com',
        displayName: 'Bulk User 3',
        role: 'User',
        createdAt: new Date().toISOString(),
        lastSeenAt: null
      }
    ];

    let userList = [...users];

    // Mock GET
    await page.route(new RegExp(`${apiBase}/api/v1/admin/users\\??.*`), async (route) => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: userList,
            total: userList.length,
            page: 1,
            pageSize: 20
          })
        });
      }
    });

    // Mock DELETE
    await page.route(new RegExp(`${apiBase}/api/v1/admin/users/bulk-\\d+`), async (route) => {
      if (route.request().method() === 'DELETE') {
        const userId = route.request().url().split('/').pop();
        userList = userList.filter((u) => u.id !== userId);
        await route.fulfill({ status: 204 });
      }
    });

    await page.goto('http://localhost:3000/admin/users');

    // Verify users loaded
    await expect(page.getByText('bulk1@example.com')).toBeVisible();
    await expect(page.getByText('bulk2@example.com')).toBeVisible();

    // Select first two users
    const checkboxes = page.getByRole('checkbox');
    await checkboxes.nth(1).check(); // First user
    await checkboxes.nth(2).check(); // Second user

    // Verify bulk delete button appears with count
    await expect(page.getByText('Delete Selected (2)')).toBeVisible();

    // Click bulk delete
    await page.getByText('Delete Selected (2)').click();

    // Verify confirmation dialog
    await expect(page.getByText('Delete Multiple Users')).toBeVisible();
    await expect(page.getByText(/2 user\(s\)/)).toBeVisible();

    // Confirm bulk delete
    await page.getByRole('button', { name: 'Confirm' }).click();

    // Verify success toast
    await expect(page.getByText(/2 user\(s\) deleted successfully/)).toBeVisible();

    // Verify users removed (would need page reload in real scenario)
  });

  test('sorting columns', async ({ page }) => {
    await mockAuthenticatedAdmin(page);

    const users = [
      {
        id: 'sort-1',
        email: 'alpha@example.com',
        displayName: 'Alpha',
        role: 'Admin',
        createdAt: new Date('2024-01-03T00:00:00Z').toISOString(),
        lastSeenAt: null
      },
      {
        id: 'sort-2',
        email: 'beta@example.com',
        displayName: 'Beta',
        role: 'User',
        createdAt: new Date('2024-01-01T00:00:00Z').toISOString(),
        lastSeenAt: null
      },
      {
        id: 'sort-3',
        email: 'gamma@example.com',
        displayName: 'Gamma',
        role: 'Editor',
        createdAt: new Date('2024-01-02T00:00:00Z').toISOString(),
        lastSeenAt: null
      }
    ];

    await page.route(new RegExp(`${apiBase}/api/v1/admin/users\\??.*`), async (route) => {
      if (route.request().method() === 'GET') {
        const url = new URL(route.request().url());
        const sortBy = url.searchParams.get('sortBy') || 'createdAt';
        const sortOrder = url.searchParams.get('sortOrder') || 'desc';

        let sortedUsers = [...users];
        sortedUsers.sort((a, b) => {
          let aVal: string | Date = a[sortBy as keyof typeof a] || '';
          let bVal: string | Date = b[sortBy as keyof typeof b] || '';

          if (sortBy === 'createdAt') {
            aVal = new Date(a.createdAt);
            bVal = new Date(b.createdAt);
          }

          if (sortOrder === 'asc') {
            return aVal < bVal ? -1 : aVal > bVal ? 1 : 0;
          } else {
            return aVal > bVal ? -1 : aVal < bVal ? 1 : 0;
          }
        });

        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: sortedUsers,
            total: sortedUsers.length,
            page: 1,
            pageSize: 20
          })
        });
      }
    });

    await page.goto('http://localhost:3000/admin/users');

    // Default: sorted by createdAt desc (newest first)
    await expect(page.getByText('alpha@example.com')).toBeVisible();

    // Click Email header to sort by email
    await page.getByText(/Email/).click();

    // Should show sort indicator
    await expect(page.getByText(/Email ↑/)).toBeVisible();

    // Click again to reverse sort
    await page.getByText(/Email/).click();
    await expect(page.getByText(/Email ↓/)).toBeVisible();
  });

  test('pagination navigation', async ({ page }) => {
    await mockAuthenticatedAdmin(page);

    // Create 25 users to test pagination (20 per page)
    const users = Array.from({ length: 25 }, (_, i) => ({
      id: `paginate-${i + 1}`,
      email: `user${i + 1}@example.com`,
      displayName: `User ${i + 1}`,
      role: 'User',
      createdAt: new Date().toISOString(),
      lastSeenAt: null
    }));

    await page.route(new RegExp(`${apiBase}/api/v1/admin/users\\??.*`), async (route) => {
      if (route.request().method() === 'GET') {
        const url = new URL(route.request().url());
        const page = parseInt(url.searchParams.get('page') || '1');
        const limit = parseInt(url.searchParams.get('limit') || '20');

        const start = (page - 1) * limit;
        const end = start + limit;
        const pagedUsers = users.slice(start, end);

        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: pagedUsers,
            total: users.length,
            page,
            pageSize: limit
          })
        });
      }
    });

    await page.goto('http://localhost:3000/admin/users');

    // Verify page 1
    await expect(page.getByText('Page 1 of 2')).toBeVisible();
    await expect(page.getByText('Showing 1 to 20 of 25 users')).toBeVisible();
    await expect(page.getByText('user1@example.com')).toBeVisible();

    // Previous should be disabled
    await expect(page.getByTestId('pagination-previous')).toBeDisabled();

    // Go to page 2
    await page.getByTestId('pagination-next').click();

    // Verify page 2
    await expect(page.getByText('Page 2 of 2')).toBeVisible();
    await expect(page.getByText('Showing 21 to 25 of 25 users')).toBeVisible();

    // Next should be disabled on last page
    await expect(page.getByTestId('pagination-next')).toBeDisabled();

    // Go back to page 1
    await page.getByTestId('pagination-previous').click();
    await expect(page.getByText('Page 1 of 2')).toBeVisible();
  });

  test('form validation in create modal', async ({ page }) => {
    await mockAuthenticatedAdmin(page);

    await page.route(new RegExp(`${apiBase}/api/v1/admin/users\\??.*`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [],
          total: 0,
          page: 1,
          pageSize: 20
        })
      });
    });

    await page.goto('http://localhost:3000/admin/users');

    // Open create modal
    await page.getByTestId('open-create-user-modal').click();

    // Try to submit empty form
    await page.getByTestId('submit-user-form').click();

    // Should show validation errors
    await expect(page.getByText('Valid email is required')).toBeVisible();
    await expect(page.getByText(/at least 8 characters/)).toBeVisible();
    await expect(page.getByText('Display name is required')).toBeVisible();

    // Fill with valid data
    await page.getByLabel('Email *').fill('valid@example.com');
    await page.getByLabel('Password *').fill('ValidPass123!');
    await page.getByLabel('Display Name *').fill('Valid User');

    // Validation errors should disappear after filling
    await page.getByTestId('submit-user-form').click();

    // Should not show validation errors now (modal will close or show API error)
    await expect(page.getByText('Valid email is required')).not.toBeVisible();
  });

  test('cancel buttons close modals without making changes', async ({ page }) => {
    await mockAuthenticatedAdmin(page);

    const users = [
      {
        id: 'cancel-1',
        email: 'cancel@example.com',
        displayName: 'Cancel User',
        role: 'User',
        createdAt: new Date().toISOString(),
        lastSeenAt: null
      }
    ];

    await page.route(new RegExp(`${apiBase}/api/v1/admin/users\\??.*`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: users,
          total: users.length,
          page: 1,
          pageSize: 20
        })
      });
    });

    await page.goto('http://localhost:3000/admin/users');

    // Test create modal cancel
    await page.getByTestId('open-create-user-modal').click();
    await expect(page.getByRole('heading', { name: 'Create User' })).toBeVisible();
    await page.getByRole('button', { name: 'Cancel' }).click();
    await expect(page.getByRole('heading', { name: 'Create User' })).not.toBeVisible();

    // Test edit modal cancel
    await page.getByRole('button', { name: 'Edit' }).click();
    await expect(page.getByRole('heading', { name: 'Edit User' })).toBeVisible();
    await page.getByRole('button', { name: 'Cancel' }).click();
    await expect(page.getByRole('heading', { name: 'Edit User' })).not.toBeVisible();

    // Test delete confirmation cancel
    await page.getByRole('button', { name: 'Delete' }).click();
    await expect(page.getByText('Delete User')).toBeVisible();
    await page.getByRole('button', { name: 'Cancel' }).click();
    await expect(page.getByText('Delete User')).not.toBeVisible();
  });
});
