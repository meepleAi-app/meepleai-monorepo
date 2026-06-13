/**
 * GamePicker — Unit tests for the 5 canonical states.
 *
 * Issue #2089 — Unified GamePicker shared component.
 *
 * Canonical states tested:
 *   1. loading       — input shows aria-busy + loader visible during search
 *   2. error         — sonner toast.error called when API fails (replaces silent catch{})
 *   3. empty         — "Nessun risultato" status when search has no matches
 *   4. manual-entry  — allowManualEntry=true surfaces a manual-input + "Usa" button
 *   5. manual-unknown-warning — selecting a manual entry surfaces the AI-not-available alert
 *
 * Plus: source dispatch contract (library | catalog | both) using mocked api.
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import { toast } from 'sonner';

// Mock the api module BEFORE importing the component
vi.mock('@/lib/api', () => ({
  api: {
    library: {
      getLibrary: vi.fn(),
    },
    sharedGames: {
      search: vi.fn(),
    },
  },
}));

vi.mock('sonner', () => ({
  toast: {
    error: vi.fn(),
    success: vi.fn(),
  },
}));

import { api } from '@/lib/api';
import { GamePicker, type GameOption } from '../GamePicker';

const mockGetLibrary = vi.mocked(api.library.getLibrary);
const mockSharedSearch = vi.mocked(api.sharedGames.search);
const mockToastError = vi.mocked(toast.error);

const sampleLibraryItem = {
  id: 'entry-1',
  userId: 'user-1',
  gameId: 'game-wingspan',
  gameTitle: 'Wingspan',
  gamePublisher: null,
  gameYearPublished: null,
  gameIconUrl: null,
  gameImageUrl: 'https://example.com/wingspan.png',
  addedAt: '2026-01-01T00:00:00.000Z',
  notes: null,
  isFavorite: false,
  currentState: 'Owned',
  stateChangedAt: '2026-01-01T00:00:00.000Z',
  stateNotes: null,
  hasKb: false,
  kbCardCount: 0,
  kbIndexedCount: 0,
  kbProcessingCount: 0,
  agentIsOwned: true,
  minPlayers: null,
  maxPlayers: null,
  playingTimeMinutes: null,
  complexityRating: null,
  averageRating: null,
  ownershipDeclaredAt: null,
  hasRagAccess: false,
  timesPlayed: 0,
  lastPlayed: null,
  coverUrl: null,
} as const;

const sampleCatalogItem = {
  id: 'cat-catan',
  title: 'Catan',
  yearPublished: 1995,
  thumbnailUrl: 'https://example.com/catan.png',
  minPlayers: 3,
  maxPlayers: 4,
  playingTimeMinutes: 90,
  averageRating: 7.2,
  bggId: 13,
} as const;

describe('GamePicker', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers({ shouldAdvanceTime: true });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  // -------------------------------------------------------------------------
  // State 1: Loading
  // -------------------------------------------------------------------------
  it('shows loading state (aria-busy + loader) while a search is in flight', async () => {
    mockGetLibrary.mockImplementation(
      () => new Promise(() => {}) // never resolves — hold loading state
    );

    render(<GamePicker source="library" value={null} onChange={vi.fn()} />);

    const input = screen.getByTestId('game-picker-input');
    fireEvent.change(input, { target: { value: 'wing' } });

    // Advance debounce timer (300ms default)
    await act(async () => {
      vi.advanceTimersByTime(350);
    });

    expect(input).toHaveAttribute('aria-busy', 'true');
    expect(screen.getByTestId('game-picker-loader')).toBeInTheDocument();
  });

  // -------------------------------------------------------------------------
  // State 2: Error
  // -------------------------------------------------------------------------
  it('shows error toast when the search API rejects (no silent catch)', async () => {
    mockGetLibrary.mockRejectedValue(new Error('Network down'));

    render(<GamePicker source="library" value={null} onChange={vi.fn()} />);

    const input = screen.getByTestId('game-picker-input');
    fireEvent.change(input, { target: { value: 'wing' } });

    await act(async () => {
      vi.advanceTimersByTime(350);
    });

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith(
        expect.stringContaining('Impossibile cercare i giochi')
      );
    });
  });

  // -------------------------------------------------------------------------
  // State 3: Empty
  // -------------------------------------------------------------------------
  it('shows empty state when results are []', async () => {
    mockGetLibrary.mockResolvedValue({
      items: [],
      page: 1,
      pageSize: 10,
      totalCount: 0,
      totalPages: 0,
      hasNextPage: false,
      hasPreviousPage: false,
    });

    render(<GamePicker source="library" value={null} onChange={vi.fn()} />);

    const input = screen.getByTestId('game-picker-input');
    fireEvent.change(input, { target: { value: 'nope' } });

    await act(async () => {
      vi.advanceTimersByTime(350);
    });

    await waitFor(() => {
      expect(screen.getByTestId('game-picker-empty')).toBeInTheDocument();
    });
    expect(screen.getByTestId('game-picker-empty')).toHaveTextContent(/nessun risultato/i);
  });

  // -------------------------------------------------------------------------
  // State 4: Manual entry
  // -------------------------------------------------------------------------
  it('renders manual-entry input when allowManualEntry=true', () => {
    render(<GamePicker source="library" value={null} onChange={vi.fn()} allowManualEntry />);

    expect(screen.getByTestId('game-picker-manual-input')).toBeInTheDocument();
    expect(screen.getByTestId('game-picker-manual-confirm')).toBeInTheDocument();
  });

  it('does NOT render manual-entry input when allowManualEntry=false (default)', () => {
    render(<GamePicker source="library" value={null} onChange={vi.fn()} />);

    expect(screen.queryByTestId('game-picker-manual-input')).not.toBeInTheDocument();
  });

  it('emits a manual GameOption when the user confirms a manual entry', () => {
    const onChange = vi.fn();
    render(<GamePicker source="library" value={null} onChange={onChange} allowManualEntry />);

    fireEvent.change(screen.getByTestId('game-picker-manual-input'), {
      target: { value: 'Homebrew Game' },
    });
    fireEvent.click(screen.getByTestId('game-picker-manual-confirm'));

    expect(onChange).toHaveBeenCalledWith(
      expect.objectContaining({
        title: 'Homebrew Game',
        manual: true,
      })
    );
  });

  // -------------------------------------------------------------------------
  // State 5: Manual-unknown warning
  // -------------------------------------------------------------------------
  it('shows the manual-unknown warning when the selected value is manual', () => {
    const value: GameOption = { id: 'manual-foo', title: 'Foo', manual: true };

    render(<GamePicker source="library" value={value} onChange={vi.fn()} />);

    expect(screen.getByTestId('game-picker-manual-warning')).toBeInTheDocument();
    expect(screen.getByTestId('game-picker-manual-warning')).toHaveTextContent(
      /gioco non riconosciuto/i
    );
  });

  it('does NOT show the manual-unknown warning for a non-manual (library/catalog) selection', () => {
    const value: GameOption = { id: 'game-1', title: 'Wingspan' };

    render(<GamePicker source="library" value={value} onChange={vi.fn()} />);

    expect(screen.queryByTestId('game-picker-manual-warning')).not.toBeInTheDocument();
  });

  // -------------------------------------------------------------------------
  // Source dispatch contract
  // -------------------------------------------------------------------------
  it('dispatches to api.library.getLibrary when source="library"', async () => {
    mockGetLibrary.mockResolvedValue({
      items: [sampleLibraryItem],
      page: 1,
      pageSize: 10,
      totalCount: 1,
      totalPages: 1,
      hasNextPage: false,
      hasPreviousPage: false,
    });

    render(<GamePicker source="library" value={null} onChange={vi.fn()} />);

    fireEvent.change(screen.getByTestId('game-picker-input'), { target: { value: 'wing' } });

    await act(async () => {
      vi.advanceTimersByTime(350);
    });

    await waitFor(() => {
      expect(mockGetLibrary).toHaveBeenCalledWith({ search: 'wing', pageSize: 10 });
    });
    expect(mockSharedSearch).not.toHaveBeenCalled();
  });

  it('dispatches to api.sharedGames.search when source="catalog"', async () => {
    mockSharedSearch.mockResolvedValue({
      items: [sampleCatalogItem],
      total: 1,
      page: 1,
      pageSize: 10,
    });

    render(<GamePicker source="catalog" value={null} onChange={vi.fn()} />);

    fireEvent.change(screen.getByTestId('game-picker-input'), { target: { value: 'catan' } });

    await act(async () => {
      vi.advanceTimersByTime(350);
    });

    await waitFor(() => {
      expect(mockSharedSearch).toHaveBeenCalledWith({
        searchTerm: 'catan',
        page: 1,
        pageSize: 10,
        status: 1,
      });
    });
    expect(mockGetLibrary).not.toHaveBeenCalled();
  });

  it('dispatches to BOTH library AND catalog when source="both"', async () => {
    mockGetLibrary.mockResolvedValue({
      items: [sampleLibraryItem],
      page: 1,
      pageSize: 10,
      totalCount: 1,
      totalPages: 1,
      hasNextPage: false,
      hasPreviousPage: false,
    });
    mockSharedSearch.mockResolvedValue({
      items: [sampleCatalogItem],
      total: 1,
      page: 1,
      pageSize: 10,
    });

    render(<GamePicker source="both" value={null} onChange={vi.fn()} />);

    fireEvent.change(screen.getByTestId('game-picker-input'), { target: { value: 'game' } });

    await act(async () => {
      vi.advanceTimersByTime(350);
    });

    await waitFor(() => {
      expect(mockGetLibrary).toHaveBeenCalled();
      expect(mockSharedSearch).toHaveBeenCalled();
    });
  });

  // -------------------------------------------------------------------------
  // Selection + clear
  // -------------------------------------------------------------------------
  it('selects an option via the click handler', async () => {
    mockGetLibrary.mockResolvedValue({
      items: [sampleLibraryItem],
      page: 1,
      pageSize: 10,
      totalCount: 1,
      totalPages: 1,
      hasNextPage: false,
      hasPreviousPage: false,
    });

    const onChange = vi.fn();
    render(<GamePicker source="library" value={null} onChange={onChange} />);

    fireEvent.change(screen.getByTestId('game-picker-input'), { target: { value: 'wing' } });

    await act(async () => {
      vi.advanceTimersByTime(350);
    });

    await waitFor(() => {
      expect(screen.getByTestId('game-picker-result-game-wingspan')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId('game-picker-result-game-wingspan'));

    expect(onChange).toHaveBeenCalledWith({
      id: 'game-wingspan',
      title: 'Wingspan',
      imageUrl: 'https://example.com/wingspan.png',
    });
  });

  it('clears the selection when the user clicks the × button', () => {
    const onChange = vi.fn();
    const value: GameOption = { id: 'game-1', title: 'Wingspan' };

    render(<GamePicker source="library" value={value} onChange={onChange} />);

    fireEvent.click(screen.getByTestId('game-picker-clear'));

    expect(onChange).toHaveBeenCalledWith(null);
  });

  it('does NOT trigger search for queries shorter than 2 chars', async () => {
    render(<GamePicker source="library" value={null} onChange={vi.fn()} />);

    fireEvent.change(screen.getByTestId('game-picker-input'), { target: { value: 'a' } });

    await act(async () => {
      vi.advanceTimersByTime(500);
    });

    expect(mockGetLibrary).not.toHaveBeenCalled();
  });
});
