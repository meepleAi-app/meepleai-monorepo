/**
 * PlayHistory Component Tests
 *
 * Tests filtering, sorting, and MeepleCard integration.
 * Issue #3892: Play Records Frontend UI
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { PlayHistory } from '@/components/play-records/PlayHistory';
import * as usePlayRecordsHooks from '@/lib/hooks/use-play-records';

// Mock next/navigation
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: vi.fn(),
  }),
}));

// Mock hooks
vi.mock('@/lib/hooks/use-play-records', () => ({
  usePlayHistory: vi.fn(),
}));

// Mock Zustand store — support selector pattern: usePlayRecordsStore(state => state.field)
const mockStoreState = {
  viewMode: 'grid' as const,
  sortBy: 'recent' as const,
  sidebarOpen: true,
  setFilter: vi.fn(),
  resetFilters: vi.fn(),
  setViewMode: vi.fn(),
  setSortBy: vi.fn(),
  toggleSidebar: vi.fn(),
};
vi.mock('@/lib/stores/play-records-store', () => ({
  usePlayRecordsStore: vi.fn((selector?: (state: typeof mockStoreState) => unknown) =>
    selector ? selector(mockStoreState) : mockStoreState
  ),
  selectFilters: vi.fn(() => ({
    status: 'all',
    searchQuery: '',
  })),
  selectViewPreferences: vi.fn(() => ({
    viewMode: 'grid',
    sortBy: 'recent',
    sidebarOpen: true,
  })),
  selectHasActiveFilters: vi.fn(() => false),
}));

const mockHistoryData = {
  records: [
    {
      id: '1',
      gameName: 'Twilight Imperium',
      sessionDate: '2026-02-01T14:00:00Z',
      duration: 'PT4H30M',
      status: 'Completed' as const,
      playerCount: 6,
    },
    {
      id: '2',
      gameName: 'Wingspan',
      sessionDate: '2026-02-05T18:00:00Z',
      duration: 'PT1H45M',
      status: 'InProgress' as const,
      playerCount: 4,
    },
  ],
  total: 2,
  page: 1,
  pageSize: 20,
  totalPages: 1,
};

describe('PlayHistory', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false },
        mutations: { retry: false },
      },
    });

    vi.mocked(usePlayRecordsHooks.usePlayHistory).mockReturnValue({
      data: mockHistoryData,
      isLoading: false,
      error: null,
    } as any);
  });

  const renderComponent = () => {
    return render(
      <QueryClientProvider client={queryClient}>
        <PlayHistory />
      </QueryClientProvider>
    );
  };

  it('renders play history title', () => {
    renderComponent();
    expect(screen.getByText('Play History')).toBeInTheDocument();
    expect(screen.getByText(/Your recorded game sessions/)).toBeInTheDocument();
  });

  it('displays session cards', () => {
    renderComponent();
    expect(screen.getByText('Twilight Imperium')).toBeInTheDocument();
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
  });

  it('shows loading state', () => {
    vi.mocked(usePlayRecordsHooks.usePlayHistory).mockReturnValue({
      data: undefined,
      isLoading: true,
      error: null,
    } as any);

    renderComponent();
    const skeletons = screen.getAllByRole('generic');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('shows empty state when no records', () => {
    vi.mocked(usePlayRecordsHooks.usePlayHistory).mockReturnValue({
      data: { ...mockHistoryData, records: [], total: 0 },
      isLoading: false,
      error: null,
    } as any);

    renderComponent();
    expect(screen.getByText('No Play Records Yet')).toBeInTheDocument();
  });

  it('toggles view mode', () => {
    renderComponent();
    const gridButton = screen.getByLabelText('Grid view');
    const listButton = screen.getByLabelText('List view');

    expect(gridButton).toBeInTheDocument();
    expect(listButton).toBeInTheDocument();

    fireEvent.click(listButton);
    // Store update would be tested separately
  });

  it('shows error state', () => {
    vi.mocked(usePlayRecordsHooks.usePlayHistory).mockReturnValue({
      data: undefined,
      isLoading: false,
      error: new Error('Network error'),
    } as any);

    renderComponent();
    expect(screen.getByText(/Network error/)).toBeInTheDocument();
  });
});
