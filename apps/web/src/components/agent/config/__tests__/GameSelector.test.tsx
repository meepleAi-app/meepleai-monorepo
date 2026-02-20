/**
 * GameSelector Component Tests
 * Issue #4774: GameSelector API Integration
 *
 * Tests the GameSelector connected to real API via React Query.
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import React from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { Mock } from 'vitest';

import { createTestQueryClient } from '@/__tests__/utils/query-test-utils';

// Mock API module
vi.mock('@/lib/api', () => ({
  api: {
    library: {
      getLibrary: vi.fn(),
    },
  },
}));

import { GameSelector } from '../GameSelector';
import { api } from '@/lib/api';

const mockLibraryResponse = {
  items: [
    {
      id: 'entry-1',
      userId: 'user-1',
      gameId: 'game-1',
      gameTitle: 'Catan',
      gamePublisher: 'Kosmos',
      gameYearPublished: 1995,
      gameIconUrl: null,
      gameImageUrl: null,
      addedAt: '2024-01-01T00:00:00Z',
      notes: null,
      isFavorite: true,
      currentState: 'Owned',
      stateChangedAt: null,
      stateNotes: null,
      hasPdfDocuments: true,
    },
    {
      id: 'entry-2',
      userId: 'user-1',
      gameId: 'game-2',
      gameTitle: 'Terraforming Mars',
      gamePublisher: 'FryxGames',
      gameYearPublished: 2016,
      gameIconUrl: null,
      gameImageUrl: null,
      addedAt: '2024-01-02T00:00:00Z',
      notes: null,
      isFavorite: false,
      currentState: 'Owned',
      stateChangedAt: null,
      stateNotes: null,
      hasPdfDocuments: false,
    },
    {
      id: 'entry-3',
      userId: 'user-1',
      gameId: 'game-3',
      gameTitle: 'Wingspan',
      gamePublisher: 'Stonemaier Games',
      gameYearPublished: 2019,
      gameIconUrl: null,
      gameImageUrl: null,
      addedAt: '2024-01-03T00:00:00Z',
      notes: null,
      isFavorite: false,
      currentState: 'Owned',
      stateChangedAt: null,
      stateNotes: null,
      hasPdfDocuments: true,
    },
  ],
  page: 1,
  pageSize: 100,
  totalCount: 3,
  totalPages: 1,
  hasNextPage: false,
  hasPreviousPage: false,
};

describe('GameSelector', () => {
  let queryClient: QueryClient;
  const mockOnChange = vi.fn();

  beforeEach(() => {
    queryClient = createTestQueryClient();
    vi.clearAllMocks();
  });

  const wrapper = ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );

  describe('Loading State', () => {
    it('shows loading state while fetching', () => {
      (api.library.getLibrary as Mock).mockImplementation(() => new Promise(() => {}));

      render(<GameSelector onChange={mockOnChange} />, { wrapper });

      expect(screen.getByText('Loading games...')).toBeInTheDocument();
    });
  });

  describe('Loaded State', () => {
    beforeEach(() => {
      (api.library.getLibrary as Mock).mockResolvedValue(mockLibraryResponse);
    });

    it('renders Select Game label', async () => {
      render(<GameSelector onChange={mockOnChange} />, { wrapper });

      await waitFor(() => {
        expect(screen.getByText('Select Game')).toBeInTheDocument();
      });
    });

    it('renders required asterisk', async () => {
      render(<GameSelector onChange={mockOnChange} />, { wrapper });

      await waitFor(() => {
        expect(screen.getByText('*')).toBeInTheDocument();
      });
    });

    it('renders placeholder text when no value', async () => {
      render(<GameSelector onChange={mockOnChange} />, { wrapper });

      await waitFor(() => {
        expect(screen.getByText('Choose a game...')).toBeInTheDocument();
      });
    });

    it('opens dropdown with games when clicked', async () => {
      render(<GameSelector onChange={mockOnChange} />, { wrapper });

      await waitFor(() => {
        expect(screen.queryByText('Loading games...')).not.toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole('combobox'));

      expect(screen.getByText('Catan')).toBeInTheDocument();
      expect(screen.getByText('Terraforming Mars')).toBeInTheDocument();
      expect(screen.getByText('Wingspan')).toBeInTheDocument();
    });

    it('shows publisher for games', async () => {
      render(<GameSelector onChange={mockOnChange} />, { wrapper });

      await waitFor(() => {
        expect(screen.queryByText('Loading games...')).not.toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole('combobox'));

      expect(screen.getByText('Kosmos')).toBeInTheDocument();
      expect(screen.getByText('FryxGames')).toBeInTheDocument();
    });

    it('shows Rulebook badge for games with PDF', async () => {
      render(<GameSelector onChange={mockOnChange} />, { wrapper });

      await waitFor(() => {
        expect(screen.queryByText('Loading games...')).not.toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole('combobox'));

      const rulebookBadges = screen.getAllByText(/Rulebook/);
      expect(rulebookBadges.length).toBe(2); // Catan and Wingspan have PDF
    });

    it('shows favorite indicator', async () => {
      render(<GameSelector onChange={mockOnChange} />, { wrapper });

      await waitFor(() => {
        expect(screen.queryByText('Loading games...')).not.toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole('combobox'));

      expect(screen.getByText('★')).toBeInTheDocument(); // Catan is favorite
    });

    it('calls onChange with gameId and game object when selected', async () => {
      render(<GameSelector onChange={mockOnChange} />, { wrapper });

      await waitFor(() => {
        expect(screen.queryByText('Loading games...')).not.toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole('combobox'));
      fireEvent.click(screen.getByText('Catan'));

      expect(mockOnChange).toHaveBeenCalledWith('game-1', expect.objectContaining({
        gameId: 'game-1',
        gameTitle: 'Catan',
      }));
    });

    it('shows selected game in trigger when value provided', async () => {
      render(<GameSelector value="game-1" onChange={mockOnChange} />, { wrapper });

      await waitFor(() => {
        expect(screen.queryByText('Loading games...')).not.toBeInTheDocument();
      });

      expect(screen.getByText('Catan')).toBeInTheDocument();
      expect(screen.queryByText('Choose a game...')).not.toBeInTheDocument();
    });
  });

  describe('Error State', () => {
    it('shows error message when API fails', async () => {
      (api.library.getLibrary as Mock).mockRejectedValue(new Error('Network error'));

      render(<GameSelector onChange={mockOnChange} />, { wrapper });

      await waitFor(() => {
        expect(screen.getByText('Failed to load games. Please try again.')).toBeInTheDocument();
      });
    });
  });

  describe('Empty State', () => {
    it('shows empty message when no games in library', async () => {
      (api.library.getLibrary as Mock).mockResolvedValue({
        ...mockLibraryResponse,
        items: [],
        totalCount: 0,
      });

      render(<GameSelector onChange={mockOnChange} />, { wrapper });

      await waitFor(() => {
        expect(screen.queryByText('Loading games...')).not.toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole('combobox'));

      expect(screen.getByText('No games in your library')).toBeInTheDocument();
    });
  });

  describe('Disabled State', () => {
    it('disables the select when disabled prop is true', async () => {
      (api.library.getLibrary as Mock).mockResolvedValue(mockLibraryResponse);

      render(<GameSelector onChange={mockOnChange} disabled />, { wrapper });

      await waitFor(() => {
        expect(screen.queryByText('Loading games...')).not.toBeInTheDocument();
      });

      const trigger = screen.getByRole('combobox');
      expect(trigger).toBeDisabled();
    });
  });
});
