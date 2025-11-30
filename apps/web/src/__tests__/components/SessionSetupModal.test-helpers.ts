/**
 * Test helpers for SessionSetupModal component tests
 */

import { Game, GameSessionDto } from '@/lib/api';

/**
 * Sample game data for testing
 */
export const mockGame: Game = {
  id: 'game-123',
  title: 'Catan',
  publisher: 'Catan Studio',
  yearPublished: 1995,
  minPlayers: 3,
  maxPlayers: 4,
  minPlayTimeMinutes: 60,
  maxPlayTimeMinutes: 120,
  bggId: 13,
  createdAt: '2024-01-01T00:00:00Z',
};

/**
 * Create mock game session
 */
export const createMockSession = (overrides?: Partial<GameSessionDto>): GameSessionDto => ({
  id: 'session-123',
  gameId: mockGame.id,
  status: 'InProgress',
  startedAt: new Date().toISOString(),
  completedAt: null,
  playerCount: 3,
  players: [
    { playerName: 'Alice', playerOrder: 1, color: '#E63946' },
    { playerName: 'Bob', playerOrder: 2, color: '#F77F00' },
    { playerName: 'Charlie', playerOrder: 3, color: '#06D6A0' },
  ],
  winnerName: null,
  notes: null,
  durationMinutes: 0,
  ...overrides,
});

/**
 * Sample game configurations
 */
export const gameConfigurations = {
  standard: mockGame,
  fixedPlayers: {
    ...mockGame,
    minPlayers: 2,
    maxPlayers: 2,
  },
  noLimits: {
    ...mockGame,
    minPlayers: null,
    maxPlayers: null,
  },
  largeLimits: {
    ...mockGame,
    minPlayers: 4,
    maxPlayers: 6,
  },
};
