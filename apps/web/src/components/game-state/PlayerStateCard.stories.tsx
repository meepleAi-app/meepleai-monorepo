/**
 * PlayerStateCard Storybook Stories
 * Issue #2406: Game State Editor UI
 *
 * Visual regression tests for Chromatic.
 */

import { fn } from 'storybook/test';

import { PlayerStateCard } from './PlayerStateCard';

import type { Meta, StoryObj } from '@storybook/react';

const meta = {
  title: 'Components/GameState/PlayerStateCard',
  component: PlayerStateCard,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Card component displaying per-player game state with color coding, score tracking, and resource management.',
      },
    },
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
  args: {
    onScoreChange: fn(),
    onResourceChange: fn(),
  },
  decorators: [
    Story => (
      <div className="max-w-md">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof PlayerStateCard>;

export default meta;
type Story = StoryObj<typeof meta>;

// Basic States
export const Default: Story = {
  args: {
    player: {
      playerName: 'Alice',
      playerOrder: 1,
      color: '#FF6B6B',
      score: 15,
      resources: { wood: 5, stone: 3, gold: 2 },
    },
    editable: true,
  },
};

export const CurrentPlayer: Story = {
  args: {
    player: {
      playerName: 'Bob',
      playerOrder: 2,
      color: '#4ECDC4',
      score: 22,
      resources: { wood: 8, stone: 1, gold: 0 },
    },
    isCurrentPlayer: true,
    editable: true,
  },
};

export const ReadOnly: Story = {
  args: {
    player: {
      playerName: 'Charlie',
      playerOrder: 3,
      color: '#95E1D3',
      score: 10,
      resources: { wood: 2, stone: 6, gold: 1 },
    },
    editable: false,
  },
};

export const NoResources: Story = {
  args: {
    player: {
      playerName: 'Diana',
      playerOrder: 4,
      color: '#F38181',
      score: 5,
    },
    editable: true,
  },
};

export const ManyResources: Story = {
  args: {
    player: {
      playerName: 'Eve',
      playerOrder: 1,
      color: '#AA96DA',
      score: 30,
      resources: {
        wood: 10,
        stone: 8,
        gold: 5,
        silver: 12,
        food: 7,
        ore: 3,
      },
    },
    editable: true,
  },
};

export const NoColor: Story = {
  args: {
    player: {
      playerName: 'Frank',
      playerOrder: 5,
      score: 0,
    },
    editable: true,
  },
  parameters: {
    docs: {
      description: {
        story: 'Player without assigned color uses default gray',
      },
    },
  },
};

// Dark Mode
export const DarkMode: Story = {
  args: {
    player: {
      playerName: 'Grace',
      playerOrder: 1,
      color: '#FFB6B9',
      score: 18,
      resources: { wood: 4, stone: 4 },
    },
    isCurrentPlayer: true,
    editable: true,
  },
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => (
      <div className="dark p-6 bg-slate-900">
        <div className="max-w-md">
          <Story />
        </div>
      </div>
    ),
  ],
};
