/**
 * ActivityFeedWithFilters Tests (Issue #3925)
 *
 * Integration tests for the combined filters + feed component.
 * Tests: rendering, filter integration, URL state, clear all.
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { ActivityFeedWithFilters } from '../ActivityFeedWithFilters';

// ============================================================================
// Mocks
// ============================================================================

const mockPush = vi.fn();
let mockSearchParams = new URLSearchParams();

vi.mock('next/navigation', () => ({
  useSearchParams: () => mockSearchParams,
  useRouter: () => ({ push: mockPush }),
  usePathname: () => '/dashboard',
}));

vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: { children: React.ReactNode; href: string; [key: string]: unknown }) => (
    <a href={href} {...props}>{children}</a>
  ),
}));

vi.mock('framer-motion', () => ({
  motion: {
    div: ({ children, ...props }: { children: React.ReactNode; [key: string]: unknown }) => {
      const { initial: _initial, animate: _animate, transition: _transition, variants: _variants, ...rest } = props as Record<string, unknown>;
      return <div {...rest}>{children}</div>;
    },
  },
}));

// ============================================================================
// Helpers
// ============================================================================

function renderComponent(searchParams?: URLSearchParams) {
  if (searchParams) {
    mockSearchParams = searchParams;
  }

  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
    },
  });

  return render(
    <QueryClientProvider client={queryClient}>
      <ActivityFeedWithFilters />
    </QueryClientProvider>
  );
}

// ============================================================================
// Tests
// ============================================================================

describe('ActivityFeedWithFilters', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockSearchParams = new URLSearchParams();
  });

  describe('Rendering', () => {
    it('renders the container', async () => {
      renderComponent();

      await waitFor(() => {
        expect(screen.getByTestId('activity-feed-with-filters')).toBeInTheDocument();
      });
    });

    it('renders the title', async () => {
      renderComponent();

      await waitFor(() => {
        expect(screen.getByTestId('activity-feed-title')).toHaveTextContent('Attività Recente');
      });
    });

    it('renders the filter component', async () => {
      renderComponent();

      await waitFor(() => {
        expect(screen.getByTestId('activity-feed-filters')).toBeInTheDocument();
      });
    });

    it('renders the activity feed', async () => {
      renderComponent();

      // Wait for mock data to load
      await waitFor(() => {
        expect(screen.getByTestId('activity-feed-widget')).toBeInTheDocument();
      });
    });

    it('renders search input', async () => {
      renderComponent();

      await waitFor(() => {
        expect(screen.getByTestId('activity-search-input')).toBeInTheDocument();
      });
    });

    it('renders all filter chips', async () => {
      renderComponent();

      await waitFor(() => {
        expect(screen.getByTestId('filter-chip-game_added')).toBeInTheDocument();
        expect(screen.getByTestId('filter-chip-session_completed')).toBeInTheDocument();
        expect(screen.getByTestId('filter-chip-chat_saved')).toBeInTheDocument();
        expect(screen.getByTestId('filter-chip-wishlist_added')).toBeInTheDocument();
        expect(screen.getByTestId('filter-chip-achievement_unlocked')).toBeInTheDocument();
      });
    });
  });

  describe('URL State Integration', () => {
    it('initializes filters from URL params', async () => {
      renderComponent(new URLSearchParams('filter=game_added'));

      await waitFor(() => {
        const chip = screen.getByTestId('filter-chip-game_added');
        expect(chip).toHaveAttribute('aria-pressed', 'true');
      });
    });

    it('initializes search from URL params', async () => {
      renderComponent(new URLSearchParams('search=wingspan'));

      await waitFor(() => {
        expect(screen.getByTestId('activity-search-input')).toHaveValue('wingspan');
      });
    });

    it('updates URL when filter chip is clicked', async () => {
      renderComponent();

      await waitFor(() => {
        expect(screen.getByTestId('filter-chip-game_added')).toBeInTheDocument();
      });

      fireEvent.click(screen.getByTestId('filter-chip-game_added'));

      expect(mockPush).toHaveBeenCalledWith(
        expect.stringContaining('filter=game_added'),
        { scroll: false }
      );
    });
  });

  describe('Clear Filters', () => {
    it('does not show reset button when no filters active', async () => {
      renderComponent();

      await waitFor(() => {
        expect(screen.queryByTestId('clear-filters-header')).not.toBeInTheDocument();
      });
    });

    it('shows reset button when filters are active from URL', async () => {
      renderComponent(new URLSearchParams('filter=game_added'));

      await waitFor(() => {
        expect(screen.getByTestId('clear-filters-header')).toBeInTheDocument();
      });
    });

    it('clears URL when reset button clicked', async () => {
      renderComponent(new URLSearchParams('filter=game_added&search=test'));

      await waitFor(() => {
        expect(screen.getByTestId('clear-filters-header')).toBeInTheDocument();
      });

      fireEvent.click(screen.getByTestId('clear-filters-header'));

      expect(mockPush).toHaveBeenCalledWith('/dashboard', { scroll: false });
    });
  });

  describe('Activity Events', () => {
    it('displays activity events from mock data', async () => {
      renderComponent();

      // Wait for events to load from mock
      await waitFor(() => {
        const timeline = screen.getByTestId('activity-timeline');
        expect(timeline).toBeInTheDocument();
      });
    });

    it('shows filtered events when type filter is active from URL', async () => {
      renderComponent(new URLSearchParams('filter=game_added'));

      await waitFor(() => {
        const timeline = screen.getByTestId('activity-timeline');
        expect(timeline).toBeInTheDocument();
      });
    });
  });

  describe('Glassmorphic Styling', () => {
    it('has glassmorphic container styling', async () => {
      renderComponent();

      await waitFor(() => {
        const container = screen.getByTestId('activity-feed-with-filters');
        expect(container).toHaveClass('backdrop-blur-xl');
        expect(container).toHaveClass('rounded-2xl');
      });
    });
  });

  describe('Custom className', () => {
    it('applies custom className', async () => {
      const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false } },
      });

      render(
        <QueryClientProvider client={queryClient}>
          <ActivityFeedWithFilters className="custom-class" />
        </QueryClientProvider>
      );

      await waitFor(() => {
        expect(screen.getByTestId('activity-feed-with-filters')).toHaveClass('custom-class');
      });
    });
  });
});
