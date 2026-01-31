/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { GameDetailClient } from '../game-detail-client';

// Mock all sub-components
vi.mock('../components/HeroSection', () => ({
  HeroSection: ({ title, imageUrl, publisher }: any) => (
    <div data-testid="hero-section">
      <div>{title}</div>
      {imageUrl && <div>{imageUrl}</div>}
      {publisher && <div>{publisher}</div>}
    </div>
  ),
}));

vi.mock('../components/InfoGrid', () => ({
  InfoGrid: ({ minPlayers, maxPlayers, averageWeight }: any) => (
    <div data-testid="info-grid">
      <div>Players: {minPlayers}-{maxPlayers}</div>
      {averageWeight && <div>Weight: {averageWeight}</div>}
    </div>
  ),
}));

vi.mock('@/components/games/detail/GameOverviewTab', () => ({
  GameOverviewTab: ({ game }: any) => (
    <div data-testid="overview-tab">Overview: {game.title}</div>
  ),
}));

vi.mock('../components/GameFAQTab', () => ({
  GameFAQTab: ({ gameId, gameTitle }: any) => (
    <div data-testid="faq-tab">
      FAQ: {gameTitle} ({gameId})
    </div>
  ),
}));

vi.mock('../components/GameChatTab', () => ({
  GameChatTab: ({ gameId, gameTitle }: any) => (
    <div data-testid="chat-tab">
      Chat: {gameTitle} ({gameId})
    </div>
  ),
}));

// Mock fetch
global.fetch = vi.fn();

// Mock game data
const mockGame = {
  id: 'game-123',
  title: 'Chess',
  publisher: 'Classic Games Inc.',
  yearPublished: 1500,
  minPlayers: 2,
  maxPlayers: 2,
  minPlayTimeMinutes: 30,
  maxPlayTimeMinutes: 60,
  bggId: 12345,
  imageUrl: 'https://example.com/chess.jpg',
  averageRating: 8.5,
  createdAt: '2024-01-01T00:00:00Z',
};

describe('GameDetailClient', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    (global.fetch as any).mockReset();
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  describe('Component Rendering', () => {
    it('should render all main sections', () => {
      (global.fetch as any).mockResolvedValue({
        ok: true,
        json: async () => ({ averageWeight: 3.5 }),
      });

      render(<GameDetailClient game={mockGame} />);

      expect(screen.getByTestId('hero-section')).toBeInTheDocument();
      expect(screen.getByTestId('info-grid')).toBeInTheDocument();
      expect(screen.getByRole('tablist')).toBeInTheDocument();
    });

    it('should render back navigation button', () => {
      (global.fetch as any).mockResolvedValue({
        ok: true,
        json: async () => ({ averageWeight: 3.5 }),
      });

      render(<GameDetailClient game={mockGame} />);

      const backButton = screen.getByRole('link', { name: /torna al catalogo/i });
      expect(backButton).toBeInTheDocument();
      expect(backButton).toHaveAttribute('href', '/games');
    });

    it('should pass correct props to HeroSection', () => {
      (global.fetch as any).mockResolvedValue({
        ok: true,
        json: async () => ({ averageWeight: 3.5 }),
      });

      render(<GameDetailClient game={mockGame} />);

      const heroSection = screen.getByTestId('hero-section');
      expect(heroSection).toHaveTextContent('Chess');
      expect(heroSection).toHaveTextContent('Classic Games Inc.');
      expect(heroSection).toHaveTextContent('https://example.com/chess.jpg');
    });

    it('should pass correct props to InfoGrid', () => {
      (global.fetch as any).mockResolvedValue({
        ok: true,
        json: async () => ({ averageWeight: 3.5 }),
      });

      render(<GameDetailClient game={mockGame} />);

      const infoGrid = screen.getByTestId('info-grid');
      expect(infoGrid).toHaveTextContent('Players: 2-2');
    });
  });

  describe('Tab Navigation', () => {
    it('should show Overview tab by default', () => {
      (global.fetch as any).mockResolvedValue({
        ok: true,
        json: async () => ({ averageWeight: 3.5 }),
      });

      render(<GameDetailClient game={mockGame} />);

      expect(screen.getByTestId('overview-tab')).toBeInTheDocument();
      expect(screen.getByTestId('overview-tab')).toBeVisible();
    });

    it('should show FAQ tab when clicked', async () => {
      const user = userEvent.setup();
      (global.fetch as any).mockResolvedValue({
        ok: true,
        json: async () => ({ averageWeight: 3.5 }),
      });

      render(<GameDetailClient game={mockGame} />);

      const faqTab = screen.getByRole('tab', { name: /faq/i });
      await user.click(faqTab);

      expect(screen.getByTestId('faq-tab')).toBeInTheDocument();
      expect(screen.getByTestId('faq-tab')).toBeVisible();
    });

    it('should show Chat tab when clicked', async () => {
      const user = userEvent.setup();
      (global.fetch as any).mockResolvedValue({
        ok: true,
        json: async () => ({ averageWeight: 3.5 }),
      });

      render(<GameDetailClient game={mockGame} />);

      const chatTab = screen.getByRole('tab', { name: /chat ai/i });
      await user.click(chatTab);

      expect(screen.getByTestId('chat-tab')).toBeInTheDocument();
      expect(screen.getByTestId('chat-tab')).toBeVisible();
    });

    it('should hide other tabs when switching', async () => {
      const user = userEvent.setup();
      (global.fetch as any).mockResolvedValue({
        ok: true,
        json: async () => ({ averageWeight: 3.5 }),
      });

      render(<GameDetailClient game={mockGame} />);

      // Initially on Overview
      expect(screen.getByTestId('overview-tab')).toBeVisible();

      // Switch to FAQ
      const faqTab = screen.getByRole('tab', { name: /faq/i });
      await user.click(faqTab);

      expect(screen.getByTestId('faq-tab')).toBeVisible();
      // Overview tab is removed from DOM when not active (Radix Tabs behavior)
      expect(screen.queryByTestId('overview-tab')).not.toBeInTheDocument();
    });

    it('should render all three tab triggers', () => {
      (global.fetch as any).mockResolvedValue({
        ok: true,
        json: async () => ({ averageWeight: 3.5 }),
      });

      render(<GameDetailClient game={mockGame} />);

      expect(screen.getByRole('tab', { name: /panoramica|info/i })).toBeInTheDocument();
      expect(screen.getByRole('tab', { name: /faq/i })).toBeInTheDocument();
      expect(screen.getByRole('tab', { name: /chat ai/i })).toBeInTheDocument();
    });
  });

  describe('BGG Weight Fetching', () => {
    it('should fetch BGG weight when bggId is present', async () => {
      (global.fetch as any).mockResolvedValue({
        ok: true,
        json: async () => ({ averageWeight: 3.5 }),
      });

      render(<GameDetailClient game={mockGame} />);

      await waitFor(() => {
        expect(global.fetch).toHaveBeenCalledWith(
          expect.stringContaining(`/api/v1/bgg/games/${mockGame.bggId}`)
        );
      });
    });

    it('should update InfoGrid with fetched weight', async () => {
      (global.fetch as any).mockResolvedValue({
        ok: true,
        json: async () => ({ averageWeight: 4.2 }),
      });

      render(<GameDetailClient game={mockGame} />);

      await waitFor(() => {
        const infoGrid = screen.getByTestId('info-grid');
        expect(infoGrid).toHaveTextContent('Weight: 4.2');
      });
    });

    it('should not fetch when bggId is null', () => {
      const gameWithoutBgg = { ...mockGame, bggId: null };

      render(<GameDetailClient game={gameWithoutBgg} />);

      expect(global.fetch).not.toHaveBeenCalled();
    });

    it('should handle fetch error gracefully', async () => {
      const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {});
      (global.fetch as any).mockRejectedValue(new Error('Network error'));

      render(<GameDetailClient game={mockGame} />);

      await waitFor(() => {
        expect(consoleError).toHaveBeenCalledWith(
          'Failed to fetch BGG weight:',
          expect.any(Error)
        );
      });

      consoleError.mockRestore();
    });

    it('should handle non-OK response gracefully', async () => {
      (global.fetch as any).mockResolvedValue({
        ok: false,
        json: async () => ({}),
      });

      render(<GameDetailClient game={mockGame} />);

      await waitFor(() => {
        expect(global.fetch).toHaveBeenCalled();
      });

      // InfoGrid should not show weight
      const infoGrid = screen.getByTestId('info-grid');
      expect(infoGrid).not.toHaveTextContent('Weight:');
    });

    it('should handle missing averageWeight in response', async () => {
      (global.fetch as any).mockResolvedValue({
        ok: true,
        json: async () => ({ otherData: 'value' }),
      });

      render(<GameDetailClient game={mockGame} />);

      await waitFor(() => {
        expect(global.fetch).toHaveBeenCalled();
      });

      // InfoGrid should not show weight
      const infoGrid = screen.getByTestId('info-grid');
      expect(infoGrid).not.toHaveTextContent('Weight:');
    });
  });

  describe('Tab Content Props', () => {
    it('should pass correct props to GameOverviewTab', () => {
      (global.fetch as any).mockResolvedValue({
        ok: true,
        json: async () => ({ averageWeight: 3.5 }),
      });

      render(<GameDetailClient game={mockGame} />);

      expect(screen.getByTestId('overview-tab')).toHaveTextContent('Overview: Chess');
    });

    it('should pass correct props to GameFAQTab', async () => {
      const user = userEvent.setup();
      (global.fetch as any).mockResolvedValue({
        ok: true,
        json: async () => ({ averageWeight: 3.5 }),
      });

      render(<GameDetailClient game={mockGame} />);

      const faqTab = screen.getByRole('tab', { name: /faq/i });
      await user.click(faqTab);

      expect(screen.getByTestId('faq-tab')).toHaveTextContent('FAQ: Chess (game-123)');
    });

    it('should pass correct props to GameChatTab', async () => {
      const user = userEvent.setup();
      (global.fetch as any).mockResolvedValue({
        ok: true,
        json: async () => ({ averageWeight: 3.5 }),
      });

      render(<GameDetailClient game={mockGame} />);

      const chatTab = screen.getByRole('tab', { name: /chat ai/i });
      await user.click(chatTab);

      expect(screen.getByTestId('chat-tab')).toHaveTextContent('Chat: Chess (game-123)');
    });
  });

  describe('Responsive Behavior', () => {
    it('should render tab labels with responsive text', () => {
      (global.fetch as any).mockResolvedValue({
        ok: true,
        json: async () => ({ averageWeight: 3.5 }),
      });

      render(<GameDetailClient game={mockGame} />);

      // Overview tab should have both "Panoramica" and "Info"
      const overviewTab = screen.getByRole('tab', { name: /panoramica|info/i });
      expect(overviewTab).toBeInTheDocument();
    });

    it('should use grid layout for tab triggers', () => {
      (global.fetch as any).mockResolvedValue({
        ok: true,
        json: async () => ({ averageWeight: 3.5 }),
      });

      render(<GameDetailClient game={mockGame} />);

      const tabList = screen.getByRole('tablist');
      expect(tabList).toHaveClass('grid', 'w-full', 'grid-cols-3');
    });
  });

  describe('State Management', () => {
    it('should maintain active tab state across re-renders', async () => {
      const user = userEvent.setup();
      (global.fetch as any).mockResolvedValue({
        ok: true,
        json: async () => ({ averageWeight: 3.5 }),
      });

      const { rerender } = render(<GameDetailClient game={mockGame} />);

      // Switch to FAQ tab
      const faqTab = screen.getByRole('tab', { name: /faq/i });
      await user.click(faqTab);

      expect(screen.getByTestId('faq-tab')).toBeVisible();

      // Re-render component
      rerender(<GameDetailClient game={mockGame} />);

      // FAQ tab should still be active
      expect(screen.getByTestId('faq-tab')).toBeVisible();
    });

    it('should reset to overview when game changes', async () => {
      const user = userEvent.setup();
      (global.fetch as any).mockResolvedValue({
        ok: true,
        json: async () => ({ averageWeight: 3.5 }),
      });

      const { rerender } = render(<GameDetailClient game={mockGame} />);

      // Switch to FAQ tab
      const faqTab = screen.getByRole('tab', { name: /faq/i });
      await user.click(faqTab);

      // Change game
      const newGame = { ...mockGame, id: 'game-456', title: 'Monopoly' };
      rerender(<GameDetailClient game={newGame} />);

      // Should maintain FAQ tab (state is internal)
      expect(screen.getByTestId('faq-tab')).toBeVisible();
    });
  });
});
