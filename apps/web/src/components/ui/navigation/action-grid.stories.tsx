import {
  UsersIcon,
  SettingsIcon,
  PlusIcon,
  DownloadIcon,
  AlertTriangleIcon,
  TrashIcon,
  ZapIcon,
  FileTextIcon,
  CheckIcon,
} from 'lucide-react';

import { ActionGrid } from './action-grid';

import type { ActionItem } from './action-grid';
import type { Meta, StoryObj } from '@storybook/react';

/**
 * Sample actions for stories
 */
const sampleActions: ActionItem[] = [
  {
    id: 'add',
    label: 'Add Item',
    description: 'Create a new item',
    href: '/add',
    icon: PlusIcon,
    variant: 'primary',
  },
  {
    id: 'users',
    label: 'Manage Users',
    description: '1,247 users',
    href: '/users',
    icon: UsersIcon,
    variant: 'default',
  },
  {
    id: 'settings',
    label: 'Settings',
    description: 'System configuration',
    href: '/settings',
    icon: SettingsIcon,
    variant: 'default',
  },
  {
    id: 'export',
    label: 'Export Data',
    description: 'Download reports',
    href: '/export',
    icon: DownloadIcon,
    variant: 'default',
  },
];

const gradientActions: ActionItem[] = [
  {
    id: 'approve',
    label: 'Approve Items',
    description: 'Review pending',
    href: '/approve',
    icon: CheckIcon,
    gradient: 'green-emerald',
    badge: 5,
  },
  {
    id: 'users',
    label: 'Manage Users',
    description: '1,247 users',
    href: '/users',
    icon: UsersIcon,
    gradient: 'blue-indigo',
  },
  {
    id: 'alerts',
    label: 'View Alerts',
    description: 'System notifications',
    href: '/alerts',
    icon: AlertTriangleIcon,
    gradient: 'amber-orange',
    badge: 3,
  },
  {
    id: 'clear-cache',
    label: 'Clear Cache',
    description: 'Reset cached data',
    href: '/cache',
    icon: TrashIcon,
    gradient: 'red-rose',
  },
  {
    id: 'export',
    label: 'Export Data',
    description: 'Download reports',
    href: '/export',
    icon: DownloadIcon,
    gradient: 'purple-violet',
  },
  {
    id: 'config',
    label: 'Configuration',
    description: 'System settings',
    href: '/config',
    icon: SettingsIcon,
    gradient: 'stone-stone',
  },
];

/**
 * ActionGrid - Generic Quick Actions Grid
 *
 * A reusable responsive grid of action links with icons, descriptions,
 * badges, and variant or gradient styling.
 *
 * @see Issue #2925 - Component Library extraction
 */
const meta = {
  title: 'Navigation/ActionGrid',
  component: ActionGrid,
  parameters: {
    layout: 'centered',
    chromatic: {
      viewports: [375, 768, 1024],
    },
    docs: {
      description: {
        component: `
A versatile quick actions grid component that supports:
- **Responsive layout**: 3 cols desktop, 2 tablet, 1 mobile
- **Icons**: Lucide icons with hover scaling for gradients
- **Styling**: Variant-based or gradient-based
- **Badges**: Optional notification badges
- **Card wrapper**: Optional card with title
- **Loading state**: Skeleton placeholder

## Usage

\`\`\`tsx
import { ActionGrid, type ActionItem } from '@/components/ui/navigation/action-grid';
import { PlusIcon, SettingsIcon } from 'lucide-react';

const actions: ActionItem[] = [
  { id: 'add', label: 'Add Item', href: '/add', icon: PlusIcon },
  { id: 'settings', label: 'Settings', href: '/settings', icon: SettingsIcon },
];

<ActionGrid actions={actions} title="Quick Actions" />
\`\`\`
        `,
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    columns: {
      control: 'select',
      options: [2, 3, 4],
      description: 'Number of columns on large screens',
    },
    loading: {
      control: 'boolean',
      description: 'Show loading skeleton',
    },
    showCard: {
      control: 'boolean',
      description: 'Show card wrapper',
    },
  },
  decorators: [
    (Story) => (
      <div className="w-[700px]">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof ActionGrid>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default action grid without card wrapper
 */
export const Default: Story = {
  args: {
    actions: sampleActions,
  },
};

/**
 * With card wrapper and title
 */
export const WithTitle: Story = {
  args: {
    actions: sampleActions,
    title: 'Quick Actions',
    titleIcon: ZapIcon,
  },
};

/**
 * Gradient styling (modern look)
 */
export const GradientStyle: Story = {
  args: {
    actions: gradientActions,
    title: 'Quick Actions',
    titleIcon: ZapIcon,
  },
};

/**
 * With badges
 */
export const WithBadges: Story = {
  args: {
    actions: [
      ...sampleActions,
      {
        id: 'alerts',
        label: 'View Alerts',
        description: 'System notifications',
        href: '/alerts',
        icon: AlertTriangleIcon,
        variant: 'warning',
        badge: 12,
      },
    ],
    title: 'Quick Actions',
  },
};

/**
 * Dynamic badges via props
 */
export const DynamicBadges: Story = {
  args: {
    actions: gradientActions,
    title: 'Quick Actions',
    badges: {
      approve: 8,
      alerts: 15,
    },
  },
};

/**
 * Two columns
 */
export const TwoColumns: Story = {
  args: {
    actions: sampleActions,
    columns: 2,
    title: 'Actions',
  },
};

/**
 * Four columns
 */
export const FourColumns: Story = {
  args: {
    actions: [...sampleActions, ...sampleActions],
    columns: 4,
    title: 'Extended Actions',
  },
  decorators: [
    (Story) => (
      <div className="w-[900px]">
        <Story />
      </div>
    ),
  ],
};

/**
 * Loading state
 */
export const Loading: Story = {
  args: {
    actions: sampleActions,
    title: 'Quick Actions',
    loading: true,
  },
};

/**
 * Without card wrapper
 */
export const NoCard: Story = {
  args: {
    actions: gradientActions.slice(0, 4),
    showCard: false,
  },
};

/**
 * Variant styling (non-gradient)
 */
export const VariantStyle: Story = {
  args: {
    actions: [
      { id: 'primary', label: 'Primary Action', href: '#', icon: PlusIcon, variant: 'primary' },
      { id: 'warning', label: 'Warning Action', href: '#', icon: AlertTriangleIcon, variant: 'warning' },
      { id: 'danger', label: 'Danger Action', href: '#', icon: TrashIcon, variant: 'danger' },
      { id: 'default', label: 'Default Action', href: '#', icon: FileTextIcon, variant: 'default' },
    ],
    title: 'Variant Examples',
  },
};

/**
 * Badge overflow (99+)
 */
export const BadgeOverflow: Story = {
  args: {
    actions: [
      {
        id: 'notifications',
        label: 'Notifications',
        href: '/notifications',
        icon: AlertTriangleIcon,
        gradient: 'amber-orange',
        badge: 150,
      },
    ],
    title: 'Badge Overflow',
  },
};

/**
 * All gradient presets
 */
export const GradientShowcase: Story = {
  render: () => (
    <ActionGrid
      actions={[
        { id: 'green', label: 'Green Emerald', href: '#', icon: CheckIcon, gradient: 'green-emerald' },
        { id: 'blue', label: 'Blue Indigo', href: '#', icon: UsersIcon, gradient: 'blue-indigo' },
        { id: 'amber', label: 'Amber Orange', href: '#', icon: AlertTriangleIcon, gradient: 'amber-orange' },
        { id: 'red', label: 'Red Rose', href: '#', icon: TrashIcon, gradient: 'red-rose' },
        { id: 'purple', label: 'Purple Violet', href: '#', icon: DownloadIcon, gradient: 'purple-violet' },
        { id: 'stone', label: 'Stone Stone', href: '#', icon: SettingsIcon, gradient: 'stone-stone' },
      ]}
      title="Gradient Presets"
      titleIcon={ZapIcon}
    />
  ),
};
