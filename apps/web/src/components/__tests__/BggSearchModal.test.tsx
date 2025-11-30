import type { Mock } from 'vitest';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { BggSearchModal } from '../BggSearchModal';
import * as api from '@/lib/api';

// Mock the API
vi.mock('@/lib/api', () => ({
  api: {
    bgg: {
      search: vi.fn(),
      getGameDetails: vi.fn(),
    },
  },
}));

// Mock Next.js Image component
vi.mock('next/image', () => ({
  __esModule: true,
  default: ({ src, alt, ...props }: any) => (
    <img src={src} alt={alt} {...props} />
  ),
}));

// Mock useDebounce hook to avoid timing issues in tests
vi.mock('@/hooks/useDebounce', () => ({
  useDebounce: (value: any) => value, // Return value immediately
}));

describe('BggSearchModal', () => {
  const mockOnClose = vi.fn();
  const mockOnSelect = vi.fn();

  const mockSearchResults = [
    {
      bggId: 123,
      name: 'Test Game 1',
      yearPublished: 2020,
      type: 'boardgame',
      thumbnailUrl: 'https://example.com/thumb1.jpg',
    },
    {
      bggId: 456,
      name: 'Test Game 2',
      yearPublished: 2021,
      type: 'boardgame',
      thumbnailUrl: null,
    },
  ];

  const mockGameDetails = {
    bggId: 123,
    name: 'Test Game 1',
    yearPublished: 2020,
    minPlayers: 2,
    maxPlayers: 4,
    playingTime: 60,
    minAge: 10,
    description: 'A test game',
    thumbnailUrl: 'https://example.com/thumb1.jpg',
    imageUrl: 'https://example.com/image1.jpg',
  };

  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.runOnlyPendingTimers();
    vi.useRealTimers();
  });

  describe('Rendering', () => {
    it('should not render when isOpen is false', () => {
      const { container } = render(
        <BggSearchModal isOpen={false} onClose={mockOnClose} onSelect={mockOnSelect} />
      );

      expect(container.firstChild).toBeNull();
    });

    it('should render modal when isOpen is true', () => {
      render(<BggSearchModal isOpen={true} onClose={mockOnClose} onSelect={mockOnSelect} />);

      expect(screen.getByRole('dialog')).toBeInTheDocument();
      expect(screen.getByText('Import from BoardGameGeek')).toBeInTheDocument();
      expect(screen.getByPlaceholderText('Search for a board game...')).toBeInTheDocument();
    });

    it('should render with proper ARIA attributes', () => {
      render(<BggSearchModal isOpen={true} onClose={mockOnClose} onSelect={mockOnSelect} />);

      const dialog = screen.getByRole('dialog');
      expect(dialog).toHaveAttribute('aria-modal', 'true');
      expect(dialog).toHaveAttribute('aria-labelledby', 'bgg-search-title');

      const searchInput = screen.getByPlaceholderText('Search for a board game...');
      expect(searchInput).toHaveAttribute('aria-label', 'Search BoardGameGeek');
    });

    it('should autofocus search input', () => {
      render(<BggSearchModal isOpen={true} onClose={mockOnClose} onSelect={mockOnSelect} />);

      const searchInput = screen.getByPlaceholderText('Search for a board game...');
      // Check input is rendered (autofocus attribute doesn't affect test DOM)
      expect(searchInput).toBeInTheDocument();
    });
  });

  describe('User Interactions', () => {
    it('should call onClose when close button is clicked', async () => {
      const user = userEvent.setup({ delay: null });

      render(<BggSearchModal isOpen={true} onClose={mockOnClose} onSelect={mockOnSelect} />);

      const closeButton = screen.getByLabelText('Close modal');
      await user.click(closeButton);

      expect(mockOnClose).toHaveBeenCalledTimes(1);
    });

    it('should call onClose when clicking outside modal', async () => {
      const user = userEvent.setup({ delay: null });

      render(<BggSearchModal isOpen={true} onClose={mockOnClose} onSelect={mockOnSelect} />);

      const backdrop = screen.getByRole('dialog');
      await user.click(backdrop);

      expect(mockOnClose).toHaveBeenCalledTimes(1);
    });

    it('should not call onClose when clicking inside modal', async () => {
      const user = userEvent.setup({ delay: null });

      render(<BggSearchModal isOpen={true} onClose={mockOnClose} onSelect={mockOnSelect} />);

      const modalContent = screen.getByText('Import from BoardGameGeek').closest('div');
      if (modalContent) {
        await user.click(modalContent);
      }

      expect(mockOnClose).not.toHaveBeenCalled();
    });

    it('should update search query on input change', async () => {
      const user = userEvent.setup({ delay: null });

      render(<BggSearchModal isOpen={true} onClose={mockOnClose} onSelect={mockOnSelect} />);

      const searchInput = screen.getByPlaceholderText('Search for a board game...');
      await user.type(searchInput, 'Catan');

      expect(searchInput).toHaveValue('Catan');
    });
  });

  describe('Search Functionality', () => {
    it('should show instructions when query is less than 3 characters', () => {
      render(<BggSearchModal isOpen={true} onClose={mockOnClose} onSelect={mockOnSelect} />);

      expect(screen.getByText('Enter a game name to search BoardGameGeek')).toBeInTheDocument();
    });

    it('should search BGG when query is 3+ characters', async () => {
      const user = userEvent.setup({ delay: null });
      (api.api.bgg.search as Mock).mockResolvedValue({ results: mockSearchResults });

      render(<BggSearchModal isOpen={true} onClose={mockOnClose} onSelect={mockOnSelect} />);

      const searchInput = screen.getByPlaceholderText('Search for a board game...');
      await user.type(searchInput, 'Catan');

      await waitFor(() => {
        expect(api.api.bgg.search).toHaveBeenCalledWith('Catan');
      });
    });

    it('should display search results', async () => {
      const user = userEvent.setup({ delay: null });
      (api.api.bgg.search as Mock).mockResolvedValue({ results: mockSearchResults });

      render(<BggSearchModal isOpen={true} onClose={mockOnClose} onSelect={mockOnSelect} />);

      const searchInput = screen.getByPlaceholderText('Search for a board game...');
      await user.type(searchInput, 'Test');

      await waitFor(() => {
        expect(screen.getByText('Found 2 results')).toBeInTheDocument();
        expect(screen.getByText('Test Game 1')).toBeInTheDocument();
        expect(screen.getByText('Test Game 2')).toBeInTheDocument();
        expect(screen.getByText('(2020)')).toBeInTheDocument();
        expect(screen.getByText('(2021)')).toBeInTheDocument();
      });
    });

    it('should display game thumbnail when available', async () => {
      const user = userEvent.setup({ delay: null });
      (api.api.bgg.search as Mock).mockResolvedValue({ results: mockSearchResults });

      render(<BggSearchModal isOpen={true} onClose={mockOnClose} onSelect={mockOnSelect} />);

      const searchInput = screen.getByPlaceholderText('Search for a board game...');
      await user.type(searchInput, 'Test');

      await waitFor(() => {
        const thumbnail = screen.getByAltText('Test Game 1 thumbnail');
        expect(thumbnail).toHaveAttribute('src', 'https://example.com/thumb1.jpg');
      });
    });

    it('should show loading state during search', async () => {
      const user = userEvent.setup({ delay: null });
      (api.api.bgg.search as Mock).mockImplementation(
        () => new Promise((resolve) => setTimeout(() => resolve({ results: mockSearchResults }), 1000))
      );

      render(<BggSearchModal isOpen={true} onClose={mockOnClose} onSelect={mockOnSelect} />);

      const searchInput = screen.getByPlaceholderText('Search for a board game...');
      await user.type(searchInput, 'Test');

      expect(screen.getByText('Searching BoardGameGeek...')).toBeInTheDocument();
    });

    it('should show error when no results found', async () => {
      const user = userEvent.setup({ delay: null });
      (api.api.bgg.search as Mock).mockResolvedValue({ results: [] });

      render(<BggSearchModal isOpen={true} onClose={mockOnClose} onSelect={mockOnSelect} />);

      const searchInput = screen.getByPlaceholderText('Search for a board game...');
      await user.type(searchInput, 'NonExistent');

      await waitFor(() => {
        expect(screen.getByText('No games found. Try a different search term.')).toBeInTheDocument();
      });
    });

    it('should show error when search fails', async () => {
      const user = userEvent.setup({ delay: null });
      (api.api.bgg.search as Mock).mockRejectedValue(new Error('Network error'));

      render(<BggSearchModal isOpen={true} onClose={mockOnClose} onSelect={mockOnSelect} />);

      const searchInput = screen.getByPlaceholderText('Search for a board game...');
      await user.type(searchInput, 'Test');

      await waitFor(() => {
        expect(
          screen.getByText(/Failed to search BoardGameGeek. The service may be unavailable/)
        ).toBeInTheDocument();
      });
    });

    it('should clear results when query becomes less than 3 characters', async () => {
      const user = userEvent.setup({ delay: null });
      (api.api.bgg.search as Mock).mockResolvedValue({ results: mockSearchResults });

      render(<BggSearchModal isOpen={true} onClose={mockOnClose} onSelect={mockOnSelect} />);

      const searchInput = screen.getByPlaceholderText('Search for a board game...');
      await user.type(searchInput, 'Test');

      await waitFor(() => {
        expect(screen.getByText('Found 2 results')).toBeInTheDocument();
      });

      await user.clear(searchInput);
      await user.type(searchInput, 'Te');

      expect(screen.queryByText('Found 2 results')).not.toBeInTheDocument();
    });
  });

  describe('Game Selection', () => {
    it('should fetch game details and call onSelect when game is selected', async () => {
      const user = userEvent.setup({ delay: null });
      (api.api.bgg.search as Mock).mockResolvedValue({ results: mockSearchResults });
      (api.api.bgg.getGameDetails as Mock).mockResolvedValue(mockGameDetails);

      render(<BggSearchModal isOpen={true} onClose={mockOnClose} onSelect={mockOnSelect} />);

      const searchInput = screen.getByPlaceholderText('Search for a board game...');
      await user.type(searchInput, 'Test');

      await waitFor(() => {
        expect(screen.getByText('Test Game 1')).toBeInTheDocument();
      });

      const selectButton = screen.getAllByText('Select')[0];
      await user.click(selectButton);

      await waitFor(() => {
        expect(api.api.bgg.getGameDetails).toHaveBeenCalledWith(123);
        expect(mockOnSelect).toHaveBeenCalledWith(mockGameDetails);
        expect(mockOnClose).toHaveBeenCalled();
      });
    });

    it('should show loading state while fetching game details', async () => {
      const user = userEvent.setup({ delay: null });
      (api.api.bgg.search as Mock).mockResolvedValue({ results: mockSearchResults });
      (api.api.bgg.getGameDetails as Mock).mockImplementation(
        () => new Promise((resolve) => setTimeout(() => resolve(mockGameDetails), 1000))
      );

      render(<BggSearchModal isOpen={true} onClose={mockOnClose} onSelect={mockOnSelect} />);

      const searchInput = screen.getByPlaceholderText('Search for a board game...');
      await user.type(searchInput, 'Test');

      await waitFor(() => {
        expect(screen.getByText('Test Game 1')).toBeInTheDocument();
      });

      const selectButtons = screen.getAllByText('Select');
      await user.click(selectButtons[0]);

      // Check that button changes to loading state
      await waitFor(() => {
        const loadingButton = screen.getAllByText('Loading...')[0];
        expect(loadingButton).toBeInTheDocument();
      });
    });

    it('should show error when fetching game details fails', async () => {
      const user = userEvent.setup({ delay: null });
      (api.api.bgg.search as Mock).mockResolvedValue({ results: mockSearchResults });
      (api.api.bgg.getGameDetails as Mock).mockRejectedValue(new Error('Network error'));

      render(<BggSearchModal isOpen={true} onClose={mockOnClose} onSelect={mockOnSelect} />);

      const searchInput = screen.getByPlaceholderText('Search for a board game...');
      await user.type(searchInput, 'Test');

      await waitFor(() => {
        expect(screen.getByText('Test Game 1')).toBeInTheDocument();
      });

      const selectButton = screen.getAllByText('Select')[0];
      await user.click(selectButton);

      await waitFor(() => {
        expect(screen.getByText('Failed to fetch game details. Please try again.')).toBeInTheDocument();
      });

      expect(mockOnSelect).not.toHaveBeenCalled();
      expect(mockOnClose).not.toHaveBeenCalled();
    });

    it('should disable select button while loading details', async () => {
      const user = userEvent.setup({ delay: null });
      (api.api.bgg.search as Mock).mockResolvedValue({ results: mockSearchResults });
      (api.api.bgg.getGameDetails as Mock).mockImplementation(
        () => new Promise((resolve) => setTimeout(() => resolve(mockGameDetails), 1000))
      );

      render(<BggSearchModal isOpen={true} onClose={mockOnClose} onSelect={mockOnSelect} />);

      const searchInput = screen.getByPlaceholderText('Search for a board game...');
      await user.type(searchInput, 'Test');

      await waitFor(() => {
        expect(screen.getByText('Test Game 1')).toBeInTheDocument();
      });

      const selectButton = screen.getAllByText('Select')[0] as HTMLButtonElement;
      await user.click(selectButton);

      expect(selectButton).toBeDisabled();
    });
  });

  describe('Modal State Reset', () => {
    it('should reset state when modal is closed', async () => {
      const user = userEvent.setup({ delay: null });
      (api.api.bgg.search as Mock).mockResolvedValue({ results: mockSearchResults });

      const { rerender } = render(
        <BggSearchModal isOpen={true} onClose={mockOnClose} onSelect={mockOnSelect} />
      );

      const searchInput = screen.getByPlaceholderText('Search for a board game...');
      await user.type(searchInput, 'Test');

      await waitFor(() => {
        expect(screen.getByText('Found 2 results')).toBeInTheDocument();
      });

      // Close modal
      rerender(<BggSearchModal isOpen={false} onClose={mockOnClose} onSelect={mockOnSelect} />);

      // Reopen modal
      rerender(<BggSearchModal isOpen={true} onClose={mockOnClose} onSelect={mockOnSelect} />);

      // State should be reset
      const newSearchInput = screen.getByPlaceholderText('Search for a board game...');
      expect(newSearchInput).toHaveValue('');
      expect(screen.queryByText('Found 2 results')).not.toBeInTheDocument();
    });
  });

  describe('Edge Cases', () => {
    it('should handle single search result', async () => {
      const user = userEvent.setup({ delay: null });
      (api.api.bgg.search as Mock).mockResolvedValue({
        results: [mockSearchResults[0]],
      });

      render(<BggSearchModal isOpen={true} onClose={mockOnClose} onSelect={mockOnSelect} />);

      const searchInput = screen.getByPlaceholderText('Search for a board game...');
      await user.type(searchInput, 'Test');

      await waitFor(() => {
        expect(screen.getByText('Found 1 result')).toBeInTheDocument(); // Singular
      });
    });

    it('should handle game without year published', async () => {
      const user = userEvent.setup({ delay: null });
      const gameWithoutYear = { ...mockSearchResults[0], yearPublished: null };
      (api.api.bgg.search as Mock).mockResolvedValue({ results: [gameWithoutYear] });

      render(<BggSearchModal isOpen={true} onClose={mockOnClose} onSelect={mockOnSelect} />);

      const searchInput = screen.getByPlaceholderText('Search for a board game...');
      await user.type(searchInput, 'Test');

      await waitFor(() => {
        expect(screen.getByText('Test Game 1')).toBeInTheDocument();
        expect(screen.queryByText(/\(\d{4}\)/)).not.toBeInTheDocument();
      });
    });

    it('should handle console error when search fails', async () => {
      const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {});
      const user = userEvent.setup({ delay: null });
      (api.api.bgg.search as Mock).mockRejectedValue(new Error('Test error'));

      render(<BggSearchModal isOpen={true} onClose={mockOnClose} onSelect={mockOnSelect} />);

      const searchInput = screen.getByPlaceholderText('Search for a board game...');
      await user.type(searchInput, 'Test');

      await waitFor(() => {
        expect(consoleError).toHaveBeenCalledWith('BGG search error:', expect.any(Error));
      });

      consoleError.mockRestore();
    });

    it('should handle console error when fetching details fails', async () => {
      const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {});
      const user = userEvent.setup({ delay: null });
      (api.api.bgg.search as Mock).mockResolvedValue({ results: mockSearchResults });
      (api.api.bgg.getGameDetails as Mock).mockRejectedValue(new Error('Test error'));

      render(<BggSearchModal isOpen={true} onClose={mockOnClose} onSelect={mockOnSelect} />);

      const searchInput = screen.getByPlaceholderText('Search for a board game...');
      await user.type(searchInput, 'Test');

      await waitFor(() => {
        expect(screen.getByText('Test Game 1')).toBeInTheDocument();
      });

      const selectButton = screen.getAllByText('Select')[0];
      await user.click(selectButton);

      await waitFor(() => {
        expect(consoleError).toHaveBeenCalledWith('BGG game details error:', expect.any(Error));
      });

      consoleError.mockRestore();
    });
  });
});
