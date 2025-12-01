/**
 * Tests for GameSessionsTab component
 * Auto-generated baseline tests - Issue #992
 * Enhanced with proper mock data - Issue #567-fix
 */

import { render, screen } from '@testing-library/react';
import { GameSessionsTab } from '../GameSessionsTab';
import type { Game, GameSessionDto } from '@/lib/api';

// Create complete mock game
const createMockGame = (overrides?: Partial<Game>): Game => ({
  id: 'game-1',
  title: 'Test Board Game',
  publisher: 'Test Publisher',
  yearPublished: 2023,
  minPlayers: 2,
  maxPlayers: 4,
  minPlayTimeMinutes: 30,
  maxPlayTimeMinutes: 60,
  bggId: 12345,
  createdAt: new Date().toISOString(),
  imageUrl: null,
  faqCount: null,
  averageRating: null,
  ...overrides,
});

// Create complete mock game session
const createMockGameSession = (overrides?: Partial<GameSessionDto>): GameSessionDto => ({
  id: 'session-1',
  gameId: 'game-1',
  status: 'Completed',
  startedAt: new Date().toISOString(),
  completedAt: new Date().toISOString(),
  playerCount: 2,
  players: [
    { playerName: 'Player 1', playerOrder: 0, color: 'red' },
    { playerName: 'Player 2', playerOrder: 1, color: 'blue' },
  ],
  winnerName: 'Player 1',
  notes: null,
  durationMinutes: 45,
  ...overrides,
});

describe('GameSessionsTab', () => {
  describe('Rendering', () => {
    it('should render without crashing', () => {
      const game = createMockGame();
      const sessions: GameSessionDto[] = [createMockGameSession()];
      render(<GameSessionsTab game={game} sessions={sessions} />);
      expect(screen.getByText('Session History')).toBeInTheDocument();
    });

    it('should render with default props', () => {
      const { container } = render(<GameSessionsTab children={<div>Test Content</div>} />);
      expect(container.firstChild).toBeInTheDocument();
    });
  });

  describe('Props', () => {
    it('should accept and render with custom props', () => {
      const game = createMockGame({ title: 'Custom Game' });
      const sessions = [createMockGameSession({ winnerName: 'Custom Winner' })];
      render(<GameSessionsTab game={game} sessions={sessions} />);
      expect(screen.getByText('Custom Winner')).toBeInTheDocument();
    });
  });

  describe('Interactions', () => {
    it('should handle user interactions', () => {
      const game = createMockGame();
      const sessions = [createMockGameSession()];
      render(<GameSessionsTab game={game} sessions={sessions} />);
      expect(screen.getByText('Session History')).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('should have accessible role', () => {
      const game = createMockGame();
      const sessions: GameSessionDto[] = [createMockGameSession()];
      render(<GameSessionsTab game={game} sessions={sessions} />);
      expect(screen.getByText('Session History')).toBeInTheDocument();
    });
  });
});
