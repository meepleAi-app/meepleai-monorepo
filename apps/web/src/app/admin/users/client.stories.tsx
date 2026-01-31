import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { AuthProvider } from '@/components/auth/AuthProvider';

import { AdminPageClient } from './client';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * User Management Page - Admin Users Client
 *
 * ## Features
 * - **User CRUD**: Create, read, update, delete users
 * - **Role Management**: Assign Admin, Editor, or User roles
 * - **Bulk Actions**: Delete, suspend, export, change roles for multiple users
 * - **Filtering**: Search by email/name, filter by role and status
 * - **Sorting**: TanStack table with sortable columns
 * - **Pagination**: Client-side pagination (20 per page)
 *
 * ## Access Control
 * - Requires Admin role (wrapped with AdminAuthGuard)
 *
 * ## Visual Regression Testing
 * Chromatic captures visual snapshots at multiple viewports:
 * - Mobile (375px)
 * - Tablet (768px)
 * - Desktop (1920px)
 */
const meta = {
  title: 'Pages/Admin/Users',
  component: AdminPageClient,
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      viewports: [375, 768, 1920],
      delay: 300,
      diffThreshold: 0.2,
    },
  },
  tags: ['autodocs'],
  decorators: [
    Story => {
      const queryClient = new QueryClient({
        defaultOptions: {
          queries: {
            retry: false,
            refetchInterval: false,
          },
        },
      });
      return (
        <QueryClientProvider client={queryClient}>
          <AuthProvider>
            <div className="min-h-screen bg-gray-50">
              <Story />
            </div>
          </AuthProvider>
        </QueryClientProvider>
      );
    },
  ],
} satisfies Meta<typeof AdminPageClient>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default view with multiple users
 */
export const Default: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/users',
          method: 'GET',
          status: 200,
          response: {
            items: [
              {
                id: '11111111-1111-1111-1111-111111111111',
                email: 'admin@meepleai.com',
                displayName: 'Admin User',
                role: 'Admin',
                createdAt: '2025-01-01T00:00:00Z',
                lastSeenAt: '2025-12-15T10:30:00Z',
                isSuspended: false,
              },
              {
                id: '22222222-2222-2222-2222-222222222222',
                email: 'editor@meepleai.com',
                displayName: 'Editor User',
                role: 'Editor',
                createdAt: '2025-02-15T00:00:00Z',
                lastSeenAt: '2025-12-14T09:15:00Z',
                isSuspended: false,
              },
              {
                id: '33333333-3333-3333-3333-333333333333',
                email: 'user1@example.com',
                displayName: 'Regular User',
                role: 'User',
                createdAt: '2025-03-20T00:00:00Z',
                lastSeenAt: '2025-12-13T14:22:00Z',
                isSuspended: false,
              },
              {
                id: '44444444-4444-4444-4444-444444444444',
                email: 'suspended@example.com',
                displayName: 'Suspended User',
                role: 'User',
                createdAt: '2025-04-10T00:00:00Z',
                lastSeenAt: '2025-11-30T08:00:00Z',
                isSuspended: true,
              },
              {
                id: '55555555-5555-5555-5555-555555555555',
                email: 'newuser@example.com',
                displayName: 'New User',
                role: 'User',
                createdAt: '2025-12-01T00:00:00Z',
                lastSeenAt: null,
                isSuspended: false,
              },
            ],
            total: 5,
            page: 1,
            pageSize: 20,
          },
        },
      ],
    },
  },
};

/**
 * Loading state - fetching users
 */
export const Loading: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/users',
          method: 'GET',
          delay: 5000,
          status: 200,
          response: { items: [], total: 0, page: 1, pageSize: 20 },
        },
      ],
    },
  },
};

/**
 * Empty state - no users found
 */
export const Empty: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/users',
          method: 'GET',
          status: 200,
          response: {
            items: [],
            total: 0,
            page: 1,
            pageSize: 20,
          },
        },
      ],
    },
  },
};

/**
 * Error state - API failure
 */
export const ErrorState: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/users',
          method: 'GET',
          status: 500,
          response: { error: 'Internal Server Error' },
        },
      ],
    },
  },
};

/**
 * Filtered by Admin role
 */
export const FilteredAdminRole: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/users',
          method: 'GET',
          status: 200,
          response: {
            items: [
              {
                id: '11111111-1111-1111-1111-111111111111',
                email: 'admin@meepleai.com',
                displayName: 'Admin User',
                role: 'Admin',
                createdAt: '2025-01-01T00:00:00Z',
                lastSeenAt: '2025-12-15T10:30:00Z',
                isSuspended: false,
              },
              {
                id: '66666666-6666-6666-6666-666666666666',
                email: 'superadmin@meepleai.com',
                displayName: 'Super Admin',
                role: 'Admin',
                createdAt: '2024-12-01T00:00:00Z',
                lastSeenAt: '2025-12-15T11:00:00Z',
                isSuspended: false,
              },
            ],
            total: 2,
            page: 1,
            pageSize: 20,
          },
        },
      ],
    },
  },
};

/**
 * Filtered by Suspended status
 */
export const FilteredSuspended: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/users',
          method: 'GET',
          status: 200,
          response: {
            items: [
              {
                id: '44444444-4444-4444-4444-444444444444',
                email: 'suspended@example.com',
                displayName: 'Suspended User',
                role: 'User',
                createdAt: '2025-04-10T00:00:00Z',
                lastSeenAt: '2025-11-30T08:00:00Z',
                isSuspended: true,
              },
              {
                id: '77777777-7777-7777-7777-777777777777',
                email: 'banned@example.com',
                displayName: 'Banned User',
                role: 'User',
                createdAt: '2025-05-20T00:00:00Z',
                lastSeenAt: '2025-12-01T10:00:00Z',
                isSuspended: true,
              },
            ],
            total: 2,
            page: 1,
            pageSize: 20,
          },
        },
      ],
    },
  },
};

/**
 * Mobile view (375px viewport)
 */
export const MobileView: Story = {
  ...Default,
  parameters: {
    ...Default.parameters,
    viewport: {
      defaultViewport: 'mobile1',
    },
    chromatic: {
      viewports: [375],
    },
  },
};

/**
 * Tablet view (768px viewport)
 */
export const TabletView: Story = {
  ...Default,
  parameters: {
    ...Default.parameters,
    viewport: {
      defaultViewport: 'tablet',
    },
    chromatic: {
      viewports: [768],
    },
  },
};

/**
 * Large dataset with pagination
 */
export const LargeDataset: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/users',
          method: 'GET',
          status: 200,
          response: {
            items: Array.from({ length: 20 }, (_, i) => ({
              id: `${i + 1}0000000-0000-0000-0000-000000000000`,
              email: `user${i + 1}@example.com`,
              displayName: `User ${i + 1}`,
              role: i % 3 === 0 ? 'Admin' : i % 2 === 0 ? 'Editor' : 'User',
              createdAt: new Date(2025, 0, i + 1).toISOString(),
              lastSeenAt: new Date(2025, 11, 15 - i).toISOString(),
              isSuspended: i % 7 === 0,
            })),
            total: 250,
            page: 1,
            pageSize: 20,
          },
        },
      ],
    },
  },
};
