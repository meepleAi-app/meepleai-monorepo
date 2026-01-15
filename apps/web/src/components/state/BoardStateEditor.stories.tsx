/**
 * BoardStateEditor - Issue #2420
 *
 * Editor for managing board grid and piece placement.
 * Features grid configuration, visual preview, and piece management.
 */

import { fn } from 'storybook/test';

import { BoardStateEditor } from './BoardStateEditor';

import type { Meta, StoryObj } from '@storybook/react';

const meta = {
  title: 'State/BoardStateEditor',
  component: BoardStateEditor,
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
    board: { control: 'object' },
    players: { control: 'object' },
    readonly: { control: 'boolean' },
    onChange: { action: 'board changed' },
    validationErrors: { control: 'object' },
  },
  args: {
    onChange: fn(),
    readonly: false,
    validationErrors: {},
    players: [
      { id: '1', name: 'Alice', score: 0, color: '#ff6b6b' },
      { id: '2', name: 'Bob', score: 0, color: '#4ecdc4' },
    ],
  },
} satisfies Meta<typeof BoardStateEditor>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Empty Board - Default 10x10 grid with no pieces
 */
export const EmptyBoard: Story = {
  args: {
    board: {
      gridWidth: 10,
      gridHeight: 10,
      pieces: [],
    },
  },
};

/**
 * Small Grid - Compact 5x5 grid
 */
export const SmallGrid: Story = {
  args: {
    board: {
      gridWidth: 5,
      gridHeight: 5,
      pieces: [],
    },
  },
};

/**
 * Large Grid - Expanded 20x20 grid
 */
export const LargeGrid: Story = {
  args: {
    board: {
      gridWidth: 20,
      gridHeight: 20,
      pieces: [],
    },
  },
};

/**
 * Rectangular Grid - Non-square 15x8 grid
 */
export const RectangularGrid: Story = {
  args: {
    board: {
      gridWidth: 15,
      gridHeight: 8,
      pieces: [],
    },
  },
};

/**
 * With Single Piece - Board with one piece placed
 */
export const WithSinglePiece: Story = {
  args: {
    board: {
      gridWidth: 10,
      gridHeight: 10,
      pieces: [{ id: 'p1', type: 'pawn', position: { x: 5, y: 5 }, ownerId: '1' }],
    },
  },
};

/**
 * With Multiple Pieces - Board with several pieces
 */
export const WithMultiplePieces: Story = {
  args: {
    board: {
      gridWidth: 10,
      gridHeight: 10,
      pieces: [
        { id: 'p1', type: 'pawn', position: { x: 1, y: 1 }, ownerId: '1' },
        { id: 'p2', type: 'knight', position: { x: 3, y: 3 }, ownerId: '1' },
        { id: 'p3', type: 'pawn', position: { x: 8, y: 8 }, ownerId: '2' },
        { id: 'p4', type: 'knight', position: { x: 6, y: 6 }, ownerId: '2' },
        { id: 'p5', type: 'castle', position: { x: 5, y: 5 } }, // Unassigned
      ],
    },
  },
};

/**
 * Chess-like Setup - 8x8 board with chess-like piece arrangement
 */
export const ChessLikeSetup: Story = {
  args: {
    board: {
      gridWidth: 8,
      gridHeight: 8,
      pieces: [
        // White pieces (Player 1)
        { id: 'w-r1', type: 'rook', position: { x: 0, y: 0 }, ownerId: '1' },
        { id: 'w-n1', type: 'knight', position: { x: 1, y: 0 }, ownerId: '1' },
        { id: 'w-b1', type: 'bishop', position: { x: 2, y: 0 }, ownerId: '1' },
        { id: 'w-q', type: 'queen', position: { x: 3, y: 0 }, ownerId: '1' },
        { id: 'w-k', type: 'king', position: { x: 4, y: 0 }, ownerId: '1' },
        { id: 'w-b2', type: 'bishop', position: { x: 5, y: 0 }, ownerId: '1' },
        { id: 'w-n2', type: 'knight', position: { x: 6, y: 0 }, ownerId: '1' },
        { id: 'w-r2', type: 'rook', position: { x: 7, y: 0 }, ownerId: '1' },
        // Black pieces (Player 2)
        { id: 'b-r1', type: 'rook', position: { x: 0, y: 7 }, ownerId: '2' },
        { id: 'b-n1', type: 'knight', position: { x: 1, y: 7 }, ownerId: '2' },
        { id: 'b-b1', type: 'bishop', position: { x: 2, y: 7 }, ownerId: '2' },
        { id: 'b-q', type: 'queen', position: { x: 3, y: 7 }, ownerId: '2' },
        { id: 'b-k', type: 'king', position: { x: 4, y: 7 }, ownerId: '2' },
        { id: 'b-b2', type: 'bishop', position: { x: 5, y: 7 }, ownerId: '2' },
        { id: 'b-n2', type: 'knight', position: { x: 6, y: 7 }, ownerId: '2' },
        { id: 'b-r2', type: 'rook', position: { x: 7, y: 7 }, ownerId: '2' },
      ],
    },
  },
};

/**
 * Corners Occupied - Pieces at all four corners
 */
export const CornersOccupied: Story = {
  args: {
    board: {
      gridWidth: 10,
      gridHeight: 10,
      pieces: [
        { id: 'p1', type: 'tower', position: { x: 0, y: 0 }, ownerId: '1' },
        { id: 'p2', type: 'tower', position: { x: 9, y: 0 }, ownerId: '1' },
        { id: 'p3', type: 'tower', position: { x: 0, y: 9 }, ownerId: '2' },
        { id: 'p4', type: 'tower', position: { x: 9, y: 9 }, ownerId: '2' },
      ],
    },
  },
};

/**
 * Diagonal Line - Pieces arranged diagonally
 */
export const DiagonalLine: Story = {
  args: {
    board: {
      gridWidth: 10,
      gridHeight: 10,
      pieces: Array.from({ length: 10 }, (_, i) => ({
        id: `p${i}`,
        type: 'piece',
        position: { x: i, y: i },
        ownerId: i % 2 === 0 ? '1' : '2',
      })),
    },
  },
};

/**
 * Unassigned Pieces - Pieces without owners
 */
export const UnassignedPieces: Story = {
  args: {
    board: {
      gridWidth: 8,
      gridHeight: 8,
      pieces: [
        { id: 'p1', type: 'obstacle', position: { x: 2, y: 2 } },
        { id: 'p2', type: 'treasure', position: { x: 5, y: 5 } },
        { id: 'p3', type: 'trap', position: { x: 7, y: 3 } },
        { id: 'p4', type: 'goal', position: { x: 7, y: 7 } },
      ],
    },
  },
};

/**
 * Readonly Mode - View-only, no editing allowed
 */
export const ReadonlyMode: Story = {
  args: {
    board: {
      gridWidth: 8,
      gridHeight: 8,
      pieces: [
        { id: 'p1', type: 'pawn', position: { x: 1, y: 1 }, ownerId: '1' },
        { id: 'p2', type: 'knight', position: { x: 6, y: 6 }, ownerId: '2' },
      ],
    },
    readonly: true,
  },
};

/**
 * No Players - Pieces without player assignment option
 */
export const NoPlayers: Story = {
  args: {
    board: {
      gridWidth: 8,
      gridHeight: 8,
      pieces: [
        { id: 'p1', type: 'piece', position: { x: 2, y: 2 } },
        { id: 'p2', type: 'piece', position: { x: 5, y: 5 } },
      ],
    },
    players: [], // No players available
  },
};

/**
 * Many Pieces - Stress test with many pieces
 */
export const ManyPieces: Story = {
  args: {
    board: {
      gridWidth: 10,
      gridHeight: 10,
      pieces: Array.from({ length: 30 }, (_, i) => ({
        id: `p${i}`,
        type: ['pawn', 'knight', 'bishop', 'rook'][i % 4],
        position: {
          x: Math.floor(Math.random() * 10),
          y: Math.floor(Math.random() * 10),
        },
        ownerId: Math.random() > 0.5 ? ['1', '2'][Math.floor(Math.random() * 2)] : undefined,
      })),
    },
  },
};

/**
 * With Validation Errors - Shows validation feedback
 */
export const WithValidationErrors: Story = {
  args: {
    board: {
      gridWidth: 0, // Invalid: below minimum
      gridHeight: 5,
      pieces: [
        { id: 'p1', type: '', position: { x: 0, y: 0 } }, // Empty type error
      ],
    },
    validationErrors: {
      'board.gridWidth': 'Larghezza griglia minima: 1',
      'board.pieces.0.type': 'Tipo pezzo obbligatorio',
    },
  },
};
