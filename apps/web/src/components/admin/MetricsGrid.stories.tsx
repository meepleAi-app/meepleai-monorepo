import type { Meta, StoryObj } from '@storybook/react';
import { MetricsGrid } from './MetricsGrid';
import type { StatCardProps } from './StatCard';

/**
 * MetricsGrid - Issue #874
 *
 * Responsive 4-column grid layout for admin dashboard metrics.
 * Automatically adapts to mobile (1col), tablet (2col), desktop (3-4col).
 */
const meta = {
  title: 'Admin/MetricsGrid',
  component: MetricsGrid,
  parameters: {
    layout: 'padded',
    chromatic: {
      viewports: [375, 768, 1024, 1920],
    },
  },
  tags: ['autodocs'],
} satisfies Meta<typeof MetricsGrid>;

export default meta;
type Story = StoryObj<typeof meta>;

const mockMetrics: StatCardProps[] = [
  { label: 'Total Users', value: '1,247', variant: 'default' },
  { label: 'Active Sessions', value: '42', variant: 'success' },
  { label: 'Total Games', value: '125', variant: 'default' },
  { label: 'API Requests (24h)', value: '3,456', variant: 'default' },
  { label: 'API Requests (7d)', value: '24,891', variant: 'default' },
  { label: 'API Requests (30d)', value: '112,034', variant: 'default' },
  { label: 'Avg Latency (24h)', value: '215ms', variant: 'success' },
  { label: 'Avg Latency (7d)', value: '228ms', variant: 'default' },
  { label: 'Error Rate (24h)', value: '2.5%', variant: 'warning' },
  { label: 'Total PDFs', value: '847', variant: 'default' },
  { label: 'Total Chat Messages', value: '15,234', variant: 'default' },
  { label: 'Avg Confidence', value: '94.2%', variant: 'success' },
  { label: 'Total RAG Requests', value: '18,547', variant: 'default' },
  { label: 'Total Tokens', value: '15.7M', variant: 'default' },
  { label: 'Active Alerts', value: '2', variant: 'warning' },
  { label: 'Resolved Alerts', value: '37', variant: 'default' },
];

/**
 * Full grid with 16 metrics (4x4 layout)
 */
export const Full: Story = {
  args: {
    metrics: mockMetrics,
  },
};

/**
 * Grid with few metrics (partial data scenario)
 */
export const PartialData: Story = {
  args: {
    metrics: mockMetrics.slice(0, 6),
  },
};

/**
 * Grid with mixed variant states
 */
export const MixedVariants: Story = {
  args: {
    metrics: [
      { label: 'Healthy Metric', value: '100', variant: 'success' },
      { label: 'Warning Metric', value: '75', variant: 'warning' },
      { label: 'Critical Metric', value: '15', variant: 'danger' },
      { label: 'Normal Metric', value: '50', variant: 'default' },
    ],
  },
};

/**
 * Mobile viewport (375px) - stacks vertically
 */
export const Mobile: Story = {
  args: {
    metrics: mockMetrics,
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
 * Tablet viewport (768px) - 2 columns
 */
export const Tablet: Story = {
  args: {
    metrics: mockMetrics,
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
 * Desktop viewport (1920px) - 4 columns
 */
export const Desktop: Story = {
  args: {
    metrics: mockMetrics,
  },
  parameters: {
    viewport: {
      defaultViewport: 'desktop',
    },
    chromatic: {
      viewports: [1920],
    },
  },
};
