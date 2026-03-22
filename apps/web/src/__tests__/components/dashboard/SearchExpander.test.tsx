import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';

import { SearchExpander } from '@/components/dashboard/SearchExpander';
import type { SharedGameSearchResult } from '@/components/dashboard/SearchExpander';

// Mock the api module (project pattern: import { api } from '@/lib/api')
const mockSearch = vi.fn();
vi.mock('@/lib/api', () => ({
  api: {
    sharedGames: {
      search: (...args: unknown[]) => mockSearch(...args),
    },
  },
}));

// Mock the user library hook
// useLibrary returns UseQueryResult<PaginatedLibraryResponse>
// PaginatedLibraryResponse has items: UserLibraryEntry[] with gameId field
vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibrary: () => ({
    data: {
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 20,
      totalPages: 0,
      hasNextPage: false,
      hasPreviousPage: false,
    },
    isLoading: false,
  }),
}));

// Mock next/navigation
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

describe('SearchExpander', () => {
  const mockOnViewGame = vi.fn();
  const mockOnAskAboutGame = vi.fn<(game: SharedGameSearchResult) => void>();
  const mockOnClose = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    mockSearch.mockResolvedValue({
      items: [
        {
          id: 'g1',
          title: 'Catan',
          thumbnailUrl: '/catan.jpg',
          imageUrl: '/catan-full.jpg',
          minPlayers: 3,
          maxPlayers: 4,
          yearPublished: 1995,
          playingTimeMinutes: 90,
          minAge: 10,
          description: 'A strategy game',
          complexityRating: 2.3,
          averageRating: 7.2,
          status: 'Published',
          isRagPublic: false,
          createdAt: '2024-01-01T00:00:00Z',
          modifiedAt: null,
          bggId: 13,
        },
        {
          id: 'g2',
          title: 'Catan: Seafarers',
          thumbnailUrl: '/seafarers.jpg',
          imageUrl: '/seafarers-full.jpg',
          minPlayers: 3,
          maxPlayers: 4,
          yearPublished: 1997,
          playingTimeMinutes: 120,
          minAge: 10,
          description: 'An expansion',
          complexityRating: 2.5,
          averageRating: 7.0,
          status: 'Published',
          isRagPublic: false,
          createdAt: '2024-01-01T00:00:00Z',
          modifiedAt: null,
          bggId: 325,
        },
      ],
      total: 2,
      page: 1,
      pageSize: 5,
    });
  });

  it('renders search input when open', () => {
    render(
      <SearchExpander
        isOpen={true}
        onClose={mockOnClose}
        onViewGame={mockOnViewGame}
        onAskAboutGame={mockOnAskAboutGame}
      />
    );
    expect(screen.getByPlaceholderText(/cerca un gioco/i)).toBeInTheDocument();
  });

  it('does not render when closed', () => {
    render(
      <SearchExpander
        isOpen={false}
        onClose={mockOnClose}
        onViewGame={mockOnViewGame}
        onAskAboutGame={mockOnAskAboutGame}
      />
    );
    expect(screen.queryByPlaceholderText(/cerca un gioco/i)).not.toBeInTheDocument();
  });

  it('searches after debounce and shows results', async () => {
    render(
      <SearchExpander
        isOpen={true}
        onClose={mockOnClose}
        onViewGame={mockOnViewGame}
        onAskAboutGame={mockOnAskAboutGame}
      />
    );

    const input = screen.getByPlaceholderText(/cerca un gioco/i);
    await act(async () => {
      fireEvent.change(input, { target: { value: 'Catan' } });
    });

    await waitFor(() => {
      expect(mockSearch).toHaveBeenCalledWith(
        expect.objectContaining({ searchTerm: 'Catan', pageSize: 5 })
      );
    });

    await waitFor(() => {
      expect(screen.getByText('Catan')).toBeInTheDocument();
      expect(screen.getByText('Catan: Seafarers')).toBeInTheDocument();
    });
  });

  it('calls onViewGame when "Vedi" is clicked', async () => {
    render(
      <SearchExpander
        isOpen={true}
        onClose={mockOnClose}
        onViewGame={mockOnViewGame}
        onAskAboutGame={mockOnAskAboutGame}
      />
    );

    const input = screen.getByPlaceholderText(/cerca un gioco/i);
    await act(async () => {
      fireEvent.change(input, { target: { value: 'Catan' } });
    });

    await waitFor(() => {
      expect(screen.getByText('Catan')).toBeInTheDocument();
    });

    const vediButtons = screen.getAllByText(/vedi/i);
    fireEvent.click(vediButtons[0]);
    expect(mockOnViewGame).toHaveBeenCalledWith('g1');
  });

  it('calls onAskAboutGame when "Chiedi" is clicked', async () => {
    render(
      <SearchExpander
        isOpen={true}
        onClose={mockOnClose}
        onViewGame={mockOnViewGame}
        onAskAboutGame={mockOnAskAboutGame}
      />
    );

    const input = screen.getByPlaceholderText(/cerca un gioco/i);
    await act(async () => {
      fireEvent.change(input, { target: { value: 'Catan' } });
    });

    await waitFor(() => {
      expect(screen.getByText('Catan')).toBeInTheDocument();
    });

    const chiediButtons = screen.getAllByText(/chiedi/i);
    fireEvent.click(chiediButtons[0]);
    expect(mockOnAskAboutGame).toHaveBeenCalledWith(
      expect.objectContaining({ id: 'g1', title: 'Catan' })
    );
  });

  it('closes on ESC key', () => {
    render(
      <SearchExpander
        isOpen={true}
        onClose={mockOnClose}
        onViewGame={mockOnViewGame}
        onAskAboutGame={mockOnAskAboutGame}
      />
    );

    fireEvent.keyDown(document, { key: 'Escape' });
    expect(mockOnClose).toHaveBeenCalled();
  });
});
