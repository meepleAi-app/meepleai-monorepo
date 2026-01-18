/**
 * GameStateViewer Storybook Stories
 * Issue #2406: Game State Editor UI
 *
 * Visual regression tests for Chromatic.
 */

import type { GameState } from '@/types/game-state';

import { GameStateViewer } from './GameStateViewer';

import type { Meta, StoryObj } from '@storybook/react';

const mockState: GameState = {
  sessionId: 'session-123',
  gameId: 'game-456',
  templateId: 'template-789',
  version: '1.0',
  phase: 'Main Game',
  currentPlayerIndex: 0,
  roundNumber: 3,
  players: [
    {
      playerName: 'Alice',
      playerOrder: 1,
      color: '#FF6B6B',
      score: 25,
      resources: { wood: 8, stone: 5, gold: 3 },
    },
    {
      playerName: 'Bob',
      playerOrder: 2,
      color: '#4ECDC4',
      score: 18,
      resources: { wood: 5, stone: 8, gold: 2 },
    },
    {
      playerName: 'Charlie',
      playerOrder: 3,
      color: '#95E1D3',
      score: 22,
      resources: { wood: 6, stone: 6, gold: 4 },
    },
  ],
  globalResources: { bank: 87, victory_points: 35 },
  metadata: { difficulty: 'Medium', variant: 'Standard' },
};

const meta = {
  title: 'Components/GameState/GameStateViewer',
  component: GameStateViewer,
  parameters: {
    layout: 'padded',
    docs: {
      description: {
        component:
          'Read-only display of complete game state including players, resources, phase, and metadata.',
      },
    },
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
} satisfies Meta<typeof GameStateViewer>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  args: {
    state: mockState,
    currentPlayerIndex: 0,
  },
};

export const MinimalState: Story = {
  args: {
    state: {
      sessionId: 'session-123',
      gameId: 'game-456',
      templateId: 'template-789',
      version: '1.0',
      players: [
        {
          playerName: 'Alice',
          playerOrder: 1,
          score: 10,
        },
      ],
    },
  },
};

export const ManyPlayers: Story = {
  args: {
    state: {
      ...mockState,
      players: [
        ...mockState.players,
        {
          playerName: 'Diana',
          playerOrder: 4,
          color: '#F38181',
          score: 12,
          resources: { wood: 2, stone: 3, gold: 1 },
        },
        {
          playerName: 'Eve',
          playerOrder: 5,
          color: '#AA96DA',
          score: 16,
          resources: { wood: 4, stone: 2, gold: 5 },
        },
      ],
    },
  },
};

export const NoGlobalResources: Story = {
  args: {
    state: {
      ...mockState,
      globalResources: undefined,
    },
  },
};

export const DarkMode: Story = {
  args: {
    state: mockState,
    currentPlayerIndex: 1,
  },
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => (
      <div className="dark p-6 bg-slate-900">
        <Story />
      </div>
    ),
  ],
};

/**
 * Mobile Viewport - Issue #2406 Mobile-friendly compact view
 */
export const Mobile: Story = {
  args: {
    state: mockState,
    currentPlayerIndex: 0,
  },
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
    chromatic: {
      viewports: [375],
    },
  },
};
