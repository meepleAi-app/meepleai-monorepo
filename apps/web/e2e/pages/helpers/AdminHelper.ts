/**
 * AdminHelper - Centralized admin utilities
 *
 * Handles all admin-related mocking operations including:
 * - Admin stats dashboard
 * - User management endpoints
 * - Request analytics
 * - Bulk export operations
 * - Configuration management
 *
 * Replaces legacy mockAdmin* functions scattered across admin test files.
 */

import { Page } from '@playwright/test';

const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

export class AdminHelper {
  constructor(private readonly page: Page) {}

  /**
   * Setup complete admin authentication with catch-all mocks
   * This method combines auth session + admin-specific route mocks
   * Equivalent to loginAsAdmin() from fixtures/auth.ts
   */
  async setupAdminAuth(skipNavigation: boolean = false): Promise<void> {
    // Setup authenticated admin session
    const adminUser = {
      id: 'admin-test-id',
      email: 'admin@meepleai.dev',
      displayName: 'Test Admin',
      role: 'Admin' as const,
    };

    // Mock /auth/me
    await this.page.route(`${apiBase}/api/v1/auth/me`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          user: adminUser,
          expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
        }),
      });
    });

    // Catch-all for unmocked admin API calls
    await this.page.route(`${apiBase}/api/v1/admin/**`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: [] }),
      });
    });

    // Navigate if not skipped
    if (!skipNavigation) {
      await this.page.goto('/');
      await this.page.waitForLoadState('networkidle');
    }
  }

  /**
   * Mock admin stats endpoint
   */
  async mockAdminStats(stats?: {
    totalUsers?: number;
    totalGames?: number;
    totalQueries?: number;
    avgLatencyMs?: number;
    totalTokens?: number;
    successRate?: number;
  }): Promise<void> {
    const defaultStats = {
      totalUsers: 10,
      totalGames: 5,
      totalQueries: 100,
      avgLatencyMs: 350,
      totalTokens: 50000,
      successRate: 0.95,
      ...stats,
    };

    await this.page.route(`${apiBase}/api/v1/admin/stats`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(defaultStats),
      });
    });
  }

  /**
   * Mock admin users list endpoint
   */
  async mockAdminUsers(users?: Array<any>): Promise<void> {
    const defaultUsers = users || [
      {
        id: 'user-1',
        email: 'admin@example.com',
        displayName: 'Admin User',
        role: 'Admin',
        createdAt: new Date().toISOString(),
      },
      {
        id: 'user-2',
        email: 'editor@example.com',
        displayName: 'Editor User',
        role: 'Editor',
        createdAt: new Date().toISOString(),
      },
    ];

    await this.page.route(`${apiBase}/api/v1/admin/users*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          users: defaultUsers,
          totalCount: defaultUsers.length,
          pageNumber: 1,
          pageSize: 10,
          totalPages: 1,
        }),
      });
    });
  }

  /**
   * Mock admin requests/analytics endpoint
   */
  async mockAdminRequests(requests?: Array<any>): Promise<void> {
    const defaultRequests = requests || [
      {
        id: 'req-1',
        userId: 'user-1',
        gameId: 'game-1',
        endpoint: 'qa',
        query: 'Test query',
        responseSnippet: 'Test response',
        latencyMs: 320,
        tokenCount: 740,
        confidence: 0.92,
        status: 'Success',
        createdAt: new Date().toISOString(),
        model: 'gpt-4.1-mini',
      },
    ];

    await this.page.route(new RegExp(`${apiBase}/api/v1/admin/requests.*`), async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ requests: defaultRequests }),
      });
    });
  }

  /**
   * Mock configuration endpoints
   */
  async mockConfiguration(configs?: Record<string, any>): Promise<void> {
    const defaultConfigs = configs || {
      'Features.EnableRegistration': true,
      'Features.EnableOAuth': true,
      'RateLimit.RequestsPerMinute': 60,
    };

    await this.page.route(`${apiBase}/api/v1/admin/configuration*`, async route => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(defaultConfigs),
        });
      } else if (route.request().method() === 'PUT') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ success: true }),
        });
      }
    });
  }

  /**
   * Mock bulk export endpoint
   */
  async mockBulkExport(csvData?: string): Promise<void> {
    const defaultCsv =
      csvData || 'id,email,role\nuser-1,admin@example.com,Admin\nuser-2,editor@example.com,Editor';

    await this.page.route(`${apiBase}/api/v1/admin/export*`, async route => {
      await route.fulfill({
        status: 200,
        headers: {
          'Content-Type': 'text/csv',
          'Content-Disposition': 'attachment; filename="export.csv"',
        },
        body: defaultCsv,
      });
    });
  }

  /**
   * Mock user update endpoint
   */
  async mockUserUpdate(success: boolean = true): Promise<void> {
    await this.page.route(`${apiBase}/api/v1/admin/users/*`, async route => {
      if (route.request().method() === 'PUT' || route.request().method() === 'PATCH') {
        if (success) {
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({ success: true, message: 'User updated' }),
          });
        } else {
          await route.fulfill({
            status: 400,
            contentType: 'application/json',
            body: JSON.stringify({ error: 'Update failed' }),
          });
        }
      } else if (route.request().method() === 'DELETE') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ success: true }),
        });
      }
    });
  }

  /**
   * Mock complete CRUD operations for user management with stateful logic
   * Handles GET (with search/filter/sort/pagination), POST (create), PUT (update), DELETE
   *
   * @param initialUsers - Starting users list (defaults to sample users)
   * @returns Object with methods to access/modify user state during test
   */
  async mockUsersCRUD(initialUsers?: Array<any>): Promise<{
    users: Array<any>;
    addUser: (user: any) => void;
    removeUser: (id: string) => void;
    updateUser: (id: string, updates: any) => void;
  }> {
    const defaultUsers = initialUsers || [
      {
        id: 'user-1',
        email: 'existing@example.com',
        displayName: 'Existing User',
        role: 'User',
        createdAt: new Date('2024-01-01T10:00:00Z').toISOString(),
        lastSeenAt: new Date('2024-01-15T14:30:00Z').toISOString(),
      },
      {
        id: 'user-2',
        email: 'editor@example.com',
        displayName: 'Editor User',
        role: 'Editor',
        createdAt: new Date('2024-01-02T10:00:00Z').toISOString(),
        lastSeenAt: null,
      },
    ];

    const users = [...defaultUsers];
    let nextUserId = users.length + 1;

    // Mock GET /api/v1/admin/users with full query support
    await this.page.route(new RegExp(`${apiBase}/api/v1/admin/users\\??.*`), async route => {
      if (route.request().method() === 'GET') {
        const url = new URL(route.request().url());
        const search = url.searchParams.get('search');
        const role = url.searchParams.get('role');
        const status = url.searchParams.get('status');
        const sortBy = url.searchParams.get('sortBy') || 'createdAt';
        const sortOrder = url.searchParams.get('sortOrder') || 'desc';
        const page = parseInt(url.searchParams.get('page') || '1');
        const limit = parseInt(url.searchParams.get('limit') || '20');

        let filteredUsers = [...users];

        // Apply search filter
        if (search) {
          const term = search.toLowerCase();
          filteredUsers = filteredUsers.filter(
            u => u.email.toLowerCase().includes(term) || u.displayName.toLowerCase().includes(term)
          );
        }

        // Apply role filter
        if (role && role !== 'all') {
          filteredUsers = filteredUsers.filter(u => u.role === role);
        }

        // Apply status filter (active/suspended)
        if (status && status !== 'all') {
          const isSuspended = status === 'suspended';
          filteredUsers = filteredUsers.filter(u => (u.isSuspended || false) === isSuspended);
        }

        // Apply sorting
        filteredUsers.sort((a, b) => {
          let aVal: any = a[sortBy as keyof typeof a] || '';
          let bVal: any = b[sortBy as keyof typeof b] || '';

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

        // Apply pagination
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
            pageSize: limit,
          }),
        });
      }
    });

    // Mock POST /api/v1/admin/users (Create)
    await this.page.route(`${apiBase}/api/v1/admin/users`, async route => {
      if (route.request().method() === 'POST') {
        const requestData = JSON.parse(route.request().postData() || '{}');
        const newUser = {
          id: `user-${nextUserId++}`,
          email: requestData.email,
          displayName: requestData.displayName,
          role: requestData.role || 'User',
          createdAt: new Date().toISOString(),
          lastSeenAt: null,
        };
        users.push(newUser);

        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify(newUser),
        });
      }
    });

    // Mock PUT /api/v1/admin/users/{id} (Update) and DELETE
    await this.page.route(new RegExp(`${apiBase}/api/v1/admin/users/.+`), async route => {
      if (route.request().method() === 'PUT') {
        const userId = route.request().url().split('/').pop();
        const requestData = JSON.parse(route.request().postData() || '{}');
        const userIndex = users.findIndex(u => u.id === userId);

        if (userIndex !== -1) {
          users[userIndex] = {
            ...users[userIndex],
            ...requestData,
          };

          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify(users[userIndex]),
          });
        } else {
          await route.fulfill({
            status: 404,
            contentType: 'application/json',
            body: JSON.stringify({ error: 'User not found' }),
          });
        }
      } else if (route.request().method() === 'DELETE') {
        const userId = route.request().url().split('/').pop();
        const userIndex = users.findIndex(u => u.id === userId);

        if (userIndex !== -1) {
          users.splice(userIndex, 1);
          await route.fulfill({ status: 204 });
        } else {
          await route.fulfill({
            status: 404,
            contentType: 'application/json',
            body: JSON.stringify({ error: 'User not found' }),
          });
        }
      } else if (route.request().method() === 'POST') {
        // Handle suspend/unsuspend endpoints
        const urlPath = route.request().url();
        const suspendMatch = urlPath.match(/\/users\/([^/]+)\/suspend$/);
        const unsuspendMatch = urlPath.match(/\/users\/([^/]+)\/unsuspend$/);

        if (suspendMatch) {
          const userId = suspendMatch[1];
          const requestData = JSON.parse(route.request().postData() || '{}');
          const userIndex = users.findIndex(u => u.id === userId);

          if (userIndex !== -1) {
            users[userIndex] = {
              ...users[userIndex],
              isSuspended: true,
              suspendReason: requestData.reason || 'Suspended by admin',
            };

            await route.fulfill({
              status: 200,
              contentType: 'application/json',
              body: JSON.stringify({ user: users[userIndex] }),
            });
          } else {
            await route.fulfill({
              status: 404,
              contentType: 'application/json',
              body: JSON.stringify({ error: 'User not found' }),
            });
          }
        } else if (unsuspendMatch) {
          const userId = unsuspendMatch[1];
          const userIndex = users.findIndex(u => u.id === userId);

          if (userIndex !== -1) {
            users[userIndex] = {
              ...users[userIndex],
              isSuspended: false,
              suspendReason: null,
            };

            await route.fulfill({
              status: 200,
              contentType: 'application/json',
              body: JSON.stringify({ user: users[userIndex] }),
            });
          } else {
            await route.fulfill({
              status: 404,
              contentType: 'application/json',
              body: JSON.stringify({ error: 'User not found' }),
            });
          }
        }
      }
    });

    // Return state access methods for advanced test scenarios
    return {
      users,
      addUser: (user: any) => users.push(user),
      removeUser: (id: string) => {
        const index = users.findIndex(u => u.id === id);
        if (index !== -1) users.splice(index, 1);
      },
      updateUser: (id: string, updates: any) => {
        const index = users.findIndex(u => u.id === id);
        if (index !== -1) users[index] = { ...users[index], ...updates };
      },
    };
  }

  /**
   * Mock prompts management endpoint
   */
  async mockPrompts(prompts?: Array<any>): Promise<void> {
    const defaultPrompts = prompts || [
      {
        id: 'prompt-1',
        name: 'QA Prompt',
        template: 'Answer the question: {{query}}',
        category: 'qa',
        isActive: true,
      },
    ];

    await this.page.route(`${apiBase}/api/v1/admin/prompts*`, async route => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ prompts: defaultPrompts }),
        });
      } else if (route.request().method() === 'POST' || route.request().method() === 'PUT') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ success: true }),
        });
      }
    });
  }
}
