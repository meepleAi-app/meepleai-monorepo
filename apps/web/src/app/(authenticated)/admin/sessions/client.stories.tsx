import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { AuthProvider } from '@/components/auth/AuthProvider';

import { SessionsMonitoringClient } from './client';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * Sessions Monitoring Page - Gap Analysis Implementation
 *
 * ## Features
 * - **Real-Time Monitoring**: View all active user sessions
 * - **Session Details**: IP, user agent, timestamps, device info
 * - **Status Tracking**: Active, Expired, Revoked badges
 * - **User Filtering**: Search sessions by user ID
 * - **Revoke Actions**: Single session or all user sessions
 * - **Stats Dashboard**: Total, active, expired/revoked counts
 * - **Limit Control**: Configurable result limit (25/50/100/200)
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
  title: 'Pages/Admin/Sessions',
  component: SessionsMonitoringClient,
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
} satisfies Meta<typeof SessionsMonitoringClient>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default view with active and expired sessions
 */
export const Default: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/sessions',
          method: 'GET',
          status: 200,
          response: [
            {
              id: 'session-1',
              userId: '11111111-1111-1111-1111-111111111111',
              userEmail: 'admin@meepleai.com',
              createdAt: '2025-12-15T08:00:00Z',
              expiresAt: '2025-12-22T08:00:00Z',
              lastSeenAt: '2025-12-15T14:30:00Z',
              ipAddress: '192.168.1.1',
              userAgent:
                'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0.0.0',
              revokedAt: null,
            },
            {
              id: 'session-2',
              userId: '22222222-2222-2222-2222-222222222222',
              userEmail: 'user@example.com',
              createdAt: '2025-12-14T10:00:00Z',
              expiresAt: '2025-12-21T10:00:00Z',
              lastSeenAt: '2025-12-15T12:15:00Z',
              ipAddress: '10.0.0.45',
              userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) Safari/604.1',
              revokedAt: null,
            },
            {
              id: 'session-3',
              userId: '33333333-3333-3333-3333-333333333333',
              userEmail: 'editor@meepleai.com',
              createdAt: '2025-12-10T15:30:00Z',
              expiresAt: '2025-12-13T15:30:00Z',
              lastSeenAt: '2025-12-13T14:00:00Z',
              ipAddress: '172.16.0.10',
              userAgent: 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) Safari/605.1.15',
              revokedAt: null,
            },
            {
              id: 'session-4',
              userId: '44444444-4444-4444-4444-444444444444',
              userEmail: 'revoked@example.com',
              createdAt: '2025-12-12T09:00:00Z',
              expiresAt: '2025-12-19T09:00:00Z',
              lastSeenAt: '2025-12-14T10:30:00Z',
              ipAddress: '203.0.113.42',
              userAgent: 'Mozilla/5.0 (X11; Linux x86_64) Firefox/121.0',
              revokedAt: '2025-12-14T11:00:00Z',
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
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/sessions',
          method: 'GET',
          delay: 5000,
          status: 200,
          response: [],
        },
      ],
    },
  },
};

/**
 * Empty state - no sessions
 */
export const Empty: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/sessions',
          method: 'GET',
          status: 200,
          response: [],
        },
      ],
    },
  },
};

/**
 * Error state
 */
export const ErrorState: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/sessions',
          method: 'GET',
          status: 500,
          response: { error: 'Internal Server Error' },
        },
      ],
    },
  },
};

/**
 * All active sessions
 */
export const AllActive: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/sessions',
          method: 'GET',
          status: 200,
          response: [
            {
              id: 'session-1',
              userId: '11111111-1111-1111-1111-111111111111',
              userEmail: 'user1@example.com',
              createdAt: '2025-12-15T08:00:00Z',
              expiresAt: '2025-12-22T08:00:00Z',
              lastSeenAt: '2025-12-15T14:30:00Z',
              ipAddress: '192.168.1.1',
              userAgent: 'Chrome/120.0.0.0 Windows',
              revokedAt: null,
            },
            {
              id: 'session-2',
              userId: '22222222-2222-2222-2222-222222222222',
              userEmail: 'user2@example.com',
              createdAt: '2025-12-15T09:00:00Z',
              expiresAt: '2025-12-22T09:00:00Z',
              lastSeenAt: '2025-12-15T14:45:00Z',
              ipAddress: '10.0.0.50',
              userAgent: 'Safari/605.1.15 macOS',
              revokedAt: null,
            },
            {
              id: 'session-3',
              userId: '33333333-3333-3333-3333-333333333333',
              userEmail: 'user3@example.com',
              createdAt: '2025-12-15T10:00:00Z',
              expiresAt: '2025-12-22T10:00:00Z',
              lastSeenAt: '2025-12-15T14:50:00Z',
              ipAddress: '172.16.0.25',
              userAgent: 'Firefox/121.0 Linux',
              revokedAt: null,
            },
          ],
        },
      ],
    },
  },
};

/**
 * Mixed status sessions (active, expired, revoked)
 */
export const MixedStatus: Story = {
  ...Default,
};

/**
 * Filtered by user ID
 */
export const FilteredByUser: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/sessions',
          method: 'GET',
          status: 200,
          response: [
            {
              id: 'session-1',
              userId: '11111111-1111-1111-1111-111111111111',
              userEmail: 'admin@meepleai.com',
              createdAt: '2025-12-15T08:00:00Z',
              expiresAt: '2025-12-22T08:00:00Z',
              lastSeenAt: '2025-12-15T14:30:00Z',
              ipAddress: '192.168.1.1',
              userAgent: 'Chrome/120.0.0.0 Desktop',
              revokedAt: null,
            },
            {
              id: 'session-2',
              userId: '11111111-1111-1111-1111-111111111111',
              userEmail: 'admin@meepleai.com',
              createdAt: '2025-12-14T10:00:00Z',
              expiresAt: '2025-12-21T10:00:00Z',
              lastSeenAt: '2025-12-15T12:00:00Z',
              ipAddress: '10.0.0.100',
              userAgent: 'Safari/605.1.15 iPhone',
              revokedAt: null,
            },
          ],
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
 * Tablet view
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
 * Large dataset (100 sessions)
 */
export const LargeDataset: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/sessions',
          method: 'GET',
          status: 200,
          response: Array.from({ length: 100 }, (_, i) => ({
            id: `session-${i + 1}`,
            userId: `user-${(i % 20) + 1}`,
            userEmail: `user${(i % 20) + 1}@example.com`,
            createdAt: new Date(2025, 11, 15 - (i % 7), 8 + (i % 12)).toISOString(),
            expiresAt: new Date(2025, 11, 22 - (i % 7), 8 + (i % 12)).toISOString(),
            lastSeenAt: new Date(2025, 11, 15, 14, i % 60).toISOString(),
            ipAddress: `192.168.${(i % 255) + 1}.${(i % 100) + 1}`,
            userAgent:
              i % 3 === 0
                ? 'Chrome/120.0.0.0 Windows'
                : i % 3 === 1
                  ? 'Safari/605.1.15 macOS'
                  : 'Firefox/121.0 Linux',
            revokedAt: i % 10 === 0 ? new Date(2025, 11, 15, 10).toISOString() : null,
          })),
        },
      ],
    },
  },
};

/**
 * Multiple devices per user
 */
export const MultipleDevices: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/sessions',
          method: 'GET',
          status: 200,
          response: [
            {
              id: 'session-1',
              userId: '11111111-1111-1111-1111-111111111111',
              userEmail: 'poweruser@example.com',
              createdAt: '2025-12-15T08:00:00Z',
              expiresAt: '2025-12-22T08:00:00Z',
              lastSeenAt: '2025-12-15T14:30:00Z',
              ipAddress: '192.168.1.100',
              userAgent:
                'Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0.0.0 Desktop',
              revokedAt: null,
            },
            {
              id: 'session-2',
              userId: '11111111-1111-1111-1111-111111111111',
              userEmail: 'poweruser@example.com',
              createdAt: '2025-12-15T09:00:00Z',
              expiresAt: '2025-12-22T09:00:00Z',
              lastSeenAt: '2025-12-15T13:45:00Z',
              ipAddress: '10.0.0.50',
              userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_0) Safari/604.1 Mobile',
              revokedAt: null,
            },
            {
              id: 'session-3',
              userId: '11111111-1111-1111-1111-111111111111',
              userEmail: 'poweruser@example.com',
              createdAt: '2025-12-15T07:30:00Z',
              expiresAt: '2025-12-22T07:30:00Z',
              lastSeenAt: '2025-12-15T14:00:00Z',
              ipAddress: '172.16.0.25',
              userAgent: 'Mozilla/5.0 (iPad; CPU OS 17_0) Safari/604.1 Tablet',
              revokedAt: null,
            },
          ],
        },
      ],
    },
  },
};
