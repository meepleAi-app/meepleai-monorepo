/**
 * GameStatusBadge - Issue #2372
 *
 * Displays the publication status of a shared game with appropriate styling.
 * Statuses: Draft, PendingApproval, Published, Archived (string enum)
 */

import { GameStatusBadge } from './GameStatusBadge';

import type { Meta, StoryObj } from '@storybook/react';

const meta = {
  title: 'Admin/SharedGames/GameStatusBadge',
  component: GameStatusBadge,
  parameters: {
    layout: 'centered',
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
} satisfies Meta<typeof GameStatusBadge>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Draft status - Yellow styling for unpublished content
 */
export const Draft: Story = {
  args: {
    status: 'Draft',
    size: 'default',
  },
};

/**
 * Pending Approval status - Blue styling for content awaiting review
 */
export const PendingApproval: Story = {
  args: {
    status: 'PendingApproval',
    size: 'default',
  },
};

/**
 * Published status - Green styling for live content
 */
export const Published: Story = {
  args: {
    status: 'Published',
    size: 'default',
  },
};

/**
 * Archived status - Gray styling for inactive content
 */
export const Archived: Story = {
  args: {
    status: 'Archived',
    size: 'default',
  },
};

/**
 * Small size - Draft
 */
export const SmallDraft: Story = {
  args: {
    status: 'Draft',
    size: 'sm',
  },
};

/**
 * Small size - Pending Approval
 */
export const SmallPendingApproval: Story = {
  args: {
    status: 'PendingApproval',
    size: 'sm',
  },
};

/**
 * Small size - Published
 */
export const SmallPublished: Story = {
  args: {
    status: 'Published',
    size: 'sm',
  },
};

/**
 * Small size - Archived
 */
export const SmallArchived: Story = {
  args: {
    status: 'Archived',
    size: 'sm',
  },
};

/**
 * All status badges side by side
 */
export const AllStatuses: Story = {
  render: () => (
    <div className="flex items-center gap-4">
      <GameStatusBadge status="Draft" />
      <GameStatusBadge status="PendingApproval" />
      <GameStatusBadge status="Published" />
      <GameStatusBadge status="Archived" />
    </div>
  ),
};

/**
 * All status badges - small size
 */
export const AllStatusesSmall: Story = {
  render: () => (
    <div className="flex items-center gap-4">
      <GameStatusBadge status="Draft" size="sm" />
      <GameStatusBadge status="PendingApproval" size="sm" />
      <GameStatusBadge status="Published" size="sm" />
      <GameStatusBadge status="Archived" size="sm" />
    </div>
  ),
};

/**
 * Size comparison
 */
export const SizeComparison: Story = {
  render: () => (
    <div className="space-y-4">
      <div className="flex items-center gap-4">
        <span className="w-20 text-sm text-muted-foreground">Default:</span>
        <GameStatusBadge status="Draft" />
        <GameStatusBadge status="PendingApproval" />
        <GameStatusBadge status="Published" />
        <GameStatusBadge status="Archived" />
      </div>
      <div className="flex items-center gap-4">
        <span className="w-20 text-sm text-muted-foreground">Small:</span>
        <GameStatusBadge status="Draft" size="sm" />
        <GameStatusBadge status="PendingApproval" size="sm" />
        <GameStatusBadge status="Published" size="sm" />
        <GameStatusBadge status="Archived" size="sm" />
      </div>
    </div>
  ),
};
