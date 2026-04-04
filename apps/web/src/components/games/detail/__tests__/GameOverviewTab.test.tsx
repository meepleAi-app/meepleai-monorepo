/**
 * GameOverviewTab Component Tests
 *
 * Tests for GameOverviewTab component that displays game metadata.
 *
 * Issue #1887 - Batch 14: Test rewrite with correct component text expectations
 */

import { render, screen } from '@testing-library/react';
import { GameOverviewTab } from '../GameOverviewTab';
import type { Game } from '@/lib/api';

const createMockGame = (overrides?: Partial<Game>): Game => ({
  id: 'game-1',
  title: 'Catan',
  publisher: 'Catan Studio',
  yearPublished: 1995,
  minPlayers: 3,
  maxPlayers: 4,
  minPlayTimeMinutes: 60,
  maxPlayTimeMinutes: 120,
  bggId: 13,
  createdAt: new Date().toISOString(),
  imageUrl: null,
  faqCount: null,
  averageRating: null,
  ...overrides,
});

describe('GameOverviewTab', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('renders game information card', () => {
      const game = createMockGame();

      render(<GameOverviewTab game={game} />);

      expect(screen.getByText('Game Information')).toBeInTheDocument();
      expect(screen.getByText('Core game details and specifications')).toBeInTheDocument();
    });

    it('displays game publisher', () => {
      const game = createMockGame({ publisher: 'Test Publisher' });

      render(<GameOverviewTab game={game} />);

      expect(screen.getByText('Publisher')).toBeInTheDocument();
      expect(screen.getByText('Test Publisher')).toBeInTheDocument();
    });

    it('displays year published', () => {
      const game = createMockGame({ yearPublished: 2020 });

      render(<GameOverviewTab game={game} />);

      expect(screen.getByText('Year Published')).toBeInTheDocument();
      expect(screen.getByText('2020')).toBeInTheDocument();
    });

    it('displays player count range', () => {
      const game = createMockGame({ minPlayers: 2, maxPlayers: 5 });

      render(<GameOverviewTab game={game} />);

      expect(screen.getByText('Players')).toBeInTheDocument();
      expect(screen.getByText('2-5 players')).toBeInTheDocument();
    });

    it('displays single player count when min equals max', () => {
      const game = createMockGame({ minPlayers: 4, maxPlayers: 4 });

      render(<GameOverviewTab game={game} />);

      expect(screen.getByText('4 players')).toBeInTheDocument();
    });

    it('displays play time range', () => {
      const game = createMockGame({ minPlayTimeMinutes: 30, maxPlayTimeMinutes: 90 });

      render(<GameOverviewTab game={game} />);

      expect(screen.getByText('Play Time')).toBeInTheDocument();
      expect(screen.getByText('30-90 minutes')).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('has accessible headings', () => {
      const game = createMockGame();

      render(<GameOverviewTab game={game} />);

      expect(screen.getByText('Game Information')).toBeInTheDocument();
    });
  });
});
