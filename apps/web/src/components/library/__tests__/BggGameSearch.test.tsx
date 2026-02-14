/**
 * BggGameSearch Component Tests
 * Issue #4053: User-Facing BGG Search for Private Game Creation
 */

import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { BggGameSearch } from '../BggGameSearch';

// Mock API
const mockSearch = vi.fn();
vi.mock('@/lib/api', () => ({
  api: {
    bgg: {
      search: (...args: unknown[]) => mockSearch(...args),
    },
  },
}));

// Mock sonner
vi.mock('sonner', () => ({
  toast: {
    info: vi.fn(),
    error: vi.fn(),
  },
}));

const mockResults = [
  {
    bggId: 13,
    name: 'Catan',
    yearPublished: 1995,
    thumbnailUrl: 'https://example.com/catan.jpg',
    type: 'boardgame',
  },
  {
    bggId: 822,
    name: 'Carcassonne',
    yearPublished: 2000,
    thumbnailUrl: null,
    type: 'boardgame',
  },
];

describe('BggGameSearch', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    mockSearch.mockReset();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  // ===== Rendering =====

  it('renders search input and exact search button', () => {
    render(<BggGameSearch onSelect={vi.fn()} />);

    expect(screen.getByTestId('bgg-search-input')).toBeInTheDocument();
    expect(screen.getByTestId('bgg-exact-search-btn')).toBeInTheDocument();
  });

  it('renders with custom placeholder', () => {
    render(<BggGameSearch onSelect={vi.fn()} placeholder="Custom placeholder" />);

    expect(screen.getByPlaceholderText('Custom placeholder')).toBeInTheDocument();
  });

  it('has correct ARIA attributes on input', () => {
    render(<BggGameSearch onSelect={vi.fn()} />);
    const input = screen.getByTestId('bgg-search-input');

    expect(input).toHaveAttribute('role', 'combobox');
    expect(input).toHaveAttribute('aria-autocomplete', 'list');
    expect(input).toHaveAttribute('aria-expanded', 'false');
  });

  // ===== Search Behavior =====

  it('performs debounced search after 300ms', async () => {
    mockSearch.mockResolvedValue({ results: mockResults, total: 2, page: 1, pageSize: 20, totalPages: 1 });
    render(<BggGameSearch onSelect={vi.fn()} />);

    const input = screen.getByTestId('bgg-search-input');
    fireEvent.change(input, { target: { value: 'Catan' } });

    // Should not search immediately
    expect(mockSearch).not.toHaveBeenCalled();

    // Advance past debounce
    await act(async () => {
      vi.advanceTimersByTime(300);
    });

    await waitFor(() => {
      expect(mockSearch).toHaveBeenCalledWith('Catan', false);
    });
  });

  it('does not search with less than 2 characters', async () => {
    render(<BggGameSearch onSelect={vi.fn()} />);

    const input = screen.getByTestId('bgg-search-input');
    fireEvent.change(input, { target: { value: 'C' } });

    await act(async () => {
      vi.advanceTimersByTime(300);
    });

    expect(mockSearch).not.toHaveBeenCalled();
  });

  it('displays search results', async () => {
    mockSearch.mockResolvedValue({ results: mockResults, total: 2, page: 1, pageSize: 20, totalPages: 1 });
    render(<BggGameSearch onSelect={vi.fn()} />);

    const input = screen.getByTestId('bgg-search-input');
    fireEvent.change(input, { target: { value: 'Catan' } });

    await act(async () => {
      vi.advanceTimersByTime(300);
    });

    await waitFor(() => {
      expect(screen.getByTestId('bgg-search-results')).toBeInTheDocument();
      expect(screen.getByText('Catan')).toBeInTheDocument();
      expect(screen.getByText('Carcassonne')).toBeInTheDocument();
    });
  });

  it('displays year and type for results', async () => {
    mockSearch.mockResolvedValue({ results: mockResults, total: 2, page: 1, pageSize: 20, totalPages: 1 });
    render(<BggGameSearch onSelect={vi.fn()} />);

    const input = screen.getByTestId('bgg-search-input');
    fireEvent.change(input, { target: { value: 'Catan' } });

    await act(async () => {
      vi.advanceTimersByTime(300);
    });

    await waitFor(() => {
      expect(screen.getByText('1995')).toBeInTheDocument();
      expect(screen.getByText('2000')).toBeInTheDocument();
    });
  });

  it('renders thumbnail for results with image', async () => {
    mockSearch.mockResolvedValue({ results: mockResults, total: 2, page: 1, pageSize: 20, totalPages: 1 });
    render(<BggGameSearch onSelect={vi.fn()} />);

    const input = screen.getByTestId('bgg-search-input');
    fireEvent.change(input, { target: { value: 'Catan' } });

    await act(async () => {
      vi.advanceTimersByTime(300);
    });

    await waitFor(() => {
      const img = screen.getByAltText('Catan');
      expect(img).toHaveAttribute('src', 'https://example.com/catan.jpg');
    });
  });

  // ===== Selection =====

  it('calls onSelect when result is clicked', async () => {
    mockSearch.mockResolvedValue({ results: mockResults, total: 2, page: 1, pageSize: 20, totalPages: 1 });
    const onSelect = vi.fn();
    render(<BggGameSearch onSelect={onSelect} />);

    const input = screen.getByTestId('bgg-search-input');
    fireEvent.change(input, { target: { value: 'Catan' } });

    await act(async () => {
      vi.advanceTimersByTime(300);
    });

    await waitFor(() => {
      expect(screen.getByTestId('bgg-search-result-0')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId('bgg-search-result-0'));

    expect(onSelect).toHaveBeenCalledWith(mockResults[0]);
  });

  // ===== Keyboard Navigation =====

  it('supports ArrowDown keyboard navigation', async () => {
    mockSearch.mockResolvedValue({ results: mockResults, total: 2, page: 1, pageSize: 20, totalPages: 1 });
    render(<BggGameSearch onSelect={vi.fn()} />);

    const input = screen.getByTestId('bgg-search-input');
    fireEvent.change(input, { target: { value: 'Catan' } });

    await act(async () => {
      vi.advanceTimersByTime(300);
    });

    await waitFor(() => {
      expect(screen.getByTestId('bgg-search-results')).toBeInTheDocument();
    });

    fireEvent.keyDown(input, { key: 'ArrowDown' });

    const firstOption = screen.getByTestId('bgg-search-result-0');
    expect(firstOption).toHaveAttribute('aria-selected', 'true');
  });

  it('selects highlighted result on Enter', async () => {
    mockSearch.mockResolvedValue({ results: mockResults, total: 2, page: 1, pageSize: 20, totalPages: 1 });
    const onSelect = vi.fn();
    render(<BggGameSearch onSelect={onSelect} />);

    const input = screen.getByTestId('bgg-search-input');
    fireEvent.change(input, { target: { value: 'Catan' } });

    await act(async () => {
      vi.advanceTimersByTime(300);
    });

    await waitFor(() => {
      expect(screen.getByTestId('bgg-search-results')).toBeInTheDocument();
    });

    fireEvent.keyDown(input, { key: 'ArrowDown' });
    fireEvent.keyDown(input, { key: 'Enter' });

    expect(onSelect).toHaveBeenCalledWith(mockResults[0]);
  });

  it('closes dropdown on Escape', async () => {
    mockSearch.mockResolvedValue({ results: mockResults, total: 2, page: 1, pageSize: 20, totalPages: 1 });
    render(<BggGameSearch onSelect={vi.fn()} />);

    const input = screen.getByTestId('bgg-search-input');
    fireEvent.change(input, { target: { value: 'Catan' } });

    await act(async () => {
      vi.advanceTimersByTime(300);
    });

    await waitFor(() => {
      expect(screen.getByTestId('bgg-search-results')).toBeInTheDocument();
    });

    fireEvent.keyDown(input, { key: 'Escape' });

    expect(screen.queryByTestId('bgg-search-results')).not.toBeInTheDocument();
  });

  // ===== Exact Search =====

  it('performs exact search when button is clicked', async () => {
    mockSearch.mockResolvedValue({ results: mockResults, total: 2, page: 1, pageSize: 20, totalPages: 1 });
    render(<BggGameSearch onSelect={vi.fn()} />);

    const input = screen.getByTestId('bgg-search-input');
    fireEvent.change(input, { target: { value: 'Catan' } });

    // Click exact search without waiting for debounce
    fireEvent.click(screen.getByTestId('bgg-exact-search-btn'));

    await waitFor(() => {
      expect(mockSearch).toHaveBeenCalledWith('Catan', true);
    });
  });

  it('disables exact search button with less than 2 characters', () => {
    render(<BggGameSearch onSelect={vi.fn()} />);

    expect(screen.getByTestId('bgg-exact-search-btn')).toBeDisabled();
  });

  // ===== Error Handling =====

  it('shows error message on search failure', async () => {
    mockSearch.mockRejectedValue(new Error('Network error'));
    render(<BggGameSearch onSelect={vi.fn()} />);

    const input = screen.getByTestId('bgg-search-input');
    fireEvent.change(input, { target: { value: 'Catan' } });

    await act(async () => {
      vi.advanceTimersByTime(300);
    });

    await waitFor(() => {
      expect(screen.getByTestId('bgg-search-error')).toBeInTheDocument();
    });
  });

  // ===== Empty State =====

  it('shows empty state when no results found', async () => {
    mockSearch.mockResolvedValue({ results: [], total: 0, page: 1, pageSize: 20, totalPages: 0 });
    render(<BggGameSearch onSelect={vi.fn()} />);

    const input = screen.getByTestId('bgg-search-input');
    fireEvent.change(input, { target: { value: 'zzznoexist' } });

    await act(async () => {
      vi.advanceTimersByTime(300);
    });

    await waitFor(() => {
      expect(screen.getByTestId('bgg-search-empty')).toBeInTheDocument();
    });
  });

  // ===== BGG External Links =====

  it('renders BGG external links for each result', async () => {
    mockSearch.mockResolvedValue({ results: mockResults, total: 2, page: 1, pageSize: 20, totalPages: 1 });
    render(<BggGameSearch onSelect={vi.fn()} />);

    const input = screen.getByTestId('bgg-search-input');
    fireEvent.change(input, { target: { value: 'Catan' } });

    await act(async () => {
      vi.advanceTimersByTime(300);
    });

    await waitFor(() => {
      const links = screen.getAllByTitle('Vedi su BGG');
      expect(links).toHaveLength(2);
      expect(links[0]).toHaveAttribute('href', 'https://boardgamegeek.com/boardgame/13');
    });
  });
});
