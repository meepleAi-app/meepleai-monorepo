import { AuthProvider } from '@/components/auth/AuthProvider';
import type { AuthUser } from '@/types/auth';

import { ApiKeysPageClient } from './client';

import type { Meta, StoryObj } from '@storybook/react';

const _mockAdminUser: AuthUser = {
  id: '123e4567-e89b-12d3-a456-426614174000',
  email: 'admin@meepleai.com',
  displayName: 'Admin User',
  role: 'Admin',
  createdAt: new Date().toISOString(),
  isTwoFactorEnabled: false,
};

const meta: Meta<typeof ApiKeysPageClient> = {
  title: 'Admin/API Keys',
  component: ApiKeysPageClient,
  parameters: {
    layout: 'fullscreen',
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
type Story = StoryObj<typeof ApiKeysPageClient>;

export const Default: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/api-keys/stats': {
        keys: [
          {
            apiKey: {
              id: '11111111-1111-1111-1111-111111111111',
              keyName: 'Production API Key',
              keyPrefix: 'mpl_prod_',
              scopes: 'read:games,read:rules,write:sessions',
              createdAt: '2025-01-01T00:00:00Z',
              expiresAt: '2026-01-01T00:00:00Z',
              lastUsedAt: '2025-12-11T10:30:00Z',
              isActive: true,
            },
            usageStats: {
              keyId: '11111111-1111-1111-1111-111111111111',
              totalUsageCount: 1543,
              lastUsedAt: '2025-12-11T10:30:00Z',
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
              lastUsedAt: '2025-12-10T15:20:00Z',
              isActive: true,
            },
            usageStats: {
              keyId: '22222222-2222-2222-2222-222222222222',
              totalUsageCount: 234,
              lastUsedAt: '2025-12-10T15:20:00Z',
              usageCountLast24Hours: 12,
              usageCountLast7Days: 89,
              usageCountLast30Days: 234,
              averageRequestsPerDay: 7.8,
            },
          },
          {
            apiKey: {
              id: '33333333-3333-3333-3333-333333333333',
              keyName: 'Revoked Test Key',
              keyPrefix: 'mpl_test_',
              scopes: 'read:games',
              createdAt: '2025-03-10T08:00:00Z',
              expiresAt: null,
              lastUsedAt: '2025-11-01T09:00:00Z',
              isActive: false,
            },
            usageStats: {
              keyId: '33333333-3333-3333-3333-333333333333',
              totalUsageCount: 567,
              lastUsedAt: '2025-11-01T09:00:00Z',
              usageCountLast24Hours: 0,
              usageCountLast7Days: 0,
              usageCountLast30Days: 0,
              averageRequestsPerDay: 0,
            },
          },
          {
            apiKey: {
              id: '44444444-4444-4444-4444-444444444444',
              keyName: 'Expired Key',
              keyPrefix: 'mpl_exp_',
              scopes: 'read:games,read:rules',
              createdAt: '2024-01-01T00:00:00Z',
              expiresAt: '2024-12-31T23:59:59Z',
              lastUsedAt: '2024-12-30T10:00:00Z',
              isActive: true,
            },
            usageStats: {
              keyId: '44444444-4444-4444-4444-444444444444',
              totalUsageCount: 8923,
              lastUsedAt: '2024-12-30T10:00:00Z',
              usageCountLast24Hours: 0,
              usageCountLast7Days: 0,
              usageCountLast30Days: 0,
              averageRequestsPerDay: 0,
            },
          },
        ],
        count: 4,
        filters: {
          userId: null,
          includeRevoked: false,
        },
      },
    },
  },
};

export const Loading: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/api-keys/stats': {
        delay: 'infinite',
      },
    },
  },
};

export const Empty: Story = {
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

export const ErrorState: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/api-keys/stats': {
        status: 500,
        error: 'Internal Server Error',
      },
    },
  },
};
