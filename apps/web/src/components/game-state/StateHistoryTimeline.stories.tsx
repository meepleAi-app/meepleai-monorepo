/**
 * StateHistoryTimeline Storybook Stories
 * Issue #2406: Game State Editor UI
 *
 * Visual regression tests for Chromatic.
 */

import type { GameStateSnapshot } from '@/types/game-state';

import { StateHistoryTimeline } from './StateHistoryTimeline';

import type { Meta, StoryObj } from '@storybook/react';

const mockSnapshots: GameStateSnapshot[] = [
  {
    id: 'snapshot-3',
    timestamp: new Date().toISOString(),
    userId: 'user-123',
    action: 'Alice scored 5 points',
    state: {
      sessionId: 'session-1',
      gameId: 'game-1',
      templateId: 'template-1',
      version: '1.0',
      roundNumber: 3,
      phase: 'Main Game',
      players: [
        { playerName: 'Alice', playerOrder: 1, score: 25 },
        { playerName: 'Bob', playerOrder: 2, score: 18 },
      ],
    },
  },
  {
    id: 'snapshot-2',
    timestamp: new Date(Date.now() - 5 * 60 * 1000).toISOString(),
    userId: 'user-123',
    action: 'Bob collected 3 wood',
    state: {
      sessionId: 'session-1',
      gameId: 'game-1',
      templateId: 'template-1',
      version: '1.0',
      roundNumber: 2,
      phase: 'Main Game',
      players: [
        { playerName: 'Alice', playerOrder: 1, score: 20 },
        { playerName: 'Bob', playerOrder: 2, score: 18 },
      ],
    },
  },
  {
    id: 'snapshot-1',
    timestamp: new Date(Date.now() - 15 * 60 * 1000).toISOString(),
    userId: 'user-123',
    action: 'Game started',
    state: {
      sessionId: 'session-1',
      gameId: 'game-1',
      templateId: 'template-1',
      version: '1.0',
      roundNumber: 1,
      phase: 'Setup',
      players: [
        { playerName: 'Alice', playerOrder: 1, score: 0 },
        { playerName: 'Bob', playerOrder: 2, score: 0 },
      ],
    },
  },
];

// Mock Zustand store
const mockStore = {
  snapshots: mockSnapshots,
  loadSnapshot: (id: string) => console.log('Load snapshot:', id),
};

const meta = {
  title: 'Components/GameState/StateHistoryTimeline',
  component: StateHistoryTimeline,
  parameters: {
    layout: 'padded',
    docs: {
      description: {
        component:
          'Timeline displaying state history snapshots with restore functionality and visual timeline.',
      },
    },
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
  decorators: [
    Story => {
      // Mock Zustand hook
      const useGameStateStoreMock = () => mockStore;
      (global as any).useGameStateStore = useGameStateStoreMock;
      return (
        <div className="max-w-2xl">
          <Story />
        </div>
      );
    },
  ],
} satisfies Meta<typeof StateHistoryTimeline>;

export default meta;
type Story = StoryObj<typeof meta>;

export const WithSnapshots: Story = {
  args: {
    sessionId: 'session-123',
  },
};

export const EmptyHistory: Story = {
  args: {
    sessionId: 'session-123',
  },
  decorators: [
    Story => {
      const emptyStore = { ...mockStore, snapshots: [] };
      (global as any).useGameStateStore = () => emptyStore;
      return (
        <div className="max-w-2xl">
          <Story />
        </div>
      );
    },
  ],
};

export const DarkMode: Story = {
  args: {
    sessionId: 'session-123',
  },
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => (
      <div className="dark p-6 bg-slate-900">
        <div className="max-w-2xl">
          <Story />
        </div>
      </div>
    ),
  ],
};

/**
 * Mobile Viewport - Issue #2406 Mobile-friendly compact view
 */
export const Mobile: Story = {
  args: {
    sessionId: 'session-mobile',
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
