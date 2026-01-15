/**
 * StateEditor - Issue #2420
 *
 * Container for game state editing with tabs for players, resources, and board.
 * Features validation, readonly mode, and sub-editor orchestration.
 */

import { fn } from 'storybook/test';

import { StateEditor } from './StateEditor';

import type { GameState } from './StateEditor';
import type { Meta, StoryObj } from '@storybook/react';

const meta = {
  title: 'State/StateEditor',
  component: StateEditor,
  parameters: {
    layout: 'padded',
    chromatic: {
      viewports: [768, 1024, 1280],
      delay: 500,
    },
  },
  tags: ['autodocs'],
  decorators: [
    Story => (
      <div className="max-w-5xl mx-auto p-6">
        <Story />
      </div>
    ),
  ],
  argTypes: {
    initialState: { control: 'object' },
    readonly: { control: 'boolean' },
    onChange: { action: 'state changed' },
    onValidationError: { action: 'validation error' },
  },
  args: {
    onChange: fn(),
    onValidationError: fn(),
    readonly: false,
  },
} satisfies Meta<typeof StateEditor>;

export default meta;
type Story = StoryObj<typeof meta>;

// Mock game states
const emptyState: GameState = {
  players: [],
  resources: [],
  board: { gridWidth: 10, gridHeight: 10, pieces: [] },
};

const stateWithPlayers: GameState = {
  players: [
    { id: '1', name: 'Alice', score: 15 },
    { id: '2', name: 'Bob', score: 12, color: '#ff6b6b' },
    { id: '3', name: 'Charlie', score: 18, color: '#4ecdc4' },
  ],
  resources: [],
  board: { gridWidth: 10, gridHeight: 10, pieces: [] },
};

const stateWithResources: GameState = {
  players: [
    { id: '1', name: 'Alice', score: 15 },
    { id: '2', name: 'Bob', score: 12 },
  ],
  resources: [
    { id: 'r1', type: 'token', name: 'Gold Coin', quantity: 25 },
    { id: 'r2', type: 'card', name: 'Victory Point', quantity: 10, ownerId: '1' },
    { id: 'r3', type: 'resource', name: 'Wood', quantity: 8, ownerId: '2' },
  ],
  board: { gridWidth: 10, gridHeight: 10, pieces: [] },
};

const fullState: GameState = {
  players: [
    { id: '1', name: 'Alice', score: 15, color: '#ff6b6b' },
    { id: '2', name: 'Bob', score: 12, color: '#4ecdc4' },
  ],
  resources: [
    { id: 'r1', type: 'token', name: 'Gold Coin', quantity: 25 },
    { id: 'r2', type: 'card', name: 'Victory Point', quantity: 10, ownerId: '1' },
  ],
  board: {
    gridWidth: 8,
    gridHeight: 8,
    pieces: [
      { id: 'p1', type: 'pawn', position: { x: 0, y: 0 }, ownerId: '1' },
      { id: 'p2', type: 'knight', position: { x: 7, y: 7 }, ownerId: '2' },
      { id: 'p3', type: 'castle', position: { x: 3, y: 4 } },
    ],
  },
};

/**
 * Empty State - Starting point with no data
 */
export const EmptyState: Story = {
  args: {
    initialState: emptyState,
  },
};

/**
 * With Players Only - Game with players but no resources or board pieces
 */
export const WithPlayersOnly: Story = {
  args: {
    initialState: stateWithPlayers,
  },
};

/**
 * With Resources - Game with players and resources
 */
export const WithResources: Story = {
  args: {
    initialState: stateWithResources,
  },
};

/**
 * Full Game State - Complete game with players, resources, and board
 */
export const FullGameState: Story = {
  args: {
    initialState: fullState,
  },
};

/**
 * Readonly Mode - View-only mode, no editing allowed
 */
export const ReadonlyMode: Story = {
  args: {
    initialState: fullState,
    readonly: true,
  },
};

/**
 * Large Grid - Board with large dimensions
 */
export const LargeGrid: Story = {
  args: {
    initialState: {
      players: [{ id: '1', name: 'Player 1', score: 0 }],
      resources: [],
      board: {
        gridWidth: 20,
        gridHeight: 20,
        pieces: [
          { id: 'p1', type: 'piece', position: { x: 0, y: 0 } },
          { id: 'p2', type: 'piece', position: { x: 19, y: 19 } },
        ],
      },
    },
  },
};

/**
 * Many Players - Game with many players
 */
export const ManyPlayers: Story = {
  args: {
    initialState: {
      players: Array.from({ length: 8 }, (_, i) => ({
        id: `player-${i}`,
        name: `Player ${i + 1}`,
        score: Math.floor(Math.random() * 50),
        color: `#${Math.floor(Math.random() * 16777215).toString(16)}`,
      })),
      resources: [],
      board: { gridWidth: 10, gridHeight: 10, pieces: [] },
    },
  },
};

/**
 * Many Resources - Game with diverse resource types
 */
export const ManyResources: Story = {
  args: {
    initialState: {
      players: [{ id: '1', name: 'Player 1', score: 0 }],
      resources: [
        { id: 'r1', type: 'token', name: 'Gold', quantity: 50 },
        { id: 'r2', type: 'token', name: 'Silver', quantity: 30 },
        { id: 'r3', type: 'card', name: 'Action Card', quantity: 12 },
        { id: 'r4', type: 'card', name: 'Event Card', quantity: 8 },
        { id: 'r5', type: 'resource', name: 'Wood', quantity: 20 },
        { id: 'r6', type: 'resource', name: 'Stone', quantity: 15 },
      ],
      board: { gridWidth: 10, gridHeight: 10, pieces: [] },
    },
  },
};

/**
 * Complex Board - Board with many pieces
 */
export const ComplexBoard: Story = {
  args: {
    initialState: {
      players: [
        { id: '1', name: 'Alice', score: 0, color: '#ff6b6b' },
        { id: '2', name: 'Bob', score: 0, color: '#4ecdc4' },
      ],
      resources: [],
      board: {
        gridWidth: 8,
        gridHeight: 8,
        pieces: [
          { id: 'p1', type: 'pawn', position: { x: 1, y: 1 }, ownerId: '1' },
          { id: 'p2', type: 'pawn', position: { x: 2, y: 1 }, ownerId: '1' },
          { id: 'p3', type: 'knight', position: { x: 0, y: 2 }, ownerId: '1' },
          { id: 'p4', type: 'pawn', position: { x: 6, y: 6 }, ownerId: '2' },
          { id: 'p5', type: 'knight', position: { x: 7, y: 5 }, ownerId: '2' },
          { id: 'p6', type: 'castle', position: { x: 3, y: 3 } },
        ],
      },
    },
  },
};
