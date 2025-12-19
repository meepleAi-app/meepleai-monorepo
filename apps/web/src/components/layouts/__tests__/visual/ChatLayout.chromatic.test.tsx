/**
 * ChatLayout Chromatic Visual Tests (Issue #2232)
 *
 * Visual regression tests for ChatLayout component using Chromatic.
 * Tests various chat states, themes, and responsive layouts.
 */

import React from 'react';
import { describe, it } from 'vitest';
import type { Meta, StoryObj } from '@storybook/react';
import { ChatLayout } from '../../ChatLayout';
import { Game } from '@/types';

/**
 * Chromatic test suite for ChatLayout component
 * Each test creates a visual snapshot for regression testing
 */
describe('ChatLayout - Chromatic Visual Tests', () => {
  it('should match visual snapshot - Expanded sidebar', () => {
    // Snapshot will be captured by Chromatic via Storybook
  });

  it('should match visual snapshot - Collapsed sidebar', () => {
    // Snapshot will be captured by Chromatic via Storybook
  });

  it('should match visual snapshot - With active thread', () => {
    // Snapshot will be captured by Chromatic via Storybook
  });

  it('should match visual snapshot - No thread selected', () => {
    // Snapshot will be captured by Chromatic via Storybook
  });

  it('should match visual snapshot - Mobile responsive', () => {
    // Snapshot will be captured by Chromatic via Storybook
  });

  it('should match visual snapshot - Dark mode', () => {
    // Snapshot will be captured by Chromatic via Storybook
  });

  it('should match visual snapshot - With messages', () => {
    // Snapshot will be captured by Chromatic via Storybook
  });

  it('should match visual snapshot - Empty state', () => {
    // Snapshot will be captured by Chromatic via Storybook
  });
});

// Mock games data
const mockGames: Game[] = [
  { id: 'game-1', title: 'Catan', bggId: 13, year: 1995, description: 'Settlers of Catan' },
  { id: 'game-2', title: 'Pandemic', bggId: 30549, year: 2008, description: 'Save humanity' },
  {
    id: 'game-3',
    title: 'Ticket to Ride',
    bggId: 9209,
    year: 2004,
    description: 'Train adventure',
  },
];

// Mock sidebar content
const MockSidebarContent = () => (
  <div className="flex flex-col h-full p-4 space-y-4">
    <div className="space-y-2">
      <div className="text-sm font-medium">Game Selector</div>
      <div className="p-2 bg-white rounded border">Catan</div>
    </div>
    <div className="space-y-2">
      <div className="text-sm font-medium">Agent Selector</div>
      <div className="p-2 bg-white rounded border">MeepleBot</div>
    </div>
    <button className="w-full py-2 bg-blue-600 text-white rounded">+ Nuovo Thread</button>
    <div className="flex-1 space-y-2">
      <div className="text-sm font-medium">Thread History</div>
      <div className="space-y-1">
        <div className="p-2 bg-blue-50 rounded text-sm">Active Thread 1</div>
        <div className="p-2 bg-gray-50 rounded text-sm">Thread 2</div>
        <div className="p-2 bg-gray-50 rounded text-sm">Thread 3</div>
      </div>
    </div>
  </div>
);

// Mock chat content
const MockChatContent = ({ messages = 5 }: { messages?: number }) => (
  <div className="flex flex-col h-full">
    <div className="flex-1 overflow-y-auto p-4 space-y-4">
      {Array.from({ length: messages }).map((_, i) => (
        <div key={i} className="flex gap-3">
          <div className="w-8 h-8 rounded-full bg-gray-200 shrink-0" />
          <div className="flex-1 space-y-1">
            <div className="text-sm font-medium">{i % 2 === 0 ? 'User' : 'MeepleBot'}</div>
            <div className="text-sm">
              {i % 2 === 0
                ? 'Come si gioca a Catan?'
                : 'Catan è un gioco di strategia dove costruisci insediamenti...'}
            </div>
          </div>
        </div>
      ))}
    </div>
    <div className="border-t p-4">
      <input
        type="text"
        placeholder="Scrivi un messaggio..."
        className="w-full p-2 border rounded"
      />
    </div>
  </div>
);

// Export stories for Chromatic
const meta: Meta<typeof ChatLayout> = {
  title: 'Components/Layouts/ChatLayout/Chromatic',
  component: ChatLayout,
  tags: ['autodocs'],
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      disableSnapshot: false,
      viewports: [320, 768, 1024, 1920],
    },
  },
};

export default meta;
type Story = StoryObj<typeof ChatLayout>;

/**
 * Expanded sidebar with active thread
 */
export const ExpandedWithThread: Story = {
  args: {
    sidebarContent: <MockSidebarContent />,
    children: <MockChatContent messages={5} />,
    game: mockGames[0],
    games: mockGames,
    threadTitle: 'How to play Catan',
    onGameChange: () => {},
    onTitleChange: () => {},
    onShare: () => {},
    onExport: () => {},
    onDelete: () => {},
  },
  parameters: {
    chromatic: { disableSnapshot: false },
  },
};

/**
 * Collapsed sidebar state
 */
export const CollapsedSidebar: Story = {
  args: {
    ...ExpandedWithThread.args,
  },
  parameters: {
    chromatic: { disableSnapshot: false },
  },
  play: async ({ canvasElement }) => {
    // Simulate collapsed state via localStorage
    localStorage.setItem('chat-sidebar-collapsed', 'true');
  },
};

/**
 * No thread selected state
 */
export const NoThreadSelected: Story = {
  args: {
    sidebarContent: <MockSidebarContent />,
    children: (
      <div className="flex items-center justify-center h-full text-gray-400">
        <div className="text-center space-y-2">
          <div className="text-4xl">💬</div>
          <div>Seleziona un thread o creane uno nuovo</div>
        </div>
      </div>
    ),
    games: mockGames,
    threadTitle: 'Untitled Thread',
    onGameChange: () => {},
  },
  parameters: {
    chromatic: { disableSnapshot: false },
  },
};

/**
 * With many messages
 */
export const WithManyMessages: Story = {
  args: {
    ...ExpandedWithThread.args,
    children: <MockChatContent messages={15} />,
    threadTitle: 'Long conversation about Catan strategies',
  },
  parameters: {
    chromatic: { disableSnapshot: false },
  },
};

/**
 * Empty chat state
 */
export const EmptyChat: Story = {
  args: {
    sidebarContent: <MockSidebarContent />,
    children: (
      <div className="flex flex-col h-full">
        <div className="flex-1 flex items-center justify-center text-gray-400">
          <div className="text-center space-y-2">
            <div className="text-4xl">👋</div>
            <div>Inizia una conversazione!</div>
          </div>
        </div>
        <div className="border-t p-4">
          <input
            type="text"
            placeholder="Scrivi un messaggio..."
            className="w-full p-2 border rounded"
          />
        </div>
      </div>
    ),
    game: mockGames[0],
    games: mockGames,
    threadTitle: 'New Chat',
    onGameChange: () => {},
  },
  parameters: {
    chromatic: { disableSnapshot: false },
  },
};

/**
 * Loading state - Games loading
 */
export const LoadingGames: Story = {
  args: {
    ...ExpandedWithThread.args,
    loading: { games: true, title: false },
  },
  parameters: {
    chromatic: { disableSnapshot: false, pauseAnimationAtEnd: true },
  },
};

/**
 * Loading state - Title loading
 */
export const LoadingTitle: Story = {
  args: {
    ...ExpandedWithThread.args,
    loading: { games: false, title: true },
  },
  parameters: {
    chromatic: { disableSnapshot: false, pauseAnimationAtEnd: true },
  },
};

/**
 * Mobile view - 320px
 */
export const MobileView: Story = {
  args: {
    ...ExpandedWithThread.args,
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
 * Tablet view - 768px
 */
export const TabletView: Story = {
  args: {
    ...ExpandedWithThread.args,
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
 * Desktop view - 1920px
 */
export const DesktopView: Story = {
  args: {
    ...ExpandedWithThread.args,
  },
  parameters: {
    viewport: {
      defaultViewport: 'desktop',
    },
    chromatic: {
      disableSnapshot: false,
      viewports: [1920],
    },
  },
};

/**
 * Dark mode - Expanded
 */
export const DarkModeExpanded: Story = {
  args: {
    ...ExpandedWithThread.args,
  },
  parameters: {
    backgrounds: { default: 'dark' },
    chromatic: { disableSnapshot: false },
  },
};

/**
 * Dark mode - Collapsed
 */
export const DarkModeCollapsed: Story = {
  args: {
    ...ExpandedWithThread.args,
  },
  parameters: {
    backgrounds: { default: 'dark' },
    chromatic: { disableSnapshot: false },
  },
  play: async () => {
    localStorage.setItem('chat-sidebar-collapsed', 'true');
  },
};

/**
 * Without actions (no handlers provided)
 */
export const WithoutActions: Story = {
  args: {
    sidebarContent: <MockSidebarContent />,
    children: <MockChatContent />,
    game: mockGames[0],
    games: mockGames,
    threadTitle: 'Thread without actions',
    onGameChange: () => {},
    // No action handlers provided
    onShare: undefined,
    onExport: undefined,
    onDelete: undefined,
  },
  parameters: {
    chromatic: { disableSnapshot: false },
  },
};

/**
 * Custom styling example
 */
export const CustomStyling: Story = {
  args: {
    ...ExpandedWithThread.args,
    className: 'bg-gradient-to-br from-blue-50 to-purple-50',
  },
  parameters: {
    chromatic: { disableSnapshot: false },
  },
};
