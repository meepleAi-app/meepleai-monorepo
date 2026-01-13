/**
 * GameStatusBadge - Issue #2372
 *
 * Displays the publication status of a shared game with appropriate styling.
 * Statuses: Draft (0), Published (1), Archived (2)
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
    status: 0,
    size: 'default',
  },
};

/**
 * Published status - Green styling for live content
 */
export const Published: Story = {
  args: {
    status: 1,
    size: 'default',
  },
};

/**
 * Archived status - Gray styling for inactive content
 */
export const Archived: Story = {
  args: {
    status: 2,
    size: 'default',
  },
};

/**
 * Unknown status - Fallback styling
 */
export const Unknown: Story = {
  args: {
    status: 99,
    size: 'default',
  },
};

/**
 * Small size - Draft
 */
export const SmallDraft: Story = {
  args: {
    status: 0,
    size: 'sm',
  },
};

/**
 * Small size - Published
 */
export const SmallPublished: Story = {
  args: {
    status: 1,
    size: 'sm',
  },
};

/**
 * Small size - Archived
 */
export const SmallArchived: Story = {
  args: {
    status: 2,
    size: 'sm',
  },
};

/**
 * All status badges side by side
 */
export const AllStatuses: Story = {
  render: () => (
    <div className="flex items-center gap-4">
      <GameStatusBadge status={0} />
      <GameStatusBadge status={1} />
      <GameStatusBadge status={2} />
    </div>
  ),
};

/**
 * All status badges - small size
 */
export const AllStatusesSmall: Story = {
  render: () => (
    <div className="flex items-center gap-4">
      <GameStatusBadge status={0} size="sm" />
      <GameStatusBadge status={1} size="sm" />
      <GameStatusBadge status={2} size="sm" />
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
        <GameStatusBadge status={0} />
        <GameStatusBadge status={1} />
        <GameStatusBadge status={2} />
      </div>
      <div className="flex items-center gap-4">
        <span className="w-20 text-sm text-muted-foreground">Small:</span>
        <GameStatusBadge status={0} size="sm" />
        <GameStatusBadge status={1} size="sm" />
        <GameStatusBadge status={2} size="sm" />
      </div>
    </div>
  ),
};
