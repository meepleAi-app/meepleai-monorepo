/**
 * ActivityFeedFilters Tests (Issue #3322)
 *
 * Test coverage for advanced activity feed filters.
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import {
  ActivityFeedFilters,
  type ActivityFilters,
  type ActivityCounts,
} from '../ActivityFeedFilters';

// ============================================================================
// Test Data
// ============================================================================

const defaultFilters: ActivityFilters = {
  types: [],
  search: '',
};

const mockCounts: ActivityCounts = {
  game_added: 12,
  session_completed: 8,
  chat_saved: 5,
  wishlist_added: 3,
  achievement_unlocked: 2,
  total: 30,
};

// ============================================================================
// Test Helpers
// ============================================================================

let queryClient: QueryClient;

function renderComponent(props: Partial<React.ComponentProps<typeof ActivityFeedFilters>> = {}) {
  queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
    },
  });

  const defaultProps = {
    filters: defaultFilters,
    onFiltersChange: vi.fn(),
    ...props,
  };

  return {
    ...render(
      <QueryClientProvider client={queryClient}>
        <ActivityFeedFilters {...defaultProps} />
      </QueryClientProvider>
    ),
    onFiltersChange: defaultProps.onFiltersChange,
  };
}

// ============================================================================
// Tests
// ============================================================================

describe('ActivityFeedFilters', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  describe('Rendering', () => {
    it('renders filter component', () => {
      renderComponent();

      expect(screen.getByTestId('activity-feed-filters')).toBeInTheDocument();
    });

    it('renders search input', () => {
      renderComponent();

      expect(screen.getByTestId('activity-search-input')).toBeInTheDocument();
      expect(screen.getByPlaceholderText('Cerca attività...')).toBeInTheDocument();
    });

    it('renders all filter chips', () => {
      renderComponent();

      expect(screen.getByTestId('filter-chip-game_added')).toBeInTheDocument();
      expect(screen.getByTestId('filter-chip-session_completed')).toBeInTheDocument();
      expect(screen.getByTestId('filter-chip-chat_saved')).toBeInTheDocument();
      expect(screen.getByTestId('filter-chip-wishlist_added')).toBeInTheDocument();
      expect(screen.getByTestId('filter-chip-achievement_unlocked')).toBeInTheDocument();
    });

    it('renders filter chips with correct labels', () => {
      renderComponent();

      expect(screen.getByText('Giochi')).toBeInTheDocument();
      expect(screen.getByText('Sessioni')).toBeInTheDocument();
      expect(screen.getByText('Chat')).toBeInTheDocument();
      expect(screen.getByText('Wishlist')).toBeInTheDocument();
      expect(screen.getByText('Achievement')).toBeInTheDocument();
    });
  });

  describe('Filter Chips', () => {
    it('shows counts on filter chips when provided', () => {
      renderComponent({ counts: mockCounts });

      expect(screen.getByTestId('filter-count-game_added')).toHaveTextContent('12');
      expect(screen.getByTestId('filter-count-session_completed')).toHaveTextContent('8');
    });

    it('toggles filter when chip is clicked', () => {
      const { onFiltersChange } = renderComponent();

      fireEvent.click(screen.getByTestId('filter-chip-game_added'));

      expect(onFiltersChange).toHaveBeenCalledWith({
        types: ['game_added'],
        search: '',
      });
    });

    it('removes filter when selected chip is clicked', () => {
      const filters: ActivityFilters = {
        types: ['game_added'],
        search: '',
      };
      const { onFiltersChange } = renderComponent({ filters });

      fireEvent.click(screen.getByTestId('filter-chip-game_added'));

      expect(onFiltersChange).toHaveBeenCalledWith({
        types: [],
        search: '',
      });
    });

    it('supports multiple selected filters', () => {
      const filters: ActivityFilters = {
        types: ['game_added'],
        search: '',
      };
      const { onFiltersChange } = renderComponent({ filters });

      fireEvent.click(screen.getByTestId('filter-chip-session_completed'));

      expect(onFiltersChange).toHaveBeenCalledWith({
        types: ['game_added', 'session_completed'],
        search: '',
      });
    });

    it('marks selected chips with aria-pressed', () => {
      const filters: ActivityFilters = {
        types: ['game_added'],
        search: '',
      };
      renderComponent({ filters });

      expect(screen.getByTestId('filter-chip-game_added')).toHaveAttribute('aria-pressed', 'true');
      expect(screen.getByTestId('filter-chip-session_completed')).toHaveAttribute('aria-pressed', 'false');
    });
  });

  describe('Search Input', () => {
    it('updates search input value', () => {
      renderComponent();

      const input = screen.getByTestId('activity-search-input');
      fireEvent.change(input, { target: { value: 'wingspan' } });

      expect(input).toHaveValue('wingspan');
    });

    it('does not call onFiltersChange immediately on search input change', () => {
      const { onFiltersChange } = renderComponent();

      const input = screen.getByTestId('activity-search-input');
      fireEvent.change(input, { target: { value: 'wingspan' } });

      // Should not call immediately due to debounce
      expect(onFiltersChange).not.toHaveBeenCalled();
    });

    it('shows clear button when search has value', () => {
      const filters: ActivityFilters = {
        types: [],
        search: 'test',
      };
      renderComponent({ filters });

      expect(screen.getByTestId('clear-search-button')).toBeInTheDocument();
    });

    it('does not show clear button when search is empty', () => {
      renderComponent();

      expect(screen.queryByTestId('clear-search-button')).not.toBeInTheDocument();
    });

    it('clears search when clear button is clicked', async () => {
      const filters: ActivityFilters = {
        types: [],
        search: 'test',
      };
      renderComponent({ filters });

      fireEvent.click(screen.getByTestId('clear-search-button'));

      expect(screen.getByTestId('activity-search-input')).toHaveValue('');
    });
  });

  describe('Clear All Button', () => {
    it('shows clear all button when filters are active', () => {
      const filters: ActivityFilters = {
        types: ['game_added'],
        search: '',
      };
      renderComponent({ filters });

      expect(screen.getByTestId('clear-all-filters')).toBeInTheDocument();
    });

    it('shows clear all button when search is active', () => {
      const filters: ActivityFilters = {
        types: [],
        search: 'test',
      };
      renderComponent({ filters });

      expect(screen.getByTestId('clear-all-filters')).toBeInTheDocument();
    });

    it('does not show clear all button when no filters active', () => {
      renderComponent();

      expect(screen.queryByTestId('clear-all-filters')).not.toBeInTheDocument();
    });

    it('clears all filters when clicked', () => {
      const filters: ActivityFilters = {
        types: ['game_added', 'session_completed'],
        search: 'test',
      };
      const { onFiltersChange } = renderComponent({ filters });

      fireEvent.click(screen.getByTestId('clear-all-filters'));

      expect(onFiltersChange).toHaveBeenCalledWith({
        types: [],
        search: '',
        dateFrom: undefined,
        dateTo: undefined,
      });
    });
  });

  describe('Active Filter Badges', () => {
    it('shows active filter badges when filters selected', () => {
      const filters: ActivityFilters = {
        types: ['game_added'],
        search: '',
      };
      renderComponent({ filters, counts: mockCounts });

      expect(screen.getByTestId('active-filter-badges')).toBeInTheDocument();
      expect(screen.getByText(/Giochi \(12\)/)).toBeInTheDocument();
    });

    it('shows multiple filter badges', () => {
      const filters: ActivityFilters = {
        types: ['game_added', 'session_completed'],
        search: '',
      };
      renderComponent({ filters, counts: mockCounts });

      expect(screen.getByText(/Giochi \(12\)/)).toBeInTheDocument();
      expect(screen.getByText(/Sessioni \(8\)/)).toBeInTheDocument();
    });

    it('shows search term in active badges', () => {
      const filters: ActivityFilters = {
        types: [],
        search: 'wingspan',
      };
      renderComponent({ filters });

      expect(screen.getByTestId('active-filter-badges')).toBeInTheDocument();
      expect(screen.getByText(/"wingspan"/)).toBeInTheDocument();
    });

    it('does not show active badges when no filters', () => {
      renderComponent();

      expect(screen.queryByTestId('active-filter-badges')).not.toBeInTheDocument();
    });
  });

  describe('Styling', () => {
    it('applies custom className', () => {
      renderComponent({ className: 'custom-class' });

      expect(screen.getByTestId('activity-feed-filters')).toHaveClass('custom-class');
    });

    it('disables search input when loading', () => {
      renderComponent({ isLoading: true });

      expect(screen.getByTestId('activity-search-input')).toBeDisabled();
    });
  });

  describe('Accessibility', () => {
    it('search input is labeled', () => {
      renderComponent();

      const input = screen.getByTestId('activity-search-input');
      expect(input).toHaveAttribute('placeholder');
    });

    it('clear search button has aria-label', () => {
      const filters: ActivityFilters = {
        types: [],
        search: 'test',
      };
      renderComponent({ filters });

      expect(screen.getByTestId('clear-search-button')).toHaveAttribute('aria-label');
    });

    it('filter chips have aria-pressed attribute', () => {
      renderComponent();

      const chip = screen.getByTestId('filter-chip-game_added');
      expect(chip).toHaveAttribute('aria-pressed');
    });
  });
});
