/**
 * CollectionDashboard Component Unit Tests - Issue #3632
 * EPIC #3475: User Private Library & Collections Management
 *
 * Tests for the new enhanced CollectionDashboard component with:
 * - Hero stats section
 * - Advanced filtering and search
 * - Grid/List view toggle
 * - Pagination
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { CollectionDashboard } from '@/components/collection/CollectionDashboard';

// Mock window.matchMedia to avoid jsdom limitation (MeepleCard uses it)
vi.stubGlobal('matchMedia', (query: string) => ({
  matches: false,
  media: query,
  onchange: null,
  addListener: vi.fn(),
  removeListener: vi.fn(),
  addEventListener: vi.fn(),
  removeEventListener: vi.fn(),
  dispatchEvent: vi.fn(),
}));

// Mock useTranslation to avoid IntlProvider dependency
vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({
    t: (key: string) => {
      const map: Record<string, string> = {
        'collection.addFromCatalog': 'Aggiungi dal catalogo',
        'collection.addPrivateGame': 'Aggiungi gioco privato',
        'collection.year': 'Anno',
        'collection.plays': 'Partite',
      };
      return map[key] ?? key;
    },
    formatMessage: ({ id }: { id: string }) => id,
    locale: 'it',
  }),
}));

// ============================================================================
// Mock Data
// ============================================================================

const mockLibraryStats = {
  totalGames: 25,
  favoriteGames: 8,
  oldestAddedAt: '2020-01-01T00:00:00Z',
  newestAddedAt: '2024-01-20T00:00:00Z',
};

const mockQuota = {
  currentCount: 25,
  maxAllowed: 100,
  remainingSlots: 75,
  percentageUsed: 25,
};

const mockLibraryItems = [
  {
    id: 'lib-entry-1',
    userId: 'user-1',
    gameId: 'game-1',
    gameTitle: 'Wingspan',
    gameIconUrl: '/images/wingspan-icon.jpg',
    gameImageUrl: '/images/wingspan.jpg',
    gameYearPublished: 2019,
    gamePublisher: 'Stonemaier Games',
    isFavorite: true,
    currentState: 'Owned' as const,
    hasKb: true,
    kbCardCount: 1,
    kbIndexedCount: 1,
    kbProcessingCount: 0,
    agentIsOwned: true,
    addedAt: '2023-06-01T00:00:00Z',
  },
  {
    id: 'lib-entry-2',
    userId: 'user-1',
    gameId: 'game-2',
    gameTitle: 'Catan',
    gameIconUrl: '/images/catan-icon.jpg',
    gameImageUrl: '/images/catan.jpg',
    gameYearPublished: 1995,
    gamePublisher: 'Kosmos',
    isFavorite: false,
    currentState: 'Owned' as const,
    hasKb: false,
    kbCardCount: 0,
    kbIndexedCount: 0,
    kbProcessingCount: 0,
    agentIsOwned: true,
    addedAt: '2020-01-01T00:00:00Z',
  },
];

const mockLibraryResponse = {
  items: mockLibraryItems,
  totalCount: 25,
  pageSize: 20,
  page: 1,
  totalPages: 2,
  hasNextPage: true,
  hasPreviousPage: false,
};

// ============================================================================
// Mock Hooks
// ============================================================================

vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibrary: vi.fn(() => ({
    data: mockLibraryResponse,
    isLoading: false,
    error: null,
  })),
  useLibraryStats: vi.fn(() => ({
    data: mockLibraryStats,
    isLoading: false,
  })),
  useLibraryQuota: vi.fn(() => ({
    data: mockQuota,
    isLoading: false,
  })),
  libraryKeys: {
    all: ['library'],
    lists: () => ['library', 'list'],
    list: (params: unknown) => ['library', 'list', { params }],
    stats: () => ['library', 'stats'],
    quota: () => ['library', 'quota'],
  },
}));

// ============================================================================
// Test Utilities
// ============================================================================

function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        gcTime: 0,
      },
    },
  });
}

function renderWithProviders(component: React.ReactElement) {
  const queryClient = createTestQueryClient();
  return render(
    <QueryClientProvider client={queryClient}>
      {component}
    </QueryClientProvider>
  );
}

// ============================================================================
// Tests
// ============================================================================

describe('CollectionDashboard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe('Rendering', () => {
    it('should render the dashboard container', () => {
      renderWithProviders(<CollectionDashboard />);
      expect(screen.getByTestId('collection-dashboard')).toBeInTheDocument();
    });

    it('should render hero stats section', () => {
      renderWithProviders(<CollectionDashboard />);

      expect(screen.getByTestId('hero-stat-total')).toBeInTheDocument();
      expect(screen.getByTestId('hero-stat-favorites')).toBeInTheDocument();
      expect(screen.getByTestId('hero-stat-quota')).toBeInTheDocument();
      expect(screen.getByTestId('hero-stat-usage')).toBeInTheDocument();
    });

    it('should render toolbar with search input', () => {
      renderWithProviders(<CollectionDashboard />);

      expect(screen.getByTestId('collection-toolbar')).toBeInTheDocument();
      expect(screen.getByTestId('collection-search')).toBeInTheDocument();
    });

    it('should render view mode toggle buttons', () => {
      renderWithProviders(<CollectionDashboard />);

      expect(screen.getByTestId('view-mode-grid')).toBeInTheDocument();
      expect(screen.getByTestId('view-mode-list')).toBeInTheDocument();
    });

    it('should render filter chips', () => {
      renderWithProviders(<CollectionDashboard />);

      expect(screen.getByTestId('filter-chip-all')).toBeInTheDocument();
      expect(screen.getByTestId('filter-chip-favorites')).toBeInTheDocument();
      expect(screen.getByTestId('filter-chip-nuovo')).toBeInTheDocument();
      expect(screen.getByTestId('filter-chip-inprestito')).toBeInTheDocument();
      expect(screen.getByTestId('filter-chip-wishlist')).toBeInTheDocument();
      expect(screen.getByTestId('filter-chip-owned')).toBeInTheDocument();
    });

    it('should render game grid', () => {
      renderWithProviders(<CollectionDashboard />);

      expect(screen.getByTestId('collection-grid')).toBeInTheDocument();
    });

    it('should display correct stat values', () => {
      renderWithProviders(<CollectionDashboard />);

      // Check hero stats display correct values
      const totalStat = screen.getByTestId('hero-stat-total');
      expect(within(totalStat).getByText('25')).toBeInTheDocument();

      const favoritesStat = screen.getByTestId('hero-stat-favorites');
      expect(within(favoritesStat).getByText('8')).toBeInTheDocument();
    });
  });

  describe('Interactions', () => {
    it('should toggle view mode when clicking grid/list buttons', async () => {
      const user = userEvent.setup();
      renderWithProviders(<CollectionDashboard />);

      // Grid should be default active
      const gridButton = screen.getByTestId('view-mode-grid');
      const listButton = screen.getByTestId('view-mode-list');

      // Click list view
      await user.click(listButton);

      // Grid should still exist (view changes don't remove elements)
      expect(screen.getByTestId('collection-grid')).toBeInTheDocument();
    });

    it('should update search when typing in search input', async () => {
      const user = userEvent.setup();
      renderWithProviders(<CollectionDashboard />);

      const searchInput = screen.getByTestId('collection-search');
      await user.type(searchInput, 'Wingspan');

      expect(searchInput).toHaveValue('Wingspan');
    });

    it('should toggle filter chip when clicked', async () => {
      const user = userEvent.setup();
      renderWithProviders(<CollectionDashboard />);

      const favoritesChip = screen.getByTestId('filter-chip-favorites');
      await user.click(favoritesChip);

      // Check aria-pressed state changed
      expect(favoritesChip).toHaveAttribute('aria-pressed', 'true');
    });

    it('should clear filters when clicking clear button', async () => {
      const user = userEvent.setup();
      renderWithProviders(<CollectionDashboard />);

      // First activate a filter
      const favoritesChip = screen.getByTestId('filter-chip-favorites');
      await user.click(favoritesChip);

      // Should show clear button
      const clearButton = screen.getByText('Pulisci');
      await user.click(clearButton);

      // All chip should now be active
      const allChip = screen.getByTestId('filter-chip-all');
      expect(allChip).toHaveAttribute('aria-pressed', 'true');
    });
  });

  describe('Loading States', () => {
    it('should show skeleton while loading stats', async () => {
      const { useLibraryStats } = await import('@/hooks/queries/useLibrary');
      vi.mocked(useLibraryStats).mockReturnValue({
        data: undefined,
        isLoading: true,
        error: null,
      } as ReturnType<typeof useLibraryStats>);

      renderWithProviders(<CollectionDashboard />);

      // When loading, hero stats section should still exist
      // but with skeleton placeholders (implementation detail)
      expect(screen.getByRole('region', { name: /statistics/i })).toBeInTheDocument();
    });
  });

  describe('Empty States', () => {
    it('should show empty state when no games in library', async () => {
      const { useLibrary } = await import('@/hooks/queries/useLibrary');
      vi.mocked(useLibrary).mockReturnValue({
        data: { items: [], totalCount: 0, pageSize: 20, page: 1, totalPages: 0 },
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibrary>);

      renderWithProviders(<CollectionDashboard />);

      expect(screen.getByTestId('collection-empty-state')).toBeInTheDocument();
      expect(screen.getByText('La tua collezione è vuota')).toBeInTheDocument();
    });

    it('should show empty state when filters return no results', async () => {
      const { useLibrary } = await import('@/hooks/queries/useLibrary');

      // Pre-mock before render so the component sees empty results immediately
      vi.mocked(useLibrary).mockReturnValue({
        data: { items: [], totalCount: 0, pageSize: 20, page: 1, totalPages: 0 },
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibrary>);

      const user = userEvent.setup();
      renderWithProviders(<CollectionDashboard />);

      // Apply a filter
      const favoritesChip = screen.getByTestId('filter-chip-favorites');
      await user.click(favoritesChip);

      // Empty state should still be visible
      expect(screen.getByTestId('collection-empty-state')).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('should have proper ARIA labels on search input', () => {
      renderWithProviders(<CollectionDashboard />);

      const searchInput = screen.getByRole('searchbox', { name: /search collection/i });
      expect(searchInput).toBeInTheDocument();
    });

    it('should have proper ARIA labels on view mode buttons', () => {
      renderWithProviders(<CollectionDashboard />);

      expect(screen.getByRole('button', { name: /grid view/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /list view/i })).toBeInTheDocument();
    });

    it('should have proper ARIA labels on filter chips', () => {
      renderWithProviders(<CollectionDashboard />);

      // Initially "all" chip is active (aria-pressed="true"), others are inactive
      const allChip = screen.getByTestId('filter-chip-all');
      expect(allChip).toHaveAttribute('aria-pressed', 'true');

      const favoritesChip = screen.getByTestId('filter-chip-favorites');
      expect(favoritesChip).toHaveAttribute('aria-pressed', 'false');
    });

    it('should have proper section landmarks', () => {
      renderWithProviders(<CollectionDashboard />);

      expect(screen.getByRole('region', { name: /statistics/i })).toBeInTheDocument();
      expect(screen.getByRole('region', { name: /filters/i })).toBeInTheDocument();
      // Use exact match for "Game collection" to avoid matching "Collection statistics"
      expect(screen.getByRole('region', { name: 'Game collection' })).toBeInTheDocument();
    });
  });

  describe('Pagination', () => {
    it('should display pagination when multiple pages exist', () => {
      renderWithProviders(<CollectionDashboard />);

      expect(screen.getByText(/Pagina 1 di 2/i)).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /precedente/i })).toBeDisabled();
      expect(screen.getByRole('button', { name: /successiva/i })).toBeEnabled();
    });

    it('should navigate to next page when clicking next button', async () => {
      const user = userEvent.setup();
      renderWithProviders(<CollectionDashboard />);

      const nextButton = screen.getByRole('button', { name: /successiva/i });
      await user.click(nextButton);

      // Page state should update
      await waitFor(() => {
        expect(screen.getByText(/Pagina 2 di 2/i)).toBeInTheDocument();
      });
    });
  });
});
