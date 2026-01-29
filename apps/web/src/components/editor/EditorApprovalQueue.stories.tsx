/**
 * EditorApprovalQueue Storybook Stories (Issue #2896)
 */

import type { Meta, StoryObj } from '@storybook/react';
import { fn } from '@storybook/test';
import { EditorApprovalQueue } from './EditorApprovalQueue';
import type { SharedGame } from '@/lib/api/schemas/shared-games.schemas';

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
    imageUrl: 'https://cf.geekdo-images.com/W3Bsga_uLP9kO91gZ7H8yw__imagepage/img/M_3Vg1j2HlNgkv7PL2xl0-9_Sx0=/fit-in/900x600/filters:no_upscale():strip_icc()/pic2419375.jpg',
    thumbnailUrl: 'https://cf.geekdo-images.com/W3Bsga_uLP9kO91gZ7H8yw__thumb/img/IWYLbX7cDaBMnGZBQsS_mbXgG5k=/fit-in/200x150/filters:strip_icc()/pic2419375.jpg',
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
    imageUrl: 'https://cf.geekdo-images.com/Z3upN53-fsVPUDimN9SpOA__imagepage/img/sT0kjr-Klona2rygvD8kURJgqdU=/fit-in/900x600/filters:no_upscale():strip_icc()/pic2337577.jpg',
    thumbnailUrl: 'https://cf.geekdo-images.com/Z3upN53-fsVPUDimN9SpOA__thumb/img/OEbb8W7T97EQV8WDxrOmyjQw7Tw=/fit-in/200x150/filters:strip_icc()/pic2337577.jpg',
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
    imageUrl: 'https://cf.geekdo-images.com/j6iQpZ4XkemZP07HNCODBA__imagepage/img/RuuCDC1L3eR6plJqE_Pl1rDCiQg=/fit-in/900x600/filters:no_upscale():strip_icc()/pic394356.jpg',
    thumbnailUrl: 'https://cf.geekdo-images.com/j6iQpZ4XkemZP07HNCODBA__thumb/img/Dm0UO6fkjSiEMB8HMT_Ef8kTsv8=/fit-in/200x150/filters:strip_icc()/pic394356.jpg',
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
    imageUrl: 'https://cf.geekdo-images.com/yLZJCVLlIx4c7eJEWUNJ7w__imagepage/img/uIjeoKgHMcRtzRSR4MoUYl3nXxs=/fit-in/900x600/filters:no_upscale():strip_icc()/pic4458123.jpg',
    thumbnailUrl: 'https://cf.geekdo-images.com/yLZJCVLlIx4c7eJEWUNJ7w__thumb/img/3bp6Mn4QQ3cyYfHLBm7trA_-9xs=/fit-in/200x150/filters:strip_icc()/pic4458123.jpg',
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
    imageUrl: 'https://cf.geekdo-images.com/wg9oOLcsKvDesSUdZQ4rxw__imagepage/img/FS1RE8Ue6nk1pNbPI3l-OSapQGc=/fit-in/900x600/filters:no_upscale():strip_icc()/pic3536616.jpg',
    thumbnailUrl: 'https://cf.geekdo-images.com/wg9oOLcsKvDesSUdZQ4rxw__thumb/img/yMK-rRr9IS3Vv0T0rQwJwGLAETQ=/fit-in/200x150/filters:strip_icc()/pic3536616.jpg',
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
    games: mockGames.map((g) => ({
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
    onReview: (gameId) => console.log('Review:', gameId),
    onApprove: (gameId) => console.log('Approve:', gameId),
    onReject: (gameId) => console.log('Reject:', gameId),
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
