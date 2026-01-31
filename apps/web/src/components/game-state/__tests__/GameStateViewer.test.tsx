/**
 * GameStateViewer Tests
 * Issue #2766: Sprint 6 - Test Coverage Push
 *
 * Tests for read-only game state display component.
 */

import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { GameState, PlayerState } from '@/types/game-state';

import { GameStateViewer } from '../GameStateViewer';

// Mock PlayerStateCard component
vi.mock('../PlayerStateCard', () => ({
  PlayerStateCard: ({
    player,
    isCurrentPlayer,
    editable,
  }: {
    player: PlayerState;
    isCurrentPlayer?: boolean;
    editable: boolean;
  }) => (
    <div data-testid={`player-card-${player.playerName}`}>
      <span>{player.playerName}</span>
      {isCurrentPlayer && <span data-testid="current-player-indicator">Current</span>}
      {editable && <span data-testid="editable-indicator">Editable</span>}
    </div>
  ),
}));

const createMockGameState = (overrides?: Partial<GameState>): GameState => ({
  sessionId: 'session-123',
  gameId: 'game-456',
  templateId: 'template-789',
  version: '1.0.0',
  phase: 'Action',
  currentPlayerIndex: 0,
  roundNumber: 3,
  players: [
    {
      playerName: 'Alice',
      playerOrder: 1,
      color: '#FF6B6B',
      score: 25,
      resources: { gold: 10, wood: 5 },
    },
    {
      playerName: 'Bob',
      playerOrder: 2,
      color: '#4ECDC4',
      score: 20,
      resources: { gold: 8, wood: 7 },
    },
  ],
  globalResources: {
    market_cards: 15,
    victory_points_pool: 50,
  },
  metadata: {
    difficulty: 'medium',
    lastAction: 'card_played',
  },
  ...overrides,
});

describe('GameStateViewer - Issue #2766', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Basic Rendering', () => {
    it('should render the game state viewer container', () => {
      const state = createMockGameState();
      render(<GameStateViewer state={state} />);

      expect(screen.getByTestId('game-state-viewer')).toBeInTheDocument();
    });

    it('should render game information card', () => {
      const state = createMockGameState();
      render(<GameStateViewer state={state} />);

      expect(screen.getByText('Game Information')).toBeInTheDocument();
      expect(screen.getByText('Current game session state')).toBeInTheDocument();
    });
  });

  describe('Phase Display', () => {
    it('should render phase badge when phase exists', () => {
      const state = createMockGameState({ phase: 'Action' });
      render(<GameStateViewer state={state} />);

      expect(screen.getByText('Phase')).toBeInTheDocument();
      expect(screen.getByText('Action')).toBeInTheDocument();
    });

    it('should not render phase when undefined', () => {
      const state = createMockGameState({ phase: undefined });
      render(<GameStateViewer state={state} />);

      expect(screen.queryByText('Phase')).not.toBeInTheDocument();
    });
  });

  describe('Round Number Display', () => {
    it('should render round number when exists', () => {
      const state = createMockGameState({ roundNumber: 5 });
      render(<GameStateViewer state={state} />);

      expect(screen.getByText('Round')).toBeInTheDocument();
      expect(screen.getByText('5')).toBeInTheDocument();
    });

    it('should render round 0 correctly', () => {
      const state = createMockGameState({ roundNumber: 0 });
      render(<GameStateViewer state={state} />);

      expect(screen.getByText('Round')).toBeInTheDocument();
      expect(screen.getByText('0')).toBeInTheDocument();
    });

    it('should not render round when undefined', () => {
      const state = createMockGameState({ roundNumber: undefined });
      render(<GameStateViewer state={state} />);

      expect(screen.queryByText('Round')).not.toBeInTheDocument();
    });
  });

  describe('Current Player Display', () => {
    it('should render current player name', () => {
      const state = createMockGameState({ currentPlayerIndex: 0 });
      render(<GameStateViewer state={state} />);

      expect(screen.getByText('Current Player')).toBeInTheDocument();
      // Alice appears twice: in "Current Player" section and in PlayerStateCard
      expect(screen.getAllByText('Alice')).toHaveLength(2);
    });

    it('should show Unknown when player index is out of bounds', () => {
      const state = createMockGameState({ currentPlayerIndex: 99 });
      render(<GameStateViewer state={state} />);

      expect(screen.getByText('Unknown')).toBeInTheDocument();
    });

    it('should not render current player when index is undefined', () => {
      const state = createMockGameState({ currentPlayerIndex: undefined });
      render(<GameStateViewer state={state} />);

      expect(screen.queryByText('Current Player')).not.toBeInTheDocument();
    });
  });

  describe('Global Resources Display', () => {
    it('should render global resources section when present', () => {
      const state = createMockGameState({
        globalResources: { market_cards: 15, victory_points_pool: 50 },
      });
      render(<GameStateViewer state={state} />);

      expect(screen.getByText('Global Resources')).toBeInTheDocument();
      expect(screen.getByText('market cards')).toBeInTheDocument();
      expect(screen.getByText('15')).toBeInTheDocument();
      expect(screen.getByText('victory points pool')).toBeInTheDocument();
      expect(screen.getByText('50')).toBeInTheDocument();
    });

    it('should not render global resources when empty', () => {
      const state = createMockGameState({ globalResources: {} });
      render(<GameStateViewer state={state} />);

      expect(screen.queryByText('Global Resources')).not.toBeInTheDocument();
    });

    it('should not render global resources when undefined', () => {
      const state = createMockGameState({ globalResources: undefined });
      render(<GameStateViewer state={state} />);

      expect(screen.queryByText('Global Resources')).not.toBeInTheDocument();
    });

    it('should replace underscores with spaces in resource names', () => {
      const state = createMockGameState({
        globalResources: { some_long_resource_name: 42 },
      });
      render(<GameStateViewer state={state} />);

      expect(screen.getByText('some long resource name')).toBeInTheDocument();
    });
  });

  describe('Metadata Display', () => {
    it('should render metadata section when present', () => {
      const state = createMockGameState({
        metadata: { difficulty: 'hard', turnLimit: 20 },
      });
      render(<GameStateViewer state={state} />);

      expect(screen.getByText('Additional Information')).toBeInTheDocument();
      expect(screen.getByText('difficulty')).toBeInTheDocument();
      expect(screen.getByText('hard')).toBeInTheDocument();
    });

    it('should not render metadata when empty', () => {
      const state = createMockGameState({ metadata: {} });
      render(<GameStateViewer state={state} />);

      expect(screen.queryByText('Additional Information')).not.toBeInTheDocument();
    });

    it('should not render metadata when undefined', () => {
      const state = createMockGameState({ metadata: undefined });
      render(<GameStateViewer state={state} />);

      expect(screen.queryByText('Additional Information')).not.toBeInTheDocument();
    });

    it('should convert camelCase keys to readable format', () => {
      const state = createMockGameState({
        metadata: { lastActionTaken: 'draw_card' },
      });
      render(<GameStateViewer state={state} />);

      expect(screen.getByText('last Action Taken')).toBeInTheDocument();
    });

    it('should convert non-string values to string', () => {
      const state = createMockGameState({
        metadata: { isComplete: true, count: 42 },
      });
      render(<GameStateViewer state={state} />);

      expect(screen.getByText('true')).toBeInTheDocument();
      expect(screen.getByText('42')).toBeInTheDocument();
    });
  });

  describe('Players Display', () => {
    it('should render players count in header', () => {
      const state = createMockGameState();
      render(<GameStateViewer state={state} />);

      expect(screen.getByText('Players (2)')).toBeInTheDocument();
    });

    it('should render PlayerStateCard for each player', () => {
      const state = createMockGameState();
      render(<GameStateViewer state={state} />);

      expect(screen.getByTestId('player-card-Alice')).toBeInTheDocument();
      expect(screen.getByTestId('player-card-Bob')).toBeInTheDocument();
    });

    it('should mark current player with isCurrentPlayer prop', () => {
      const state = createMockGameState({ currentPlayerIndex: 1 });
      render(<GameStateViewer state={state} currentPlayerIndex={1} />);

      const bobCard = screen.getByTestId('player-card-Bob');
      expect(bobCard.querySelector('[data-testid="current-player-indicator"]')).toBeInTheDocument();
    });

    it('should render players with editable=false', () => {
      const state = createMockGameState();
      render(<GameStateViewer state={state} />);

      // PlayerStateCard should not have editable indicator
      const aliceCard = screen.getByTestId('player-card-Alice');
      expect(
        aliceCard.querySelector('[data-testid="editable-indicator"]')
      ).not.toBeInTheDocument();
    });

    it('should handle empty players array', () => {
      const state = createMockGameState({ players: [] });
      render(<GameStateViewer state={state} />);

      expect(screen.getByText('Players (0)')).toBeInTheDocument();
    });

    it('should handle single player', () => {
      const state = createMockGameState({
        players: [{ playerName: 'Solo', playerOrder: 1 }],
      });
      render(<GameStateViewer state={state} />);

      expect(screen.getByText('Players (1)')).toBeInTheDocument();
      expect(screen.getByTestId('player-card-Solo')).toBeInTheDocument();
    });
  });

  describe('Current Player Index Prop', () => {
    it('should use currentPlayerIndex prop to highlight correct player', () => {
      const state = createMockGameState();
      render(<GameStateViewer state={state} currentPlayerIndex={0} />);

      const aliceCard = screen.getByTestId('player-card-Alice');
      expect(
        aliceCard.querySelector('[data-testid="current-player-indicator"]')
      ).toBeInTheDocument();

      const bobCard = screen.getByTestId('player-card-Bob');
      expect(
        bobCard.querySelector('[data-testid="current-player-indicator"]')
      ).not.toBeInTheDocument();
    });

    it('should not highlight any player when currentPlayerIndex is undefined', () => {
      const state = createMockGameState();
      render(<GameStateViewer state={state} />);

      const aliceCard = screen.getByTestId('player-card-Alice');
      expect(
        aliceCard.querySelector('[data-testid="current-player-indicator"]')
      ).not.toBeInTheDocument();

      const bobCard = screen.getByTestId('player-card-Bob');
      expect(
        bobCard.querySelector('[data-testid="current-player-indicator"]')
      ).not.toBeInTheDocument();
    });
  });

  describe('Minimal State', () => {
    it('should render with minimal required state', () => {
      const minimalState: GameState = {
        sessionId: 'min-session',
        gameId: 'min-game',
        templateId: 'min-template',
        version: '1.0.0',
        players: [],
      };

      render(<GameStateViewer state={minimalState} />);

      expect(screen.getByTestId('game-state-viewer')).toBeInTheDocument();
      expect(screen.getByText('Game Information')).toBeInTheDocument();
      expect(screen.getByText('Players (0)')).toBeInTheDocument();
    });
  });
});
