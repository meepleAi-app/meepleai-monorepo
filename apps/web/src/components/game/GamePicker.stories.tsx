import { GamePicker } from './GamePicker';

import type { Meta, StoryObj } from '@storybook/react';

// Mock function for onClick handlers in Storybook (avoids Vitest/Jest dependency)
const fn = <T extends unknown[], R>(implementation?: (...args: T) => R) =>
  implementation ?? ((() => {}) as unknown as (...args: T) => R);

/**
 * GamePicker - Game selection and creation component
 *
 * ## Features
 * - Select from existing games dropdown
 * - Create new games with validation
 * - Loading states for async operations
 * - Error handling with user-friendly messages
 * - Selected game confirmation alert
 * - Design system compliance (Shadcn/UI)
 * - Accessible forms with proper labels
 *
 * ## Accessibility
 * - ✅ Label associations for form controls
 * - ✅ Keyboard navigation in select dropdown
 * - ✅ Form validation with error messaging
 * - ✅ Disabled state management
 * - ✅ Success alert with icon
 */
const meta = {
  title: 'Game/GamePicker',
  component: GamePicker,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Game selection and creation component allowing users to choose from existing games or create new ones with validation.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    games: {
      description: 'Array of available games',
      control: 'object',
    },
    selectedGameId: {
      control: 'text',
      description: 'ID of currently selected game',
    },
    loading: {
      control: 'boolean',
      description: 'Loading state for async operations',
    },
  },
} satisfies Meta<typeof GamePicker>;

export default meta;
type Story = StoryObj<typeof meta>;

// Fixed test data
const mockGames = [
  { id: '1', title: 'Gloomhaven', createdAt: '2024-01-15T10:00:00Z' },
  { id: '2', title: 'Wingspan', createdAt: '2024-01-14T15:30:00Z' },
  { id: '3', title: 'Terraforming Mars', createdAt: '2024-01-13T09:15:00Z' },
  { id: '4', title: 'Spirit Island', createdAt: '2024-01-12T14:20:00Z' },
  { id: '5', title: 'Scythe', createdAt: '2024-01-11T11:45:00Z' },
];

const manyGames = [
  ...mockGames,
  { id: '6', title: 'Brass: Birmingham', createdAt: '2024-01-10T08:30:00Z' },
  { id: '7', title: 'Ark Nova', createdAt: '2024-01-09T16:45:00Z' },
  { id: '8', title: 'Dune: Imperium', createdAt: '2024-01-08T13:20:00Z' },
  { id: '9', title: 'Root', createdAt: '2024-01-07T10:15:00Z' },
  { id: '10', title: 'Everdell', createdAt: '2024-01-06T14:50:00Z' },
];

/**
 * Default state with games available.
 * Shows dropdown with 5 games, no selection.
 */
export const Default: Story = {
  args: {
    games: mockGames,
    selectedGameId: null,
    onGameSelect: () => {},
    onGameCreate: fn(async (name: string) => {
      console.log('Creating game:', name);
    }),
  },
};

/**
 * Game selected with confirmation alert.
 * Shows selected game in alert below form.
 */
export const Selected: Story = {
  args: {
    games: mockGames,
    selectedGameId: '1',
    onGameSelect: () => {},
    onGameCreate: fn(async (name: string) => {
      console.log('Creating game:', name);
    }),
  },
};

/**
 * Empty state with no games available.
 * Shows only create form, disabled select.
 */
export const Empty: Story = {
  args: {
    games: [],
    selectedGameId: null,
    onGameSelect: () => {},
    onGameCreate: fn(async (name: string) => {
      console.log('Creating game:', name);
    }),
  },
};

/**
 * Loading state during async operations.
 * Disables all form controls.
 */
export const Loading: Story = {
  args: {
    games: mockGames,
    selectedGameId: null,
    loading: true,
    onGameSelect: () => {},
    onGameCreate: fn(async (name: string) => {
      console.log('Creating game:', name);
    }),
  },
};

/**
 * Many games in dropdown.
 * Tests scrollable select with 10 games.
 */
export const ManyGames: Story = {
  args: {
    games: manyGames,
    selectedGameId: null,
    onGameSelect: () => {},
    onGameCreate: fn(async (name: string) => {
      console.log('Creating game:', name);
    }),
  },
};

/**
 * Validation error state.
 * Shows error when creating game with invalid name.
 */
export const ValidationError: Story = {
  args: {
    games: mockGames,
    selectedGameId: null,
    onGameSelect: () => {},
    onGameCreate: fn(async (name: string) => {
      if (name.length < 2) {
        throw new Error('Game name must be at least 2 characters');
      }
    }),
  },
  play: async ({ canvasElement: _canvasElement }) => {
    // Note: This is for documentation purposes
    // In actual usage, user would type short name and click Create
  },
};

/**
 * Long game names in dropdown.
 * Tests text truncation and layout.
 */
export const LongNames: Story = {
  args: {
    games: [
      {
        id: '1',
        title: 'Gloomhaven: Jaws of the Lion - Complete Edition',
        createdAt: '2024-01-15T10:00:00Z',
      },
      {
        id: '2',
        title: 'Twilight Imperium: Fourth Edition - Prophecy of Kings Expansion',
        createdAt: '2024-01-14T15:30:00Z',
      },
      {
        id: '3',
        title: 'Arkham Horror: The Card Game - The Dunwich Legacy Campaign',
        createdAt: '2024-01-13T09:15:00Z',
      },
    ],
    selectedGameId: null,
    onGameSelect: () => {},
    onGameCreate: fn(async (name: string) => {
      console.log('Creating game:', name);
    }),
  },
};

/**
 * Dark theme variant.
 * Shows component on dark background.
 */
export const DarkTheme: Story = {
  args: {
    games: mockGames,
    selectedGameId: '1',
    onGameSelect: () => {},
    onGameCreate: fn(async (name: string) => {
      console.log('Creating game:', name);
    }),
  },
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => (
      <div className="dark p-8 bg-background">
        <div className="w-[400px]">
          <Story />
        </div>
      </div>
    ),
  ],
};
