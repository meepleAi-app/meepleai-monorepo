/**
 * Tests for GameOverviewTab component
 * Auto-generated baseline tests - Issue #992
 * Enhanced with proper mock data - Issue #567-fix
 */

import { render, screen } from '@testing-library/react';
import { vi } from 'vitest';
import { GameOverviewTab } from '../GameOverviewTab';
import type { Game } from '@/lib/api';

// Mock the API module
vi.mock('@/lib/api', () => ({
  api: {
    bgg: {
      getGameDetails: vi.fn(),
    },
  },
}));

// Create a complete mock game with all required properties
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

describe('GameOverviewTab', () => {
  describe('Rendering', () => {
    it('should render without crashing', () => {
      const game = createMockGame();
      render(<GameOverviewTab game={game} />);
      expect(screen.getByText('Game Information')).toBeInTheDocument();
    });

    it('should render with default props', () => {
      const game = createMockGame();
      const { container } = render(<GameOverviewTab game={game} />);
      expect(container.firstChild).toBeInTheDocument();
      expect(screen.getByText('Game Information')).toBeInTheDocument();
    });
  });

  describe('Props', () => {
    it('should accept and render with custom props', () => {
      const game = createMockGame({
        title: 'Custom Game',
        publisher: 'Custom Publisher',
      });
      render(<GameOverviewTab game={game} />);
      expect(screen.getByText('Custom Game')).toBeInTheDocument();
      expect(screen.getByText('Custom Publisher')).toBeInTheDocument();
    });

    it('should handle game without BGG ID', () => {
      const game = createMockGame({ bggId: null });
      render(<GameOverviewTab game={game} />);
      expect(screen.getByText('Game Information')).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('should have accessible headings', () => {
      const game = createMockGame();
      render(<GameOverviewTab game={game} />);
      expect(screen.getByText('Game Information')).toBeInTheDocument();
    });
  });
});
