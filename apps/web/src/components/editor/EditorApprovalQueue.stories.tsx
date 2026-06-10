/**
 * EditorApprovalQueue Storybook Stories (Issue #2896)
 */

import { fn } from 'storybook/test';

import type { SharedGame } from '@/lib/api/schemas/shared-games.schemas';

import { EditorApprovalQueue } from './EditorApprovalQueue';

import type { Meta, StoryObj } from '@storybook/react';

const meta = {
  title: 'Components/Editor/EditorApprovalQueue',
  component: EditorApprovalQueue,
  parameters: {
    layout: 'padded',
    docs: {
      description: {
        component:
          'Approval queue container with bulk selection and operations. Integrates EditorApprovalQueueItem with BulkActionBar for efficient bulk approval/rejection workflow.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    games: {
      description: 'Array of shared games pending approval',
      control: 'object',
    },
    onReview: {
      description: 'Callback when Review button is clicked',
      action: 'review',
    },
    onApprove: {
      description: 'Callback when individual Approve is clicked',
      action: 'approve',
    },
    onReject: {
      description: 'Callback when individual Reject is clicked',
      action: 'reject',
    },
    onBulkComplete: {
      description: 'Callback after bulk operations complete',
      action: 'bulk-complete',
    },
  },
  args: {
    onReview: fn(),
    onApprove: fn(),
    onReject: fn(),
    onBulkComplete: fn(),
  },
} satisfies Meta<typeof EditorApprovalQueue>;

export default meta;
type Story = StoryObj<typeof meta>;

// Mock games with varying ages for priority testing
const mockGames: SharedGame[] = [
  {
    id: '1',
    bggId: 13,
    title: 'Catan',
    yearPublished: 1995,
    description: 'Classic trading and building game',
    minPlayers: 3,
    maxPlayers: 4,
    playingTimeMinutes: 120,
    minAge: 10,
    complexityRating: 2.3,
    averageRating: 7.2,
    imageUrl:
      'https://placehold.co/600x400/4f46e5/ffffff?text=cover-62aa53):strip_icc()/pic2419375.jpg',
    thumbnailUrl: 'https://placehold.co/600x400/4f46e5/ffffff?text=cover-44a2e4)/pic2419375.jpg',
    status: 'PendingApproval',
    createdAt: new Date(Date.now() - 10 * 24 * 60 * 60 * 1000).toISOString(), // 10 days ago (HIGH)
    modifiedAt: null,
  },
  {
    id: '2',
    bggId: 822,
    title: 'Carcassonne',
    yearPublished: 2000,
    description: 'Tile-placement game',
    minPlayers: 2,
    maxPlayers: 5,
    playingTimeMinutes: 45,
    minAge: 7,
    complexityRating: 1.9,
    averageRating: 7.4,
    imageUrl:
      'https://placehold.co/600x400/4f46e5/ffffff?text=cover-23d024):strip_icc()/pic2337577.jpg',
    thumbnailUrl: 'https://placehold.co/600x400/4f46e5/ffffff?text=cover-52eb57)/pic2337577.jpg',
    status: 'PendingApproval',
    createdAt: new Date(Date.now() - 5 * 24 * 60 * 60 * 1000).toISOString(), // 5 days ago (MEDIUM)
    modifiedAt: null,
  },
  {
    id: '3',
    bggId: 36218,
    title: 'Dominion',
    yearPublished: 2008,
    description: 'Deck-building card game',
    minPlayers: 2,
    maxPlayers: 4,
    playingTimeMinutes: 30,
    minAge: 13,
    complexityRating: 2.3,
    averageRating: 7.6,
    imageUrl:
      'https://placehold.co/600x400/4f46e5/ffffff?text=cover-4b547f):strip_icc()/pic394356.jpg',
    thumbnailUrl: 'https://placehold.co/600x400/4f46e5/ffffff?text=cover-8aac0b)/pic394356.jpg',
    status: 'PendingApproval',
    createdAt: new Date(Date.now() - 1 * 24 * 60 * 60 * 1000).toISOString(), // 1 day ago (LOW)
    modifiedAt: null,
  },
  {
    id: '4',
    bggId: 266192,
    title: 'Wingspan',
    yearPublished: 2019,
    description: 'Bird-collection engine-building game',
    minPlayers: 1,
    maxPlayers: 5,
    playingTimeMinutes: 70,
    minAge: 10,
    complexityRating: 2.4,
    averageRating: 8.0,
    imageUrl:
      'https://placehold.co/600x400/4f46e5/ffffff?text=cover-765d11):strip_icc()/pic4458123.jpg',
    thumbnailUrl: 'https://placehold.co/600x400/4f46e5/ffffff?text=cover-53cee7)/pic4458123.jpg',
    status: 'PendingApproval',
    createdAt: new Date(Date.now() - 4 * 24 * 60 * 60 * 1000).toISOString(), // 4 days ago (MEDIUM)
    modifiedAt: null,
  },
  {
    id: '5',
    bggId: 167791,
    title: 'Terraforming Mars',
    yearPublished: 2016,
    description: 'Compete to terraform Mars',
    minPlayers: 1,
    maxPlayers: 5,
    playingTimeMinutes: 120,
    minAge: 12,
    complexityRating: 3.2,
    averageRating: 8.4,
    imageUrl:
      'https://placehold.co/600x400/4f46e5/ffffff?text=cover-630fbf):strip_icc()/pic3536616.jpg',
    thumbnailUrl: 'https://placehold.co/600x400/4f46e5/ffffff?text=cover-25e365)/pic3536616.jpg',
    status: 'PendingApproval',
    createdAt: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000).toISOString(), // 2 days ago (LOW)
    modifiedAt: null,
  },
];

/**
 * Default: Queue with 5 Items
 * Shows mixed priority levels and action bar on selection
 */
export const Default: Story = {
  args: {
    games: mockGames,
  },
  parameters: {
    docs: {
      description: {
        story:
          'Approval queue with 5 items showing different priority levels. Select items to see the floating action bar appear at the bottom.',
      },
    },
  },
};

/**
 * Empty Queue
 * Shows empty state when no items pending
 */
export const Empty: Story = {
  args: {
    games: [],
  },
  parameters: {
    docs: {
      description: {
        story: 'Empty approval queue with no items pending approval.',
      },
    },
  },
};

/**
 * Single Item
 * Queue with only one item
 */
export const SingleItem: Story = {
  args: {
    games: [mockGames[0]],
  },
  parameters: {
    docs: {
      description: {
        story: 'Queue with single item. Selection shows "1 gioco selezionato" in singular form.',
      },
    },
  },
};

/**
 * Many Items (10+)
 * Larger queue demonstrating scrolling
 */
export const ManyItems: Story = {
  args: {
    games: [
      ...mockGames,
      ...mockGames.map((g, i) => ({
        ...g,
        id: `${g.id}-copy-${i}`,
        title: `${g.title} (Copy ${i + 1})`,
      })),
    ],
  },
  parameters: {
    docs: {
      description: {
        story: 'Queue with 10+ items. Tests scrolling behavior and bulk action bar positioning.',
      },
    },
  },
};

/**
 * All High Priority
 * Queue where all items are urgent (>7 days old)
 */
export const AllHighPriority: Story = {
  args: {
    games: mockGames.map(g => ({
      ...g,
      createdAt: new Date(Date.now() - 15 * 24 * 60 * 60 * 1000).toISOString(), // 15 days ago
    })),
  },
  parameters: {
    docs: {
      description: {
        story:
          'All items show high priority (red badges). Demonstrates urgent approval queue requiring immediate attention.',
      },
    },
  },
};

/**
 * Interactive Demo
 * Full functionality with console logging
 */
export const Interactive: Story = {
  args: {
    games: mockGames,
    onReview: gameId => console.log('Review:', gameId),
    onApprove: gameId => console.log('Approve:', gameId),
    onReject: gameId => console.log('Reject:', gameId),
    onBulkComplete: () => console.log('Bulk operation completed'),
  },
  parameters: {
    docs: {
      description: {
        story:
          'Interactive demo with console logging. Try: 1) Select items with checkboxes, 2) Click Bulk Approve/Reject in floating bar, 3) Confirm in dialog.',
      },
    },
  },
};
