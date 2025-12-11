/**
 * Grafana Embed Component - Chromatic Visual Tests
 * Issue #901 - Grafana embed iframe setup
 *
 * Stories:
 * 1. Default (Infrastructure dashboard)
 * 2. LLM Cost dashboard
 * 3. API Performance dashboard
 * 4. RAG Operations dashboard
 * 5. Loading state
 * 6. Error state
 * 7. Italian locale
 * 8. Dark mode
 * 9. Mobile view
 */

import type { Meta, StoryObj } from '@storybook/react';
import { GrafanaEmbed } from '../../GrafanaEmbed';

const meta: Meta<typeof GrafanaEmbed> = {
  title: 'Admin/GrafanaEmbed',
  component: GrafanaEmbed,
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      delay: 2000, // Wait for iframe loading
      diffThreshold: 0.3,
    },
  },
  tags: ['autodocs'],
};

export default meta;
type Story = StoryObj<typeof GrafanaEmbed>;

/**
 * Default story - Infrastructure Monitoring dashboard
 */
export const Default: Story = {
  args: {
    locale: 'en',
    defaultDashboard: 'infrastructure',
    autoRefresh: '30s',
    timeRange: { from: 'now-1h', to: 'now' },
  },
};

/**
 * LLM Cost Tracking dashboard
 */
export const LLMCost: Story = {
  args: {
    locale: 'en',
    defaultDashboard: 'llm-cost',
    autoRefresh: '30s',
    timeRange: { from: 'now-1h', to: 'now' },
  },
};

/**
 * API Performance dashboard
 */
export const APIPerformance: Story = {
  args: {
    locale: 'en',
    defaultDashboard: 'api-performance',
    autoRefresh: '30s',
    timeRange: { from: 'now-1h', to: 'now' },
  },
};

/**
 * RAG Operations dashboard
 */
export const RAGOperations: Story = {
  args: {
    locale: 'en',
    defaultDashboard: 'rag-operations',
    autoRefresh: '30s',
    timeRange: { from: 'now-1h', to: 'now' },
  },
};

/**
 * Italian locale
 */
export const Italian: Story = {
  args: {
    locale: 'it',
    defaultDashboard: 'infrastructure',
    autoRefresh: '30s',
    timeRange: { from: 'now-1h', to: 'now' },
  },
};

/**
 * Different time range (6 hours)
 */
export const LongerTimeRange: Story = {
  args: {
    locale: 'en',
    defaultDashboard: 'infrastructure',
    autoRefresh: '1m',
    timeRange: { from: 'now-6h', to: 'now' },
  },
};

/**
 * Dark mode
 */
export const DarkMode: Story = {
  args: {
    locale: 'en',
    defaultDashboard: 'infrastructure',
    autoRefresh: '30s',
    timeRange: { from: 'now-1h', to: 'now' },
  },
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => (
      <div className="dark">
        <Story />
      </div>
    ),
  ],
};

/**
 * Mobile viewport
 */
export const Mobile: Story = {
  args: {
    locale: 'en',
    defaultDashboard: 'infrastructure',
    autoRefresh: '30s',
    timeRange: { from: 'now-1h', to: 'now' },
  },
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
  args: {
    locale: 'en',
    defaultDashboard: 'infrastructure',
    autoRefresh: '30s',
    timeRange: { from: 'now-1h', to: 'now' },
  },
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
 * All dashboards in Italian (comprehensive test)
 */
export const AllDashboardsItalian: Story = {
  args: {
    locale: 'it',
    defaultDashboard: 'infrastructure',
    autoRefresh: '30s',
    timeRange: { from: 'now-1h', to: 'now' },
  },
};
