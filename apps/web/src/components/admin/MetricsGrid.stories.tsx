import type { Meta, StoryObj } from '@storybook/react';
import { MetricsGrid } from './MetricsGrid';
import type { StatCardProps } from './StatCard';
import {
  Users,
  Activity,
  Gamepad2,
  Server,
  Clock,
  AlertTriangle,
  FileText,
  MessageCircle,
  Brain,
  Zap,
  Bell,
  CheckCircle,
} from 'lucide-react';

/**
 * MetricsGrid - Issue #874, #883
 *
 * Responsive 4x3 grid layout for admin dashboard metrics.
 * Features: responsive layout (4 cols desktop, 2 tablet, 1 mobile),
 * loading skeleton, smooth transitions, empty state.
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
  argTypes: {
    loading: {
      control: 'boolean',
      description: 'Show loading skeleton state',
    },
    emptyStateMessage: {
      control: 'text',
      description: 'Message shown when metrics array is empty',
    },
  },
} satisfies Meta<typeof MetricsGrid>;

export default meta;
type Story = StoryObj<typeof meta>;

const mockMetrics: StatCardProps[] = [
  {
    label: 'Total Users',
    value: '1,247',
    icon: Users,
    variant: 'default',
    trend: 'up',
    trendValue: '+12%',
  },
  {
    label: 'Active Sessions',
    value: '42',
    icon: Activity,
    variant: 'success',
    trend: 'up',
    trendValue: '+5',
  },
  { label: 'Total Games', value: '125', icon: Gamepad2, variant: 'default' },
  {
    label: 'API Requests (24h)',
    value: '3,456',
    icon: Server,
    variant: 'default',
    trend: 'up',
    trendValue: '+8%',
  },
  {
    label: 'Avg Latency',
    value: '215ms',
    icon: Clock,
    variant: 'success',
    trend: 'down',
    trendValue: '-15ms',
  },
  {
    label: 'Error Rate',
    value: '2.5%',
    icon: AlertTriangle,
    variant: 'warning',
    trend: 'up',
    trendValue: '+0.3%',
  },
  { label: 'Total PDFs', value: '847', icon: FileText, variant: 'default' },
  {
    label: 'Chat Messages',
    value: '15,234',
    icon: MessageCircle,
    variant: 'default',
    trend: 'up',
    trendValue: '+234',
  },
  { label: 'Avg Confidence', value: '94.2%', icon: Brain, variant: 'success' },
  { label: 'RAG Requests', value: '18,547', icon: Zap, variant: 'default' },
  { label: 'Active Alerts', value: '2', icon: Bell, variant: 'warning' },
  { label: 'Resolved Alerts', value: '37', icon: CheckCircle, variant: 'default' },
];

/**
 * Full grid with 12 metrics (4x3 layout)
 */
export const Full: Story = {
  args: {
    metrics: mockMetrics,
  },
};

/**
 * Loading state - shows 12 skeleton cards
 */
export const Loading: Story = {
  args: {
    metrics: [],
    loading: true,
  },
  parameters: {
    chromatic: {
      viewports: [375, 768, 1920],
    },
  },
};

/**
 * Empty state - no metrics available
 */
export const Empty: Story = {
  args: {
    metrics: [],
    loading: false,
  },
};

/**
 * Empty state with custom message
 */
export const EmptyCustomMessage: Story = {
  args: {
    metrics: [],
    loading: false,
    emptyStateMessage: 'Dashboard metrics are being loaded...',
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
