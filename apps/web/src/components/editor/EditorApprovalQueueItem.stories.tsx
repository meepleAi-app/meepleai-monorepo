/**
 * EditorApprovalQueueItem Storybook Stories (Issue #2895)
 */

import { fn } from 'storybook/test';

import type { SharedGame } from '@/lib/api/schemas/shared-games.schemas';

import { EditorApprovalQueueItem } from './EditorApprovalQueueItem';

import type { Meta, StoryObj } from '@storybook/react';

const meta = {
  title: 'Components/Editor/EditorApprovalQueueItem',
  component: EditorApprovalQueueItem,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Approval queue item component for Editor Dashboard. Displays game information with priority indicators and action buttons. Priority is calculated client-side based on submission age.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    game: {
      description: 'Shared game pending approval',
      control: 'object',
    },
    onReview: {
      description: 'Callback when Review button is clicked',
      action: 'review',
    },
    onApprove: {
      description: 'Callback when Approve button is clicked',
      action: 'approve',
    },
    onReject: {
      description: 'Callback when Reject button is clicked',
      action: 'reject',
    },
  },
  args: {
    onReview: fn(),
    onApprove: fn(),
    onReject: fn(),
  },
  decorators: [
    (Story) => (
      <div className="w-[600px]">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof EditorApprovalQueueItem>;

export default meta;
type Story = StoryObj<typeof meta>;

// Base game template
const baseGame: SharedGame = {
  id: '123e4567-e89b-12d3-a456-426614174000',
  bggId: 12345,
  title: 'Catan',
  yearPublished: 1995,
  description:
    'In Catan, players try to be the dominant force on the island of Catan by building settlements, cities, and roads.',
  minPlayers: 3,
  maxPlayers: 4,
  playingTimeMinutes: 120,
  minAge: 10,
  complexityRating: 2.3,
  averageRating: 7.2,
  imageUrl: 'https://cf.geekdo-images.com/W3Bsga_uLP9kO91gZ7H8yw__imagepage/img/M_3Vg1j2HlNgkv7PL2xl0-9_Sx0=/fit-in/900x600/filters:no_upscale():strip_icc()/pic2419375.jpg',
  thumbnailUrl: 'https://cf.geekdo-images.com/W3Bsga_uLP9kO91gZ7H8yw__thumb/img/IWYLbX7cDaBMnGZBQsS_mbXgG5k=/fit-in/200x150/filters:strip_icc()/pic2419375.jpg',
  status: 'PendingApproval',
  createdAt: '2026-01-28T10:00:00Z', // Will be adjusted per story
  modifiedAt: null,
};

/**
 * Low Priority (< 3 days old)
 * No priority badge shown, normal visual treatment
 */
export const LowPriority: Story = {
  args: {
    game: {
      ...baseGame,
      createdAt: new Date(Date.now() - 1 * 24 * 60 * 60 * 1000).toISOString(), // 1 day ago
    },
  },
  parameters: {
    docs: {
      description: {
        story:
          'Game submitted within the last 3 days. No priority badge is displayed, indicating normal processing time.',
      },
    },
  },
};

/**
 * Medium Priority (3-7 days old)
 * Yellow warning badge indicating needs attention soon
 */
export const MediumPriority: Story = {
  args: {
    game: {
      ...baseGame,
      createdAt: new Date(Date.now() - 5 * 24 * 60 * 60 * 1000).toISOString(), // 5 days ago
    },
  },
  parameters: {
    docs: {
      description: {
        story:
          'Game submitted 3-7 days ago. Yellow "Media priorità" badge indicates it needs attention soon.',
      },
    },
  },
};

/**
 * High Priority (> 7 days old)
 * Red destructive badge indicating urgent action needed
 */
export const HighPriority: Story = {
  args: {
    game: {
      ...baseGame,
      createdAt: new Date(Date.now() - 10 * 24 * 60 * 60 * 1000).toISOString(), // 10 days ago
    },
  },
  parameters: {
    docs: {
      description: {
        story:
          'Game submitted over 7 days ago. Red "Alta priorità" badge indicates urgent action is needed.',
      },
    },
  },
};

/**
 * Very Old Submission (30+ days)
 * High priority with extended waiting time
 */
export const VeryOldSubmission: Story = {
  args: {
    game: {
      ...baseGame,
      title: 'Twilight Imperium: Fourth Edition',
      createdAt: new Date(Date.now() - 32 * 24 * 60 * 60 * 1000).toISOString(), // 32 days ago
    },
  },
  parameters: {
    docs: {
      description: {
        story: 'Game submitted over a month ago. Displays high priority indicator for urgent review.',
      },
    },
  },
};

/**
 * No Cover Image
 * Displays placeholder when thumbnail URL is missing
 */
export const NoCoverImage: Story = {
  args: {
    game: {
      ...baseGame,
      thumbnailUrl: '',
      imageUrl: '',
      createdAt: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000).toISOString(), // 2 days ago
    },
  },
  parameters: {
    docs: {
      description: {
        story: 'Queue item without cover image. Shows "No cover" placeholder instead.',
      },
    },
  },
};

/**
 * Long Title
 * Tests line clamping for very long game titles
 */
export const LongTitle: Story = {
  args: {
    game: {
      ...baseGame,
      title:
        'This is an extremely long game title that should be properly clamped to two lines to prevent layout issues and maintain consistent card heights in the approval queue interface',
      createdAt: new Date(Date.now() - 4 * 24 * 60 * 60 * 1000).toISOString(), // 4 days ago
    },
  },
  parameters: {
    docs: {
      description: {
        story:
          'Queue item with very long title. Title is clamped to 2 lines with ellipsis to maintain consistent layout.',
      },
    },
  },
};

/**
 * Multiple Items in List
 * Shows how items look stacked in a list view
 */
export const InList: Story = {
  render: (args) => (
    <div className="flex flex-col gap-3 w-[600px]">
      <EditorApprovalQueueItem
        {...args}
        game={{
          ...baseGame,
          id: '1',
          title: 'Catan',
          createdAt: new Date(Date.now() - 10 * 24 * 60 * 60 * 1000).toISOString(), // High
        }}
      />
      <EditorApprovalQueueItem
        {...args}
        game={{
          ...baseGame,
          id: '2',
          title: 'Ticket to Ride',
          createdAt: new Date(Date.now() - 5 * 24 * 60 * 60 * 1000).toISOString(), // Medium
        }}
      />
      <EditorApprovalQueueItem
        {...args}
        game={{
          ...baseGame,
          id: '3',
          title: 'Pandemic',
          createdAt: new Date(Date.now() - 1 * 24 * 60 * 60 * 1000).toISOString(), // Low
        }}
      />
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story:
          'Multiple queue items displayed in a list. Shows priority distribution: high (red), medium (yellow), low (none).',
      },
    },
  },
};

/**
 * Interactive Example
 * All actions enabled with console logging
 */
export const Interactive: Story = {
  args: {
    game: {
      ...baseGame,
      createdAt: new Date(Date.now() - 5 * 24 * 60 * 60 * 1000).toISOString(), // Medium priority
    },
    onReview: (gameId) => console.log('Review clicked:', gameId),
    onApprove: (gameId) => console.log('Approve clicked:', gameId),
    onReject: (gameId) => console.log('Reject clicked:', gameId),
  },
  parameters: {
    docs: {
      description: {
        story: 'Interactive example with console logging. Try clicking the action buttons.',
      },
    },
  },
};
