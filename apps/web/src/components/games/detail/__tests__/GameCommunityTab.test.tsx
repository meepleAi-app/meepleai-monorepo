/**
 * Tests for GameCommunityTab component
 * Auto-generated baseline tests - Issue #992
 * Enhanced with proper mock data - Issue #567-fix
 */

import { render, screen } from '@testing-library/react';
import { GameCommunityTab } from '../GameCommunityTab';
import type { Game } from '@/lib/api';

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

describe('GameCommunityTab', () => {
  describe('Rendering', () => {
    it('should render without crashing', () => {
      const game = createMockGame();
      render(<GameCommunityTab game={game} />);
      expect(screen.getByRole('region')).toBeInTheDocument();
    });

    it('should render with default props', () => {
      const game = createMockGame();
      const { container } = render(<GameCommunityTab game={game} />);
      expect(container.firstChild).toBeInTheDocument();
    });
  });

  describe('Props', () => {
    it('should accept and render with custom props', () => {
      const game = createMockGame({ title: 'Custom Game' });
      render(<GameCommunityTab game={game} />);
      expect(screen.getByRole('region')).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('should have accessible role', () => {
      const game = createMockGame();
      render(<GameCommunityTab game={game} />);
      expect(screen.getByRole('region')).toBeInTheDocument();
    });
  });
});
