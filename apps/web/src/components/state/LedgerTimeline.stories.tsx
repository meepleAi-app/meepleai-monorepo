/**
 * LedgerTimeline Storybook Stories
 * Issue #2422: Ledger Mode History Timeline
 *
 * Visual regression tests for Chromatic (3 scenarios as per DoD).
 */

import type { GameStateSnapshot } from '@/types/game-state';

import { LedgerTimeline } from './LedgerTimeline';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * Generate mock snapshots for testing
 */
function generateMockSnapshots(count: number): GameStateSnapshot[] {
  const actions = [
    'Game started',
    'Alice collected 3 wood',
    'Bob scored 5 points',
    'Charlie built a settlement',
    'Alice traded resources',
    'Round advanced to 2',
    'Bob played development card',
    'Charlie expanded territory',
    'Alice gained bonus points',
    'Phase changed to Main Game',
  ];

  return Array.from({ length: count }, (_, i) => ({
    id: `snapshot-${i + 1}`,
    timestamp: new Date(Date.now() - (count - i) * 60 * 1000).toISOString(),
    userId: 'user-123',
    action: actions[i % actions.length],
    state: {
      sessionId: 'session-1',
      gameId: 'game-1',
      templateId: 'template-1',
      version: '1.0',
      roundNumber: Math.floor(i / 3) + 1,
      phase: i < 5 ? 'Setup' : 'Main Game',
      players: [
        {
          playerName: 'Alice',
          playerOrder: 1,
          score: 10 + i * 2,
          resources: { wood: 3 + i, brick: 2, wheat: 1 },
        },
        {
          playerName: 'Bob',
          playerOrder: 2,
          score: 8 + i,
          resources: { wood: 2, brick: 3 + i, wheat: 2 },
        },
        {
          playerName: 'Charlie',
          playerOrder: 3,
          score: 12 + i * 3,
          resources: { wood: 1, brick: 1, wheat: 3 + i },
        },
      ],
    },
  }));
}

// Mock Zustand store
const createMockStore = (snapshots: GameStateSnapshot[]) => ({
  snapshots,
  loadSnapshot: (id: string) => console.log('Load snapshot:', id),
});

const meta = {
  title: 'Components/State/LedgerTimeline',
  component: LedgerTimeline,
  parameters: {
    layout: 'padded',
    docs: {
      description: {
        component:
          'Timeline displaying ledger history with diff visualization, rollback confirmation, and export functionality (JSON/CSV).',
      },
    },
    chromatic: {
      viewports: [375, 768, 1024, 1920],
    },
  },
  tags: ['autodocs'],
  decorators: [
    (Story, context) => {
      const mockStore = createMockStore(context.args.mockSnapshots || []);
      (global as any).useGameStateStore = () => mockStore;
      return (
        <div className="max-w-4xl mx-auto">
          <Story />
        </div>
      );
    },
  ],
} satisfies Meta<typeof LedgerTimeline>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Scenario 1: Empty State
 * DoD: "Timeline shows action history" (empty case)
 */
export const EmptyState: Story = {
  args: {
    sessionId: 'session-empty',
    mockSnapshots: [],
  },
  parameters: {
    docs: {
      description: {
        story: 'Empty timeline with no actions recorded yet.',
      },
    },
  },
};

/**
 * Scenario 2: With 5 Actions
 * DoD: "Diff highlights state changes" + "Rollback restores previous state"
 */
export const With5Actions: Story = {
  args: {
    sessionId: 'session-5',
    mockSnapshots: generateMockSnapshots(5),
  },
  parameters: {
    docs: {
      description: {
        story: '5 actions with diff visualization and rollback functionality.',
      },
    },
  },
};

/**
 * Scenario 3: With 50+ Actions
 * DoD: Test scrolling and performance with large history
 */
export const With50Actions: Story = {
  args: {
    sessionId: 'session-50',
    mockSnapshots: generateMockSnapshots(50),
  },
  parameters: {
    docs: {
      description: {
        story: '50+ actions to test scrolling, performance, and export functionality.',
      },
    },
  },
};

/**
 * Scenario 4: Dark Mode (Bonus)
 */
export const DarkMode: Story = {
  args: {
    sessionId: 'session-dark',
    mockSnapshots: generateMockSnapshots(10),
  },
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => (
      <div className="dark p-6 bg-slate-900 min-h-screen">
        <div className="max-w-4xl mx-auto">
          <Story />
        </div>
      </div>
    ),
  ],
};

/**
 * Scenario 5: Mobile Viewport (Bonus)
 */
export const Mobile: Story = {
  args: {
    sessionId: 'session-mobile',
    mockSnapshots: generateMockSnapshots(8),
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
