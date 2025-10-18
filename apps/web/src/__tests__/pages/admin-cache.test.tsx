import { render, screen, waitFor, within, cleanup } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import CacheDashboard from '../../pages/admin/cache';
import { API_BASE_FALLBACK } from '../../lib/api';

type FetchMock = jest.MockedFunction<typeof fetch>;

const createJsonResponse = (data: unknown, ok = true, status = 200) =>
  ({
    ok,
    status,
    json: async () => data
  } as unknown as Response);

describe('CacheDashboard', () => {
  const originalFetch = global.fetch;
  let fetchMock: FetchMock;
  const apiBase = 'https://api.example.com';

  const loadCacheDashboard = () => {
    const cachePath = require.resolve('../../pages/admin/cache');
    const apiCacheKeys = Object.keys(require.cache).filter((key) => key.includes('lib/api'));

    delete require.cache[cachePath];
    apiCacheKeys.forEach((key) => {
      delete require.cache[key];
    });

    return require('../../pages/admin/cache').default;
  };

  beforeAll(() => {
    fetchMock = jest.fn() as FetchMock;
    global.fetch = fetchMock;
  });

  afterAll(() => {
    global.fetch = originalFetch;
  });

  beforeEach(() => {
    fetchMock.mockReset();
  });

  afterEach(() => {
    delete process.env.NEXT_PUBLIC_API_BASE;
    jest.clearAllMocks();
  });

  const mockGamesResponse = [
    { id: 'game-1', name: 'Chess' },
    { id: 'game-2', name: 'Tic-Tac-Toe' }
  ];

  const mockStatsResponse = {
    totalHits: 750,
    totalMisses: 250,
    hitRate: 0.75,
    totalKeys: 3,
    cacheSizeBytes: 5242880, // 5 MB
    topQuestions: [
      {
        questionHash: 'a1b2c3d4e5f6',
        hitCount: 50,
        missCount: 10,
        lastHitAt: '2024-01-15T10:30:00.000Z'
      },
      {
        questionHash: 'f6e5d4c3b2a1',
        hitCount: 35,
        missCount: 15,
        lastHitAt: '2024-01-15T11:00:00.000Z'
      },
      {
        questionHash: '123456789abc',
        hitCount: 20,
        missCount: 5,
        lastHitAt: '2024-01-15T09:45:00.000Z'
      }
    ]
  };

  it('renders loading state while data is being fetched', () => {
    fetchMock.mockImplementation(() => new Promise(() => {}));

    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const CacheDashboard = loadCacheDashboard();

    render(<CacheDashboard />);

    expect(screen.getByText('Loading...')).toBeInTheDocument();
  });

  it('renders cache statistics and top questions successfully', async () => {
    fetchMock
      .mockResolvedValueOnce(createJsonResponse(mockGamesResponse))
      .mockResolvedValueOnce(createJsonResponse(mockStatsResponse));

    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const CacheDashboard = loadCacheDashboard();

    render(<CacheDashboard />);

    // Wait for data to load
    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/api/v1/games`,
        expect.objectContaining({ credentials: 'include' })
      )
    );

    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/api/v1/admin/cache/stats`,
        expect.objectContaining({ credentials: 'include' })
      )
    );

    // Check page title
    expect(await screen.findByText('Cache Management Dashboard')).toBeInTheDocument();
    expect(screen.getByText('Monitor cache performance and manage cached responses')).toBeInTheDocument();

    // Check statistics cards
    expect(screen.getByText('Cache Hit Rate')).toBeInTheDocument();
    expect(screen.getByText('75.0%')).toBeInTheDocument();
    expect(screen.getByText('Miss Rate: 25.0%')).toBeInTheDocument();

    expect(screen.getByText('Total Requests')).toBeInTheDocument();
    expect(screen.getByText('1,000')).toBeInTheDocument();
    expect(screen.getByText('Cached: 750')).toBeInTheDocument();
    expect(screen.getByText('Not Cached: 250')).toBeInTheDocument();

    expect(screen.getByText('Cache Size')).toBeInTheDocument();
    expect(screen.getByText('5.00 MB')).toBeInTheDocument();
    expect(screen.getByText('3 cached keys')).toBeInTheDocument();

    // Check top questions table
    expect(screen.getByText('Top Cached Questions')).toBeInTheDocument();
    expect(screen.getByText('a1b2c3d4e5f6')).toBeInTheDocument();
    expect(screen.getByText('f6e5d4c3b2a1')).toBeInTheDocument();
    expect(screen.getByText('123456789abc')).toBeInTheDocument();

    // Check hit counts
    expect(screen.getByText('50')).toBeInTheDocument();
    expect(screen.getByText('35')).toBeInTheDocument();
    expect(screen.getByText('20')).toBeInTheDocument();
  });

  it('displays hit rate with appropriate color coding', async () => {
    const highHitRate = { ...mockStatsResponse, hitRate: 0.8, totalHits: 800, totalMisses: 200 };
    const mediumHitRate = { ...mockStatsResponse, hitRate: 0.5, totalHits: 500, totalMisses: 500 };
    const lowHitRate = { ...mockStatsResponse, hitRate: 0.3, totalHits: 300, totalMisses: 700 };

    // Test high hit rate (green)
    fetchMock
      .mockResolvedValueOnce(createJsonResponse(mockGamesResponse))
      .mockResolvedValueOnce(createJsonResponse(highHitRate));

    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const CacheDashboard = loadCacheDashboard();

    render(<CacheDashboard />);

    await waitFor(() => expect(screen.getByText('80.0%')).toBeInTheDocument());

    cleanup();

    // Test medium hit rate (yellow)
    fetchMock.mockReset();
    fetchMock
      .mockResolvedValueOnce(createJsonResponse(mockGamesResponse))
      .mockResolvedValueOnce(createJsonResponse(mediumHitRate));

    render(<CacheDashboard />);

    await waitFor(() => expect(screen.getByText('50.0%')).toBeInTheDocument());

    cleanup();

    // Test low hit rate (red)
    fetchMock.mockReset();
    fetchMock
      .mockResolvedValueOnce(createJsonResponse(mockGamesResponse))
      .mockResolvedValueOnce(createJsonResponse(lowHitRate));

    render(<CacheDashboard />);

    await waitFor(() => expect(screen.getByText('30.0%')).toBeInTheDocument());
  });

  it('filters cache stats by selected game', async () => {
    const gameSpecificStats = {
      ...mockStatsResponse,
      totalHits: 375,
      totalMisses: 125,
      totalKeys: 1,
      topQuestions: [
        {
          questionHash: 'chess-question-hash',
          hitCount: 25,
          missCount: 5,
          lastHitAt: '2024-01-15T10:30:00.000Z'
        }
      ]
    };

    fetchMock
      .mockResolvedValueOnce(createJsonResponse(mockGamesResponse))
      .mockResolvedValueOnce(createJsonResponse(mockStatsResponse))
      .mockResolvedValueOnce(createJsonResponse(mockGamesResponse))
      .mockResolvedValueOnce(createJsonResponse(gameSpecificStats));

    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const CacheDashboard = loadCacheDashboard();
    const user = userEvent.setup({ delay: null });

    render(<CacheDashboard />);

    await waitFor(() => expect(screen.getByText('Cache Management Dashboard')).toBeInTheDocument());

    // Select a specific game
    const gameFilter = screen.getByLabelText('Filter by Game');
    await user.selectOptions(gameFilter, 'game-1');

    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/api/v1/admin/cache/stats?gameId=game-1`,
        expect.objectContaining({ credentials: 'include' })
      )
    );

    // Check that game-specific stats are displayed
    await waitFor(() => expect(screen.getByText('500')).toBeInTheDocument());
    expect(screen.getByText('chess-question-hash')).toBeInTheDocument();
  });

  it('handles cache invalidation for a specific game with confirmation', async () => {
    fetchMock
      .mockResolvedValueOnce(createJsonResponse(mockGamesResponse))
      .mockResolvedValueOnce(createJsonResponse(mockStatsResponse))
      .mockResolvedValueOnce(createJsonResponse(null, true, 204)) // DELETE response
      .mockResolvedValueOnce(createJsonResponse(mockGamesResponse))
      .mockResolvedValueOnce(createJsonResponse(mockStatsResponse)); // Refresh after invalidation

    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const CacheDashboard = loadCacheDashboard();
    const user = userEvent.setup({ delay: null });

    render(<CacheDashboard />);

    await waitFor(() => expect(screen.getByText('Cache Management Dashboard')).toBeInTheDocument());

    // Select a game
    const gameFilter = screen.getByLabelText('Filter by Game');
    await user.selectOptions(gameFilter, 'game-1');

    // Click invalidate button
    const invalidateButton = await screen.findByText('Invalidate Cache for Chess');
    await user.click(invalidateButton);

    // Check confirmation dialog appears
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByText('Invalidate Game Cache')).toBeInTheDocument();
    expect(screen.getByText(/Are you sure you want to invalidate all cached responses for "Chess"/)).toBeInTheDocument();

    // Confirm invalidation
    const confirmButton = screen.getByRole('button', { name: 'Confirm' });
    await user.click(confirmButton);

    // Check DELETE request was made
    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/api/v1/admin/cache/games/game-1`,
        expect.objectContaining({
          method: 'DELETE',
          credentials: 'include'
        })
      )
    );

    // Check success toast appears
    await waitFor(() => {
      expect(screen.getByText(/Cache invalidated successfully for "Chess"/)).toBeInTheDocument();
    });

    // Check stats were refreshed
    await waitFor(() => {
      const calls = fetchMock.mock.calls.filter((call) =>
        call[0].toString().includes('/api/v1/admin/cache/stats')
      );
      expect(calls.length).toBeGreaterThanOrEqual(2);
    });
  });

  it('handles cache invalidation by tag with confirmation', async () => {
    fetchMock
      .mockResolvedValueOnce(createJsonResponse(mockGamesResponse))
      .mockResolvedValueOnce(createJsonResponse(mockStatsResponse))
      .mockResolvedValueOnce(createJsonResponse(null, true, 204)) // DELETE response
      .mockResolvedValueOnce(createJsonResponse(mockGamesResponse))
      .mockResolvedValueOnce(createJsonResponse(mockStatsResponse)); // Refresh after invalidation

    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const CacheDashboard = loadCacheDashboard();
    const user = userEvent.setup({ delay: null });

    render(<CacheDashboard />);

    await waitFor(() => expect(screen.getByText('Cache Management Dashboard')).toBeInTheDocument());

    // Enter tag
    const tagInput = screen.getByPlaceholderText('Enter tag (e.g., qa, setup)');
    await user.type(tagInput, 'qa');

    // Click invalidate button
    const invalidateButton = screen.getByLabelText('Invalidate cache by tag');
    await user.click(invalidateButton);

    // Check confirmation dialog appears
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByText('Invalidate Cache by Tag')).toBeInTheDocument();
    expect(screen.getByText(/Are you sure you want to invalidate all cached responses with tag "qa"/)).toBeInTheDocument();

    // Confirm invalidation
    const confirmButton = screen.getByRole('button', { name: 'Confirm' });
    await user.click(confirmButton);

    // Check DELETE request was made
    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/api/v1/admin/cache/tags/qa`,
        expect.objectContaining({
          method: 'DELETE',
          credentials: 'include'
        })
      )
    );

    // Check success toast appears
    await waitFor(() => {
      expect(screen.getByText(/Cache invalidated successfully for tag "qa"/)).toBeInTheDocument();
    });

    // Check tag input was cleared
    expect(tagInput).toHaveValue('');
  });

  it('validates tag input before invalidation', async () => {
    fetchMock
      .mockResolvedValueOnce(createJsonResponse(mockGamesResponse))
      .mockResolvedValueOnce(createJsonResponse(mockStatsResponse));

    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const CacheDashboard = loadCacheDashboard();
    const user = userEvent.setup({ delay: null });

    render(<CacheDashboard />);

    await waitFor(() => expect(screen.getByText('Cache Management Dashboard')).toBeInTheDocument());

    // Try to invalidate without entering a tag
    const invalidateButton = screen.getByLabelText('Invalidate cache by tag');
    expect(invalidateButton).toBeDisabled();

    // Enter whitespace only
    const tagInput = screen.getByPlaceholderText('Enter tag (e.g., qa, setup)');
    await user.type(tagInput, '   ');

    // Button should still be disabled
    expect(invalidateButton).toBeDisabled();
  });

  it('allows canceling confirmation dialog', async () => {
    fetchMock
      .mockResolvedValueOnce(createJsonResponse(mockGamesResponse))
      .mockResolvedValueOnce(createJsonResponse(mockStatsResponse));

    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const CacheDashboard = loadCacheDashboard();
    const user = userEvent.setup({ delay: null });

    render(<CacheDashboard />);

    await waitFor(() => expect(screen.getByText('Cache Management Dashboard')).toBeInTheDocument());

    // Enter tag and click invalidate
    const tagInput = screen.getByPlaceholderText('Enter tag (e.g., qa, setup)');
    await user.type(tagInput, 'qa');

    const invalidateButton = screen.getByLabelText('Invalidate cache by tag');
    await user.click(invalidateButton);

    // Cancel confirmation
    const cancelButton = screen.getByRole('button', { name: 'Cancel' });
    await user.click(cancelButton);

    // Dialog should be closed
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();

    // No DELETE request should be made
    const deleteCalls = fetchMock.mock.calls.filter((call) => call[1]?.method === 'DELETE');
    expect(deleteCalls.length).toBe(0);
  });

  it('handles invalidation error with error toast', async () => {
    fetchMock
      .mockResolvedValueOnce(createJsonResponse(mockGamesResponse))
      .mockResolvedValueOnce(createJsonResponse(mockStatsResponse))
      .mockResolvedValueOnce(createJsonResponse({ error: 'Unauthorized' }, false, 401));

    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const CacheDashboard = loadCacheDashboard();
    const user = userEvent.setup({ delay: null });

    render(<CacheDashboard />);

    await waitFor(() => expect(screen.getByText('Cache Management Dashboard')).toBeInTheDocument());

    // Select game and try to invalidate
    const gameFilter = screen.getByLabelText('Filter by Game');
    await user.selectOptions(gameFilter, 'game-1');

    const invalidateButton = await screen.findByText('Invalidate Cache for Chess');
    await user.click(invalidateButton);

    const confirmButton = screen.getByRole('button', { name: 'Confirm' });
    await user.click(confirmButton);

    // Check error toast appears
    await waitFor(() => {
      expect(screen.getByText(/Failed to invalidate cache/)).toBeInTheDocument();
    });
  });

  it('refreshes stats when refresh button is clicked', async () => {
    fetchMock
      .mockResolvedValueOnce(createJsonResponse(mockGamesResponse))
      .mockResolvedValueOnce(createJsonResponse(mockStatsResponse))
      .mockResolvedValueOnce(createJsonResponse(mockGamesResponse))
      .mockResolvedValueOnce(createJsonResponse(mockStatsResponse));

    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const CacheDashboard = loadCacheDashboard();
    const user = userEvent.setup({ delay: null });

    render(<CacheDashboard />);

    await waitFor(() => expect(screen.getByText('Cache Management Dashboard')).toBeInTheDocument());

    // Click refresh button
    const refreshButton = screen.getByLabelText('Refresh cache statistics');
    await user.click(refreshButton);

    // Check info toast appears
    await waitFor(() => {
      expect(screen.getByText('Refreshing cache statistics...')).toBeInTheDocument();
    });

    // Check stats endpoint was called again
    await waitFor(() => {
      const calls = fetchMock.mock.calls.filter((call) =>
        call[0].toString().includes('/api/v1/admin/cache/stats')
      );
      expect(calls.length).toBe(2);
    });
  });

  it('displays empty state when no cached questions exist', async () => {
    const emptyStats = { ...mockStatsResponse, topQuestions: [] };

    fetchMock
      .mockResolvedValueOnce(createJsonResponse(mockGamesResponse))
      .mockResolvedValueOnce(createJsonResponse(emptyStats));

    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const CacheDashboard = loadCacheDashboard();

    render(<CacheDashboard />);

    await waitFor(() => expect(screen.getByText('Cache Management Dashboard')).toBeInTheDocument());

    expect(screen.getByText('No cached questions found. Cache will populate as users interact with the system.')).toBeInTheDocument();
    expect(screen.queryByText('Top Cached Questions')).not.toBeInTheDocument();
  });

  it('renders error state when API returns unauthorized', async () => {
    fetchMock
      .mockResolvedValueOnce(createJsonResponse(mockGamesResponse))
      .mockResolvedValueOnce(createJsonResponse(null, false, 401));

    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const CacheDashboard = loadCacheDashboard();

    render(<CacheDashboard />);

    expect(await screen.findByText('Error')).toBeInTheDocument();
    expect(screen.getByText('Unauthorized - Admin access required')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Back to Admin Dashboard' })).toBeInTheDocument();
  });

  it('automatically dismisses toast notifications after 5 seconds', async () => {
    jest.useFakeTimers();

    fetchMock
      .mockResolvedValueOnce(createJsonResponse(mockGamesResponse))
      .mockResolvedValueOnce(createJsonResponse(mockStatsResponse))
      .mockResolvedValueOnce(createJsonResponse(mockGamesResponse))
      .mockResolvedValueOnce(createJsonResponse(mockStatsResponse));

    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const CacheDashboard = loadCacheDashboard();
    const user = userEvent.setup({ delay: null });

    render(<CacheDashboard />);

    await waitFor(() => expect(screen.getByText('Cache Management Dashboard')).toBeInTheDocument());

    // Trigger a refresh to create a toast
    const refreshButton = screen.getByLabelText('Refresh cache statistics');
    await user.click(refreshButton);

    await waitFor(() => {
      expect(screen.getByText('Refreshing cache statistics...')).toBeInTheDocument();
    });

    // Advance timers by 5 seconds
    jest.advanceTimersByTime(5000);

    // Toast should be removed
    await waitFor(() => {
      expect(screen.queryByText('Refreshing cache statistics...')).not.toBeInTheDocument();
    });

    jest.useRealTimers();
  });

  it('allows manual dismissal of toast notifications', async () => {
    fetchMock
      .mockResolvedValueOnce(createJsonResponse(mockGamesResponse))
      .mockResolvedValueOnce(createJsonResponse(mockStatsResponse))
      .mockResolvedValueOnce(createJsonResponse(mockGamesResponse))
      .mockResolvedValueOnce(createJsonResponse(mockStatsResponse));

    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const CacheDashboard = loadCacheDashboard();
    const user = userEvent.setup({ delay: null });

    render(<CacheDashboard />);

    await waitFor(() => expect(screen.getByText('Cache Management Dashboard')).toBeInTheDocument());

    // Trigger a refresh to create a toast
    const refreshButton = screen.getByLabelText('Refresh cache statistics');
    await user.click(refreshButton);

    await waitFor(() => {
      expect(screen.getByText('Refreshing cache statistics...')).toBeInTheDocument();
    });

    // Click close button
    const closeButton = screen.getByLabelText('Close notification');
    await user.click(closeButton);

    // Toast should be removed immediately
    await waitFor(() => {
      expect(screen.queryByText('Refreshing cache statistics...')).not.toBeInTheDocument();
    });
  });

  it('formats cache size correctly for different units', async () => {
    const smallCache = { ...mockStatsResponse, cacheSizeBytes: 512 * 1024 }; // 512 KB
    const largeCache = { ...mockStatsResponse, cacheSizeBytes: 50 * 1024 * 1024 }; // 50 MB

    // Test KB formatting
    fetchMock
      .mockResolvedValueOnce(createJsonResponse(mockGamesResponse))
      .mockResolvedValueOnce(createJsonResponse(smallCache));

    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const CacheDashboard = loadCacheDashboard();

    render(<CacheDashboard />);

    await waitFor(() => expect(screen.getByText('512.00 KB')).toBeInTheDocument());

    cleanup();

    // Test MB formatting
    fetchMock.mockReset();
    fetchMock
      .mockResolvedValueOnce(createJsonResponse(mockGamesResponse))
      .mockResolvedValueOnce(createJsonResponse(largeCache));

    render(<CacheDashboard />);

    await waitFor(() => expect(screen.getByText('50.00 MB')).toBeInTheDocument());
  });

  it('falls back to localhost API base when NEXT_PUBLIC_API_BASE is unset', async () => {
    delete process.env.NEXT_PUBLIC_API_BASE;

    fetchMock
      .mockResolvedValueOnce(createJsonResponse(mockGamesResponse))
      .mockResolvedValueOnce(createJsonResponse(mockStatsResponse));

    render(<CacheDashboard />);

    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        `${API_BASE_FALLBACK}/api/v1/games`,
        expect.objectContaining({ credentials: 'include' })
      )
    );

    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        `${API_BASE_FALLBACK}/api/v1/admin/cache/stats`,
        expect.objectContaining({ credentials: 'include' })
      )
    );

    process.env.NEXT_PUBLIC_API_BASE = apiBase;
  });

  it('handles Enter key press for tag invalidation', async () => {
    fetchMock
      .mockResolvedValueOnce(createJsonResponse(mockGamesResponse))
      .mockResolvedValueOnce(createJsonResponse(mockStatsResponse));

    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const CacheDashboard = loadCacheDashboard();
    const user = userEvent.setup({ delay: null });

    render(<CacheDashboard />);

    await waitFor(() => expect(screen.getByText('Cache Management Dashboard')).toBeInTheDocument());

    // Enter tag
    const tagInput = screen.getByPlaceholderText('Enter tag (e.g., qa, setup)');
    await user.type(tagInput, 'qa{Enter}');

    // Check confirmation dialog appears
    await waitFor(() => {
      expect(screen.getByRole('dialog')).toBeInTheDocument();
      expect(screen.getByText('Invalidate Cache by Tag')).toBeInTheDocument();
    });
  });

  it('displays game selector with all games option', async () => {
    fetchMock
      .mockResolvedValueOnce(createJsonResponse(mockGamesResponse))
      .mockResolvedValueOnce(createJsonResponse(mockStatsResponse));

    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const CacheDashboard = loadCacheDashboard();

    render(<CacheDashboard />);

    await waitFor(() => expect(screen.getByText('Cache Management Dashboard')).toBeInTheDocument());

    const gameFilter = screen.getByLabelText('Filter by Game');
    expect(gameFilter).toBeInTheDocument();

    // Check all options are present
    expect(within(gameFilter).getByText('All Games')).toBeInTheDocument();
    expect(within(gameFilter).getByText('Chess')).toBeInTheDocument();
    expect(within(gameFilter).getByText('Tic-Tac-Toe')).toBeInTheDocument();
  });

  it('shows message when no game is selected for invalidation', async () => {
    fetchMock
      .mockResolvedValueOnce(createJsonResponse(mockGamesResponse))
      .mockResolvedValueOnce(createJsonResponse(mockStatsResponse));

    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const CacheDashboard = loadCacheDashboard();

    render(<CacheDashboard />);

    await waitFor(() => expect(screen.getByText('Cache Management Dashboard')).toBeInTheDocument());

    // With "All Games" selected, should show message instead of button
    expect(screen.getByText('Select a specific game to invalidate its cache')).toBeInTheDocument();
    expect(screen.queryByText(/Invalidate Cache for/)).not.toBeInTheDocument();
  });
});
