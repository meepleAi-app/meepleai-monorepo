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

import { StatCard } from './stat-card';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * StatCard - Generic Metric Display Card
 *
 * A reusable card component for displaying metrics with optional icon,
 * trend indicator, and loading state. Used in dashboards and analytics pages.
 *
 * @see Issue #2925 - Component Library extraction
 */
const meta = {
  title: 'Data Display/StatCard',
  component: StatCard,
  parameters: {
    layout: 'centered',
    chromatic: {
      viewports: [375, 768, 1024],
    },
    docs: {
      description: {
        component: `
A versatile metric display card that supports:
- **Icons**: Any Lucide icon for visual context
- **Trends**: Up/down/neutral indicators with descriptions
- **Variants**: default, success, warning, danger for semantic meaning
- **Loading state**: Skeleton placeholder for async data
- **Responsive**: Works well in grids and standalone

## Usage

\`\`\`tsx
import { StatCard } from '@/components/ui/data-display/stat-card';
import { Users } from 'lucide-react';

<StatCard
  label="Total Users"
  value="1,247"
  icon={Users}
  trend="up"
  trendValue="+15%"
  variant="success"
/>
\`\`\`
        `,
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    variant: {
      control: 'select',
      options: ['default', 'success', 'warning', 'danger'],
      description: 'Visual variant for semantic meaning',
    },
    trend: {
      control: 'select',
      options: ['up', 'down', 'neutral', undefined],
      description: 'Trend direction indicator',
    },
    loading: {
      control: 'boolean',
      description: 'Show loading skeleton',
    },
  },
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
 * With icon - most common usage
 */
export const WithIcon: Story = {
  args: {
    label: 'Total Users',
    value: '1,247',
    icon: Users,
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
    icon: Activity,
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
    icon: Clock,
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
    icon: AlertTriangle,
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
    icon: TrendingUp,
    trend: 'up',
    trendValue: '+15% from yesterday',
    variant: 'success',
  },
};

/**
 * With downward trend indicator
 */
export const WithTrendDown: Story = {
  args: {
    label: 'Active Sessions',
    value: '28',
    icon: Activity,
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
    icon: Database,
    trend: 'neutral',
    trendValue: 'No change',
    variant: 'default',
  },
};

/**
 * Loading state with skeleton
 */
export const Loading: Story = {
  args: {
    label: 'Total Users',
    value: '1,247',
    icon: Users,
    loading: true,
    variant: 'default',
  },
};

/**
 * Loading state without icon
 */
export const LoadingNoIcon: Story = {
  args: {
    label: 'Total Users',
    value: '1,247',
    loading: true,
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
    icon: Zap,
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
    icon: TrendingUp,
    variant: 'success',
  },
};

/**
 * Dashboard grid example - shows how cards look together
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
 * Icon showcase - all common metric icons
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
 * Loading grid - skeleton state for all variants
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
