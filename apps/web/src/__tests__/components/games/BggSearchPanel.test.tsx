/**
 * BggSearchPanel Component Tests - E1-5
 *
 * Tests for the Game Night BGG search panel:
 * - Renders search input
 * - Debounces search and shows results
 * - Shows loading state during search
 * - "Add to Library" triggers import
 * - Shows error on import failure
 * - Empty state when no results
 */

import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, screen, waitFor, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { BggSearchPanel } from '@/components/games/BggSearchPanel';

// Mock sonner toast
vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
    info: vi.fn(),
    warning: vi.fn(),
  },
}));

// Mock API client
const mockSearchGames = vi.fn();
const mockImportGame = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    gameNightBgg: {
      searchGames: (...args: unknown[]) => mockSearchGames(...args),
      importGame: (...args: unknown[]) => mockImportGame(...args),
    },
  },
}));

// ============================================================================
// Test Data
// ============================================================================

const mockSearchResponse = {
  items: [
    {
      bggId: 13,
      title: 'Catan',
      yearPublished: 1995,
      thumbnailUrl: 'https://example.com/catan.jpg',
    },
    {
      bggId: 9209,
      title: 'Ticket to Ride',
      yearPublished: 2004,
      thumbnailUrl: 'https://example.com/ttr.jpg',
    },
    {
      bggId: 174430,
      title: 'Gloomhaven',
      yearPublished: 2017,
      thumbnailUrl: null,
    },
  ],
  totalCount: 3,
  page: 1,
  pageSize: 20,
};

const mockEmptyResponse = {
  items: [],
  totalCount: 0,
  page: 1,
  pageSize: 20,
};

const mockImportResponse = {
  privateGameId: 'pg-123',
  libraryEntryId: 'le-456',
  title: 'Catan',
  imageUrl: 'https://example.com/catan.jpg',
};

// ============================================================================
// Tests
// ============================================================================

describe('BggSearchPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers({ shouldAdvanceTime: true });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('renders the search input', () => {
    render(<BggSearchPanel />);

    const input = screen.getByTestId('bgg-search-input');
    expect(input).toBeInTheDocument();
    expect(input).toHaveAttribute('placeholder', 'Cerca gioco...');
  });

  it('shows initial state when no query is entered', () => {
    render(<BggSearchPanel />);

    expect(screen.getByTestId('bgg-search-initial')).toBeInTheDocument();
    expect(screen.getByText('Cerca un gioco')).toBeInTheDocument();
  });

  it('debounces search and shows results', async () => {
    mockSearchGames.mockResolvedValueOnce(mockSearchResponse);

    render(<BggSearchPanel />);

    const input = screen.getByTestId('bgg-search-input');

    // Type a search query
    await act(async () => {
      await userEvent.type(input, 'Catan');
    });

    // Advance past debounce
    await act(async () => {
      vi.advanceTimersByTime(350);
    });

    // Wait for results
    await waitFor(() => {
      expect(mockSearchGames).toHaveBeenCalledWith('Catan', 1, 20);
    });

    await waitFor(() => {
      expect(screen.getByTestId('bgg-search-results')).toBeInTheDocument();
    });

    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Ticket to Ride')).toBeInTheDocument();
    expect(screen.getByText('Gloomhaven')).toBeInTheDocument();
    expect(screen.getByText('3 results found')).toBeInTheDocument();
  });

  it('shows loading state during search', async () => {
    // Return a promise that won't resolve immediately
    let resolveSearch: (value: typeof mockSearchResponse) => void;
    mockSearchGames.mockReturnValueOnce(
      new Promise(resolve => {
        resolveSearch = resolve;
      })
    );

    render(<BggSearchPanel />);

    const input = screen.getByTestId('bgg-search-input');

    await act(async () => {
      await userEvent.type(input, 'Catan');
    });

    await act(async () => {
      vi.advanceTimersByTime(350);
    });

    // Loading skeleton should be visible
    await waitFor(() => {
      expect(screen.getByTestId('bgg-search-loading')).toBeInTheDocument();
    });

    // Resolve the search
    await act(async () => {
      resolveSearch!(mockSearchResponse);
    });

    await waitFor(() => {
      expect(screen.getByTestId('bgg-search-results')).toBeInTheDocument();
    });
  });

  it('triggers import when "Add to Library" button is clicked', async () => {
    mockSearchGames.mockResolvedValueOnce(mockSearchResponse);
    mockImportGame.mockResolvedValueOnce(mockImportResponse);

    const onImportSuccess = vi.fn();
    render(<BggSearchPanel onImportSuccess={onImportSuccess} />);

    const input = screen.getByTestId('bgg-search-input');

    await act(async () => {
      await userEvent.type(input, 'Catan');
    });

    await act(async () => {
      vi.advanceTimersByTime(350);
    });

    await waitFor(() => {
      expect(screen.getByTestId('bgg-search-results')).toBeInTheDocument();
    });

    // Click "Add to Library" for Catan (bggId: 13)
    const importBtn = screen.getByTestId('import-btn-13');
    await act(async () => {
      await userEvent.click(importBtn);
    });

    await waitFor(() => {
      expect(mockImportGame).toHaveBeenCalledWith(13);
    });

    const { toast } = await import('sonner');
    await waitFor(() => {
      expect(toast.success).toHaveBeenCalledWith('"Catan" added to your library');
    });

    expect(onImportSuccess).toHaveBeenCalledWith(mockImportResponse);
  });

  it('shows error toast on import failure', async () => {
    mockSearchGames.mockResolvedValueOnce(mockSearchResponse);
    mockImportGame.mockRejectedValueOnce(new Error('Game already exists'));

    render(<BggSearchPanel />);

    const input = screen.getByTestId('bgg-search-input');

    await act(async () => {
      await userEvent.type(input, 'Catan');
    });

    await act(async () => {
      vi.advanceTimersByTime(350);
    });

    await waitFor(() => {
      expect(screen.getByTestId('bgg-search-results')).toBeInTheDocument();
    });

    const importBtn = screen.getByTestId('import-btn-13');
    await act(async () => {
      await userEvent.click(importBtn);
    });

    const { toast } = await import('sonner');
    await waitFor(() => {
      expect(toast.error).toHaveBeenCalledWith('Game already exists');
    });
  });

  it('shows empty state when search returns no results', async () => {
    mockSearchGames.mockResolvedValueOnce(mockEmptyResponse);

    render(<BggSearchPanel />);

    const input = screen.getByTestId('bgg-search-input');

    await act(async () => {
      await userEvent.type(input, 'xyznonexistent');
    });

    await act(async () => {
      vi.advanceTimersByTime(350);
    });

    await waitFor(() => {
      expect(screen.getByTestId('bgg-search-empty')).toBeInTheDocument();
    });

    expect(screen.getByText('No games found')).toBeInTheDocument();
  });

  it('shows error toast when search fails', async () => {
    mockSearchGames.mockRejectedValueOnce(new Error('Network error'));

    render(<BggSearchPanel />);

    const input = screen.getByTestId('bgg-search-input');

    await act(async () => {
      await userEvent.type(input, 'Catan');
    });

    await act(async () => {
      vi.advanceTimersByTime(350);
    });

    const { toast } = await import('sonner');
    await waitFor(() => {
      expect(toast.error).toHaveBeenCalledWith('Network error');
    });
  });

  it('marks imported game as "Added" after successful import', async () => {
    mockSearchGames.mockResolvedValueOnce(mockSearchResponse);
    mockImportGame.mockResolvedValueOnce(mockImportResponse);

    render(<BggSearchPanel />);

    const input = screen.getByTestId('bgg-search-input');

    await act(async () => {
      await userEvent.type(input, 'Catan');
    });

    await act(async () => {
      vi.advanceTimersByTime(350);
    });

    await waitFor(() => {
      expect(screen.getByTestId('bgg-search-results')).toBeInTheDocument();
    });

    const importBtn = screen.getByTestId('import-btn-13');
    await act(async () => {
      await userEvent.click(importBtn);
    });

    // After import, button should show "Added" and be disabled
    await waitFor(() => {
      const btn = screen.getByTestId('import-btn-13');
      expect(btn).toBeDisabled();
      expect(btn).toHaveTextContent('Added');
    });
  });
});
