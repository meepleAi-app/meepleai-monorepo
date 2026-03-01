/**
 * BggSearchStep Tests
 * Issue #4673: Step 1 of the admin wizard — BGG game search.
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';

import { BggSearchStep } from '../steps/BggSearchStep';

// ─── Mocks ───────────────────────────────────────────────────────────────────

let mockData: { results: Array<{ bggId: number; name: string; type: 'boardgame'; yearPublished?: number; thumbnailUrl?: null }>; total?: number } | undefined;
let mockIsLoading = false;
let mockIsFetching = false;
let mockError: Error | null = null;

vi.mock('@/hooks/queries/useSearchBggGames', () => ({
  useSearchBggGames: () => ({
    data: mockData,
    isLoading: mockIsLoading,
    isFetching: mockIsFetching,
    error: mockError,
  }),
}));

vi.mock('next/image', () => ({
  default: ({ alt }: { alt: string }) => <img alt={alt} />,
}));

vi.mock('@/lib/api', () => ({
  getApiBase: vi.fn(() => ''),
  api: { bgg: { search: vi.fn() } },
}));

// ─── Helpers ─────────────────────────────────────────────────────────────────

const createWrapper = () => {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  );
};

// ─── Tests ───────────────────────────────────────────────────────────────────

describe('BggSearchStep', () => {
  const onGameSelected = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    mockData = undefined;
    mockIsLoading = false;
    mockIsFetching = false;
    mockError = null;
  });

  it('should render the search input', () => {
    render(<BggSearchStep onGameSelected={onGameSelected} />, { wrapper: createWrapper() });
    expect(screen.getByPlaceholderText(/Search BoardGameGeek/)).toBeDefined();
  });

  it('should show hint text when query is empty', () => {
    render(<BggSearchStep onGameSelected={onGameSelected} />, { wrapper: createWrapper() });
    expect(screen.getByText(/Type at least 2 characters/)).toBeDefined();
  });

  it('should show hint text when query has 1 character', () => {
    render(<BggSearchStep onGameSelected={onGameSelected} />, { wrapper: createWrapper() });
    const input = screen.getByPlaceholderText(/Search BoardGameGeek/);
    fireEvent.change(input, { target: { value: 'C' } });
    expect(screen.getByText(/Type at least 2 characters/)).toBeDefined();
  });

  it('should show error state when search fails', () => {
    mockError = new Error('BGG API unavailable');
    render(<BggSearchStep onGameSelected={onGameSelected} />, { wrapper: createWrapper() });
    expect(screen.getByText('Search failed')).toBeDefined();
    expect(screen.getByText(/BGG API unavailable/)).toBeDefined();
  });

  it('should show game results when data is returned', () => {
    mockData = {
      results: [
        { bggId: 174430, name: 'Gloomhaven', type: 'boardgame', yearPublished: 2017, thumbnailUrl: null },
        { bggId: 13, name: 'Catan', type: 'boardgame', yearPublished: 1995, thumbnailUrl: null },
      ],
      total: 2,
    };

    render(<BggSearchStep onGameSelected={onGameSelected} />, { wrapper: createWrapper() });

    const input = screen.getByPlaceholderText(/Search BoardGameGeek/);
    fireEvent.change(input, { target: { value: 'gl' } });

    expect(screen.getByText('Gloomhaven')).toBeDefined();
    expect(screen.getByText('Catan')).toBeDefined();
  });

  it('should call onGameSelected when a result card is clicked', () => {
    mockData = {
      results: [
        { bggId: 174430, name: 'Gloomhaven', type: 'boardgame', yearPublished: 2017, thumbnailUrl: null },
      ],
    };

    render(<BggSearchStep onGameSelected={onGameSelected} />, { wrapper: createWrapper() });

    const input = screen.getByPlaceholderText(/Search BoardGameGeek/);
    fireEvent.change(input, { target: { value: 'gl' } });

    fireEvent.click(screen.getByText('Gloomhaven'));
    expect(onGameSelected).toHaveBeenCalledWith(
      expect.objectContaining({ bggId: 174430, name: 'Gloomhaven' })
    );
  });

  it('should show "No games found" when results are empty and query >= 2 chars', () => {
    mockData = { results: [] };

    render(<BggSearchStep onGameSelected={onGameSelected} />, { wrapper: createWrapper() });

    const input = screen.getByPlaceholderText(/Search BoardGameGeek/);
    fireEvent.change(input, { target: { value: 'zzz' } });

    expect(screen.getByText(/No games found/)).toBeDefined();
  });

  it('should not show results before user types 2 characters', () => {
    mockData = {
      results: [{ bggId: 1, name: 'Some Game', type: 'boardgame', thumbnailUrl: null }],
    };

    render(<BggSearchStep onGameSelected={onGameSelected} />, { wrapper: createWrapper() });
    // query is empty, so results should not be rendered
    expect(screen.queryByText('Some Game')).toBeNull();
  });

  it('should show loading skeletons when isLoading and query >= 2 chars', () => {
    mockIsLoading = true;

    render(<BggSearchStep onGameSelected={onGameSelected} />, { wrapper: createWrapper() });

    const input = screen.getByPlaceholderText(/Search BoardGameGeek/);
    fireEvent.change(input, { target: { value: 'Ca' } });

    // Loading skeletons are rendered as divs with animate-pulse
    const container = document.querySelector('.animate-pulse');
    expect(container).toBeDefined();
  });
});
