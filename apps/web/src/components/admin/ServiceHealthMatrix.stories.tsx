/**
 * ServiceHealthMatrix Stories - Issue #896
 *
 * Visual testing for infrastructure services grid.
 * Coverage: All states, loading, empty, layouts, responsive.
 */

import type { ServiceHealthStatus } from '@/lib/api';

import { ServiceHealthMatrix } from './ServiceHealthMatrix';

import type { Meta, StoryObj } from '@storybook/react';

// Mock data generator
const createMockService = (
  name: string,
  state: 'Healthy' | 'Degraded' | 'Unhealthy',
  responseMs: number,
  errorMsg?: string
): ServiceHealthStatus => ({
  serviceName: name,
  state,
  errorMessage: errorMsg || null,
  checkedAt: new Date().toISOString(),
  responseTime: `00:00:00.${responseMs.toString().padStart(7, '0')}`, // TimeSpan format
});

const meta = {
  title: 'Admin/Infrastructure/ServiceHealthMatrix',
  component: ServiceHealthMatrix,
  parameters: {
    layout: 'padded',
    docs: {
      description: {
        component:
          'Grid display of infrastructure service health statuses. Responsive layout with loading and empty states. Issue #896.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    layout: {
      control: 'select',
      options: ['auto', 'grid-2', 'grid-3', 'grid-4'],
    },
    locale: {
      control: 'select',
      options: ['it', 'en'],
    },
  },
} satisfies Meta<typeof ServiceHealthMatrix>;

export default meta;
type Story = StoryObj<typeof meta>;

// ========== All Healthy ==========

export const AllHealthy: Story = {
  args: {
    services: [
      createMockService('postgres', 'Healthy', 15300),
      createMockService('redis', 'Healthy', 900),
      createMockService('qdrant', 'Healthy', 25000),
      createMockService('n8n', 'Healthy', 350000),
      createMockService('prometheus', 'Healthy', 10000),
      createMockService('hyperdx', 'Healthy', 50000),
    ],
    locale: 'it',
  },
};

// ========== Mixed States ==========

export const MixedStates: Story = {
  args: {
    services: [
      createMockService('postgres', 'Healthy', 15300),
      createMockService('redis', 'Degraded', 1200000, 'High latency detected'),
      createMockService('qdrant', 'Healthy', 25000),
      createMockService('n8n', 'Unhealthy', 0, 'Connection refused: ECONNREFUSED'),
      createMockService('prometheus', 'Healthy', 10000),
      createMockService('hyperdx', 'Degraded', 2500000, 'Slow response time'),
    ],
    locale: 'it',
  },
};

// ========== All Unhealthy ==========

export const AllUnhealthy: Story = {
  args: {
    services: [
      createMockService('postgres', 'Unhealthy', 0, 'Database connection lost'),
      createMockService('redis', 'Unhealthy', 0, 'Connection timeout'),
      createMockService('qdrant', 'Unhealthy', 0, 'Vector DB unavailable'),
      createMockService('n8n', 'Unhealthy', 0, 'Workflow engine down'),
      createMockService('prometheus', 'Unhealthy', 0, 'Metrics service offline'),
      createMockService('hyperdx', 'Unhealthy', 0, 'Observability stack down'),
    ],
    locale: 'it',
  },
};

// ========== Loading State ==========

export const Loading: Story = {
  args: {
    loading: true,
    locale: 'it',
  },
};

// ========== Empty State ==========

export const Empty: Story = {
  args: {
    services: [],
    locale: 'it',
  },
};

// ========== Grid Layouts ==========

export const Grid2Columns: Story = {
  args: {
    services: [
      createMockService('postgres', 'Healthy', 15300),
      createMockService('redis', 'Healthy', 900),
      createMockService('qdrant', 'Degraded', 1200000, 'High latency'),
      createMockService('n8n', 'Healthy', 350000),
    ],
    layout: 'grid-2',
    locale: 'it',
  },
};

export const Grid3Columns: Story = {
  args: {
    services: [
      createMockService('postgres', 'Healthy', 15300),
      createMockService('redis', 'Healthy', 900),
      createMockService('qdrant', 'Healthy', 25000),
      createMockService('n8n', 'Degraded', 850000, 'Intermittent issues'),
      createMockService('prometheus', 'Healthy', 10000),
      createMockService('hyperdx', 'Unhealthy', 0, 'Service down'),
    ],
    layout: 'grid-3',
    locale: 'it',
  },
};

export const Grid4Columns: Story = {
  args: {
    services: [
      createMockService('postgres', 'Healthy', 15300),
      createMockService('redis', 'Healthy', 900),
      createMockService('qdrant', 'Healthy', 25000),
      createMockService('n8n', 'Healthy', 350000),
      createMockService('prometheus', 'Healthy', 10000),
      createMockService('hyperdx', 'Healthy', 50000),
      createMockService('grafana', 'Healthy', 75000),
      createMockService('api', 'Healthy', 215000),
    ],
    layout: 'grid-4',
    locale: 'it',
  },
};

// ========== English Locale ==========

export const EnglishMixedStates: Story = {
  args: {
    services: [
      createMockService('postgres', 'Healthy', 15300),
      createMockService('redis', 'Degraded', 1200000, 'High latency detected'),
      createMockService('qdrant', 'Unhealthy', 0, 'Connection refused'),
      createMockService('n8n', 'Healthy', 350000),
    ],
    locale: 'en',
  },
};

// ========== Responsive Layouts ==========

export const ResponsiveMobile: Story = {
  args: {
    services: [
      createMockService('postgres', 'Healthy', 15300),
      createMockService('redis', 'Degraded', 1200000, 'High latency'),
      createMockService('qdrant', 'Healthy', 25000),
    ],
    locale: 'it',
  },
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
  },
};

export const ResponsiveTablet: Story = {
  args: {
    services: [
      createMockService('postgres', 'Healthy', 15300),
      createMockService('redis', 'Healthy', 900),
      createMockService('qdrant', 'Degraded', 1200000, 'High latency'),
      createMockService('n8n', 'Healthy', 350000),
    ],
    locale: 'it',
  },
  parameters: {
    viewport: {
      defaultViewport: 'tablet',
    },
  },
};
