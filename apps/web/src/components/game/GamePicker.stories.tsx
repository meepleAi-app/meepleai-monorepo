import type { Meta, StoryObj } from '@storybook/react';
import { GamePicker } from './GamePicker';
import { fn } from '@storybook/test';
import React from 'react';

/**
 * GamePicker - Game selection and creation component.
 * Features:
 * - Select from existing games
 * - Create new games with validation
 * - Loading states
 * - Design system compliance
 * - Accessible forms
 */
const meta = {
  title: 'Game/GamePicker',
  component: GamePicker,
  parameters: {
    layout: 'centered',
  },
  tags: ['autodocs'],
  argTypes: {
    loading: {
      control: 'boolean',
      description: 'Loading state for the component',
    },
  },
  args: {
    onGameSelect: fn(),
    onGameCreate: fn(async () => {}),
  },
} satisfies Meta<typeof GamePicker>;

export default meta;
type Story = StoryObj<typeof meta>;

// Mock game data
const mockGames = [
  { id: 'game-1', name: 'Gloomhaven', createdAt: '2024-01-10T10:00:00Z' },
  { id: 'game-2', name: 'Wingspan', createdAt: '2024-01-12T14:30:00Z' },
  { id: 'game-3', name: 'Terraforming Mars', createdAt: '2024-01-15T09:15:00Z' },
];

/**
 * Default state with games available
 */
export const Default: Story = {
  args: {
    games: mockGames,
    selectedGameId: null,
    loading: false,
  },
};

/**
 * With game selected
 */
export const WithSelection: Story = {
  args: {
    games: mockGames,
    selectedGameId: 'game-2',
    loading: false,
  },
};

/**
 * Loading state
 */
export const Loading: Story = {
  args: {
    games: mockGames,
    selectedGameId: null,
    loading: true,
  },
};

/**
 * No games available
 */
export const NoGames: Story = {
  args: {
    games: [],
    selectedGameId: null,
    loading: false,
  },
};

/**
 * Many games available
 */
export const ManyGames: Story = {
  args: {
    games: [
      { id: 'game-1', name: 'Gloomhaven', createdAt: '2024-01-01T10:00:00Z' },
      { id: 'game-2', name: 'Wingspan', createdAt: '2024-01-02T10:00:00Z' },
      { id: 'game-3', name: 'Terraforming Mars', createdAt: '2024-01-03T10:00:00Z' },
      { id: 'game-4', name: 'Spirit Island', createdAt: '2024-01-04T10:00:00Z' },
      { id: 'game-5', name: 'Scythe', createdAt: '2024-01-05T10:00:00Z' },
      { id: 'game-6', name: 'Pandemic Legacy', createdAt: '2024-01-06T10:00:00Z' },
      { id: 'game-7', name: 'Catan', createdAt: '2024-01-07T10:00:00Z' },
      { id: 'game-8', name: 'Ticket to Ride', createdAt: '2024-01-08T10:00:00Z' },
    ],
    selectedGameId: 'game-4',
    loading: false,
  },
};

/**
 * Interactive game creation demo
 */
export const Interactive: Story = {
  render: () => {
    const [games, setGames] = React.useState(mockGames);
    const [selectedGameId, setSelectedGameId] = React.useState<string | null>(null);
    const [loading, setLoading] = React.useState(false);

    const handleGameCreate = async (name: string) => {
      setLoading(true);

      // Simulate API call
      await new Promise((resolve) => setTimeout(resolve, 1000));

      const newGame = {
        id: `game-${Date.now()}`,
        name,
        createdAt: new Date().toISOString(),
      };

      setGames([...games, newGame]);
      setSelectedGameId(newGame.id);
      setLoading(false);
    };

    return (
      <GamePicker
        games={games}
        selectedGameId={selectedGameId}
        onGameSelect={setSelectedGameId}
        onGameCreate={handleGameCreate}
        loading={loading}
      />
    );
  },
};

/**
 * Error handling demo (validation)
 */
export const ValidationError: Story = {
  render: () => {
    const handleGameCreate = async (name: string) => {
      if (name.toLowerCase().includes('test')) {
        throw new Error('Game name cannot contain "test"');
      }
      await new Promise((resolve) => setTimeout(resolve, 500));
    };

    return (
      <GamePicker
        games={mockGames}
        selectedGameId={null}
        onGameSelect={() => {}}
        onGameCreate={handleGameCreate}
        loading={false}
      />
    );
  },
};

/**
 * Creation in progress
 */
export const CreatingGame: Story = {
  render: () => {
    const [creating, setCreating] = React.useState(false);

    const handleGameCreate = async (name: string) => {
      setCreating(true);
      await new Promise((resolve) => setTimeout(resolve, 3000));
      setCreating(false);
    };

    return (
      <div className="space-y-4">
        <div className="text-sm text-slate-600">Try creating a game to see the loading state</div>
        <GamePicker
          games={mockGames}
          selectedGameId={null}
          onGameSelect={() => {}}
          onGameCreate={handleGameCreate}
          loading={creating}
        />
      </div>
    );
  },
};

/**
 * Full workflow demo
 */
export const FullWorkflow: Story = {
  render: () => {
    const [games, setGames] = React.useState(mockGames);
    const [selectedGameId, setSelectedGameId] = React.useState<string | null>('game-1');
    const [loading, setLoading] = React.useState(false);

    const handleGameCreate = async (name: string) => {
      setLoading(true);

      // Simulate API call with potential error
      await new Promise((resolve, reject) => {
        setTimeout(() => {
          if (Math.random() > 0.8) {
            reject(new Error('Failed to create game (random error for demo)'));
          } else {
            resolve(null);
          }
        }, 1000);
      });

      const newGame = {
        id: `game-${Date.now()}`,
        name,
        createdAt: new Date().toISOString(),
      };

      setGames([...games, newGame]);
      setSelectedGameId(newGame.id);
      setLoading(false);
    };

    return (
      <div className="max-w-md">
        <div className="mb-4 text-sm text-slate-600">
          <p className="mb-2">This demo shows the full workflow:</p>
          <ul className="list-disc list-inside space-y-1">
            <li>Select existing game from dropdown</li>
            <li>Create new game (20% chance of random error)</li>
            <li>Newly created game is auto-selected</li>
            <li>Loading states during creation</li>
          </ul>
        </div>
        <GamePicker
          games={games}
          selectedGameId={selectedGameId}
          onGameSelect={setSelectedGameId}
          onGameCreate={handleGameCreate}
          loading={loading}
        />
      </div>
    );
  },
};

/**
 * Dark mode
 */
export const DarkMode: Story = {
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    (Story) => (
      <div className="dark">
        <Story />
      </div>
    ),
  ],
  args: {
    games: mockGames,
    selectedGameId: 'game-2',
    loading: false,
  },
};

/**
 * Responsive (mobile)
 */
export const Mobile: Story = {
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
  },
  args: {
    games: mockGames,
    selectedGameId: 'game-1',
    loading: false,
  },
};
