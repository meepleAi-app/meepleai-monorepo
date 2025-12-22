/**
 * BulkActionBar Storybook Stories (Issue #912)
 *
 * Visual testing and documentation for the BulkActionBar component.
 * Demonstrates various configurations and states.
 */

import { Trash2, Download, Power, Users, Archive, Mail, Tag } from 'lucide-react';

import { BulkActionBar, EmptyBulkActionBar } from './BulkActionBar';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof BulkActionBar> = {
  title: 'Admin/BulkActionBar',
  component: BulkActionBar,
  tags: ['autodocs'],
  parameters: {
    layout: 'padded',
    docs: {
      description: {
        component:
          'Reusable bulk action bar for admin pages. Provides consistent UI for performing actions on multiple selected items.',
      },
    },
  },
  argTypes: {
    selectedCount: {
      control: { type: 'number', min: 0, max: 100 },
      description: 'Number of selected items',
    },
    totalCount: {
      control: { type: 'number', min: 0, max: 100 },
      description: 'Total number of items',
    },
    itemLabel: {
      control: 'text',
      description: 'Plural label for items (e.g., "keys", "users")',
    },
    showProgress: {
      control: 'boolean',
      description: 'Show progress bar',
    },
    showTotal: {
      control: 'boolean',
      description: 'Show total count in badge',
    },
  },
};

export default meta;
type Story = StoryObj<typeof BulkActionBar>;

/**
 * Default state with basic delete and export actions.
 * Shows 3 selected items out of 10 total.
 */
export const Default: Story = {
  args: {
    selectedCount: 3,
    totalCount: 10,
    itemLabel: 'items',
    actions: [
      {
        id: 'delete',
        label: 'Delete',
        icon: Trash2,
        variant: 'destructive',
        onClick: count => alert(`Delete ${count} items`),
      },
      {
        id: 'export',
        label: 'Export',
        icon: Download,
        variant: 'outline',
        onClick: count => alert(`Export ${count} items`),
      },
    ],
    onClearSelection: () => alert('Clear selection'),
  },
};

/**
 * API Keys management with delete and export actions.
 * Real-world example from /admin/api-keys page.
 */
export const ApiKeys: Story = {
  args: {
    selectedCount: 5,
    totalCount: 23,
    itemLabel: 'keys',
    itemLabelSingular: 'key',
    actions: [
      {
        id: 'delete',
        label: 'Delete',
        icon: Trash2,
        variant: 'destructive',
        onClick: count => alert(`Delete ${count} API keys`),
      },
      {
        id: 'export',
        label: 'Export CSV',
        icon: Download,
        variant: 'outline',
        onClick: count => alert(`Export ${count} API keys to CSV`),
      },
    ],
    onClearSelection: () => alert('Clear selection'),
  },
};

/**
 * User management with multiple actions.
 * Shows delete, export, and enable/disable actions.
 */
export const UserManagement: Story = {
  args: {
    selectedCount: 7,
    totalCount: 42,
    itemLabel: 'users',
    itemLabelSingular: 'user',
    actions: [
      {
        id: 'delete',
        label: 'Delete',
        icon: Trash2,
        variant: 'destructive',
        onClick: count => alert(`Delete ${count} users`),
      },
      {
        id: 'enable',
        label: 'Enable',
        icon: Power,
        variant: 'default',
        onClick: count => alert(`Enable ${count} users`),
      },
      {
        id: 'disable',
        label: 'Disable',
        icon: Power,
        variant: 'secondary',
        onClick: count => alert(`Disable ${count} users`),
      },
      {
        id: 'export',
        label: 'Export',
        icon: Download,
        variant: 'outline',
        onClick: count => alert(`Export ${count} users`),
      },
    ],
    onClearSelection: () => alert('Clear selection'),
  },
};

/**
 * Single item selected.
 * Shows singular label form.
 */
export const SingleItem: Story = {
  args: {
    selectedCount: 1,
    totalCount: 20,
    itemLabel: 'documents',
    itemLabelSingular: 'document',
    actions: [
      {
        id: 'delete',
        label: 'Delete',
        icon: Trash2,
        variant: 'destructive',
        onClick: count => alert(`Delete ${count} document`),
      },
      {
        id: 'archive',
        label: 'Archive',
        icon: Archive,
        variant: 'secondary',
        onClick: count => alert(`Archive ${count} document`),
      },
    ],
    onClearSelection: () => alert('Clear selection'),
  },
};

/**
 * All items selected (100% progress).
 * Shows full progress bar.
 */
export const AllSelected: Story = {
  args: {
    selectedCount: 15,
    totalCount: 15,
    itemLabel: 'rules',
    itemLabelSingular: 'rule',
    actions: [
      {
        id: 'export',
        label: 'Export All',
        icon: Download,
        variant: 'default',
        onClick: count => alert(`Export all ${count} rules`),
      },
      {
        id: 'archive',
        label: 'Archive',
        icon: Archive,
        variant: 'secondary',
        onClick: count => alert(`Archive ${count} rules`),
      },
    ],
    onClearSelection: () => alert('Clear selection'),
  },
};

/**
 * Many actions available.
 * Shows how the UI handles multiple action buttons.
 */
export const ManyActions: Story = {
  args: {
    selectedCount: 12,
    totalCount: 50,
    itemLabel: 'items',
    actions: [
      {
        id: 'delete',
        label: 'Delete',
        icon: Trash2,
        variant: 'destructive',
        onClick: count => alert(`Delete ${count} items`),
      },
      {
        id: 'assign',
        label: 'Assign',
        icon: Users,
        variant: 'default',
        onClick: count => alert(`Assign ${count} items`),
      },
      {
        id: 'tag',
        label: 'Tag',
        icon: Tag,
        variant: 'secondary',
        onClick: count => alert(`Tag ${count} items`),
      },
      {
        id: 'archive',
        label: 'Archive',
        icon: Archive,
        variant: 'outline',
        onClick: count => alert(`Archive ${count} items`),
      },
      {
        id: 'email',
        label: 'Email',
        icon: Mail,
        variant: 'outline',
        onClick: count => alert(`Email ${count} items`),
      },
      {
        id: 'export',
        label: 'Export',
        icon: Download,
        variant: 'outline',
        onClick: count => alert(`Export ${count} items`),
      },
    ],
    onClearSelection: () => alert('Clear selection'),
  },
};

/**
 * Some actions disabled.
 * Shows how disabled actions appear.
 */
export const DisabledActions: Story = {
  args: {
    selectedCount: 4,
    totalCount: 20,
    itemLabel: 'files',
    itemLabelSingular: 'file',
    actions: [
      {
        id: 'delete',
        label: 'Delete',
        icon: Trash2,
        variant: 'destructive',
        onClick: count => alert(`Delete ${count} files`),
      },
      {
        id: 'archive',
        label: 'Archive',
        icon: Archive,
        variant: 'secondary',
        onClick: count => alert(`Archive ${count} files`),
        disabled: true,
        tooltip: 'Archiving is temporarily disabled',
      },
      {
        id: 'export',
        label: 'Export',
        icon: Download,
        variant: 'outline',
        onClick: count => alert(`Export ${count} files`),
        disabled: true,
        tooltip: 'Export limit reached',
      },
    ],
    onClearSelection: () => alert('Clear selection'),
  },
};

/**
 * Without progress bar.
 * Shows simplified version without progress indicator.
 */
export const NoProgress: Story = {
  args: {
    selectedCount: 8,
    totalCount: 30,
    itemLabel: 'items',
    showProgress: false,
    actions: [
      {
        id: 'delete',
        label: 'Delete',
        icon: Trash2,
        variant: 'destructive',
        onClick: count => alert(`Delete ${count} items`),
      },
      {
        id: 'export',
        label: 'Export',
        icon: Download,
        variant: 'outline',
        onClick: count => alert(`Export ${count} items`),
      },
    ],
    onClearSelection: () => alert('Clear selection'),
  },
};

/**
 * Without total count.
 * Shows only selected count without total.
 */
export const NoTotal: Story = {
  args: {
    selectedCount: 6,
    totalCount: 25,
    itemLabel: 'items',
    showTotal: false,
    actions: [
      {
        id: 'delete',
        label: 'Delete',
        icon: Trash2,
        variant: 'destructive',
        onClick: count => alert(`Delete ${count} items`),
      },
    ],
    onClearSelection: () => alert('Clear selection'),
  },
};

/**
 * Actions without count in label.
 * Shows cleaner action buttons.
 */
export const ActionsWithoutCount: Story = {
  args: {
    selectedCount: 5,
    totalCount: 20,
    itemLabel: 'items',
    actions: [
      {
        id: 'delete',
        label: 'Delete Selected',
        icon: Trash2,
        variant: 'destructive',
        onClick: count => alert(`Delete ${count} items`),
        showCount: false,
      },
      {
        id: 'export',
        label: 'Export Selected',
        icon: Download,
        variant: 'outline',
        onClick: count => alert(`Export ${count} items`),
        showCount: false,
      },
    ],
    onClearSelection: () => alert('Clear selection'),
  },
};

/**
 * Mobile responsive view.
 * Shows how the component adapts to smaller screens.
 */
export const Mobile: Story = {
  args: {
    selectedCount: 3,
    totalCount: 10,
    itemLabel: 'items',
    actions: [
      {
        id: 'delete',
        label: 'Delete',
        icon: Trash2,
        variant: 'destructive',
        onClick: count => alert(`Delete ${count} items`),
      },
      {
        id: 'export',
        label: 'Export',
        icon: Download,
        variant: 'outline',
        onClick: count => alert(`Export ${count} items`),
      },
    ],
    onClearSelection: () => alert('Clear selection'),
  },
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
  },
};

/**
 * Dark mode.
 * Shows component in dark theme.
 */
export const DarkMode: Story = {
  args: {
    selectedCount: 4,
    totalCount: 15,
    itemLabel: 'items',
    actions: [
      {
        id: 'delete',
        label: 'Delete',
        icon: Trash2,
        variant: 'destructive',
        onClick: count => alert(`Delete ${count} items`),
      },
      {
        id: 'export',
        label: 'Export',
        icon: Download,
        variant: 'outline',
        onClick: count => alert(`Export ${count} items`),
      },
    ],
    onClearSelection: () => alert('Clear selection'),
  },
  parameters: {
    backgrounds: { default: 'dark' },
  },
};

/**
 * Empty state component.
 * Shows informational message when no items are selected.
 */
export const EmptyState: StoryObj<typeof EmptyBulkActionBar> = {
  render: () => <EmptyBulkActionBar itemLabel="API keys" />,
};

/**
 * Empty state with custom message.
 * Shows custom informational message.
 */
export const EmptyStateCustomMessage: StoryObj<typeof EmptyBulkActionBar> = {
  render: () => (
    <EmptyBulkActionBar
      itemLabel="documents"
      message="Check the boxes next to documents to enable bulk operations"
    />
  ),
};
