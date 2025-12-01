/**
 * GameOverviewTab Component Tests
 *
 * Tests for GameOverviewTab component that displays game metadata and BGG integration.
 * Tests cover rendering, BGG API integration, loading states, and accessibility.
 *
 * Issue #1887 - Batch 14: Test rewrite with correct component text expectations
 */

import { render, screen, waitFor } from '@testing-library/react';
import { GameOverviewTab } from '../GameOverviewTab';
import { api } from '@/lib/api';
import type { Game, BggGameDetails } from '@/lib/api';

// Mock API
vi.mock('@/lib/api', () => ({
  api: {
    bgg: {
      getGameDetails: vi.fn(),
    },
  },
}));

// Mock logger
vi.mock('@/lib/logger', () => ({
  logger: {
    error: vi.fn(),
  },
}));

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

const createMockBggDetails = (overrides?: Partial<BggGameDetails>): BggGameDetails => ({
  id: 13,
  name: 'Catan',
  imageUrl: 'https://example.com/image.jpg',
  description: 'A game about settling an island',
  averageRating: 7.5,
  averageWeight: 2.3,
  usersRated: 50000,
  minAge: 10,
  categories: ['Strategy', 'Family'],
  mechanics: ['Dice Rolling', 'Trading'],
  designers: ['Klaus Teuber'],
  publishers: ['Catan Studio', 'Kosmos'],
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

  describe('BGG Integration', () => {
    it('fetches and displays BGG details when bggId exists', async () => {
      const game = createMockGame({ bggId: 13 });
      const bggDetails = createMockBggDetails();

      (api.bgg.getGameDetails as any).mockResolvedValue(bggDetails);

      render(<GameOverviewTab game={game} />);

      await waitFor(() => {
        expect(screen.getByText('BoardGameGeek Details')).toBeInTheDocument();
      });

      expect(screen.getByText(/7.50/)).toBeInTheDocument(); // Average rating
    });

    it('shows loading state while fetching BGG details', async () => {
      const game = createMockGame({ bggId: 13 });

      (api.bgg.getGameDetails as any).mockImplementation(
        () => new Promise(resolve => setTimeout(() => resolve(createMockBggDetails()), 100))
      );

      render(<GameOverviewTab game={game} />);

      // Component shows skeleton loaders with specific class
      await waitFor(() => {
        const loadingElements = document.querySelectorAll('.animate-pulse');
        expect(loadingElements.length).toBeGreaterThan(0);
      });
    });

    it('displays error when BGG fetch fails', async () => {
      const game = createMockGame({ bggId: 13 });

      (api.bgg.getGameDetails as any).mockRejectedValue(new Error('Network error'));

      render(<GameOverviewTab game={game} />);

      await waitFor(() => {
        expect(screen.getByText(/failed to load boardgamegeek details/i)).toBeInTheDocument();
      });
    });

    it('does not fetch BGG details when no bggId', () => {
      const game = createMockGame({ bggId: null });

      render(<GameOverviewTab game={game} />);

      expect(api.bgg.getGameDetails).not.toHaveBeenCalled();
      expect(screen.queryByText('BoardGameGeek Details')).not.toBeInTheDocument();
    });

    it('displays BGG categories and mechanics', async () => {
      const game = createMockGame({ bggId: 13 });
      const bggDetails = createMockBggDetails({
        categories: ['Strategy', 'Economic'],
        mechanics: ['Dice Rolling', 'Hand Management'],
      });

      (api.bgg.getGameDetails as any).mockResolvedValue(bggDetails);

      render(<GameOverviewTab game={game} />);

      await waitFor(() => {
        expect(screen.getByText('Categories')).toBeInTheDocument();
      });

      expect(screen.getByText('Strategy')).toBeInTheDocument();
      expect(screen.getByText('Economic')).toBeInTheDocument();
      expect(screen.getByText('Dice Rolling')).toBeInTheDocument();
    });

    it('shows BGG link button', async () => {
      const game = createMockGame({ bggId: 13 });

      (api.bgg.getGameDetails as any).mockResolvedValue(createMockBggDetails());

      render(<GameOverviewTab game={game} />);

      await waitFor(() => {
        const link = screen.getByText(/view on boardgamegeek/i);
        expect(link.closest('a')).toHaveAttribute('href', 'https://boardgamegeek.com/boardgame/13');
      });
    });
  });

  describe('Accessibility', () => {
    it('has accessible headings', () => {
      const game = createMockGame();

      render(<GameOverviewTab game={game} />);

      expect(screen.getByText('Game Information')).toBeInTheDocument();
    });

    it('has accessible BGG link with external link indicator', async () => {
      const game = createMockGame({ bggId: 13 });

      (api.bgg.getGameDetails as any).mockResolvedValue(createMockBggDetails());

      render(<GameOverviewTab game={game} />);

      await waitFor(() => {
        const link = screen.getByText(/view on boardgamegeek/i).closest('a');
        expect(link).toHaveAttribute('rel', 'noopener noreferrer');
        expect(link).toHaveAttribute('target', '_blank');
      });
    });
  });
});
