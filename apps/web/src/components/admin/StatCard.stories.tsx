import type { Meta, StoryObj } from '@storybook/react';
import { StatCard } from './StatCard';

/**
 * StatCard - Issue #874
 *
 * Reusable metric display card for admin dashboard.
 * Shows value, label, optional trend indicator with color variants.
 */
const meta = {
  title: 'Admin/StatCard',
  component: StatCard,
  parameters: {
    layout: 'centered',
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
} satisfies Meta<typeof StatCard>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default stat card with just label and value
 */
export const Default: Story = {
  args: {
    label: 'Total Users',
    value: '1,247',
    variant: 'default',
  },
};

/**
 * Success variant with positive styling
 */
export const Success: Story = {
  args: {
    label: 'Active Sessions',
    value: '42',
    variant: 'success',
  },
};

/**
 * Warning variant for concerning metrics
 */
export const Warning: Story = {
  args: {
    label: 'Avg Latency',
    value: '547ms',
    variant: 'warning',
  },
};

/**
 * Danger variant for critical metrics
 */
export const Danger: Story = {
  args: {
    label: 'Error Rate',
    value: '12.5%',
    variant: 'danger',
  },
};

/**
 * With upward trend indicator (positive)
 */
export const WithTrendUp: Story = {
  args: {
    label: 'API Requests',
    value: '2,458',
    trend: 'up',
    trendValue: '+15% from yesterday',
    variant: 'success',
  },
};

/**
 * With downward trend indicator (negative)
 */
export const WithTrendDown: Story = {
  args: {
    label: 'Active Sessions',
    value: '28',
    trend: 'down',
    trendValue: '-8% from yesterday',
    variant: 'warning',
  },
};

/**
 * With neutral trend indicator
 */
export const WithTrendNeutral: Story = {
  args: {
    label: 'Total Games',
    value: '125',
    trend: 'neutral',
    trendValue: 'No change',
    variant: 'default',
  },
};

/**
 * Large numbers formatted
 */
export const LargeNumbers: Story = {
  args: {
    label: 'Total Tokens',
    value: '15.7M',
    variant: 'default',
  },
};

/**
 * Percentage value
 */
export const Percentage: Story = {
  args: {
    label: 'Avg Confidence',
    value: '94.2%',
    variant: 'success',
  },
};

/**
 * Latency metric
 */
export const Latency: Story = {
  args: {
    label: 'Avg Latency (24h)',
    value: '215ms',
    trend: 'up',
    trendValue: '+12ms from 7d avg',
    variant: 'warning',
  },
};
