import { SystemStatus, defaultServices } from './SystemStatus';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * SystemStatus Component - Issue #885
 *
 * Displays overall system health status with service indicators.
 *
 * ## Features
 * - **Overall Health**: healthy/degraded/unhealthy indicator
 * - **Service List**: Individual service status with latency
 * - **Last Update**: Timestamp of last health check
 * - **Refresh Button**: Manual refresh capability
 * - **Loading State**: Skeleton placeholder
 */
const meta = {
  title: 'Admin/SystemStatus',
  component: SystemStatus,
  parameters: {
    layout: 'padded',
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
} satisfies Meta<typeof SystemStatus>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * All systems healthy
 */
export const Healthy: Story = {
  args: {
    services: [
      { name: 'Database', status: 'healthy', latency: 12 },
      { name: 'Redis Cache', status: 'healthy', latency: 2 },
      { name: 'Vector Store', status: 'healthy', latency: 45 },
      { name: 'AI Services', status: 'healthy', latency: 120 },
    ],
    overallStatus: 'healthy',
    lastUpdate: new Date(),
    onRefresh: () => console.log('Refresh clicked'),
  },
};

/**
 * Degraded performance
 */
export const Degraded: Story = {
  args: {
    services: [
      { name: 'Database', status: 'healthy', latency: 15 },
      { name: 'Redis Cache', status: 'degraded', latency: 85, message: 'High latency' },
      { name: 'Vector Store', status: 'healthy', latency: 52 },
      { name: 'AI Services', status: 'degraded', latency: 450, message: 'Slow responses' },
    ],
    overallStatus: 'degraded',
    lastUpdate: new Date(),
    onRefresh: () => console.log('Refresh clicked'),
  },
};

/**
 * System issues detected
 */
export const Unhealthy: Story = {
  args: {
    services: [
      { name: 'Database', status: 'healthy', latency: 18 },
      { name: 'Redis Cache', status: 'unhealthy', message: 'Connection failed' },
      { name: 'Vector Store', status: 'degraded', latency: 890 },
      { name: 'AI Services', status: 'unhealthy', message: 'Timeout errors' },
    ],
    overallStatus: 'unhealthy',
    lastUpdate: new Date(),
    onRefresh: () => console.log('Refresh clicked'),
  },
};

/**
 * Unknown status (no data)
 */
export const Unknown: Story = {
  args: {
    services: [
      { name: 'Database', status: 'unknown' },
      { name: 'Redis Cache', status: 'unknown' },
      { name: 'Vector Store', status: 'unknown' },
      { name: 'AI Services', status: 'unknown' },
    ],
    overallStatus: 'unknown',
    lastUpdate: new Date(),
    onRefresh: () => console.log('Refresh clicked'),
  },
};

/**
 * Loading skeleton state
 */
export const Loading: Story = {
  args: {
    loading: true,
  },
};

/**
 * Refreshing state (button spinning)
 */
export const Refreshing: Story = {
  args: {
    services: defaultServices,
    overallStatus: 'healthy',
    lastUpdate: new Date(),
    onRefresh: () => console.log('Refresh clicked'),
    refreshing: true,
  },
};

/**
 * Without refresh button
 */
export const NoRefresh: Story = {
  args: {
    services: defaultServices,
    overallStatus: 'healthy',
    lastUpdate: new Date(),
  },
};

/**
 * Extended service list
 */
export const ExtendedServices: Story = {
  args: {
    services: [
      { name: 'PostgreSQL', status: 'healthy', latency: 8 },
      { name: 'Redis', status: 'healthy', latency: 1 },
      { name: 'Qdrant', status: 'healthy', latency: 35 },
      { name: 'OpenRouter', status: 'healthy', latency: 180 },
      { name: 'Unstructured', status: 'healthy', latency: 220 },
      { name: 'SmolDocling', status: 'degraded', latency: 450 },
      { name: 'n8n', status: 'healthy', latency: 12 },
    ],
    overallStatus: 'degraded',
    lastUpdate: new Date(),
    onRefresh: () => console.log('Refresh clicked'),
  },
};

/**
 * Mobile viewport
 */
export const Mobile: Story = {
  ...Healthy,
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
 * Tablet viewport
 */
export const Tablet: Story = {
  ...Healthy,
  parameters: {
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
  ...Healthy,
  parameters: {
    viewport: {
      defaultViewport: 'desktop',
    },
    chromatic: {
      viewports: [1024],
    },
  },
};
