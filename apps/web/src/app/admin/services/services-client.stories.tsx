import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { AuthProvider } from '@/components/auth/AuthProvider';

import { ServicesClient } from './services-client';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * Service Status Dashboard - Issue #2516
 *
 * ## Features
 * - **Real-Time Monitoring**: Service health polling (30s intervals)
 * - **Circuit Breaker**: Pauses after 5 consecutive failures
 * - **State Change Notifications**: Toast alerts for critical services
 * - **Filtering**: All/Critical/Unhealthy service filters
 * - **Search**: Filter services by name
 * - **Export**: JSON and CSV download options
 * - **3-Column Grid**: Responsive layout
 * - **i18n**: Italian and English support
 *
 * ## Service States
 * - **Healthy**: Green - Service operational
 * - **Degraded**: Yellow - Service experiencing issues
 * - **Unhealthy**: Red - Service down or failing
 *
 * ## Access Control
 * - Requires Admin role (wrapped with RequireRole in page.tsx)
 *
 * ## Visual Regression Testing
 * Chromatic captures visual snapshots at multiple viewports:
 * - Mobile (375px)
 * - Tablet (768px)
 * - Desktop (1920px)
 */
const meta = {
  title: 'Pages/Admin/Services',
  component: ServicesClient,
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
} satisfies Meta<typeof ServicesClient>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default view - all services healthy
 */
export const AllHealthy: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/infrastructure',
          method: 'GET',
          status: 200,
          response: {
            overallState: 'Healthy',
            services: [
              {
                serviceName: 'postgres',
                state: 'Healthy',
                responseTime: 5,
                lastChecked: '2025-12-15T14:30:00Z',
                metadata: {
                  version: 'PostgreSQL 15.3',
                  connections: '45/100',
                  database: 'meepleai_prod',
                },
              },
              {
                serviceName: 'qdrant',
                state: 'Healthy',
                responseTime: 12,
                lastChecked: '2025-12-15T14:30:00Z',
                metadata: {
                  version: '1.7.0',
                  collections: '8',
                  vectors: '1.2M',
                },
              },
              {
                serviceName: 'redis',
                state: 'Healthy',
                responseTime: 2,
                lastChecked: '2025-12-15T14:30:00Z',
                metadata: {
                  version: '7.2.3',
                  memory_usage: '1.2GB/4GB',
                  uptime: '45 days',
                },
              },
              {
                serviceName: 'n8n',
                state: 'Healthy',
                responseTime: 25,
                lastChecked: '2025-12-15T14:30:00Z',
                metadata: {
                  version: '1.18.0',
                  workflows: '23',
                  executions_today: '1,542',
                },
              },
              {
                serviceName: 'grafana',
                state: 'Healthy',
                responseTime: 18,
                lastChecked: '2025-12-15T14:30:00Z',
                metadata: {
                  version: '10.2.0',
                  dashboards: '15',
                  datasources: '3',
                },
              },
              {
                serviceName: 'prometheus',
                state: 'Healthy',
                responseTime: 8,
                lastChecked: '2025-12-15T14:30:00Z',
                metadata: {
                  version: '2.48.0',
                  targets: '28',
                  storage: '12.5GB',
                },
              },
            ],
          },
        },
      ],
    },
  },
};

/**
 * Mixed health states
 */
export const MixedStates: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/infrastructure',
          method: 'GET',
          status: 200,
          response: {
            overallState: 'Degraded',
            services: [
              {
                serviceName: 'postgres',
                state: 'Healthy',
                responseTime: 5,
                lastChecked: '2025-12-15T14:30:00Z',
                metadata: {
                  version: 'PostgreSQL 15.3',
                  connections: '85/100',
                },
              },
              {
                serviceName: 'qdrant',
                state: 'Degraded',
                responseTime: 450,
                lastChecked: '2025-12-15T14:30:00Z',
                metadata: {
                  version: '1.7.0',
                  error: 'High response time detected',
                  vectors: '1.2M',
                },
              },
              {
                serviceName: 'redis',
                state: 'Healthy',
                responseTime: 3,
                lastChecked: '2025-12-15T14:30:00Z',
                metadata: {
                  version: '7.2.3',
                  memory_usage: '3.5GB/4GB',
                },
              },
              {
                serviceName: 'n8n',
                state: 'Unhealthy',
                responseTime: null,
                lastChecked: '2025-12-15T14:30:00Z',
                metadata: {
                  error: 'Connection timeout',
                  last_known_version: '1.18.0',
                },
              },
              {
                serviceName: 'grafana',
                state: 'Healthy',
                responseTime: 22,
                lastChecked: '2025-12-15T14:30:00Z',
                metadata: {
                  version: '10.2.0',
                },
              },
              {
                serviceName: 'prometheus',
                state: 'Degraded',
                responseTime: 120,
                lastChecked: '2025-12-15T14:30:00Z',
                metadata: {
                  version: '2.48.0',
                  warning: 'Scrape targets delayed',
                },
              },
            ],
          },
        },
      ],
    },
  },
};

/**
 * Critical failure - postgres and redis down
 */
export const CriticalFailure: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/infrastructure',
          method: 'GET',
          status: 200,
          response: {
            overallState: 'Unhealthy',
            services: [
              {
                serviceName: 'postgres',
                state: 'Unhealthy',
                responseTime: null,
                lastChecked: '2025-12-15T14:30:00Z',
                metadata: {
                  error: 'Connection refused',
                  last_known_version: 'PostgreSQL 15.3',
                },
              },
              {
                serviceName: 'qdrant',
                state: 'Healthy',
                responseTime: 15,
                lastChecked: '2025-12-15T14:30:00Z',
                metadata: {
                  version: '1.7.0',
                },
              },
              {
                serviceName: 'redis',
                state: 'Unhealthy',
                responseTime: null,
                lastChecked: '2025-12-15T14:30:00Z',
                metadata: {
                  error: 'ECONNREFUSED',
                  last_known_version: '7.2.3',
                },
              },
              {
                serviceName: 'n8n',
                state: 'Degraded',
                responseTime: 350,
                lastChecked: '2025-12-15T14:30:00Z',
                metadata: {
                  version: '1.18.0',
                  warning: 'High latency',
                },
              },
              {
                serviceName: 'grafana',
                state: 'Healthy',
                responseTime: 20,
                lastChecked: '2025-12-15T14:30:00Z',
                metadata: {
                  version: '10.2.0',
                },
              },
              {
                serviceName: 'prometheus',
                state: 'Healthy',
                responseTime: 10,
                lastChecked: '2025-12-15T14:30:00Z',
                metadata: {
                  version: '2.48.0',
                },
              },
            ],
          },
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
          url: '/api/v1/admin/infrastructure',
          method: 'GET',
          delay: 5000,
          status: 200,
          response: { overallState: 'Healthy', services: [] },
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
          url: '/api/v1/admin/infrastructure',
          method: 'GET',
          status: 500,
          response: { error: 'Internal Server Error' },
        },
      ],
    },
  },
};

/**
 * Empty state - no services configured
 */
export const NoServices: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/infrastructure',
          method: 'GET',
          status: 200,
          response: {
            overallState: 'Healthy',
            services: [],
          },
        },
      ],
    },
  },
};

/**
 * Mobile view
 */
export const MobileView: Story = {
  ...AllHealthy,
  parameters: {
    ...AllHealthy.parameters,
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
  ...AllHealthy,
  parameters: {
    ...AllHealthy.parameters,
    viewport: {
      defaultViewport: 'tablet',
    },
    chromatic: {
      viewports: [768],
    },
  },
};

/**
 * High response times
 */
export const HighLatency: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/infrastructure',
          method: 'GET',
          status: 200,
          response: {
            overallState: 'Degraded',
            services: [
              {
                serviceName: 'postgres',
                state: 'Degraded',
                responseTime: 850,
                lastChecked: '2025-12-15T14:30:00Z',
                metadata: {
                  version: 'PostgreSQL 15.3',
                  warning: 'Slow query detected',
                },
              },
              {
                serviceName: 'qdrant',
                state: 'Degraded',
                responseTime: 1200,
                lastChecked: '2025-12-15T14:30:00Z',
                metadata: {
                  version: '1.7.0',
                  warning: 'Vector search latency high',
                },
              },
              {
                serviceName: 'redis',
                state: 'Degraded',
                responseTime: 450,
                lastChecked: '2025-12-15T14:30:00Z',
                metadata: {
                  version: '7.2.3',
                  warning: 'Memory pressure',
                },
              },
              {
                serviceName: 'n8n',
                state: 'Healthy',
                responseTime: 35,
                lastChecked: '2025-12-15T14:30:00Z',
                metadata: {
                  version: '1.18.0',
                },
              },
            ],
          },
        },
      ],
    },
  },
};

/**
 * Minimal services (only critical)
 */
export const MinimalSetup: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/infrastructure',
          method: 'GET',
          status: 200,
          response: {
            overallState: 'Healthy',
            services: [
              {
                serviceName: 'postgres',
                state: 'Healthy',
                responseTime: 4,
                lastChecked: '2025-12-15T14:30:00Z',
                metadata: {
                  version: 'PostgreSQL 15.3',
                },
              },
              {
                serviceName: 'redis',
                state: 'Healthy',
                responseTime: 2,
                lastChecked: '2025-12-15T14:30:00Z',
                metadata: {
                  version: '7.2.3',
                },
              },
            ],
          },
        },
      ],
    },
  },
};
