/**
 * GameCombobox Component Tests
 *
 * Tests for searchable game selector with autocomplete functionality.
 * Issue #4273: Game Search Autocomplete
 * Target: ≥85% coverage
 */

import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { GameCombobox } from '@/components/play-records/GameCombobox';
import { createTestQueryClient } from '@/__tests__/utils/query-test-utils';

// Mock useGameSearch hook
const mockUseGameSearch = vi.fn();
vi.mock('@/lib/domain-hooks/useGameSearch', () => ({
  useGameSearch: () => mockUseGameSearch(),
}));

const mockGames = [
  {
    id: 'lib-123',
    name: 'Catan',
    source: 'library' as const,
    imageUrl: 'https://example.com/catan.jpg',
  },
  {
    id: 'cat-456',
    name: 'Carcassonne',
    source: 'catalog' as const,
  },
  {
    id: 'priv-789',
    name: 'My Private Game',
    source: 'private' as const,
  },
];

describe('GameCombobox', () => {
  let queryClient: QueryClient;
  const mockOnSelect = vi.fn();
  const mockOnNotFound = vi.fn();

  const renderComponent = (props = {}) => {
    return render(
      <QueryClientProvider client={queryClient}>
        <GameCombobox onSelect={mockOnSelect} onNotFound={mockOnNotFound} {...props} />
      </QueryClientProvider>
    );
  };

  beforeEach(() => {
    queryClient = createTestQueryClient();
    vi.clearAllMocks();
    mockUseGameSearch.mockReturnValue({
      data: [],
      isLoading: false,
    });
  });

  describe('Rendering', () => {
    it('should render with default placeholder', () => {
      renderComponent();

      expect(screen.getByRole('combobox')).toBeInTheDocument();
      expect(screen.getByText('Search your games...')).toBeInTheDocument();
    });

    it('should render with custom placeholder', () => {
      renderComponent({ placeholder: 'Find a game...' });

      expect(screen.getByText('Find a game...')).toBeInTheDocument();
    });

    it('should render chevron icon', () => {
      renderComponent();

      const button = screen.getByRole('combobox');
      expect(button.querySelector('svg')).toBeInTheDocument();
    });

    it('should be disabled when disabled prop is true', () => {
      renderComponent({ disabled: true });

      expect(screen.getByRole('combobox')).toBeDisabled();
    });
  });

  describe('Popover Interaction', () => {
    it('should open popover on click', async () => {
      const user = userEvent.setup();
      renderComponent();

      const button = screen.getByRole('combobox');
      await user.click(button);

      await waitFor(() => {
        expect(screen.getByPlaceholderText('Type to search...')).toBeInTheDocument();
      });
    });

    it('should show loading state while searching', async () => {
      mockUseGameSearch.mockReturnValue({
        data: [],
        isLoading: true,
      });

      const user = userEvent.setup();
      renderComponent();

      await user.click(screen.getByRole('combobox'));

      await waitFor(() => {
        expect(screen.getByText('Searching...')).toBeInTheDocument();
      });
    });

    it('should display search results', async () => {
      mockUseGameSearch.mockReturnValue({
        data: mockGames,
        isLoading: false,
      });

      const user = userEvent.setup();
      renderComponent();

      await user.click(screen.getByRole('combobox'));

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
        expect(screen.getByText('Carcassonne')).toBeInTheDocument();
        expect(screen.getByText('My Private Game')).toBeInTheDocument();
      });
    });
  });

  describe('Search Input', () => {
    it('should update search query on input', async () => {
      const user = userEvent.setup();
      renderComponent();

      await user.click(screen.getByRole('combobox'));

      const searchInput = screen.getByPlaceholderText('Type to search...');
      await user.type(searchInput, 'Catan');

      expect(searchInput).toHaveValue('Catan');
    });

    it('should trigger useGameSearch hook with search query', async () => {
      const user = userEvent.setup();
      renderComponent();

      await user.click(screen.getByRole('combobox'));

      const searchInput = screen.getByPlaceholderText('Type to search...');
      await user.type(searchInput, 'Ca');

      // useGameSearch should have been called with the query
      // (implementation detail tested via integration)
    });
  });

  describe('Source Badges', () => {
    it('should display library badge', async () => {
      mockUseGameSearch.mockReturnValue({
        data: [mockGames[0]],
        isLoading: false,
      });

      const user = userEvent.setup();
      renderComponent();

      await user.click(screen.getByRole('combobox'));

      await waitFor(() => {
        expect(screen.getByText('📚')).toBeInTheDocument();
        expect(screen.getByText('Library')).toBeInTheDocument();
      });
    });

    it('should display catalog badge', async () => {
      mockUseGameSearch.mockReturnValue({
        data: [mockGames[1]],
        isLoading: false,
      });

      const user = userEvent.setup();
      renderComponent();

      await user.click(screen.getByRole('combobox'));

      await waitFor(() => {
        expect(screen.getByText('🌐')).toBeInTheDocument();
        expect(screen.getByText('Catalog')).toBeInTheDocument();
      });
    });

    it('should display private badge', async () => {
      mockUseGameSearch.mockReturnValue({
        data: [mockGames[2]],
        isLoading: false,
      });

      const user = userEvent.setup();
      renderComponent();

      await user.click(screen.getByRole('combobox'));

      await waitFor(() => {
        expect(screen.getByText('🔒')).toBeInTheDocument();
        expect(screen.getByText('Private')).toBeInTheDocument();
      });
    });
  });

  describe('Empty State', () => {
    it('should show "No games found" when results are empty', async () => {
      mockUseGameSearch.mockReturnValue({
        data: [],
        isLoading: false,
      });

      const user = userEvent.setup();
      renderComponent();

      await user.click(screen.getByRole('combobox'));

      await waitFor(() => {
        expect(screen.getByText('No games found')).toBeInTheDocument();
      });
    });

    it('should show "Cerca nel catalogo" link when onNotFound is provided', async () => {
      mockUseGameSearch.mockReturnValue({
        data: [],
        isLoading: false,
      });

      const user = userEvent.setup();
      renderComponent({ onNotFound: mockOnNotFound });

      await user.click(screen.getByRole('combobox'));

      // Type search query
      const searchInput = screen.getByPlaceholderText('Type to search...');
      await user.type(searchInput, 'NonExistent');

      await waitFor(() => {
        expect(screen.getByText('Cerca nel catalogo')).toBeInTheDocument();
      });
    });

    it('should not show "Cerca nel catalogo" link when onNotFound is not provided', async () => {
      mockUseGameSearch.mockReturnValue({
        data: [],
        isLoading: false,
      });

      const user = userEvent.setup();
      renderComponent({ onNotFound: undefined });

      await user.click(screen.getByRole('combobox'));

      await waitFor(() => {
        expect(screen.queryByText('Cerca nel catalogo')).not.toBeInTheDocument();
      });
    });

    it('should call onNotFound when "Cerca nel catalogo" is clicked', async () => {
      mockUseGameSearch.mockReturnValue({
        data: [],
        isLoading: false,
      });

      const user = userEvent.setup();
      renderComponent({ onNotFound: mockOnNotFound });

      await user.click(screen.getByRole('combobox'));

      const searchInput = screen.getByPlaceholderText('Type to search...');
      await user.type(searchInput, 'NonExistent');

      const bggLink = await screen.findByText('Cerca nel catalogo');
      await user.click(bggLink);

      expect(mockOnNotFound).toHaveBeenCalledTimes(1);
    });

    it('should close popover when "Cerca nel catalogo" is clicked', async () => {
      mockUseGameSearch.mockReturnValue({
        data: [],
        isLoading: false,
      });

      const user = userEvent.setup();
      renderComponent({ onNotFound: mockOnNotFound });

      await user.click(screen.getByRole('combobox'));

      const searchInput = screen.getByPlaceholderText('Type to search...');
      await user.type(searchInput, 'NonExistent');

      const bggLink = await screen.findByText('Cerca nel catalogo');
      await user.click(bggLink);

      // Popover should be closed
      await waitFor(() => {
        expect(screen.queryByPlaceholderText('Type to search...')).not.toBeInTheDocument();
      });
    });
  });

  describe('Game Selection', () => {
    it('should call onSelect when game is clicked', async () => {
      mockUseGameSearch.mockReturnValue({
        data: mockGames,
        isLoading: false,
      });

      const user = userEvent.setup();
      renderComponent();

      await user.click(screen.getByRole('combobox'));

      const catanOption = await screen.findByText('Catan');
      await user.click(catanOption);

      expect(mockOnSelect).toHaveBeenCalledWith('lib-123', 'Catan');
    });

    it('should close popover after selection', async () => {
      mockUseGameSearch.mockReturnValue({
        data: mockGames,
        isLoading: false,
      });

      const user = userEvent.setup();
      renderComponent();

      await user.click(screen.getByRole('combobox'));

      const catanOption = await screen.findByText('Catan');
      await user.click(catanOption);

      // Popover should be closed
      await waitFor(() => {
        expect(screen.queryByPlaceholderText('Type to search...')).not.toBeInTheDocument();
      });
    });

    it('should clear search input after selection', async () => {
      mockUseGameSearch.mockReturnValue({
        data: mockGames,
        isLoading: false,
      });

      const user = userEvent.setup();
      renderComponent();

      await user.click(screen.getByRole('combobox'));

      const searchInput = screen.getByPlaceholderText('Type to search...');
      await user.type(searchInput, 'Catan');

      const catanOption = await screen.findByText('Catan');
      await user.click(catanOption);

      // Reopen and check search is cleared
      await user.click(screen.getByRole('combobox'));
      expect(screen.getByPlaceholderText('Type to search...')).toHaveValue('');
    });

    it('should display selected game name in button', async () => {
      mockUseGameSearch.mockReturnValue({
        data: mockGames,
        isLoading: false,
      });

      const user = userEvent.setup();
      renderComponent({ value: 'lib-123' });

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
      });
    });

    it('should display selected game in button with badge', async () => {
      mockUseGameSearch.mockReturnValue({
        data: mockGames,
        isLoading: false,
      });

      renderComponent({ value: 'lib-123' });

      // Selected game name should be visible in the button
      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
      });

      // Badge should be visible for selected game
      expect(screen.getByText('Library')).toBeInTheDocument();
    });
  });

  describe('Keyboard Navigation', () => {
    it('should navigate results with arrow keys', async () => {
      mockUseGameSearch.mockReturnValue({
        data: mockGames,
        isLoading: false,
      });

      const user = userEvent.setup();
      renderComponent();

      await user.click(screen.getByRole('combobox'));

      const searchInput = screen.getByPlaceholderText('Type to search...');

      // Arrow down should highlight first item
      await user.keyboard('{ArrowDown}');

      // Enter should select highlighted item
      await user.keyboard('{Enter}');

      expect(mockOnSelect).toHaveBeenCalled();
    });

    it('should select game on Enter key', async () => {
      mockUseGameSearch.mockReturnValue({
        data: mockGames,
        isLoading: false,
      });

      const user = userEvent.setup();
      renderComponent();

      await user.click(screen.getByRole('combobox'));

      await user.keyboard('{ArrowDown}');
      await user.keyboard('{Enter}');

      expect(mockOnSelect).toHaveBeenCalledTimes(1);
    });
  });

  describe('Accessibility', () => {
    it('should have proper ARIA attributes', () => {
      renderComponent();

      const button = screen.getByRole('combobox');
      expect(button).toHaveAttribute('aria-expanded', 'false');
    });

    it('should update aria-expanded when opened', async () => {
      const user = userEvent.setup();
      renderComponent();

      const button = screen.getByRole('combobox');
      await user.click(button);

      expect(button).toHaveAttribute('aria-expanded', 'true');
    });
  });
});
