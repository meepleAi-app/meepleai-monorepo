/**
 * Management Page - Storybook Stories
 * Issue #903: Integration page for API Keys, Users, Activity Timeline
 *
 * Visual testing with Chromatic
 */

import type { Meta, StoryObj } from '@storybook/react';
import { ManagementPageClient } from './client';
import { AuthProvider } from '@/components/auth/AuthProvider';

const mockUser = {
  id: 'test-admin-id',
  email: 'admin@example.com',
  displayName: 'Admin User',
  role: 'Admin',
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toISOString(),
  lastActivityAt: new Date().toISOString(),
};

const meta: Meta<typeof ManagementPageClient> = {
  title: 'Pages/Admin/Management',
  component: ManagementPageClient,
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      viewports: [375, 1280],
      delay: 1000,
    },
  },
  decorators: [
    Story => (
      <AuthProvider>
        <div style={{ minHeight: '100vh' }}>
          <Story />
        </div>
      </AuthProvider>
    ),
  ],
};

export default meta;
type Story = StoryObj<typeof ManagementPageClient>;

/**
 * Default state - API Keys tab
 */
export const Default: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/api-keys/stats': {
        keys: [
          {
            apiKey: {
              id: 'key-1',
              keyName: 'Production API Key',
              keyPrefix: 'mpl_prod_abc',
              scopes: 'read:games,read:rules',
              isActive: true,
              createdAt: new Date().toISOString(),
              expiresAt: null,
              lastUsedAt: new Date().toISOString(),
            },
            usageStats: {
              totalUsageCount: 1234,
              usageCountLast24Hours: 45,
              usageCountLast7Days: 320,
              usageCountLast30Days: 1100,
              averageRequestsPerDay: 36.7,
              lastUsedAt: new Date().toISOString(),
            },
          },
        ],
        count: 1,
      },
    },
  },
};

/**
 * API Keys tab with filters active
 */
export const ApiKeysFiltered: Story = {
  parameters: {
    chromatic: { delay: 1500 },
  },
};

/**
 * Users tab - bulk operations
 */
export const UsersTab: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/users': {
        users: [
          {
            id: '1',
            email: 'user1@example.com',
            displayName: 'User One',
            role: 'User',
            createdAt: new Date().toISOString(),
          },
          {
            id: '2',
            email: 'user2@example.com',
            displayName: 'User Two',
            role: 'Admin',
            createdAt: new Date().toISOString(),
          },
        ],
        total: 2,
      },
    },
  },
};

/**
 * Activity Timeline tab
 */
export const ActivityTab: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/activity': {
        activities: [
          {
            id: '1',
            action: 'user.login',
            resource: 'Authentication',
            result: 'Success',
            createdAt: new Date().toISOString(),
            details: JSON.stringify({ ipAddress: '192.168.1.1' }),
          },
          {
            id: '2',
            action: 'api_key.created',
            resource: 'ApiKey',
            result: 'Success',
            createdAt: new Date(Date.now() - 3600000).toISOString(),
            resourceId: 'key-123',
          },
        ],
        totalCount: 2,
      },
    },
  },
};

/**
 * Mobile view - API Keys tab
 */
export const Mobile: Story = {
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
    chromatic: {
      viewports: [375],
    },
  },
};

/**
 * Loading state
 */
export const Loading: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/api-keys/stats': { delay: 10000 },
    },
  },
};

/**
 * Error state
 */
export const Error: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/api-keys/stats': {
        status: 500,
        response: { error: 'Internal Server Error' },
      },
    },
  },
};
