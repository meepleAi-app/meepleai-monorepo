/**
 * @vitest-environment jsdom
 *
 * Integration Tests for Game Detail Page Flow (Issue #2842)
 *
 * Tests critical user journeys across multiple components:
 * - GameDetailClient → store → API
 * - Tab switching → data loading
 * - Optimistic updates → error recovery
 */

import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { act, renderHook } from '@testing-library/react';

import { GameDetailClient } from '../../game-detail-client';
import { useGameDetailStore } from '@/lib/stores/useGameDetailStore';
import { api } from '@/lib/api';
import type { GameFAQ } from '@/lib/api';

// Mock API
vi.mock('@/lib/api', () => ({
  api: {
    games: {
      getFAQs: vi.fn(),
      upvoteFAQ: vi.fn(),
      getQuickQuestions: vi.fn(),
    },
  },
}));

// Mock BGG fetch
global.fetch = vi.fn();

const mockGame = {
  id: 'game-integration-123',
  title: 'Integration Test Game',
  publisher: 'Test Publisher',
  yearPublished: 2024,
  minPlayers: 2,
  maxPlayers: 4,
  minPlayTimeMinutes: 30,
  maxPlayTimeMinutes: 90,
  bggId: 54321,
  imageUrl: 'https://example.com/game.jpg',
  averageRating: 7.5,
  createdAt: '2024-01-01T00:00:00Z',
};

const mockFAQs: GameFAQ[] = [
  {
    id: 'faq-int-1',
    question: 'Integration test question?',
    answer: 'Integration test answer.',
    upvotes: 10,
    gameId: 'game-integration-123',
    createdAt: '2024-01-01T00:00:00Z',
    updatedAt: '2024-01-01T00:00:00Z',
  },
];

describe('Game Detail Flow Integration Tests', () => {
  beforeEach(() => {
    vi.clearAllMocks();

    // Reset store
    act(() => {
      useGameDetailStore.getState().reset();
    });

    // Default API mocks
    (api.games.getFAQs as any).mockResolvedValue({ faqs: mockFAQs });
    (api.games.upvoteFAQ as any).mockResolvedValue({
      ...mockFAQs[0],
      upvotes: 11,
    });
    (api.games.getQuickQuestions as any).mockResolvedValue([
      { id: 'q1', text: 'Quick question 1' },
    ]);

    // Mock BGG fetch
    (global.fetch as any).mockResolvedValue({
      ok: true,
      json: async () => ({ averageWeight: 3.2 }),
    });
  });

  describe('Complete User Journey: View Game → Change State', () => {
    it('should load game, update store, and verify state', async () => {
      const user = userEvent.setup();

      render(<GameDetailClient game={mockGame} />);

      // 1. Verify game loaded
      expect(screen.getByText('Integration Test Game')).toBeInTheDocument();

      // 2. Update store state
      const { result } = renderHook(() => useGameDetailStore());
      act(() => {
        result.current.setGameId('game-integration-123');
        result.current.setCurrentState('Wishlist');
      });

      // 3. Verify store update
      expect(result.current.gameId).toBe('game-integration-123');
      expect(result.current.currentState).toBe('Wishlist');

      // 4. Simulate state change with optimistic update
      act(() => {
        result.current.setIsUpdatingState(true);
        result.current.setCurrentState('Owned');
      });

      expect(result.current.currentState).toBe('Owned');
      expect(result.current.isUpdatingState).toBe(true);

      // 5. Finalize update
      act(() => {
        result.current.setIsUpdatingState(false);
      });

      expect(result.current.currentState).toBe('Owned');
      expect(result.current.isUpdatingState).toBe(false);
    });
  });

  describe('FAQ Upvote Flow with Store Integration', () => {
    it('should upvote FAQ and update state', async () => {
      const user = userEvent.setup();

      render(<GameDetailClient game={mockGame} />);

      // Switch to FAQ tab
      const faqTab = screen.getByRole('tab', { name: /faq/i });
      await user.click(faqTab);

      // Wait for FAQ to load
      await waitFor(() => {
        expect(screen.getByText('Integration test question?')).toBeInTheDocument();
      });

      // Get initial upvote count
      expect(screen.getByText('10')).toBeInTheDocument();

      // Click upvote button
      const upvoteButtons = screen.getAllByRole('button', { name: /thumbs-up/i });
      await user.click(upvoteButtons[0]);

      // Wait for upvote to process
      await waitFor(() => {
        expect(api.games.upvoteFAQ).toHaveBeenCalledWith('faq-int-1');
        expect(screen.getByText('11')).toBeInTheDocument();
      });
    });

    it('should handle concurrent FAQ upvotes', async () => {
      const user = userEvent.setup();

      // Add more FAQs
      const multipleFAQs = [
        mockFAQs[0],
        {
          id: 'faq-int-2',
          question: 'Second FAQ?',
          answer: 'Second answer.',
          upvotes: 5,
          gameId: 'game-integration-123',
          createdAt: new Date().toISOString(),
          updatedAt: new Date().toISOString(),
        },
      ];

      (api.games.getFAQs as any).mockResolvedValue({ faqs: multipleFAQs });
      (api.games.upvoteFAQ as any).mockImplementation((id: string) =>
        Promise.resolve({
          ...multipleFAQs.find((f) => f.id === id)!,
          upvotes: id === 'faq-int-1' ? 11 : 6,
        })
      );

      render(<GameDetailClient game={mockGame} />);

      // Switch to FAQ tab
      await user.click(screen.getByRole('tab', { name: /faq/i }));

      await waitFor(() => {
        expect(screen.getByText('Integration test question?')).toBeInTheDocument();
        expect(screen.getByText('Second FAQ?')).toBeInTheDocument();
      });

      // Upvote both FAQs
      const upvoteButtons = screen.getAllByRole('button', { name: /thumbs-up/i });
      await user.click(upvoteButtons[0]);
      await user.click(upvoteButtons[1]);

      // Both should update
      await waitFor(() => {
        expect(screen.getByText('11')).toBeInTheDocument();
        expect(screen.getByText('6')).toBeInTheDocument();
      });
    });
  });

  describe('Tab Switching with Data Persistence', () => {
    it('should maintain FAQ data when switching tabs', async () => {
      const user = userEvent.setup();

      render(<GameDetailClient game={mockGame} />);

      // Switch to FAQ tab
      await user.click(screen.getByRole('tab', { name: /faq/i }));

      await waitFor(() => {
        expect(screen.getByText('Integration test question?')).toBeInTheDocument();
      });

      // Switch to Chat tab
      await user.click(screen.getByRole('tab', { name: /chat ai/i }));

      // Switch back to FAQ
      await user.click(screen.getByRole('tab', { name: /faq/i }));

      // FAQ should still be there (not refetched)
      expect(screen.getByText('Integration test question?')).toBeInTheDocument();
      expect(api.games.getFAQs).toHaveBeenCalledTimes(1); // Only initial fetch
    });

    it('should load different tab content independently', async () => {
      const user = userEvent.setup();

      render(<GameDetailClient game={mockGame} />);

      // Load FAQ tab
      await user.click(screen.getByRole('tab', { name: /faq/i }));

      await waitFor(() => {
        expect(api.games.getFAQs).toHaveBeenCalled();
      });

      // Load Chat tab
      await user.click(screen.getByRole('tab', { name: /chat ai/i }));

      await waitFor(() => {
        expect(api.games.getQuickQuestions).toHaveBeenCalled();
      });

      // Both tabs loaded independently
      expect(api.games.getFAQs).toHaveBeenCalledTimes(1);
      expect(api.games.getQuickQuestions).toHaveBeenCalledTimes(1);
    });
  });

  describe('Error Recovery Scenarios', () => {
    it('should recover from FAQ fetch error and retry', async () => {
      const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {});
      const user = userEvent.setup();

      // First attempt fails
      (api.games.getFAQs as any).mockRejectedValueOnce(new Error('Network error'));
      // Second attempt succeeds
      (api.games.getFAQs as any).mockResolvedValueOnce({ faqs: mockFAQs });

      render(<GameDetailClient game={mockGame} />);

      // Switch to FAQ tab (will fail)
      await user.click(screen.getByRole('tab', { name: /faq/i }));

      await waitFor(() => {
        expect(screen.getByText(/errore nel caricamento/i)).toBeInTheDocument();
      });

      // Re-render component to trigger retry
      const { rerender } = render(<GameDetailClient game={mockGame} />);
      rerender(<GameDetailClient game={mockGame} />);

      // Switch to FAQ tab again (should succeed)
      await user.click(screen.getByRole('tab', { name: /faq/i }));

      await waitFor(() => {
        expect(screen.getByText('Integration test question?')).toBeInTheDocument();
      });

      consoleError.mockRestore();
    });

    it('should handle BGG fetch failure gracefully', async () => {
      const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {});
      (global.fetch as any).mockRejectedValue(new Error('BGG API down'));

      render(<GameDetailClient game={mockGame} />);

      // Page should still render
      expect(screen.getByText('Integration Test Game')).toBeInTheDocument();

      // InfoGrid should render without weight
      const infoGrid = screen.getByText(/players:/i);
      expect(infoGrid).toBeInTheDocument();

      consoleError.mockRestore();
    });
  });

  describe('Store State Synchronization', () => {
    it('should sync store state across component lifecycle', async () => {
      const user = userEvent.setup();

      const { result } = renderHook(() => useGameDetailStore());

      // Set initial store state
      act(() => {
        result.current.setGameId('game-integration-123');
        result.current.setCurrentState('Nuovo');
      });

      render(<GameDetailClient game={mockGame} />);

      // Store state should persist
      expect(result.current.gameId).toBe('game-integration-123');
      expect(result.current.currentState).toBe('Nuovo');

      // Simulate state update
      act(() => {
        result.current.setIsUpdatingState(true);
        result.current.setCurrentState('Owned');
      });

      // State should update
      expect(result.current.currentState).toBe('Owned');
      expect(result.current.isUpdatingState).toBe(true);

      // Finalize
      act(() => {
        result.current.setIsUpdatingState(false);
      });

      expect(result.current.isUpdatingState).toBe(false);
    });

    it('should handle optimistic session recording flow', async () => {
      const { result } = renderHook(() => useGameDetailStore());

      render(<GameDetailClient game={mockGame} />);

      // Start recording
      act(() => {
        result.current.setGameId('game-integration-123');
        result.current.setIsRecordingSession(true);
        result.current.setOptimisticSessionId('temp-session-abc');
      });

      expect(result.current.isRecordingSession).toBe(true);
      expect(result.current.optimisticSessionId).toBe('temp-session-abc');

      // Simulate API success
      act(() => {
        result.current.setOptimisticSessionId('real-session-xyz');
        result.current.setIsRecordingSession(false);
      });

      expect(result.current.optimisticSessionId).toBe('real-session-xyz');
      expect(result.current.isRecordingSession).toBe(false);
    });
  });
});
