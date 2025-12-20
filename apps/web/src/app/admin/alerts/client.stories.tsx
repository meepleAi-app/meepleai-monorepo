import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { AlertsPageClient } from './client';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * Alert Management Page - Issue #921
 *
 * ## Features
 * - **Real-Time Alerts**: List of active/all alerts with auto-refresh (30s)
 * - **Severity Badges**: Visual indicators (critical, warning, info)
 * - **Metadata Viewer**: Dialog to inspect alert details and channel delivery status
 * - **Resolve Action**: Manually resolve active alerts
 * - **Stats Dashboard**: Quick overview (total, active, critical, warnings)
 *
 * ## Access Control
 * - Requires Admin role (wrapped with AdminAuthGuard)
 *
 * ## Visual Regression Testing
 * Chromatic captures visual snapshots at multiple viewports:
 * - Mobile (375px)
 * - Tablet (768px)
 * - Desktop (1024px, 1920px)
 */
const meta = {
  title: 'Admin/AlertsPageClient',
  component: AlertsPageClient,
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      viewports: [375, 768, 1024, 1920],
      delay: 500, // Allow animations to settle
      diffThreshold: 0.2, // 20% difference threshold for visual regressions
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
          <Story />
        </QueryClientProvider>
      );
    },
  ],
} satisfies Meta<typeof AlertsPageClient>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default view with active alerts
 */
export const WithActiveAlerts: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/alerts',
          method: 'GET',
          status: 200,
          response: [
            {
              id: '550e8400-e29b-41d4-a716-446655440001',
              alertType: 'HighErrorRate',
              severity: 'critical',
              message: 'Error rate exceeded 10% threshold (current: 12.5%)',
              metadata: {
                labels: {
                  severity: 'critical',
                  alertname: 'HighErrorRate',
                },
                annotations: {
                  summary: 'High error rate detected',
                  description: 'API error rate above 10% for 5 minutes',
                },
                starts_at: '2025-12-12T08:00:00Z',
                group_key: 'group-1',
              },
              triggeredAt: '2025-12-12T08:00:00Z',
              resolvedAt: null,
              isActive: true,
              channelSent: {
                Email: true,
                Slack: true,
                PagerDuty: false,
              },
            },
            {
              id: '550e8400-e29b-41d4-a716-446655440002',
              alertType: 'HighLatency',
              severity: 'warning',
              message: 'P95 latency exceeded 1000ms threshold (current: 1250ms)',
              metadata: {
                labels: {
                  severity: 'warning',
                  alertname: 'HighLatency',
                },
                annotations: {
                  summary: 'High latency detected',
                  description: 'API P95 latency above 1s for 10 minutes',
                },
                starts_at: '2025-12-12T07:30:00Z',
              },
              triggeredAt: '2025-12-12T07:30:00Z',
              resolvedAt: null,
              isActive: true,
              channelSent: {
                Email: true,
                Slack: true,
              },
            },
            {
              id: '550e8400-e29b-41d4-a716-446655440003',
              alertType: 'QdrantDown',
              severity: 'critical',
              message: 'Qdrant vector database is unreachable',
              metadata: {
                labels: {
                  severity: 'critical',
                  alertname: 'QdrantDown',
                  service: 'qdrant',
                },
                annotations: {
                  summary: 'Qdrant service down',
                  description: 'Vector search unavailable',
                },
                starts_at: '2025-12-12T07:00:00Z',
              },
              triggeredAt: '2025-12-12T07:00:00Z',
              resolvedAt: null,
              isActive: true,
              channelSent: {
                Email: true,
                Slack: true,
                PagerDuty: true,
              },
            },
          ],
        },
      ],
    },
  },
};

/**
 * Empty state - no alerts
 */
export const NoAlerts: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/alerts',
          method: 'GET',
          status: 200,
          response: [],
        },
      ],
    },
  },
};

/**
 * Mixed state - active and resolved alerts (7-day view)
 */
export const MixedAlerts: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/alerts',
          method: 'GET',
          status: 200,
          response: [
            {
              id: '550e8400-e29b-41d4-a716-446655440001',
              alertType: 'HighErrorRate',
              severity: 'critical',
              message: 'Error rate exceeded 10% threshold',
              metadata: {},
              triggeredAt: '2025-12-12T08:00:00Z',
              resolvedAt: null,
              isActive: true,
              channelSent: { Email: true, Slack: true },
            },
            {
              id: '550e8400-e29b-41d4-a716-446655440002',
              alertType: 'HighLatency',
              severity: 'warning',
              message: 'P95 latency exceeded 1000ms',
              metadata: {},
              triggeredAt: '2025-12-11T14:00:00Z',
              resolvedAt: '2025-12-11T15:30:00Z',
              isActive: false,
              channelSent: { Email: true },
            },
            {
              id: '550e8400-e29b-41d4-a716-446655440003',
              alertType: 'LowDiskSpace',
              severity: 'warning',
              message: 'Disk space below 20% (current: 15%)',
              metadata: {},
              triggeredAt: '2025-12-10T10:00:00Z',
              resolvedAt: '2025-12-10T12:00:00Z',
              isActive: false,
              channelSent: { Email: true, Slack: false },
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
          url: '/api/v1/admin/alerts',
          method: 'GET',
          delay: 5000, // 5s delay to show loading state
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
export const Error: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/alerts',
          method: 'GET',
          status: 500,
          response: { error: 'Internal Server Error' },
        },
      ],
    },
  },
};
