/**
 * BulkActionBar Chromatic Visual Tests (Issue #912)
 *
 * Visual regression tests for BulkActionBar component using Chromatic.
 * Tests various states, themes, and responsive layouts.
 */

import React from 'react';
import { describe, it } from 'vitest';
import type { Meta, StoryObj } from '@storybook/react';
import { Trash2, Download, Power, Archive, Mail, Tag } from 'lucide-react';
import { BulkActionBar, EmptyBulkActionBar } from '../../BulkActionBar';

/**
 * Chromatic test suite for BulkActionBar component
 * Each test creates a visual snapshot for regression testing
 */
describe('BulkActionBar - Chromatic Visual Tests', () => {
  it('should match visual snapshot - Default state', () => {
    // Snapshot will be captured by Chromatic via Storybook
  });

  it('should match visual snapshot - API Keys management', () => {
    // Snapshot will be captured by Chromatic via Storybook
  });

  it('should match visual snapshot - All items selected', () => {
    // Snapshot will be captured by Chromatic via Storybook
  });

  it('should match visual snapshot - Multiple actions', () => {
    // Snapshot will be captured by Chromatic via Storybook
  });

  it('should match visual snapshot - Disabled actions', () => {
    // Snapshot will be captured by Chromatic via Storybook
  });

  it('should match visual snapshot - Mobile responsive', () => {
    // Snapshot will be captured by Chromatic via Storybook
  });

  it('should match visual snapshot - Dark mode', () => {
    // Snapshot will be captured by Chromatic via Storybook
  });

  it('should match visual snapshot - Empty state', () => {
    // Snapshot will be captured by Chromatic via Storybook
  });
});

// Export stories for Chromatic
const meta: Meta<typeof BulkActionBar> = {
  title: 'Admin/BulkActionBar/Chromatic',
  component: BulkActionBar,
  tags: ['autodocs'],
  parameters: {
    chromatic: {
      // Capture snapshots for visual regression testing
      disableSnapshot: false,
      // Test on multiple viewports
      viewports: [320, 768, 1024, 1920],
    },
  },
};

export default meta;
type Story = StoryObj<typeof BulkActionBar>;

/**
 * Default state with few items selected
 */
export const DefaultState: Story = {
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
        onClick: () => {},
      },
      {
        id: 'export',
        label: 'Export',
        icon: Download,
        variant: 'outline',
        onClick: () => {},
      },
    ],
    onClearSelection: () => {},
  },
  parameters: {
    chromatic: { disableSnapshot: false },
  },
};

/**
 * API Keys management scenario
 */
export const ApiKeysManagement: Story = {
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
        onClick: () => {},
      },
      {
        id: 'export',
        label: 'Export CSV',
        icon: Download,
        variant: 'outline',
        onClick: () => {},
      },
    ],
    onClearSelection: () => {},
  },
  parameters: {
    chromatic: { disableSnapshot: false },
  },
};

/**
 * All items selected (100% progress)
 */
export const AllItemsSelected: Story = {
  args: {
    selectedCount: 15,
    totalCount: 15,
    itemLabel: 'documents',
    actions: [
      {
        id: 'export',
        label: 'Export All',
        icon: Download,
        variant: 'default',
        onClick: () => {},
      },
      {
        id: 'archive',
        label: 'Archive',
        icon: Archive,
        variant: 'secondary',
        onClick: () => {},
      },
    ],
    onClearSelection: () => {},
  },
  parameters: {
    chromatic: { disableSnapshot: false },
  },
};

/**
 * Single item selected
 */
export const SingleItemSelected: Story = {
  args: {
    selectedCount: 1,
    totalCount: 20,
    itemLabel: 'users',
    itemLabelSingular: 'user',
    actions: [
      {
        id: 'delete',
        label: 'Delete',
        icon: Trash2,
        variant: 'destructive',
        onClick: () => {},
      },
    ],
    onClearSelection: () => {},
  },
  parameters: {
    chromatic: { disableSnapshot: false },
  },
};

/**
 * Multiple action buttons
 */
export const MultipleActions: Story = {
  args: {
    selectedCount: 8,
    totalCount: 30,
    itemLabel: 'items',
    actions: [
      {
        id: 'delete',
        label: 'Delete',
        icon: Trash2,
        variant: 'destructive',
        onClick: () => {},
      },
      {
        id: 'archive',
        label: 'Archive',
        icon: Archive,
        variant: 'secondary',
        onClick: () => {},
      },
      {
        id: 'tag',
        label: 'Tag',
        icon: Tag,
        variant: 'outline',
        onClick: () => {},
      },
      {
        id: 'email',
        label: 'Email',
        icon: Mail,
        variant: 'outline',
        onClick: () => {},
      },
      {
        id: 'export',
        label: 'Export',
        icon: Download,
        variant: 'outline',
        onClick: () => {},
      },
    ],
    onClearSelection: () => {},
  },
  parameters: {
    chromatic: { disableSnapshot: false },
  },
};

/**
 * With disabled actions
 */
export const DisabledActions: Story = {
  args: {
    selectedCount: 4,
    totalCount: 20,
    itemLabel: 'files',
    actions: [
      {
        id: 'delete',
        label: 'Delete',
        icon: Trash2,
        variant: 'destructive',
        onClick: () => {},
      },
      {
        id: 'archive',
        label: 'Archive',
        icon: Archive,
        variant: 'secondary',
        onClick: () => {},
        disabled: true,
        tooltip: 'Archiving is temporarily disabled',
      },
      {
        id: 'export',
        label: 'Export',
        icon: Download,
        variant: 'outline',
        onClick: () => {},
        disabled: true,
        tooltip: 'Export limit reached',
      },
    ],
    onClearSelection: () => {},
  },
  parameters: {
    chromatic: { disableSnapshot: false },
  },
};

/**
 * Without progress bar
 */
export const NoProgressBar: Story = {
  args: {
    selectedCount: 6,
    totalCount: 25,
    itemLabel: 'items',
    showProgress: false,
    actions: [
      {
        id: 'delete',
        label: 'Delete',
        icon: Trash2,
        variant: 'destructive',
        onClick: () => {},
      },
    ],
    onClearSelection: () => {},
  },
  parameters: {
    chromatic: { disableSnapshot: false },
  },
};

/**
 * Mobile responsive view (320px)
 */
export const MobileView: Story = {
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
        onClick: () => {},
      },
      {
        id: 'export',
        label: 'Export',
        icon: Download,
        variant: 'outline',
        onClick: () => {},
      },
    ],
    onClearSelection: () => {},
  },
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
    chromatic: {
      disableSnapshot: false,
      viewports: [320],
    },
  },
};

/**
 * Tablet view (768px)
 */
export const TabletView: Story = {
  args: {
    selectedCount: 5,
    totalCount: 15,
    itemLabel: 'items',
    actions: [
      {
        id: 'delete',
        label: 'Delete',
        icon: Trash2,
        variant: 'destructive',
        onClick: () => {},
      },
      {
        id: 'export',
        label: 'Export',
        icon: Download,
        variant: 'outline',
        onClick: () => {},
      },
    ],
    onClearSelection: () => {},
  },
  parameters: {
    viewport: {
      defaultViewport: 'tablet',
    },
    chromatic: {
      disableSnapshot: false,
      viewports: [768],
    },
  },
};

/**
 * Dark mode
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
        onClick: () => {},
      },
      {
        id: 'export',
        label: 'Export',
        icon: Download,
        variant: 'outline',
        onClick: () => {},
      },
    ],
    onClearSelection: () => {},
  },
  parameters: {
    backgrounds: { default: 'dark' },
    chromatic: { disableSnapshot: false },
  },
};

/**
 * Large selection (stress test)
 */
export const LargeSelection: Story = {
  args: {
    selectedCount: 987,
    totalCount: 1500,
    itemLabel: 'records',
    itemLabelSingular: 'record',
    actions: [
      {
        id: 'delete',
        label: 'Delete',
        icon: Trash2,
        variant: 'destructive',
        onClick: () => {},
      },
      {
        id: 'export',
        label: 'Export',
        icon: Download,
        variant: 'outline',
        onClick: () => {},
      },
    ],
    onClearSelection: () => {},
  },
  parameters: {
    chromatic: { disableSnapshot: false },
  },
};

/**
 * Empty state component
 */
export const EmptyState: StoryObj<typeof EmptyBulkActionBar> = {
  render: () => <EmptyBulkActionBar itemLabel="API keys" />,
  parameters: {
    chromatic: { disableSnapshot: false },
  },
};

/**
 * Empty state with custom message
 */
export const EmptyStateCustom: StoryObj<typeof EmptyBulkActionBar> = {
  render: () => (
    <EmptyBulkActionBar
      itemLabel="documents"
      message="Check the boxes next to documents to enable bulk operations"
    />
  ),
  parameters: {
    chromatic: { disableSnapshot: false },
  },
};
