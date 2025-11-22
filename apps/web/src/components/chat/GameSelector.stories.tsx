import type { Meta, StoryObj } from '@storybook/react';
import { GameSelector } from './GameSelector';
import React, { useEffect } from 'react';
import { useChatStore } from '@/store/chat/store';

/**
 * GameSelector - Dropdown for selecting game context.
 * Allows users to select which board game they want to chat about.
 * Migrated to Zustand (Issue #1240)
 */
const meta = {
  title: 'Chat/GameSelector',
  component: GameSelector,
  parameters: {
    layout: 'padded',
  },
  tags: ['autodocs'],
} satisfies Meta<typeof GameSelector>;

export default meta;
type Story = StoryObj<typeof meta>;

// Zustand Store Initializer for Storybook
function StoreInitializer({
  games = [],
  selectedGameId = null,
  loadingGames = false,
  children
}: {
  games?: Array<{ id: string; name: string; createdAt: string }>;
  selectedGameId?: string | null;
  loadingGames?: boolean;
  children: React.ReactNode;
}) {
  useEffect(() => {
    useChatStore.setState({
      games: games,
      selectedGameId: selectedGameId,
      loading: {
        games: loadingGames,
        agents: false,
        chats: false,
        messages: false,
        sending: false,
        creating: false,
        updating: false,
        deleting: false,
      },
    });
  }, [games, selectedGameId, loadingGames]);

  return <>{children}</>;
}

/**
 * Default state with games available
 */
export const Default: Story = {
  decorators: [
    (Story) => (
      <StoreInitializer
        games={[
          { id: 'game-1', name: 'Chess', createdAt: new Date().toISOString() },
          { id: 'game-2', name: 'Wingspan', createdAt: new Date().toISOString() },
          { id: 'game-3', name: 'Gloomhaven', createdAt: new Date().toISOString() },
        ]}
        selectedGameId="game-1"
      >
        <Story />
      </StoreInitializer>
    ),
  ],
};

/**
 * Loading state while fetching games
 */
export const Loading: Story = {
  decorators: [
    (Story) => (
      <StoreInitializer loadingGames={true}>
        <Story />
      </StoreInitializer>
    ),
  ],
};

/**
 * No games available
 */
export const NoGames: Story = {
  decorators: [
    (Story) => (
      <StoreInitializer games={[]}>
        <Story />
      </StoreInitializer>
    ),
  ],
};

/**
 * No game selected
 */
export const NoSelection: Story = {
  decorators: [
    (Story) => (
      <StoreInitializer
        games={[
          { id: 'game-1', name: 'Chess', createdAt: new Date().toISOString() },
          { id: 'game-2', name: 'Wingspan', createdAt: new Date().toISOString() },
        ]}
        selectedGameId={null}
      >
        <Story />
      </StoreInitializer>
    ),
  ],
};

/**
 * Many games (scrollable)
 */
export const ManyGames: Story = {
  decorators: [
    (Story) => (
      <StoreInitializer
        games={[
          { id: 'game-1', name: 'Chess', createdAt: new Date().toISOString() },
          { id: 'game-2', name: 'Wingspan', createdAt: new Date().toISOString() },
          { id: 'game-3', name: 'Gloomhaven', createdAt: new Date().toISOString() },
          { id: 'game-4', name: 'Terraforming Mars', createdAt: new Date().toISOString() },
          { id: 'game-5', name: 'Spirit Island', createdAt: new Date().toISOString() },
          { id: 'game-6', name: 'Azul', createdAt: new Date().toISOString() },
          { id: 'game-7', name: 'Catan', createdAt: new Date().toISOString() },
          { id: 'game-8', name: 'Ticket to Ride', createdAt: new Date().toISOString() },
        ]}
        selectedGameId="game-3"
      >
        <Story />
      </StoreInitializer>
    ),
  ],
};

/**
 * Interactive game selection demo
 */
const InteractiveGameSelectorComponent = () => {
  return (
    <StoreInitializer
      games={[
        { id: 'game-1', name: 'Chess', createdAt: new Date().toISOString() },
        { id: 'game-2', name: 'Wingspan', createdAt: new Date().toISOString() },
        { id: 'game-3', name: 'Gloomhaven', createdAt: new Date().toISOString() },
      ]}
    >
      <div className="space-y-4">
        <p className="text-sm text-gray-600">
          Try selecting different games from the dropdown
        </p>
        <GameSelector />
      </div>
    </StoreInitializer>
  );
};

export const Interactive: Story = {
  render: () => <InteractiveGameSelectorComponent />,
};

/**
 * Dark mode example
 */
export const DarkMode: Story = {
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    (Story) => (
      <div className="dark">
        <StoreInitializer
          games={[
            { id: 'game-1', name: 'Chess', createdAt: new Date().toISOString() },
            { id: 'game-2', name: 'Wingspan', createdAt: new Date().toISOString() },
            { id: 'game-3', name: 'Gloomhaven', createdAt: new Date().toISOString() },
          ]}
          selectedGameId="game-1"
        >
          <Story />
        </StoreInitializer>
      </div>
    ),
  ],
};
