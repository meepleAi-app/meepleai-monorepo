import type { Meta, StoryObj } from '@storybook/react';
import { DashboardClient } from './dashboard-client';

/**
 * Enhanced Admin Dashboard - Issue #874
 *
 * ## Features
 * - **16 Real-Time Metrics**: System status at a glance (polling 30s)
 * - **Activity Feed**: Last 10 system events with severity indicators
 * - **AdminLayout**: Sidebar navigation for all admin sections
 * - **Responsive**: Mobile, tablet, desktop optimized
 * - **Performance**: <1s load time, <2s Time to Interactive
 *
 * ## Access Control
 * - Requires Admin role
 * - Server-side auth check via middleware
 * - Client-side role validation via RequireRole wrapper
 */
const meta = {
  title: 'Admin/DashboardClient',
  component: DashboardClient,
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      viewports: [375, 768, 1024, 1920],
    },
  },
  tags: ['autodocs'],
} satisfies Meta<typeof DashboardClient>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default dashboard with full data
 */
export const Default: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/analytics',
        method: 'GET',
        status: 200,
        response: {
          metrics: {
            totalUsers: 1247,
            activeSessions: 42,
            apiRequestsToday: 3456,
            totalPdfDocuments: 847,
            totalChatMessages: 15234,
            averageConfidenceScore: 0.942,
            totalRagRequests: 18547,
            totalTokensUsed: 15700000,
            totalGames: 125,
            apiRequests7d: 24891,
            apiRequests30d: 112034,
            averageLatency24h: 215.0,
            averageLatency7d: 228.0,
            errorRate24h: 0.025,
            activeAlerts: 2,
            resolvedAlerts: 37,
          },
          userTrend: [],
          sessionTrend: [],
          apiRequestTrend: [],
          pdfUploadTrend: [],
          chatMessageTrend: [],
          generatedAt: new Date().toISOString(),
        },
      },
      {
        url: '/api/v1/admin/activity',
        method: 'GET',
        status: 200,
        response: {
          events: [
            {
              id: '1',
              eventType: 'UserRegistered',
              description: 'New user registered: john.doe@example.com',
              userId: 'user-123',
              userEmail: 'john.doe@example.com',
              timestamp: new Date('2025-12-08T14:30:00Z').toISOString(),
              severity: 'Info',
            },
            {
              id: '2',
              eventType: 'PdfUploaded',
              description: 'PDF uploaded: Catan-Rules.pdf (2456789 bytes)',
              userId: 'user-456',
              userEmail: 'alice@example.com',
              timestamp: new Date('2025-12-08T14:25:00Z').toISOString(),
              severity: 'Info',
            },
            {
              id: '3',
              eventType: 'AlertCreated',
              description: 'Alert: High error rate detected',
              timestamp: new Date('2025-12-08T14:20:00Z').toISOString(),
              severity: 'Warning',
            },
          ],
          totalCount: 3,
          generatedAt: new Date().toISOString(),
        },
      },
    ],
  },
};

/**
 * Loading state
 */
export const Loading: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/analytics',
        method: 'GET',
        delay: 'infinite',
      },
      {
        url: '/api/v1/admin/activity',
        method: 'GET',
        delay: 'infinite',
      },
    ],
  },
};

/**
 * Error state
 */
export const Error: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/analytics',
        method: 'GET',
        status: 500,
        response: { message: 'Internal server error' },
      },
      {
        url: '/api/v1/admin/activity',
        method: 'GET',
        status: 500,
        response: { message: 'Internal server error' },
      },
    ],
  },
};

/**
 * Critical alerts scenario (high alert count)
 */
export const CriticalAlerts: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/analytics',
        method: 'GET',
        status: 200,
        response: {
          metrics: {
            totalUsers: 1247,
            activeSessions: 42,
            apiRequestsToday: 3456,
            totalPdfDocuments: 847,
            totalChatMessages: 15234,
            averageConfidenceScore: 0.942,
            totalRagRequests: 18547,
            totalTokensUsed: 15700000,
            totalGames: 125,
            apiRequests7d: 24891,
            apiRequests30d: 112034,
            averageLatency24h: 215.0,
            averageLatency7d: 228.0,
            errorRate24h: 0.125, // High error rate
            activeAlerts: 15, // Many active alerts
            resolvedAlerts: 37,
          },
          userTrend: [],
          sessionTrend: [],
          apiRequestTrend: [],
          pdfUploadTrend: [],
          chatMessageTrend: [],
          generatedAt: new Date().toISOString(),
        },
      },
      {
        url: '/api/v1/admin/activity',
        method: 'GET',
        status: 200,
        response: {
          events: [
            {
              id: '1',
              eventType: 'ErrorOccurred',
              description: 'AI Request failed: Database connection timeout',
              timestamp: new Date().toISOString(),
              severity: 'Critical',
            },
            {
              id: '2',
              eventType: 'AlertCreated',
              description: 'Alert: High error rate detected',
              timestamp: new Date().toISOString(),
              severity: 'Error',
            },
          ],
          totalCount: 2,
          generatedAt: new Date().toISOString(),
        },
      },
    ],
  },
};

/**
 * Mobile viewport
 */
export const Mobile: Story = {
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
 * Tablet viewport
 */
export const Tablet: Story = {
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
 * Desktop viewport
 */
export const Desktop: Story = {
  ...Default,
  parameters: {
    ...Default.parameters,
    viewport: {
      defaultViewport: 'desktop',
    },
    chromatic: {
      viewports: [1920],
    },
  },
};
