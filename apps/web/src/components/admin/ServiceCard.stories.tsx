/**
 * ServiceCard Stories - Issue #896
 *
 * Visual testing for infrastructure service health cards.
 * Coverage: All health states, loading, error messages, responsive.
 */

import type { Meta, StoryObj } from '@storybook/react';
import { ServiceCard } from './ServiceCard';
import type { HealthState } from '@/lib/api';

const meta = {
  title: 'Admin/Infrastructure/ServiceCard',
  component: ServiceCard,
  parameters: {
    layout: 'padded',
    docs: {
      description: {
        component:
          'Displays infrastructure service health status with real-time metrics. Issue #896.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    status: {
      control: 'select',
      options: ['Healthy', 'Degraded', 'Unhealthy'] as HealthState[],
    },
    locale: {
      control: 'select',
      options: ['it', 'en'],
    },
  },
} satisfies Meta<typeof ServiceCard>;

export default meta;
type Story = StoryObj<typeof meta>;

// ========== Healthy State ==========

export const PostgresHealthy: Story = {
  args: {
    serviceName: 'postgres',
    status: 'Healthy',
    responseTimeMs: 15.3,
    lastCheck: new Date(Date.now() - 60000), // 1 minute ago
    locale: 'it',
  },
};

export const RedisHealthy: Story = {
  args: {
    serviceName: 'redis',
    status: 'Healthy',
    responseTimeMs: 0.9,
    lastCheck: new Date(Date.now() - 30000), // 30 seconds ago
    locale: 'it',
  },
};

// ========== Degraded State ==========

export const QdrantDegraded: Story = {
  args: {
    serviceName: 'qdrant',
    status: 'Degraded',
    responseTimeMs: 1250,
    lastCheck: new Date(Date.now() - 120000), // 2 minutes ago
    errorMessage: 'High response time detected (>1000ms threshold)',
    locale: 'it',
  },
};

export const N8nDegraded: Story = {
  args: {
    serviceName: 'n8n',
    status: 'Degraded',
    responseTimeMs: 850,
    lastCheck: new Date(Date.now() - 180000), // 3 minutes ago
    errorMessage: 'Intermittent connection issues',
    locale: 'it',
  },
};

// ========== Unhealthy State ==========

export const HyperDXUnhealthy: Story = {
  args: {
    serviceName: 'hyperdx',
    status: 'Unhealthy',
    responseTimeMs: undefined,
    lastCheck: new Date(Date.now() - 300000), // 5 minutes ago
    errorMessage: 'Connection refused: ECONNREFUSED 127.0.0.1:8180',
    locale: 'it',
  },
};

export const PrometheusUnhealthy: Story = {
  args: {
    serviceName: 'prometheus',
    status: 'Unhealthy',
    responseTimeMs: undefined,
    lastCheck: new Date(Date.now() - 600000), // 10 minutes ago
    errorMessage: 'Timeout after 30s',
    locale: 'it',
  },
};

// ========== Loading State ==========

export const Loading: Story = {
  args: {
    serviceName: 'postgres',
    status: 'Healthy',
    loading: true,
    locale: 'it',
  },
};

// ========== English Locale ==========

export const EnglishHealthy: Story = {
  args: {
    serviceName: 'postgres',
    status: 'Healthy',
    responseTimeMs: 15.3,
    lastCheck: new Date(Date.now() - 60000),
    locale: 'en',
  },
};

export const EnglishUnhealthy: Story = {
  args: {
    serviceName: 'redis',
    status: 'Unhealthy',
    responseTimeMs: undefined,
    lastCheck: new Date(Date.now() - 300000),
    errorMessage: 'Connection refused: ECONNREFUSED 127.0.0.1:6379',
    locale: 'en',
  },
};

// ========== Responsive Layout ==========

export const ResponsiveSm: Story = {
  args: {
    serviceName: 'postgres',
    status: 'Healthy',
    responseTimeMs: 15.3,
    lastCheck: new Date(Date.now() - 60000),
    locale: 'it',
  },
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
  },
};

export const ResponsiveMd: Story = {
  args: {
    serviceName: 'postgres',
    status: 'Healthy',
    responseTimeMs: 15.3,
    lastCheck: new Date(Date.now() - 60000),
    locale: 'it',
  },
  parameters: {
    viewport: {
      defaultViewport: 'tablet',
    },
  },
};
