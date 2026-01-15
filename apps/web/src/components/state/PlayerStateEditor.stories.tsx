/**
 * PlayerStateEditor - Issue #2420
 *
 * Editor for managing player count, names, and scores.
 * Features add/remove players, validation, and readonly mode.
 */

import { fn } from 'storybook/test';

import { PlayerStateEditor } from './PlayerStateEditor';

import type { Meta, StoryObj } from '@storybook/react';

const meta = {
  title: 'State/PlayerStateEditor',
  component: PlayerStateEditor,
  parameters: {
    layout: 'padded',
    chromatic: {
      viewports: [768, 1024],
      delay: 500,
    },
  },
  tags: ['autodocs'],
  decorators: [
    Story => (
      <div className="max-w-3xl mx-auto p-6">
        <Story />
      </div>
    ),
  ],
  argTypes: {
    players: { control: 'object' },
    readonly: { control: 'boolean' },
    onChange: { action: 'players changed' },
    validationErrors: { control: 'object' },
  },
  args: {
    onChange: fn(),
    readonly: false,
    validationErrors: {},
  },
} satisfies Meta<typeof PlayerStateEditor>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Empty - No players added yet
 */
export const Empty: Story = {
  args: {
    players: [],
  },
};

/**
 * Single Player - One player in the game
 */
export const SinglePlayer: Story = {
  args: {
    players: [{ id: '1', name: 'Alice', score: 15 }],
  },
};

/**
 * Multiple Players - Typical game with 3 players
 */
export const MultiplePlayers: Story = {
  args: {
    players: [
      { id: '1', name: 'Alice', score: 15 },
      { id: '2', name: 'Bob', score: 12 },
      { id: '3', name: 'Charlie', score: 18 },
    ],
  },
};

/**
 * With Colors - Players with assigned colors
 */
export const WithColors: Story = {
  args: {
    players: [
      { id: '1', name: 'Alice', score: 15, color: '#ff6b6b' },
      { id: '2', name: 'Bob', score: 12, color: '#4ecdc4' },
      { id: '3', name: 'Charlie', score: 18, color: '#ffe66d' },
      { id: '4', name: 'Diana', score: 9, color: '#a8e6cf' },
    ],
  },
};

/**
 * High Scores - Players with high scores
 */
export const HighScores: Story = {
  args: {
    players: [
      { id: '1', name: 'Alice', score: 150, color: '#ff6b6b' },
      { id: '2', name: 'Bob', score: 125, color: '#4ecdc4' },
      { id: '3', name: 'Charlie', score: 180, color: '#ffe66d' },
    ],
  },
};

/**
 * Zero Scores - New game with all players at zero
 */
export const ZeroScores: Story = {
  args: {
    players: [
      { id: '1', name: 'Alice', score: 0 },
      { id: '2', name: 'Bob', score: 0 },
      { id: '3', name: 'Charlie', score: 0 },
    ],
  },
};

/**
 * Readonly Mode - View-only, no editing allowed
 */
export const ReadonlyMode: Story = {
  args: {
    players: [
      { id: '1', name: 'Alice', score: 15, color: '#ff6b6b' },
      { id: '2', name: 'Bob', score: 12, color: '#4ecdc4' },
    ],
    readonly: true,
  },
};

/**
 * With Validation Errors - Shows validation feedback
 */
export const WithValidationErrors: Story = {
  args: {
    players: [
      { id: '1', name: '', score: 15 }, // Empty name error
      { id: '2', name: 'Bob', score: -5 }, // Negative score error
    ],
    validationErrors: {
      'players.0.name': 'Nome giocatore obbligatorio',
      'players.1.score': 'Punteggio non può essere negativo',
    },
  },
};

/**
 * Many Players - Stress test with 8 players
 */
export const ManyPlayers: Story = {
  args: {
    players: Array.from({ length: 8 }, (_, i) => ({
      id: `player-${i}`,
      name: `Player ${i + 1}`,
      score: Math.floor(Math.random() * 50),
      color: `#${Math.floor(Math.random() * 16777215).toString(16)}`,
    })),
  },
};

/**
 * Long Names - Players with very long names
 */
export const LongNames: Story = {
  args: {
    players: [
      { id: '1', name: 'Alexander the Great Conqueror', score: 15 },
      { id: '2', name: 'Maximilian Augustus Ferdinand', score: 12 },
      { id: '3', name: 'Charlotte Isabella Victoria', score: 18 },
    ],
  },
};
