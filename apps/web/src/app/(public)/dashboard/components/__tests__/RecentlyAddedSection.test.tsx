/**
 * RecentlyAddedSection Component Tests (Issue #2612)
 *
 * Test Coverage:
 * - Loading state rendering
 * - Error state rendering
 * - Empty state (hidden)
 * - Data rendering with games
 * - Responsive grid layout
 * - Navigation link to library
 *
 * Target: ≥90% coverage
 */

import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { RecentlyAddedSection } from '../RecentlyAddedSection';
import * as useLibraryModule from '@/hooks/queries/useLibrary';
import type { PaginatedLibraryResponse, UserLibraryEntry } from '@/lib/api';

// ============================================================================
// Mocks
// ============================================================================

// Mock the useRecentlyAddedGames hook
vi.mock('@/hooks/queries/useLibrary', async importOriginal => {
  const actual = await importOriginal<typeof useLibraryModule>();
  return {
    ...actual,
    useRecentlyAddedGames: vi.fn(),
  };
});

// Mock Next.js Link
vi.mock('next/link', () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

// ============================================================================
// Test Data
// ============================================================================

const mockGames: UserLibraryEntry[] = [
  {
    id: '1',
    userId: 'user-1',
    gameId: 'game-1',
    gameTitle: 'Azul',
    gamePublisher: 'Plan B Games',
    gameYearPublished: 2017,
    gameIconUrl: null,
    gameImageUrl: 'https://example.com/azul.png',
    addedAt: new Date().toISOString(),
    notes: null,
    isFavorite: true,
  },
  {
    id: '2',
    userId: 'user-1',
    gameId: 'game-2',
    gameTitle: 'Wingspan',
    gamePublisher: 'Stonemaier Games',
    gameYearPublished: 2019,
    gameIconUrl: null,
    gameImageUrl: 'https://example.com/wingspan.png',
    addedAt: new Date().toISOString(),
    notes: null,
    isFavorite: false,
  },
  {
    id: '3',
    userId: 'user-1',
    gameId: 'game-3',
    gameTitle: 'Catan',
    gamePublisher: 'Kosmos',
    gameYearPublished: 1995,
    gameIconUrl: null,
    gameImageUrl: null,
    addedAt: new Date().toISOString(),
    notes: 'Classic!',
    isFavorite: false,
  },
];

const mockPaginatedResponse: PaginatedLibraryResponse = {
  items: mockGames,
  page: 1,
  pageSize: 5,
  totalCount: 3,
  totalPages: 1,
  hasNextPage: false,
  hasPreviousPage: false,
};

const emptyPaginatedResponse: PaginatedLibraryResponse = {
  items: [],
  page: 1,
  pageSize: 5,
  totalCount: 0,
  totalPages: 0,
  hasNextPage: false,
  hasPreviousPage: false,
};

// ============================================================================
// Test Suite
// ============================================================================

describe('RecentlyAddedSection', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = new QueryClient({
      defaultOptions: {
        queries: {
          retry: false,
        },
      },
    });
    vi.clearAllMocks();
  });

  const renderComponent = (limit?: number) => {
    return render(
      <QueryClientProvider client={queryClient}>
        <RecentlyAddedSection limit={limit} />
      </QueryClientProvider>
    );
  };

  // ============================================================================
  // Loading State Tests
  // ============================================================================

  describe('Loading State', () => {
    it('renders skeleton grid while loading', () => {
      vi.mocked(useLibraryModule.useRecentlyAddedGames).mockReturnValue({
        data: undefined,
        isLoading: true,
        error: null,
      } as ReturnType<typeof useLibraryModule.useRecentlyAddedGames>);

      renderComponent();

      expect(screen.getByText('Aggiunti di Recente')).toBeInTheDocument();
      expect(screen.getByLabelText('Recently added games')).toBeInTheDocument();
    });

    it('renders correct number of skeletons based on limit', () => {
      vi.mocked(useLibraryModule.useRecentlyAddedGames).mockReturnValue({
        data: undefined,
        isLoading: true,
        error: null,
      } as ReturnType<typeof useLibraryModule.useRecentlyAddedGames>);

      const { container } = renderComponent(3);

      // Check for skeleton elements
      const skeletons = container.querySelectorAll('.animate-pulse');
      expect(skeletons.length).toBe(3);
    });
  });

  // ============================================================================
  // Error State Tests
  // ============================================================================

  describe('Error State', () => {
    it('renders error alert when query fails', () => {
      vi.mocked(useLibraryModule.useRecentlyAddedGames).mockReturnValue({
        data: undefined,
        isLoading: false,
        error: new Error('Network error'),
      } as ReturnType<typeof useLibraryModule.useRecentlyAddedGames>);

      renderComponent();

      expect(screen.getByText('Errore di Caricamento')).toBeInTheDocument();
      expect(screen.getByText(/Impossibile caricare i giochi dalla libreria/)).toBeInTheDocument();
      expect(screen.getByText('Network error')).toBeInTheDocument();
    });

    it('handles non-Error error objects', () => {
      vi.mocked(useLibraryModule.useRecentlyAddedGames).mockReturnValue({
        data: undefined,
        isLoading: false,
        error: 'String error message',
      } as unknown as ReturnType<typeof useLibraryModule.useRecentlyAddedGames>);

      renderComponent();

      expect(screen.getByText('String error message')).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Empty State Tests
  // ============================================================================

  describe('Empty State', () => {
    it('returns null when library is empty', () => {
      vi.mocked(useLibraryModule.useRecentlyAddedGames).mockReturnValue({
        data: emptyPaginatedResponse,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useRecentlyAddedGames>);

      const { container } = renderComponent();

      // Section should not be rendered
      expect(container.querySelector('section')).not.toBeInTheDocument();
    });

    it('returns null when data.items is undefined', () => {
      vi.mocked(useLibraryModule.useRecentlyAddedGames).mockReturnValue({
        data: { ...emptyPaginatedResponse, items: undefined } as PaginatedLibraryResponse,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useRecentlyAddedGames>);

      const { container } = renderComponent();

      expect(container.querySelector('section')).not.toBeInTheDocument();
    });
  });

  // ============================================================================
  // Data Rendering Tests
  // ============================================================================

  describe('Data Rendering', () => {
    it('renders game cards when data is available', () => {
      vi.mocked(useLibraryModule.useRecentlyAddedGames).mockReturnValue({
        data: mockPaginatedResponse,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useRecentlyAddedGames>);

      renderComponent();

      expect(screen.getByText('Azul')).toBeInTheDocument();
      expect(screen.getByText('Wingspan')).toBeInTheDocument();
      expect(screen.getByText('Catan')).toBeInTheDocument();
    });

    it('renders section header with title', () => {
      vi.mocked(useLibraryModule.useRecentlyAddedGames).mockReturnValue({
        data: mockPaginatedResponse,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useRecentlyAddedGames>);

      renderComponent();

      expect(screen.getByText('Aggiunti di Recente')).toBeInTheDocument();
    });

    it('renders "Vedi Tutti" link to library', () => {
      vi.mocked(useLibraryModule.useRecentlyAddedGames).mockReturnValue({
        data: mockPaginatedResponse,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useRecentlyAddedGames>);

      renderComponent();

      const link = screen.getByRole('link', { name: /Vedi Tutti/i });
      expect(link).toHaveAttribute('href', '/library');
    });

    it('renders library icon in header', () => {
      vi.mocked(useLibraryModule.useRecentlyAddedGames).mockReturnValue({
        data: mockPaginatedResponse,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useRecentlyAddedGames>);

      const { container } = renderComponent();

      const libraryIcon = container.querySelector('svg.lucide-library');
      expect(libraryIcon).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Hook Integration Tests
  // ============================================================================

  describe('Hook Integration', () => {
    it('passes correct limit to useRecentlyAddedGames', () => {
      vi.mocked(useLibraryModule.useRecentlyAddedGames).mockReturnValue({
        data: mockPaginatedResponse,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useRecentlyAddedGames>);

      renderComponent(3);

      expect(useLibraryModule.useRecentlyAddedGames).toHaveBeenCalledWith(3);
    });

    it('uses default limit of 5 when not specified', () => {
      vi.mocked(useLibraryModule.useRecentlyAddedGames).mockReturnValue({
        data: mockPaginatedResponse,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useRecentlyAddedGames>);

      renderComponent();

      expect(useLibraryModule.useRecentlyAddedGames).toHaveBeenCalledWith(5);
    });
  });

  // ============================================================================
  // Accessibility Tests
  // ============================================================================

  describe('Accessibility', () => {
    it('has correct aria-label on section', () => {
      vi.mocked(useLibraryModule.useRecentlyAddedGames).mockReturnValue({
        data: mockPaginatedResponse,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useRecentlyAddedGames>);

      renderComponent();

      expect(screen.getByLabelText('Recently added games')).toBeInTheDocument();
    });

    it('has correct heading structure', () => {
      vi.mocked(useLibraryModule.useRecentlyAddedGames).mockReturnValue({
        data: mockPaginatedResponse,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useRecentlyAddedGames>);

      renderComponent();

      const heading = screen.getByRole('heading', { level: 2 });
      expect(heading).toHaveTextContent('Aggiunti di Recente');
    });
  });
});
