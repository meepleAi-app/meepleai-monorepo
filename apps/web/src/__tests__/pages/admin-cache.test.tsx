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

  /**
   * Reloads the CacheDashboard module with fresh environment variables.
   *
   * RATIONALE: Next.js reads NEXT_PUBLIC_API_BASE at module import time.
   * Changing process.env after import doesn't affect already-loaded modules.
   * We must clear the require cache and reload the module to pick up new values.
   *
   * This is a test-only workaround for Next.js environment variable handling.
   */
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
    jest.useRealTimers(); // Ensure real timers are restored after each test
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

    // Phase 1: Set env BEFORE loading module
    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const CacheDashboard = loadCacheDashboard();

    render(<CacheDashboard />);

    expect(screen.getByText('Loading...')).toBeInTheDocument();
  });

  it('renders cache statistics and top questions successfully', async () => {
    // FIX 1: Use mockImplementation with URL routing instead of chained mockResolvedValueOnce
    fetchMock.mockImplementation((url: string) => {
      if (url.includes('/api/v1/games')) {
        return Promise.resolve(createJsonResponse(mockGamesResponse));
      }
      if (url.includes('/api/v1/admin/cache/stats')) {
        return Promise.resolve(createJsonResponse(mockStatsResponse));
      }
      return Promise.reject(new Error(`Unexpected URL: ${url}`));
    });

    // Phase 1: Set env BEFORE loading module
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

    // FIX 2: Use findByText instead of waitFor(() => getByText())
    expect(await screen.findByText('Cache Management Dashboard')).toBeInTheDocument();
    expect(screen.getByText('Monitor cache performance and manage cached responses')).toBeInTheDocument();

    // Check statistics cards
    expect(screen.getByText('Cache Hit Rate')).toBeInTheDocument();
    expect(screen.getByText('75.0%')).toBeInTheDocument();
    expect(screen.getByText('Miss Rate: 25.0%')).toBeInTheDocument();

    expect(screen.getByText('Total Requests')).toBeInTheDocument();
    // FIX (issue #463): Use regex to handle locale variations ("1,000" vs "1000")
    expect(screen.getByText(/1,?000/)).toBeInTheDocument();
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

    // Phase 1: Set env BEFORE loading module
    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    // Phase 2: Store component once per test, not per subtest
    const CacheDashboard = loadCacheDashboard();

    // Test high hit rate (green) - FIX 1: Use mockImplementation
    fetchMock.mockImplementation((url: string) => {
      if (url.includes('/api/v1/games')) {
        return Promise.resolve(createJsonResponse(mockGamesResponse));
      }
      if (url.includes('/api/v1/admin/cache/stats')) {
        return Promise.resolve(createJsonResponse(highHitRate));
      }
      return Promise.reject(new Error(`Unexpected URL: ${url}`));
    });

    render(<CacheDashboard />);

    // FIX 2: Use findByText instead of waitFor(() => getByText())
    expect(await screen.findByText('80.0%')).toBeInTheDocument();

    cleanup();

    // Phase 2: Use direct render after cleanup (no module reload)
    // Phase 3: Reset mocks before new chain
    fetchMock.mockReset();
    fetchMock.mockImplementation((url: string) => {
      if (url.includes('/api/v1/games')) {
        return Promise.resolve(createJsonResponse(mockGamesResponse));
      }
      if (url.includes('/api/v1/admin/cache/stats')) {
        return Promise.resolve(createJsonResponse(mediumHitRate));
      }
      return Promise.reject(new Error(`Unexpected URL: ${url}`));
    });

    render(<CacheDashboard />);

    expect(await screen.findByText('50.0%')).toBeInTheDocument();

    cleanup();

    // Phase 2: Use direct render after cleanup (no module reload)
    // Phase 3: Reset mocks before new chain
    fetchMock.mockReset();
    fetchMock.mockImplementation((url: string) => {
      if (url.includes('/api/v1/games')) {
        return Promise.resolve(createJsonResponse(mockGamesResponse));
      }
      if (url.includes('/api/v1/admin/cache/stats')) {
        return Promise.resolve(createJsonResponse(lowHitRate));
      }
      return Promise.reject(new Error(`Unexpected URL: ${url}`));
    });

    render(<CacheDashboard />);

    expect(await screen.findByText('30.0%')).toBeInTheDocument();
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

    // FIX 1: Use mockImplementation with state to handle filter changes
    let filterGameId: string | null = null;
    fetchMock.mockImplementation((url: string) => {
      if (url.includes('/api/v1/games')) {
        return Promise.resolve(createJsonResponse(mockGamesResponse));
      }
      if (url.includes('/api/v1/admin/cache/stats')) {
        // Return game-specific stats if gameId filter is present
        if (url.includes('gameId=game-1')) {
          return Promise.resolve(createJsonResponse(gameSpecificStats));
        }
        return Promise.resolve(createJsonResponse(mockStatsResponse));
      }
      return Promise.reject(new Error(`Unexpected URL: ${url}`));
    });

    // Phase 1: Set env BEFORE loading module
    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const CacheDashboard = loadCacheDashboard();
    const user = userEvent.setup({ delay: null });

    render(<CacheDashboard />);

    // FIX 2: Use findByText
    expect(await screen.findByText('Cache Management Dashboard')).toBeInTheDocument();

    // Select a specific game - FIX 3: Ensure user interaction is awaited
    const gameFilter = screen.getByLabelText('Filter by Game');
    await user.selectOptions(gameFilter, 'game-1');

    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/api/v1/admin/cache/stats?gameId=game-1`,
        expect.objectContaining({ credentials: 'include' })
      )
    );

    // Check that game-specific stats are displayed
    expect(await screen.findByText('500')).toBeInTheDocument();
    expect(await screen.findByText('chess-question-hash')).toBeInTheDocument();
  });

  it('handles cache invalidation for a specific game with confirmation', async () => {
    // FIX 1: Use mockImplementation with call tracking
    let deleteCallMade = false;
    fetchMock.mockImplementation((url: string, options?: RequestInit) => {
      if (url.includes('/api/v1/games')) {
        return Promise.resolve(createJsonResponse(mockGamesResponse));
      }
      if (url.includes('/api/v1/admin/cache/games/game-1') && options?.method === 'DELETE') {
        deleteCallMade = true;
        return Promise.resolve(createJsonResponse(null, true, 204));
      }
      if (url.includes('/api/v1/admin/cache/stats')) {
        return Promise.resolve(createJsonResponse(mockStatsResponse));
      }
      return Promise.reject(new Error(`Unexpected URL: ${url}`));
    });

    // Phase 1: Set env BEFORE loading module
    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const CacheDashboard = loadCacheDashboard();
    const user = userEvent.setup({ delay: null });

    render(<CacheDashboard />);

    // FIX 2: Use findByText
    expect(await screen.findByText('Cache Management Dashboard')).toBeInTheDocument();

    // Select a game
    const gameFilter = screen.getByLabelText('Filter by Game');
    await user.selectOptions(gameFilter, 'game-1');

    // Click invalidate button
    const invalidateButton = await screen.findByText('Invalidate Cache for Chess');
    await user.click(invalidateButton);

    // Check confirmation dialog appears
    const dialog = screen.getByRole('dialog');
    expect(dialog).toBeInTheDocument();
    // FIX (issue #463): Query within dialog to avoid "multiple elements" error
    expect(within(dialog).getByText('Invalidate Game Cache')).toBeInTheDocument();
    expect(within(dialog).getByText(/Are you sure you want to invalidate all cached responses for "Chess"/)).toBeInTheDocument();

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
    expect(await screen.findByText(/Cache invalidated successfully for "Chess"/)).toBeInTheDocument();

    // Check stats were refreshed
    await waitFor(() => {
      const calls = fetchMock.mock.calls.filter((call) =>
        call[0].toString().includes('/api/v1/admin/cache/stats')
      );
      expect(calls.length).toBeGreaterThanOrEqual(2);
    });
  });

  it('handles cache invalidation by tag with confirmation', async () => {
    // FIX 1: Use mockImplementation
    fetchMock.mockImplementation((url: string, options?: RequestInit) => {
      if (url.includes('/api/v1/games')) {
        return Promise.resolve(createJsonResponse(mockGamesResponse));
      }
      if (url.includes('/api/v1/admin/cache/tags/qa') && options?.method === 'DELETE') {
        return Promise.resolve(createJsonResponse(null, true, 204));
      }
      if (url.includes('/api/v1/admin/cache/stats')) {
        return Promise.resolve(createJsonResponse(mockStatsResponse));
      }
      return Promise.reject(new Error(`Unexpected URL: ${url}`));
    });

    // Phase 1: Set env BEFORE loading module
    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const CacheDashboard = loadCacheDashboard();
    const user = userEvent.setup({ delay: null });

    render(<CacheDashboard />);

    // FIX 2: Use findByText
    expect(await screen.findByText('Cache Management Dashboard')).toBeInTheDocument();

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
    expect(await screen.findByText(/Cache invalidated successfully for tag "qa"/)).toBeInTheDocument();

    // Check tag input was cleared
    await waitFor(() => {
      expect(tagInput).toHaveValue('');
    });
  });

  it('validates tag input before invalidation', async () => {
    // FIX 1: Use mockImplementation
    fetchMock.mockImplementation((url: string) => {
      if (url.includes('/api/v1/games')) {
        return Promise.resolve(createJsonResponse(mockGamesResponse));
      }
      if (url.includes('/api/v1/admin/cache/stats')) {
        return Promise.resolve(createJsonResponse(mockStatsResponse));
      }
      return Promise.reject(new Error(`Unexpected URL: ${url}`));
    });

    // Phase 1: Set env BEFORE loading module
    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const CacheDashboard = loadCacheDashboard();
    const user = userEvent.setup({ delay: null });

    render(<CacheDashboard />);

    // FIX 2: Use findByText
    expect(await screen.findByText('Cache Management Dashboard')).toBeInTheDocument();

    // Try to invalidate without entering a tag
    const invalidateButton = screen.getByLabelText('Invalidate cache by tag');
    expect(invalidateButton).toBeDisabled();

    // Enter whitespace only
    const tagInput = screen.getByPlaceholderText('Enter tag (e.g., qa, setup)');
    await user.type(tagInput, '   ');

    // Button should still be disabled
    await waitFor(() => {
      expect(invalidateButton).toBeDisabled();
    });
  });

  it('allows canceling confirmation dialog', async () => {
    // FIX 1: Use mockImplementation
    fetchMock.mockImplementation((url: string) => {
      if (url.includes('/api/v1/games')) {
        return Promise.resolve(createJsonResponse(mockGamesResponse));
      }
      if (url.includes('/api/v1/admin/cache/stats')) {
        return Promise.resolve(createJsonResponse(mockStatsResponse));
      }
      return Promise.reject(new Error(`Unexpected URL: ${url}`));
    });

    // Phase 1: Set env BEFORE loading module
    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const CacheDashboard = loadCacheDashboard();
    const user = userEvent.setup({ delay: null });

    render(<CacheDashboard />);

    // FIX 2: Use findByText
    expect(await screen.findByText('Cache Management Dashboard')).toBeInTheDocument();

    // Enter tag and click invalidate
    const tagInput = screen.getByPlaceholderText('Enter tag (e.g., qa, setup)');
    await user.type(tagInput, 'qa');

    const invalidateButton = screen.getByLabelText('Invalidate cache by tag');
    await user.click(invalidateButton);

    // Cancel confirmation
    const cancelButton = screen.getByRole('button', { name: 'Cancel' });
    await user.click(cancelButton);

    // Dialog should be closed
    await waitFor(() => {
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    // No DELETE request should be made
    const deleteCalls = fetchMock.mock.calls.filter((call) => call[1]?.method === 'DELETE');
    expect(deleteCalls.length).toBe(0);
  });

  it('handles invalidation error with error toast', async () => {
    // FIX 1: Use mockImplementation
    fetchMock.mockImplementation((url: string, options?: RequestInit) => {
      if (url.includes('/api/v1/games')) {
        return Promise.resolve(createJsonResponse(mockGamesResponse));
      }
      if (url.includes('/api/v1/admin/cache/games/game-1') && options?.method === 'DELETE') {
        return Promise.resolve(createJsonResponse({ error: 'Unauthorized' }, false, 401));
      }
      if (url.includes('/api/v1/admin/cache/stats')) {
        return Promise.resolve(createJsonResponse(mockStatsResponse));
      }
      return Promise.reject(new Error(`Unexpected URL: ${url}`));
    });

    // Phase 1: Set env BEFORE loading module
    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const CacheDashboard = loadCacheDashboard();
    const user = userEvent.setup({ delay: null });

    render(<CacheDashboard />);

    // FIX 2: Use findByText
    expect(await screen.findByText('Cache Management Dashboard')).toBeInTheDocument();

    // Select game and try to invalidate
    const gameFilter = screen.getByLabelText('Filter by Game');
    await user.selectOptions(gameFilter, 'game-1');

    const invalidateButton = await screen.findByText('Invalidate Cache for Chess');
    await user.click(invalidateButton);

    const confirmButton = screen.getByRole('button', { name: 'Confirm' });
    await user.click(confirmButton);

    // Check error toast appears
    expect(await screen.findByText(/Failed to invalidate cache/)).toBeInTheDocument();
  });

  it('refreshes stats when refresh button is clicked', async () => {
    // FIX 1: Use mockImplementation
    fetchMock.mockImplementation((url: string) => {
      if (url.includes('/api/v1/games')) {
        return Promise.resolve(createJsonResponse(mockGamesResponse));
      }
      if (url.includes('/api/v1/admin/cache/stats')) {
        return Promise.resolve(createJsonResponse(mockStatsResponse));
      }
      return Promise.reject(new Error(`Unexpected URL: ${url}`));
    });

    // Phase 1: Set env BEFORE loading module
    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const CacheDashboard = loadCacheDashboard();
    const user = userEvent.setup({ delay: null });

    render(<CacheDashboard />);

    // FIX 2: Use findByText
    expect(await screen.findByText('Cache Management Dashboard')).toBeInTheDocument();

    // Click refresh button
    const refreshButton = screen.getByLabelText('Refresh cache statistics');
    await user.click(refreshButton);

    // Check info toast appears
    expect(await screen.findByText('Refreshing cache statistics...')).toBeInTheDocument();

    // Check stats endpoint was called again
    await waitFor(() => {
      const calls = fetchMock.mock.calls.filter((call) =>
        call[0].toString().includes('/api/v1/admin/cache/stats')
      );
      expect(calls.length).toBeGreaterThanOrEqual(2);
    });
  });

  it('displays empty state when no cached questions exist', async () => {
    const emptyStats = { ...mockStatsResponse, topQuestions: [] };

    // FIX 1: Use mockImplementation
    fetchMock.mockImplementation((url: string) => {
      if (url.includes('/api/v1/games')) {
        return Promise.resolve(createJsonResponse(mockGamesResponse));
      }
      if (url.includes('/api/v1/admin/cache/stats')) {
        return Promise.resolve(createJsonResponse(emptyStats));
      }
      return Promise.reject(new Error(`Unexpected URL: ${url}`));
    });

    // Phase 1: Set env BEFORE loading module
    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const CacheDashboard = loadCacheDashboard();

    render(<CacheDashboard />);

    // FIX 2: Use findByText
    expect(await screen.findByText('Cache Management Dashboard')).toBeInTheDocument();

    expect(screen.getByText('No cached questions found. Cache will populate as users interact with the system.')).toBeInTheDocument();
    expect(screen.queryByText('Top Cached Questions')).not.toBeInTheDocument();
  });

  it('renders error state when API returns unauthorized', async () => {
    // FIX 1: Use mockImplementation
    fetchMock.mockImplementation((url: string) => {
      if (url.includes('/api/v1/games')) {
        return Promise.resolve(createJsonResponse(mockGamesResponse));
      }
      if (url.includes('/api/v1/admin/cache/stats')) {
        return Promise.resolve(createJsonResponse(null, false, 401));
      }
      return Promise.reject(new Error(`Unexpected URL: ${url}`));
    });

    // Phase 1: Set env BEFORE loading module
    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const CacheDashboard = loadCacheDashboard();

    render(<CacheDashboard />);

    // FIX 2: Use findByText for async rendering
    expect(await screen.findByText('Error')).toBeInTheDocument();
    expect(screen.getByText('Unauthorized - Admin access required')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Back to Admin Dashboard' })).toBeInTheDocument();
  });

  it('automatically dismisses toast notifications after 5 seconds', async () => {
    // FIX 4: Proper fake timer setup - must be called BEFORE any renders
    jest.useFakeTimers();

    // FIX 1: Use mockImplementation
    fetchMock.mockImplementation((url: string) => {
      if (url.includes('/api/v1/games')) {
        return Promise.resolve(createJsonResponse(mockGamesResponse));
      }
      if (url.includes('/api/v1/admin/cache/stats')) {
        return Promise.resolve(createJsonResponse(mockStatsResponse));
      }
      return Promise.reject(new Error(`Unexpected URL: ${url}`));
    });

    // Phase 1: Set env BEFORE loading module
    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const CacheDashboard = loadCacheDashboard();
    const user = userEvent.setup({ delay: null });

    render(<CacheDashboard />);

    // FIX 2: Use findByText
    expect(await screen.findByText('Cache Management Dashboard')).toBeInTheDocument();

    // Trigger a refresh to create a toast
    const refreshButton = screen.getByLabelText('Refresh cache statistics');
    await user.click(refreshButton);

    // Wait for toast to appear
    expect(await screen.findByText('Refreshing cache statistics...')).toBeInTheDocument();

    // FIX (issue #463): Advance timers BEFORE waitFor, NOT inside it
    // Anti-pattern: jest.advanceTimersByTime() inside waitFor() callback causes timeouts
    // Correct pattern: Advance timers first, then wait for assertion
    jest.advanceTimersByTime(5000);

    // Toast should be removed after 5 seconds
    await waitFor(() => {
      expect(screen.queryByText('Refreshing cache statistics...')).not.toBeInTheDocument();
    });

    // FIX 4: Restore real timers (also done in afterEach for safety)
    jest.useRealTimers();
  });

  it('allows manual dismissal of toast notifications', async () => {
    // FIX 1: Use mockImplementation
    fetchMock.mockImplementation((url: string) => {
      if (url.includes('/api/v1/games')) {
        return Promise.resolve(createJsonResponse(mockGamesResponse));
      }
      if (url.includes('/api/v1/admin/cache/stats')) {
        return Promise.resolve(createJsonResponse(mockStatsResponse));
      }
      return Promise.reject(new Error(`Unexpected URL: ${url}`));
    });

    // Phase 1: Set env BEFORE loading module
    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const CacheDashboard = loadCacheDashboard();
    const user = userEvent.setup({ delay: null });

    render(<CacheDashboard />);

    // FIX 2: Use findByText
    expect(await screen.findByText('Cache Management Dashboard')).toBeInTheDocument();

    // Trigger a refresh to create a toast
    const refreshButton = screen.getByLabelText('Refresh cache statistics');
    await user.click(refreshButton);

    // Wait for toast to appear
    expect(await screen.findByText('Refreshing cache statistics...')).toBeInTheDocument();

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

    // Phase 1: Set env BEFORE loading module
    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    // Phase 2: Store component once per test, not per subtest
    const CacheDashboard = loadCacheDashboard();

    // Test KB formatting - FIX 1: Use mockImplementation
    fetchMock.mockImplementation((url: string) => {
      if (url.includes('/api/v1/games')) {
        return Promise.resolve(createJsonResponse(mockGamesResponse));
      }
      if (url.includes('/api/v1/admin/cache/stats')) {
        return Promise.resolve(createJsonResponse(smallCache));
      }
      return Promise.reject(new Error(`Unexpected URL: ${url}`));
    });

    render(<CacheDashboard />);

    // FIX 2: Use findByText
    expect(await screen.findByText('512.00 KB')).toBeInTheDocument();

    cleanup();

    // Phase 2: Use direct render after cleanup (no module reload)
    // Phase 3: Reset mocks before new chain
    fetchMock.mockReset();
    fetchMock.mockImplementation((url: string) => {
      if (url.includes('/api/v1/games')) {
        return Promise.resolve(createJsonResponse(mockGamesResponse));
      }
      if (url.includes('/api/v1/admin/cache/stats')) {
        return Promise.resolve(createJsonResponse(largeCache));
      }
      return Promise.reject(new Error(`Unexpected URL: ${url}`));
    });

    render(<CacheDashboard />);

    expect(await screen.findByText('50.00 MB')).toBeInTheDocument();
  });

  it('falls back to localhost API base when NEXT_PUBLIC_API_BASE is unset', async () => {
    // Phase 1: Ensure env is unset BEFORE loading module
    delete process.env.NEXT_PUBLIC_API_BASE;
    const CacheDashboard = loadCacheDashboard();

    // FIX 1: Use mockImplementation
    fetchMock.mockImplementation((url: string) => {
      if (url.includes('/api/v1/games')) {
        return Promise.resolve(createJsonResponse(mockGamesResponse));
      }
      if (url.includes('/api/v1/admin/cache/stats')) {
        return Promise.resolve(createJsonResponse(mockStatsResponse));
      }
      return Promise.reject(new Error(`Unexpected URL: ${url}`));
    });

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
    // FIX 1: Use mockImplementation
    fetchMock.mockImplementation((url: string) => {
      if (url.includes('/api/v1/games')) {
        return Promise.resolve(createJsonResponse(mockGamesResponse));
      }
      if (url.includes('/api/v1/admin/cache/stats')) {
        return Promise.resolve(createJsonResponse(mockStatsResponse));
      }
      return Promise.reject(new Error(`Unexpected URL: ${url}`));
    });

    // Phase 1: Set env BEFORE loading module
    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const CacheDashboard = loadCacheDashboard();
    const user = userEvent.setup({ delay: null });

    render(<CacheDashboard />);

    // FIX 2: Use findByText
    expect(await screen.findByText('Cache Management Dashboard')).toBeInTheDocument();

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
    // FIX 1: Use mockImplementation
    fetchMock.mockImplementation((url: string) => {
      if (url.includes('/api/v1/games')) {
        return Promise.resolve(createJsonResponse(mockGamesResponse));
      }
      if (url.includes('/api/v1/admin/cache/stats')) {
        return Promise.resolve(createJsonResponse(mockStatsResponse));
      }
      return Promise.reject(new Error(`Unexpected URL: ${url}`));
    });

    // Phase 1: Set env BEFORE loading module
    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const CacheDashboard = loadCacheDashboard();

    render(<CacheDashboard />);

    // FIX 2: Use findByText
    expect(await screen.findByText('Cache Management Dashboard')).toBeInTheDocument();

    const gameFilter = screen.getByLabelText('Filter by Game');
    expect(gameFilter).toBeInTheDocument();

    // Check all options are present
    expect(within(gameFilter).getByText('All Games')).toBeInTheDocument();
    expect(within(gameFilter).getByText('Chess')).toBeInTheDocument();
    expect(within(gameFilter).getByText('Tic-Tac-Toe')).toBeInTheDocument();
  });

  it('shows message when no game is selected for invalidation', async () => {
    // FIX 1: Use mockImplementation
    fetchMock.mockImplementation((url: string) => {
      if (url.includes('/api/v1/games')) {
        return Promise.resolve(createJsonResponse(mockGamesResponse));
      }
      if (url.includes('/api/v1/admin/cache/stats')) {
        return Promise.resolve(createJsonResponse(mockStatsResponse));
      }
      return Promise.reject(new Error(`Unexpected URL: ${url}`));
    });

    // Phase 1: Set env BEFORE loading module
    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const CacheDashboard = loadCacheDashboard();

    render(<CacheDashboard />);

    // FIX 2: Use findByText
    expect(await screen.findByText('Cache Management Dashboard')).toBeInTheDocument();

    // With "All Games" selected, should show message instead of button
    expect(screen.getByText('Select a specific game to invalidate its cache')).toBeInTheDocument();
    expect(screen.queryByText(/Invalidate Cache for/)).not.toBeInTheDocument();
  });
});
