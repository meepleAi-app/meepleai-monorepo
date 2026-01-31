import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { AuthProvider } from '@/components/auth/AuthProvider';

import { UserDetailClient } from './client';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * User Detail Page - Individual User View
 *
 * ## Features
 * - **User Profile**: Display name, email, role, registration date
 * - **Activity Timeline**: UserActivityTimeline with filters (action, limit)
 * - **Role History**: Track role changes over time
 * - **Badges**: Display earned badges with visibility status
 * - **Library Stats**: Game count and sessions played
 * - **Quick Actions**: Reset password, send email, impersonate user
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
  title: 'Pages/Admin/UserDetail',
  component: UserDetailClient,
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
} satisfies Meta<typeof UserDetailClient>;

export default meta;
type Story = StoryObj<typeof meta>;

const mockUserId = '11111111-1111-1111-1111-111111111111';

/**
 * Default view - Admin user with activities and badges
 */
export const Default: Story = {
  args: {
    userId: mockUserId,
  },
  parameters: {
    msw: {
      handlers: [
        // User profile
        {
          url: `/api/v1/admin/users/${mockUserId}`,
          method: 'GET',
          status: 200,
          response: {
            id: mockUserId,
            email: 'admin@meepleai.com',
            displayName: 'Admin User',
            role: 'Admin',
            createdAt: '2025-01-01T00:00:00Z',
            lastSeenAt: '2025-12-15T10:30:00Z',
            isSuspended: false,
            subscriptionTier: 'Pro',
          },
        },
        // User activities
        {
          url: `/api/v1/admin/users/${mockUserId}/activity`,
          method: 'GET',
          status: 200,
          response: {
            activities: [
              {
                id: 'act-1',
                action: 'Login',
                resource: 'Session',
                resourceId: 'session-1',
                details: 'User logged in successfully',
                result: 'Success',
                createdAt: '2025-12-15T10:30:00Z',
                ipAddress: '192.168.1.1',
              },
              {
                id: 'act-2',
                action: 'Create',
                resource: 'Game',
                resourceId: 'game-1',
                details: 'Added Catan to library',
                result: 'Success',
                createdAt: '2025-12-15T09:15:00Z',
                ipAddress: '192.168.1.1',
              },
              {
                id: 'act-3',
                action: 'Update',
                resource: 'Profile',
                resourceId: mockUserId,
                details: 'Updated display name',
                result: 'Success',
                createdAt: '2025-12-14T14:20:00Z',
                ipAddress: '192.168.1.1',
              },
            ],
            totalCount: 3,
          },
        },
        // User badges
        {
          url: `/api/v1/admin/users/${mockUserId}/badges`,
          method: 'GET',
          status: 200,
          response: [
            {
              id: 'badge-1',
              name: 'Early Adopter',
              description: 'Joined in first 100 users',
              iconUrl: '🏆',
              tier: 'Gold',
              isDisplayed: true,
              earnedAt: '2025-01-02T00:00:00Z',
            },
            {
              id: 'badge-2',
              name: 'Game Master',
              description: 'Added 50+ games to library',
              iconUrl: '🎮',
              tier: 'Silver',
              isDisplayed: true,
              earnedAt: '2025-06-15T00:00:00Z',
            },
          ],
        },
        // Library stats
        {
          url: `/api/v1/admin/users/${mockUserId}/library/stats`,
          method: 'GET',
          status: 200,
          response: {
            totalGames: 67,
            sessionsPlayed: 234,
          },
        },
        // Role history
        {
          url: `/api/v1/admin/users/${mockUserId}/role-history`,
          method: 'GET',
          status: 200,
          response: [
            {
              oldRole: 'User',
              newRole: 'Admin',
              changedByDisplayName: 'Super Admin',
              changedAt: '2025-03-15T10:00:00Z',
            },
          ],
        },
      ],
    },
  },
};

/**
 * Loading state
 */
export const Loading: Story = {
  args: {
    userId: mockUserId,
  },
  parameters: {
    msw: {
      handlers: [
        {
          url: `/api/v1/admin/users/${mockUserId}`,
          method: 'GET',
          delay: 5000,
          status: 200,
          response: {},
        },
      ],
    },
  },
};

/**
 * Error state - user not found
 */
export const ErrorState: Story = {
  args: {
    userId: 'invalid-user-id',
  },
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/users/invalid-user-id',
          method: 'GET',
          status: 404,
          response: { error: 'User not found' },
        },
      ],
    },
  },
};

/**
 * New user with no activity
 */
export const NewUser: Story = {
  args: {
    userId: mockUserId,
  },
  parameters: {
    msw: {
      handlers: [
        {
          url: `/api/v1/admin/users/${mockUserId}`,
          method: 'GET',
          status: 200,
          response: {
            id: mockUserId,
            email: 'newuser@example.com',
            displayName: 'New User',
            role: 'User',
            createdAt: '2025-12-15T00:00:00Z',
            lastSeenAt: null,
            isSuspended: false,
          },
        },
        {
          url: `/api/v1/admin/users/${mockUserId}/activity`,
          method: 'GET',
          status: 200,
          response: {
            activities: [],
            totalCount: 0,
          },
        },
        {
          url: `/api/v1/admin/users/${mockUserId}/badges`,
          method: 'GET',
          status: 200,
          response: [],
        },
        {
          url: `/api/v1/admin/users/${mockUserId}/library/stats`,
          method: 'GET',
          status: 200,
          response: {
            totalGames: 0,
            sessionsPlayed: 0,
          },
        },
        {
          url: `/api/v1/admin/users/${mockUserId}/role-history`,
          method: 'GET',
          status: 200,
          response: [],
        },
      ],
    },
  },
};

/**
 * Suspended user
 */
export const SuspendedUser: Story = {
  args: {
    userId: mockUserId,
  },
  parameters: {
    msw: {
      handlers: [
        {
          url: `/api/v1/admin/users/${mockUserId}`,
          method: 'GET',
          status: 200,
          response: {
            id: mockUserId,
            email: 'suspended@example.com',
            displayName: 'Suspended User',
            role: 'User',
            createdAt: '2025-06-01T00:00:00Z',
            lastSeenAt: '2025-11-30T08:00:00Z',
            isSuspended: true,
          },
        },
        {
          url: `/api/v1/admin/users/${mockUserId}/activity`,
          method: 'GET',
          status: 200,
          response: {
            activities: [
              {
                id: 'act-1',
                action: 'Suspend',
                resource: 'User',
                resourceId: mockUserId,
                details: 'Account suspended for ToS violation',
                result: 'Success',
                createdAt: '2025-12-01T09:00:00Z',
                ipAddress: '10.0.0.1',
              },
            ],
            totalCount: 1,
          },
        },
        {
          url: `/api/v1/admin/users/${mockUserId}/badges`,
          method: 'GET',
          status: 200,
          response: [],
        },
        {
          url: `/api/v1/admin/users/${mockUserId}/library/stats`,
          method: 'GET',
          status: 200,
          response: {
            totalGames: 12,
            sessionsPlayed: 45,
          },
        },
        {
          url: `/api/v1/admin/users/${mockUserId}/role-history`,
          method: 'GET',
          status: 200,
          response: [],
        },
      ],
    },
  },
};

/**
 * Mobile view
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
 * User with extensive role history
 */
export const ExtensiveRoleHistory: Story = {
  args: {
    userId: mockUserId,
  },
  parameters: {
    msw: {
      handlers: [
        {
          url: `/api/v1/admin/users/${mockUserId}`,
          method: 'GET',
          status: 200,
          response: {
            id: mockUserId,
            email: 'poweruser@meepleai.com',
            displayName: 'Power User',
            role: 'Admin',
            createdAt: '2024-01-01T00:00:00Z',
            lastSeenAt: '2025-12-15T10:30:00Z',
            isSuspended: false,
          },
        },
        {
          url: `/api/v1/admin/users/${mockUserId}/activity`,
          method: 'GET',
          status: 200,
          response: {
            activities: [],
            totalCount: 0,
          },
        },
        {
          url: `/api/v1/admin/users/${mockUserId}/badges`,
          method: 'GET',
          status: 200,
          response: [],
        },
        {
          url: `/api/v1/admin/users/${mockUserId}/library/stats`,
          method: 'GET',
          status: 200,
          response: {
            totalGames: 150,
            sessionsPlayed: 1200,
          },
        },
        {
          url: `/api/v1/admin/users/${mockUserId}/role-history`,
          method: 'GET',
          status: 200,
          response: [
            {
              oldRole: 'User',
              newRole: 'Editor',
              changedByDisplayName: 'Admin User',
              changedAt: '2024-06-15T10:00:00Z',
            },
            {
              oldRole: 'Editor',
              newRole: 'Admin',
              changedByDisplayName: 'Super Admin',
              changedAt: '2025-03-20T14:30:00Z',
            },
          ],
        },
      ],
    },
  },
};
