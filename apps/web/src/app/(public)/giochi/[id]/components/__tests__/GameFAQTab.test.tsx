/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { GameFAQTab } from '../GameFAQTab';
import { api } from '@/lib/api';
import type { GameFAQ } from '@/lib/api';

// Mock API
vi.mock('@/lib/api', () => ({
  api: {
    games: {
      getFAQs: vi.fn(),
      upvoteFAQ: vi.fn(),
    },
  },
}));

// Mock data
const mockFAQs: GameFAQ[] = [
  {
    id: 'faq-1',
    question: 'How many players can play?',
    answer: 'The game supports 2-4 players.',
    upvotes: 5,
    gameId: 'game-123',
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
  },
  {
    id: 'faq-2',
    question: 'What is the average playtime?',
    answer: 'Average playtime is 45-60 minutes.',
    upvotes: 3,
    gameId: 'game-123',
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
  },
  {
    id: 'faq-3',
    question: 'Is this suitable for children?',
    answer: 'Recommended for ages 10+.',
    upvotes: 8,
    gameId: 'game-123',
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
  },
];

describe('GameFAQTab', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Loading State', () => {
    it('should show loading spinner initially', async () => {
      (api.games.getFAQs as any).mockImplementation(
        () => new Promise(() => {}) // Never resolves
      );

      render(<GameFAQTab gameId="game-123" gameTitle="Chess" />);

      expect(screen.getByTestId('faq-tab')).toBeInTheDocument();
      expect(screen.getByRole('status')).toBeInTheDocument(); // Spinner
    });

    it('should hide loading spinner after data loads', async () => {
      (api.games.getFAQs as any).mockResolvedValue({ faqs: mockFAQs });

      render(<GameFAQTab gameId="game-123" gameTitle="Chess" />);

      await waitFor(() => {
        expect(screen.queryByRole('status')).not.toBeInTheDocument();
      });
    });
  });

  describe('Error State', () => {
    it('should display error message when API fails', async () => {
      const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {});
      (api.games.getFAQs as any).mockRejectedValue(new Error('Network error'));

      render(<GameFAQTab gameId="game-123" gameTitle="Chess" />);

      await waitFor(() => {
        expect(
          screen.getByText(/Errore nel caricamento delle FAQ/i)
        ).toBeInTheDocument();
      });

      consoleError.mockRestore();
    });

    it('should show destructive alert variant on error', async () => {
      const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {});
      (api.games.getFAQs as any).mockRejectedValue(new Error('API error'));

      render(<GameFAQTab gameId="game-123" gameTitle="Chess" />);

      await waitFor(() => {
        const alert = screen.getByRole('alert');
        expect(alert).toBeInTheDocument();
      });

      consoleError.mockRestore();
    });

    it('should not show FAQs when error occurs', async () => {
      const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {});
      (api.games.getFAQs as any).mockRejectedValue(new Error('Error'));

      render(<GameFAQTab gameId="game-123" gameTitle="Chess" />);

      await waitFor(() => {
        expect(screen.queryByRole('button', { name: /How many/i })).not.toBeInTheDocument();
      });

      consoleError.mockRestore();
    });
  });

  describe('Empty State', () => {
    it('should show empty message when no FAQs available', async () => {
      (api.games.getFAQs as any).mockResolvedValue({ faqs: [] });

      render(<GameFAQTab gameId="game-123" gameTitle="Chess" />);

      await waitFor(() => {
        expect(screen.getByText(/Nessuna FAQ disponibile/i)).toBeInTheDocument();
      });
    });

    it('should display game title in empty message', async () => {
      (api.games.getFAQs as any).mockResolvedValue({ faqs: [] });

      render(<GameFAQTab gameId="game-123" gameTitle="Monopoly" />);

      await waitFor(() => {
        expect(screen.getByText(/per Monopoly/i)).toBeInTheDocument();
      });
    });

    it('should show info about admin adding FAQs', async () => {
      (api.games.getFAQs as any).mockResolvedValue({ faqs: [] });

      render(<GameFAQTab gameId="game-123" gameTitle="Chess" />);

      await waitFor(() => {
        expect(
          screen.getByText(/Le FAQ verranno aggiunte dagli amministratori/i)
        ).toBeInTheDocument();
      });
    });
  });

  describe('FAQ Display', () => {
    it('should render all FAQs from API', async () => {
      (api.games.getFAQs as any).mockResolvedValue({ faqs: mockFAQs });

      render(<GameFAQTab gameId="game-123" gameTitle="Chess" />);

      await waitFor(() => {
        expect(screen.getByText('How many players can play?')).toBeInTheDocument();
        expect(screen.getByText('What is the average playtime?')).toBeInTheDocument();
        expect(screen.getByText('Is this suitable for children?')).toBeInTheDocument();
      });
    });

    it('should display upvote counts for each FAQ', async () => {
      (api.games.getFAQs as any).mockResolvedValue({ faqs: mockFAQs });

      render(<GameFAQTab gameId="game-123" gameTitle="Chess" />);

      await waitFor(() => {
        expect(screen.getByText('5')).toBeInTheDocument(); // FAQ 1 upvotes
        expect(screen.getByText('3')).toBeInTheDocument(); // FAQ 2 upvotes
        expect(screen.getByText('8')).toBeInTheDocument(); // FAQ 3 upvotes
      });
    });

    it('should use accordion for FAQ display', async () => {
      (api.games.getFAQs as any).mockResolvedValue({ faqs: mockFAQs });

      render(<GameFAQTab gameId="game-123" gameTitle="Chess" />);

      await waitFor(() => {
        const accordion = screen.getByTestId('faq-tab').querySelector('[data-state]');
        expect(accordion).toBeInTheDocument();
      });
    });
  });

  describe('Accordion Behavior', () => {
    it('should expand accordion item when clicked', async () => {
      const user = userEvent.setup();
      (api.games.getFAQs as any).mockResolvedValue({ faqs: mockFAQs });

      render(<GameFAQTab gameId="game-123" gameTitle="Chess" />);

      await waitFor(() => {
        expect(screen.getByText('How many players can play?')).toBeInTheDocument();
      });

      // Click to expand
      const trigger = screen.getByText('How many players can play?');
      await user.click(trigger);

      // Answer should be visible
      await waitFor(() => {
        expect(screen.getByText('The game supports 2-4 players.')).toBeVisible();
      });
    });

    it('should collapse accordion item when clicked again', async () => {
      const user = userEvent.setup();
      (api.games.getFAQs as any).mockResolvedValue({ faqs: mockFAQs });

      render(<GameFAQTab gameId="game-123" gameTitle="Chess" />);

      await waitFor(() => {
        expect(screen.getByText('How many players can play?')).toBeInTheDocument();
      });

      const trigger = screen.getByText('How many players can play?');

      // Expand
      await user.click(trigger);
      await waitFor(() => {
        expect(screen.getByText('The game supports 2-4 players.')).toBeVisible();
      });

      // Collapse
      await user.click(trigger);
      await waitFor(() => {
        expect(screen.queryByText('The game supports 2-4 players.')).not.toBeVisible();
      });
    });

    it('should allow only one accordion item open at a time', async () => {
      const user = userEvent.setup();
      (api.games.getFAQs as any).mockResolvedValue({ faqs: mockFAQs });

      render(<GameFAQTab gameId="game-123" gameTitle="Chess" />);

      await waitFor(() => {
        expect(screen.getByText('How many players can play?')).toBeInTheDocument();
      });

      // Open first FAQ
      await user.click(screen.getByText('How many players can play?'));
      await waitFor(() => {
        expect(screen.getByText('The game supports 2-4 players.')).toBeVisible();
      });

      // Open second FAQ
      await user.click(screen.getByText('What is the average playtime?'));
      await waitFor(() => {
        expect(screen.getByText('Average playtime is 45-60 minutes.')).toBeVisible();
        expect(screen.queryByText('The game supports 2-4 players.')).not.toBeVisible();
      });
    });
  });

  describe('Upvote Functionality', () => {
    it('should call upvote API when upvote button clicked', async () => {
      const user = userEvent.setup();
      (api.games.getFAQs as any).mockResolvedValue({ faqs: mockFAQs });
      (api.games.upvoteFAQ as any).mockResolvedValue({
        ...mockFAQs[0],
        upvotes: 6,
      });

      render(<GameFAQTab gameId="game-123" gameTitle="Chess" />);

      await waitFor(() => {
        expect(screen.getByText('5')).toBeInTheDocument();
      });

      // Find and click upvote button for first FAQ
      const upvoteButtons = screen.getAllByRole('button', { name: /thumbs-up/i });
      await user.click(upvoteButtons[0]);

      await waitFor(() => {
        expect(api.games.upvoteFAQ).toHaveBeenCalledWith('faq-1');
      });
    });

    it('should update upvote count after successful upvote', async () => {
      const user = userEvent.setup();
      (api.games.getFAQs as any).mockResolvedValue({ faqs: mockFAQs });
      (api.games.upvoteFAQ as any).mockResolvedValue({
        ...mockFAQs[0],
        upvotes: 6,
      });

      render(<GameFAQTab gameId="game-123" gameTitle="Chess" />);

      await waitFor(() => {
        expect(screen.getByText('5')).toBeInTheDocument();
      });

      const upvoteButtons = screen.getAllByRole('button', { name: /thumbs-up/i });
      await user.click(upvoteButtons[0]);

      await waitFor(() => {
        expect(screen.getByText('6')).toBeInTheDocument();
      });
    });

    it('should disable upvote button during upvote request', async () => {
      const user = userEvent.setup();
      (api.games.getFAQs as any).mockResolvedValue({ faqs: mockFAQs });
      (api.games.upvoteFAQ as any).mockImplementation(
        () => new Promise(resolve => setTimeout(resolve, 100))
      );

      render(<GameFAQTab gameId="game-123" gameTitle="Chess" />);

      await waitFor(() => {
        expect(screen.getByText('5')).toBeInTheDocument();
      });

      const upvoteButtons = screen.getAllByRole('button', { name: /thumbs-up/i });
      await user.click(upvoteButtons[0]);

      // Button should be disabled immediately
      expect(upvoteButtons[0]).toBeDisabled();
    });

    it('should handle upvote error gracefully', async () => {
      const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {});
      const user = userEvent.setup();
      (api.games.getFAQs as any).mockResolvedValue({ faqs: mockFAQs });
      (api.games.upvoteFAQ as any).mockRejectedValue(new Error('Upvote failed'));

      render(<GameFAQTab gameId="game-123" gameTitle="Chess" />);

      await waitFor(() => {
        expect(screen.getByText('5')).toBeInTheDocument();
      });

      const upvoteButtons = screen.getAllByRole('button', { name: /thumbs-up/i });
      await user.click(upvoteButtons[0]);

      // Count should remain unchanged
      await waitFor(() => {
        expect(screen.getByText('5')).toBeInTheDocument();
      });

      consoleError.mockRestore();
    });

    it('should not expand accordion when upvote button clicked', async () => {
      const user = userEvent.setup();
      (api.games.getFAQs as any).mockResolvedValue({ faqs: mockFAQs });
      (api.games.upvoteFAQ as any).mockResolvedValue({
        ...mockFAQs[0],
        upvotes: 6,
      });

      render(<GameFAQTab gameId="game-123" gameTitle="Chess" />);

      await waitFor(() => {
        expect(screen.getByText('How many players can play?')).toBeInTheDocument();
      });

      const upvoteButtons = screen.getAllByRole('button', { name: /thumbs-up/i });
      await user.click(upvoteButtons[0]);

      // Answer should NOT be visible (accordion closed)
      expect(screen.queryByText('The game supports 2-4 players.')).not.toBeVisible();
    });
  });

  describe('API Integration', () => {
    it('should call getFAQs with correct parameters', async () => {
      (api.games.getFAQs as any).mockResolvedValue({ faqs: mockFAQs });

      render(<GameFAQTab gameId="game-456" gameTitle="Chess" />);

      await waitFor(() => {
        expect(api.games.getFAQs).toHaveBeenCalledWith('game-456', 10, 0);
      });
    });

    it('should refetch FAQs when gameId changes', async () => {
      (api.games.getFAQs as any).mockResolvedValue({ faqs: mockFAQs });

      const { rerender } = render(
        <GameFAQTab gameId="game-1" gameTitle="Chess" />
      );

      await waitFor(() => {
        expect(api.games.getFAQs).toHaveBeenCalledWith('game-1', 10, 0);
      });

      // Change gameId
      rerender(<GameFAQTab gameId="game-2" gameTitle="Monopoly" />);

      await waitFor(() => {
        expect(api.games.getFAQs).toHaveBeenCalledWith('game-2', 10, 0);
        expect(api.games.getFAQs).toHaveBeenCalledTimes(2);
      });
    });

    it('should not refetch when only gameTitle changes', async () => {
      (api.games.getFAQs as any).mockResolvedValue({ faqs: mockFAQs });

      const { rerender } = render(
        <GameFAQTab gameId="game-1" gameTitle="Chess" />
      );

      await waitFor(() => {
        expect(api.games.getFAQs).toHaveBeenCalledTimes(1);
      });

      // Change only title
      rerender(<GameFAQTab gameId="game-1" gameTitle="New Title" />);

      // Should not refetch
      expect(api.games.getFAQs).toHaveBeenCalledTimes(1);
    });
  });

  describe('Accessibility', () => {
    it('should have proper ARIA roles for accordion', async () => {
      (api.games.getFAQs as any).mockResolvedValue({ faqs: mockFAQs });

      render(<GameFAQTab gameId="game-123" gameTitle="Chess" />);

      await waitFor(() => {
        const buttons = screen.getAllByRole('button');
        expect(buttons.length).toBeGreaterThan(0);
      });
    });

    it('should have descriptive button labels for upvote', async () => {
      (api.games.getFAQs as any).mockResolvedValue({ faqs: mockFAQs });

      render(<GameFAQTab gameId="game-123" gameTitle="Chess" />);

      await waitFor(() => {
        const upvoteButtons = screen.getAllByRole('button', { name: /thumbs-up/i });
        expect(upvoteButtons).toHaveLength(3);
      });
    });
  });
});
