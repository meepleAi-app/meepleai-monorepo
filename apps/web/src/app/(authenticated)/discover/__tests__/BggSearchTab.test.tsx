/**
 * BggSearchTab Component Tests
 * Task 1: BGG Search Tab on Discover Page
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';

// Mock the api module
vi.mock('@/lib/api', () => ({
  api: {
    bgg: {
      search: vi.fn(),
    },
  },
}));

// Mock AddGameWizardProvider
const mockOpenWizard = vi.fn();
vi.mock('@/components/library/add-game-sheet/AddGameWizardProvider', () => ({
  useAddGameWizard: () => ({ openWizard: mockOpenWizard, isOpen: false }),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

// Import after mocks
import { api } from '@/lib/api';
import { BggSearchTab } from '../BggSearchTab';

const mockBggSearch = vi.mocked(api.bgg.search);

const mockBggResults = {
  results: [
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
  ],
  total: 2,
  page: 1,
  pageSize: 20,
  totalPages: 1,
};

describe('BggSearchTab', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers({ shouldAdvanceTime: true });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('renders search input', () => {
    render(<BggSearchTab />);
    expect(screen.getByPlaceholderText('Cerca su BoardGameGeek...')).toBeInTheDocument();
  });

  it('searches BGG on input after debounce', async () => {
    mockBggSearch.mockResolvedValue(mockBggResults);

    render(<BggSearchTab />);

    const input = screen.getByPlaceholderText('Cerca su BoardGameGeek...');
    fireEvent.change(input, { target: { value: 'Catan' } });

    // Before debounce, should not have called
    expect(mockBggSearch).not.toHaveBeenCalled();

    // Advance timers past debounce (400ms)
    await act(async () => {
      vi.advanceTimersByTime(500);
    });

    await waitFor(() => {
      expect(mockBggSearch).toHaveBeenCalledWith('Catan', false, 1, 20);
    });
  });

  it('shows results after search', async () => {
    mockBggSearch.mockResolvedValue(mockBggResults);

    render(<BggSearchTab />);

    const input = screen.getByPlaceholderText('Cerca su BoardGameGeek...');
    fireEvent.change(input, { target: { value: 'Catan' } });

    await act(async () => {
      vi.advanceTimersByTime(500);
    });

    await waitFor(() => {
      expect(screen.getByText('Catan')).toBeInTheDocument();
      expect(screen.getByText('Carcassonne')).toBeInTheDocument();
    });
  });

  it('calls openWizard with correct WizardEntryPoint on selection', async () => {
    mockBggSearch.mockResolvedValue(mockBggResults);

    render(<BggSearchTab />);

    const input = screen.getByPlaceholderText('Cerca su BoardGameGeek...');
    fireEvent.change(input, { target: { value: 'Catan' } });

    await act(async () => {
      vi.advanceTimersByTime(500);
    });

    await waitFor(() => {
      expect(screen.getByText('Catan')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByText('Catan'));

    expect(mockOpenWizard).toHaveBeenCalledWith({ type: 'fromSearch', bggId: 13 });
  });

  it('shows "no results" message when search returns empty', async () => {
    mockBggSearch.mockResolvedValue({ ...mockBggResults, results: [], total: 0 });

    render(<BggSearchTab />);

    const input = screen.getByPlaceholderText('Cerca su BoardGameGeek...');
    fireEvent.change(input, { target: { value: 'xyznotexist' } });

    await act(async () => {
      vi.advanceTimersByTime(500);
    });

    await waitFor(() => {
      expect(screen.getByText(/Nessun risultato/i)).toBeInTheDocument();
    });
  });

  it('shows error message on API failure', async () => {
    mockBggSearch.mockRejectedValue(new Error('Network error'));

    render(<BggSearchTab />);

    const input = screen.getByPlaceholderText('Cerca su BoardGameGeek...');
    fireEvent.change(input, { target: { value: 'Catan' } });

    await act(async () => {
      vi.advanceTimersByTime(500);
    });

    await waitFor(() => {
      expect(screen.getByText(/BoardGameGeek non disponibile/i)).toBeInTheDocument();
    });
  });

  it('shows clear button when input has text and clears on click', async () => {
    render(<BggSearchTab />);

    const input = screen.getByPlaceholderText('Cerca su BoardGameGeek...');
    fireEvent.change(input, { target: { value: 'Catan' } });

    // Clear button should appear
    const clearButton = screen.getByLabelText('Cancella ricerca');
    expect(clearButton).toBeInTheDocument();

    fireEvent.click(clearButton);
    expect(input).toHaveValue('');
  });

  it('does not search when input is empty', async () => {
    render(<BggSearchTab />);

    const input = screen.getByPlaceholderText('Cerca su BoardGameGeek...');
    fireEvent.change(input, { target: { value: '' } });

    await act(async () => {
      vi.advanceTimersByTime(500);
    });

    expect(mockBggSearch).not.toHaveBeenCalled();
  });
});
