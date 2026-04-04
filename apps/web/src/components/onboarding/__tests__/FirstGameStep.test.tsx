import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import { FirstGameStep } from '../FirstGameStep';

const mockGetAll = vi.hoisted(() => vi.fn());
const mockAddGame = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    games: {
      getAll: mockGetAll,
    },
    library: {
      addGame: mockAddGame,
    },
  },
  ApiError: class ApiError extends Error {
    constructor(message: string) {
      super(message);
      this.name = 'ApiError';
    }
  },
}));

vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

describe('FirstGameStep', () => {
  const user = userEvent.setup();
  const onComplete = vi.fn();
  const onSkip = vi.fn();
  const onGameAdded = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers({ shouldAdvanceTime: true });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('renders search input', () => {
    renderWithQuery(
      <FirstGameStep onComplete={onComplete} onSkip={onSkip} onGameAdded={onGameAdded} />
    );

    expect(screen.getByText('Add Your First Game')).toBeInTheDocument();
    expect(screen.getByLabelText(/search games/i)).toBeInTheDocument();
  });

  it('calls onSkip when skip button clicked', async () => {
    vi.useRealTimers();
    const realUser = userEvent.setup();
    renderWithQuery(
      <FirstGameStep onComplete={onComplete} onSkip={onSkip} onGameAdded={onGameAdded} />
    );

    await realUser.click(screen.getByTestId('game-skip'));

    expect(onSkip).toHaveBeenCalled();
  });

  it('shows search results after typing', async () => {
    mockGetAll.mockResolvedValueOnce({
      games: [
        { id: 'g1', title: 'Catan', publisher: 'Kosmos', yearPublished: 1995 },
        { id: 'g2', title: 'Catan: Seafarers', publisher: 'Kosmos', yearPublished: 1997 },
      ],
      total: 2,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    });

    vi.useRealTimers();
    const realUser = userEvent.setup();

    renderWithQuery(
      <FirstGameStep onComplete={onComplete} onSkip={onSkip} onGameAdded={onGameAdded} />
    );

    await realUser.type(screen.getByLabelText(/search games/i), 'Catan');

    await waitFor(
      () => {
        expect(screen.getByTestId('game-search-results')).toBeInTheDocument();
      },
      { timeout: 2000 }
    );

    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Catan: Seafarers')).toBeInTheDocument();
  });

  it('selects a game from search results', async () => {
    mockGetAll.mockResolvedValueOnce({
      games: [{ id: 'g1', title: 'Catan', publisher: 'Kosmos', yearPublished: 1995 }],
      total: 1,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    });

    vi.useRealTimers();
    const realUser = userEvent.setup();

    renderWithQuery(
      <FirstGameStep onComplete={onComplete} onSkip={onSkip} onGameAdded={onGameAdded} />
    );

    await realUser.type(screen.getByLabelText(/search games/i), 'Catan');

    await waitFor(
      () => {
        expect(screen.getByTestId('game-search-results')).toBeInTheDocument();
      },
      { timeout: 2000 }
    );

    // Click on the first result
    await realUser.click(screen.getByText('Catan'));

    expect(screen.getByTestId('selected-game')).toBeInTheDocument();
    expect(screen.queryByTestId('game-search-results')).not.toBeInTheDocument();
  });

  it('adds selected game to library', async () => {
    mockGetAll.mockResolvedValueOnce({
      games: [{ id: 'g1', title: 'Catan', publisher: 'Kosmos', yearPublished: 1995 }],
      total: 1,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    });
    mockAddGame.mockResolvedValueOnce({});

    vi.useRealTimers();
    const realUser = userEvent.setup();

    renderWithQuery(
      <FirstGameStep onComplete={onComplete} onSkip={onSkip} onGameAdded={onGameAdded} />
    );

    await realUser.type(screen.getByLabelText(/search games/i), 'Catan');

    await waitFor(
      () => {
        expect(screen.getByTestId('game-search-results')).toBeInTheDocument();
      },
      { timeout: 2000 }
    );

    await realUser.click(screen.getByText('Catan'));
    await realUser.click(screen.getByRole('button', { name: /add to library/i }));

    await waitFor(() => {
      expect(mockAddGame).toHaveBeenCalledWith('g1');
    });

    await waitFor(() => {
      expect(onGameAdded).toHaveBeenCalledWith('g1', 'Catan');
      expect(onComplete).toHaveBeenCalled();
    });
  });
});
