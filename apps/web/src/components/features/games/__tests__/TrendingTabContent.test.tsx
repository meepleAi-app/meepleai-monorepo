/**
 * TrendingTabContent tests — Issue #2191
 *
 * Verifies that the `/games?tab=trending` tab body:
 *  - Calls `useCatalogTrending(20)` (default limit)
 *  - Renders the HorizontalRow grid with the returned items
 *  - Surfaces loading / error / empty states from the hook
 *  - Maps the catalog DTO → `RowItemBase` shape correctly
 */

import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const useCatalogTrendingMock = vi.fn();
vi.mock('@/hooks/queries/useCatalogTrending', () => ({
  useCatalogTrending: (limit: number) => useCatalogTrendingMock(limit),
}));

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
}));

const horizontalRowSpy = vi.fn();
vi.mock('@/components/features/discover', () => ({
  HorizontalRow: (props: unknown) => {
    horizontalRowSpy(props);
    return <div data-testid="horizontal-row-mock">HorizontalRow mock</div>;
  },
}));

vi.mock('@/lib/analytics/track-event', () => ({
  trackEvent: vi.fn(),
}));

import { TrendingTabContent } from '../TrendingTabContent';

describe('TrendingTabContent (#2191)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('calls useCatalogTrending with default limit 20', () => {
    useCatalogTrendingMock.mockReturnValue({
      data: [],
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    });

    render(<TrendingTabContent />);

    expect(useCatalogTrendingMock).toHaveBeenCalledWith(20);
  });

  it('passes loading flag through to HorizontalRow', () => {
    useCatalogTrendingMock.mockReturnValue({
      data: undefined,
      isLoading: true,
      isError: false,
      refetch: vi.fn(),
    });

    render(<TrendingTabContent />);

    expect(horizontalRowSpy).toHaveBeenCalledWith(expect.objectContaining({ isLoading: true }));
  });

  it('passes error flag through to HorizontalRow', () => {
    useCatalogTrendingMock.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
      refetch: vi.fn(),
    });

    render(<TrendingTabContent />);

    expect(horizontalRowSpy).toHaveBeenCalledWith(expect.objectContaining({ isError: true }));
  });

  it('maps the catalog DTO to RowItemBase shape', () => {
    useCatalogTrendingMock.mockReturnValue({
      data: [
        { gameId: 'g-1', title: 'Catan', thumbnailUrl: 'https://example.com/catan.jpg' },
        { gameId: 'g-2', title: 'Carcassonne', thumbnailUrl: null },
      ],
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    });

    render(<TrendingTabContent />);

    expect(horizontalRowSpy).toHaveBeenCalledWith(
      expect.objectContaining({
        items: [
          { id: 'g-1', name: 'Catan', imageUrl: 'https://example.com/catan.jpg' },
          { id: 'g-2', name: 'Carcassonne', imageUrl: null },
        ],
        variant: 'grid',
        rowId: 'trending',
      })
    );
  });

  it('renders an outer container with data-testid="games-tab-trending"', () => {
    useCatalogTrendingMock.mockReturnValue({
      data: [],
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    });

    render(<TrendingTabContent />);

    expect(screen.getByTestId('games-tab-trending')).toBeInTheDocument();
  });

  it('honors the limit prop when provided', () => {
    useCatalogTrendingMock.mockReturnValue({
      data: [],
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    });

    render(<TrendingTabContent limit={50} />);

    expect(useCatalogTrendingMock).toHaveBeenCalledWith(50);
  });
});
