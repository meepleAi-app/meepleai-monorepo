import type { Meta, StoryObj } from '@storybook/react';
import { within, userEvent, expect } from 'storybook/test';
import { ManagementPageClient } from '../../client';
import { AuthProvider } from '@/components/auth/AuthProvider';

const meta: Meta<typeof ManagementPageClient> = {
  title: 'Admin/Management/Visual Tests',
  component: ManagementPageClient,
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      disableSnapshot: false,
      delay: 500,
    },
  },
  decorators: [
    Story => (
      <AuthProvider>
        <div className="min-h-screen bg-gray-50">
          <Story />
        </div>
      </AuthProvider>
    ),
  ],
};

export default meta;
type Story = StoryObj<typeof ManagementPageClient>;

const mockApiKeysData = {
  keys: [
    {
      apiKey: {
        id: '11111111-1111-1111-1111-111111111111',
        keyName: 'Production API Key',
        keyPrefix: 'mpl_prod_',
        scopes: 'read:games,read:rules,write:sessions',
        createdAt: '2025-01-01T00:00:00Z',
        expiresAt: '2026-01-01T00:00:00Z',
        lastUsedAt: '2025-01-15T10:30:00Z',
        isActive: true,
      },
      usageStats: {
        keyId: '11111111-1111-1111-1111-111111111111',
        totalUsageCount: 1543,
        lastUsedAt: '2025-01-15T10:30:00Z',
        usageCountLast24Hours: 45,
        usageCountLast7Days: 312,
        usageCountLast30Days: 1234,
        averageRequestsPerDay: 41.13,
      },
    },
    {
      apiKey: {
        id: '22222222-2222-2222-2222-222222222222',
        keyName: 'Development API Key',
        keyPrefix: 'mpl_dev_',
        scopes: 'read:games,read:rules',
        createdAt: '2025-06-15T12:00:00Z',
        expiresAt: null,
        lastUsedAt: '2025-01-14T15:20:00Z',
        isActive: true,
      },
      usageStats: {
        keyId: '22222222-2222-2222-2222-222222222222',
        totalUsageCount: 234,
        lastUsedAt: '2025-01-14T15:20:00Z',
        usageCountLast24Hours: 12,
        usageCountLast7Days: 89,
        usageCountLast30Days: 234,
        averageRequestsPerDay: 7.8,
      },
    },
  ],
  count: 2,
  filters: {
    userId: null,
    includeRevoked: false,
  },
};

const mockUsersData = {
  users: [
    {
      id: 'user1-1111-1111-1111-111111111111',
      email: 'admin@meepleai.dev',
      role: 'Admin',
      isEmailConfirmed: true,
      createdAt: '2024-01-01T00:00:00Z',
      lastLoginAt: '2025-01-15T09:00:00Z',
    },
    {
      id: 'user2-2222-2222-2222-222222222222',
      email: 'user1@example.com',
      role: 'User',
      isEmailConfirmed: true,
      createdAt: '2024-06-15T00:00:00Z',
      lastLoginAt: '2025-01-14T14:30:00Z',
    },
    {
      id: 'user3-3333-3333-3333-333333333333',
      email: 'user2@example.com',
      role: 'User',
      isEmailConfirmed: false,
      createdAt: '2024-12-01T00:00:00Z',
      lastLoginAt: null,
    },
  ],
  totalCount: 3,
};

const mockActivityData = {
  events: [
    {
      id: 'activity1',
      timestamp: '2025-01-15T10:30:00Z',
      userId: 'user1-1111-1111-1111-111111111111',
      userEmail: 'admin@meepleai.dev',
      action: 'API_KEY_CREATED',
      resource: 'Production API Key',
      details: {
        keyId: '11111111-1111-1111-1111-111111111111',
        scopes: 'read:games,write:sessions',
      },
    },
    {
      id: 'activity2',
      timestamp: '2025-01-15T09:15:00Z',
      userId: 'user2-2222-2222-2222-222222222222',
      userEmail: 'user1@example.com',
      action: 'LOGIN',
      resource: 'Authentication',
      details: { method: 'password', ipAddress: '192.168.1.100' },
    },
    {
      id: 'activity3',
      timestamp: '2025-01-15T08:45:00Z',
      userId: 'user1-1111-1111-1111-111111111111',
      userEmail: 'admin@meepleai.dev',
      action: 'USER_UPDATED',
      resource: 'user2@example.com',
      details: { field: 'role', oldValue: 'User', newValue: 'Admin' },
    },
  ],
  totalCount: 3,
};

/**
 * Visual Test: Default view (API Keys tab)
 */
export const DefaultView: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/api-keys/stats': mockApiKeysData,
    },
  },
};

/**
 * Visual Test: API Keys tab - Empty state
 */
export const ApiKeysEmpty: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/api-keys/stats': {
        keys: [],
        count: 0,
        filters: {
          userId: null,
          includeRevoked: false,
        },
      },
    },
  },
};

/**
 * Visual Test: API Keys tab - Loading state
 */
export const ApiKeysLoading: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/api-keys/stats': {
        delay: 'infinite',
      },
    },
  },
};

/**
 * Visual Test: Users tab
 */
export const UsersTab: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/api-keys/stats': mockApiKeysData,
      '/api/v1/admin/users': mockUsersData,
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    // Wait for initial load
    await canvas.findByText('Production API Key');

    // Switch to Users tab
    const usersTab = await canvas.findByRole('tab', { name: /users/i });
    await userEvent.click(usersTab);

    // Verify users loaded
    await expect(canvas.findByText('admin@meepleai.dev')).resolves.toBeInTheDocument();
  },
};

/**
 * Visual Test: Users tab - Empty state
 */
export const UsersEmpty: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/api-keys/stats': mockApiKeysData,
      '/api/v1/admin/users': {
        users: [],
        totalCount: 0,
      },
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    // Switch to Users tab
    const usersTab = await canvas.findByRole('tab', { name: /users/i });
    await userEvent.click(usersTab);
  },
};

/**
 * Visual Test: Users tab - Loading state
 */
export const UsersLoading: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/api-keys/stats': mockApiKeysData,
      '/api/v1/admin/users': {
        delay: 'infinite',
      },
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    // Switch to Users tab
    const usersTab = await canvas.findByRole('tab', { name: /users/i });
    await userEvent.click(usersTab);
  },
};

/**
 * Visual Test: Activity tab
 */
export const ActivityTab: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/api-keys/stats': mockApiKeysData,
      '/api/v1/admin/activity': mockActivityData,
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    // Wait for initial load
    await canvas.findByText('Production API Key');

    // Switch to Activity tab
    const activityTab = await canvas.findByRole('tab', { name: /activity/i });
    await userEvent.click(activityTab);

    // Verify activity loaded
    await expect(canvas.findByText('API_KEY_CREATED')).resolves.toBeInTheDocument();
  },
};

/**
 * Visual Test: Activity tab - Empty state
 */
export const ActivityEmpty: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/api-keys/stats': mockApiKeysData,
      '/api/v1/admin/activity': {
        events: [],
        totalCount: 0,
      },
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    // Switch to Activity tab
    const activityTab = await canvas.findByRole('tab', { name: /activity/i });
    await userEvent.click(activityTab);
  },
};

/**
 * Visual Test: Bulk selection active (API Keys)
 */
export const BulkSelectionActive: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/api-keys/stats': mockApiKeysData,
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    // Wait for data to load
    await canvas.findByText('Production API Key');

    // Select checkboxes
    const checkboxes = await canvas.findAllByRole('checkbox');
    if (checkboxes.length >= 2) {
      await userEvent.click(checkboxes[0]); // First key
      await userEvent.click(checkboxes[1]); // Second key
    }

    // Verify bulk action bar appears
    await expect(canvas.findByText(/selected/i)).resolves.toBeInTheDocument();
  },
};

/**
 * Visual Test: Create API Key modal open
 */
export const CreateKeyModal: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/api-keys/stats': mockApiKeysData,
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    // Wait for data to load
    await canvas.findByText('Production API Key');

    // Find and click create button
    const createButton = await canvas.findByRole('button', { name: /create api key/i });
    await userEvent.click(createButton);

    // Verify modal opened
    await expect(canvas.findByText('Create New API Key')).resolves.toBeInTheDocument();
  },
};

/**
 * Visual Test: Filters applied (API Keys)
 */
export const FiltersApplied: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/api-keys/stats': mockApiKeysData,
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    // Wait for data to load
    await canvas.findByText('Production API Key');

    // Apply search filter
    const searchInput = await canvas.findByPlaceholderText(/search/i);
    await userEvent.type(searchInput, 'Production');
  },
};

/**
 * Visual Test: Export confirmation dialog
 */
export const ExportDialog: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/api-keys/stats': mockApiKeysData,
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    // Wait for data to load
    await canvas.findByText('Production API Key');

    // Find and click export button
    const exportButton = await canvas.findByRole('button', { name: /export/i });
    await userEvent.click(exportButton);
  },
};

/**
 * Visual Test: Delete confirmation dialog (bulk)
 */
export const DeleteConfirmation: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/api-keys/stats': mockApiKeysData,
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    // Wait for data to load
    await canvas.findByText('Production API Key');

    // Select a key
    const checkboxes = await canvas.findAllByRole('checkbox');
    if (checkboxes.length >= 1) {
      await userEvent.click(checkboxes[0]);
    }

    // Find and click delete button in bulk action bar
    const deleteButton = await canvas.findByRole('button', { name: /delete/i });
    await userEvent.click(deleteButton);

    // Verify confirmation dialog
    await expect(canvas.findByText('Delete API Keys')).resolves.toBeInTheDocument();
  },
};

/**
 * Visual Test: Mobile responsive view
 */
export const MobileView: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/api-keys/stats': mockApiKeysData,
    },
    viewport: {
      defaultViewport: 'mobile1',
    },
  },
};

/**
 * Visual Test: Tablet responsive view
 */
export const TabletView: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/api-keys/stats': mockApiKeysData,
    },
    viewport: {
      defaultViewport: 'tablet',
    },
  },
};
