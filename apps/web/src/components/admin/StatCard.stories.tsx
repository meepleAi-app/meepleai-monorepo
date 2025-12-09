import type { Meta, StoryObj } from '@storybook/react';
import { StatCard } from './StatCard';
import {
  Users,
  Activity,
  Clock,
  AlertTriangle,
  TrendingUp,
  Database,
  Zap,
  FileText,
} from 'lucide-react';

/**
 * StatCard - Issue #874, #882
 *
 * Reusable metric display card for admin dashboard.
 * Features: Icon, Label, Value, Trend indicator, Color variants, Loading skeleton, Hover effect.
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

// ==================== Issue #882 New Stories ====================

/**
 * With icon - Users metric
 */
export const WithIconUsers: Story = {
  args: {
    label: 'Total Users',
    value: '1,247',
    icon: Users,
    variant: 'default',
  },
};

/**
 * With icon - Activity metric with trend
 */
export const WithIconActivity: Story = {
  args: {
    label: 'Active Sessions',
    value: '42',
    icon: Activity,
    trend: 'up',
    trendValue: '+15% from yesterday',
    variant: 'success',
  },
};

/**
 * With icon - Warning state
 */
export const WithIconWarning: Story = {
  args: {
    label: 'Avg Latency',
    value: '547ms',
    icon: Clock,
    trend: 'up',
    trendValue: '+120ms',
    variant: 'warning',
  },
};

/**
 * With icon - Danger state
 */
export const WithIconDanger: Story = {
  args: {
    label: 'Error Rate',
    value: '12.5%',
    icon: AlertTriangle,
    trend: 'up',
    trendValue: '+5.2%',
    variant: 'danger',
  },
};

/**
 * Loading state - with icon placeholder
 */
export const LoadingWithIcon: Story = {
  args: {
    label: 'Total Users',
    value: '1,247',
    icon: Users,
    loading: true,
    variant: 'default',
  },
};

/**
 * Loading state - without icon
 */
export const LoadingWithoutIcon: Story = {
  args: {
    label: 'Total Users',
    value: '1,247',
    loading: true,
    variant: 'default',
  },
};

/**
 * Loading state - success variant
 */
export const LoadingSuccess: Story = {
  args: {
    label: 'Active Sessions',
    value: '42',
    icon: Activity,
    loading: true,
    variant: 'success',
  },
};

/**
 * Dashboard metrics grid example
 */
export const DashboardGrid: Story = {
  render: () => (
    <div className="grid grid-cols-2 gap-4 max-w-2xl">
      <StatCard
        label="Total Users"
        value="1,247"
        icon={Users}
        trend="up"
        trendValue="+12%"
        variant="default"
      />
      <StatCard
        label="Active Sessions"
        value="42"
        icon={Activity}
        trend="up"
        trendValue="+15%"
        variant="success"
      />
      <StatCard
        label="Avg Latency"
        value="215ms"
        icon={Clock}
        trend="up"
        trendValue="+12ms"
        variant="warning"
      />
      <StatCard
        label="Error Rate"
        value="0.5%"
        icon={AlertTriangle}
        trend="down"
        trendValue="-0.2%"
        variant="success"
      />
    </div>
  ),
};

/**
 * All icons showcase
 */
export const IconShowcase: Story = {
  render: () => (
    <div className="grid grid-cols-2 gap-4 max-w-2xl">
      <StatCard label="Users" value="1,247" icon={Users} variant="default" />
      <StatCard label="Activity" value="42" icon={Activity} variant="success" />
      <StatCard label="Latency" value="215ms" icon={Clock} variant="warning" />
      <StatCard label="Errors" value="3" icon={AlertTriangle} variant="danger" />
      <StatCard label="Growth" value="+25%" icon={TrendingUp} variant="success" />
      <StatCard label="Storage" value="2.4GB" icon={Database} variant="default" />
      <StatCard label="Speed" value="Fast" icon={Zap} variant="success" />
      <StatCard label="Documents" value="156" icon={FileText} variant="default" />
    </div>
  ),
};

/**
 * Loading grid example
 */
export const LoadingGrid: Story = {
  render: () => (
    <div className="grid grid-cols-2 gap-4 max-w-2xl">
      <StatCard label="" value="" icon={Users} loading variant="default" />
      <StatCard label="" value="" icon={Activity} loading variant="success" />
      <StatCard label="" value="" icon={Clock} loading variant="warning" />
      <StatCard label="" value="" icon={AlertTriangle} loading variant="danger" />
    </div>
  ),
};
