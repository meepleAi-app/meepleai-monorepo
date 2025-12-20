import type { Meta, StoryObj } from '@storybook/react';
import { QuickActions, defaultQuickActions } from './QuickActions';
import {
  FileUpIcon,
  UsersIcon,
  AlertTriangleIcon,
  SettingsIcon,
  MessageSquareIcon,
  DatabaseIcon,
} from 'lucide-react';

/**
 * QuickActions Component - Issue #885
 *
 * Grid of quick action buttons for the admin dashboard.
 *
 * ## Features
 * - **Responsive Grid**: 3 cols desktop, 2 tablet, 1 mobile
 * - **Icon + Label**: Clear visual identification
 * - **Variants**: default, primary, warning, danger styling
 * - **Badges**: Optional notification counts
 * - **Loading State**: Skeleton placeholder
 */
const meta = {
  title: 'Admin/QuickActions',
  component: QuickActions,
  parameters: {
    layout: 'padded',
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
} satisfies Meta<typeof QuickActions>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default quick actions with all variants
 */
export const Default: Story = {
  args: {
    actions: defaultQuickActions,
  },
};

/**
 * With notification badges
 */
export const WithBadges: Story = {
  args: {
    actions: [
      {
        id: 'upload-pdf',
        label: 'Upload PDF',
        description: 'Add new game rules',
        href: '/admin/bulk-export',
        icon: FileUpIcon,
        variant: 'primary',
      },
      {
        id: 'manage-users',
        label: 'Manage Users',
        description: '1,247 users',
        href: '/admin/users',
        icon: UsersIcon,
        variant: 'default',
        badge: 5,
      },
      {
        id: 'view-alerts',
        label: 'View Alerts',
        description: 'System notifications',
        href: '/admin/configuration',
        icon: AlertTriangleIcon,
        variant: 'warning',
        badge: 12,
      },
      {
        id: 'manage-prompts',
        label: 'Prompts',
        description: 'AI prompt templates',
        href: '/admin/prompts',
        icon: MessageSquareIcon,
        variant: 'default',
      },
      {
        id: 'configuration',
        label: 'Configuration',
        description: 'System settings',
        href: '/admin/configuration',
        icon: SettingsIcon,
        variant: 'default',
      },
      {
        id: 'clear-cache',
        label: 'Cache',
        description: 'Manage cache',
        href: '/admin/cache',
        icon: DatabaseIcon,
        variant: 'danger',
        badge: 150, // Shows as 99+
      },
    ],
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
 * Custom title
 */
export const CustomTitle: Story = {
  args: {
    actions: defaultQuickActions.slice(0, 3),
    title: 'Admin Actions',
  },
};

/**
 * Few actions (2 items)
 */
export const FewActions: Story = {
  args: {
    actions: defaultQuickActions.slice(0, 2),
  },
};

/**
 * Many actions (8 items)
 */
export const ManyActions: Story = {
  args: {
    actions: [
      ...defaultQuickActions,
      {
        id: 'analytics',
        label: 'Analytics',
        description: 'View analytics',
        href: '/admin/analytics',
        icon: SettingsIcon,
        variant: 'default',
      },
      {
        id: 'n8n',
        label: 'Workflows',
        description: 'n8n templates',
        href: '/admin/n8n-templates',
        icon: DatabaseIcon,
        variant: 'primary',
      },
    ],
  },
};

/**
 * All danger variant for critical actions
 */
export const CriticalActions: Story = {
  args: {
    actions: [
      {
        id: 'clear-cache',
        label: 'Clear All Cache',
        description: 'Remove cached data',
        href: '/admin/cache',
        icon: DatabaseIcon,
        variant: 'danger',
      },
      {
        id: 'reset-sessions',
        label: 'Reset Sessions',
        description: 'Force logout all',
        href: '/admin/users',
        icon: UsersIcon,
        variant: 'danger',
      },
      {
        id: 'view-errors',
        label: 'Critical Errors',
        description: '5 unresolved',
        href: '/admin/configuration',
        icon: AlertTriangleIcon,
        variant: 'danger',
        badge: 5,
      },
    ],
    title: 'Critical Actions',
  },
};

/**
 * Mobile viewport
 */
export const Mobile: Story = {
  ...Default,
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
  ...Default,
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
  ...Default,
  parameters: {
    viewport: {
      defaultViewport: 'desktop',
    },
    chromatic: {
      viewports: [1024],
    },
  },
};
