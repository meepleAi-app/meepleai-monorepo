import { render, screen, waitFor, within, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import AdminDashboard from '../../pages/admin';

type FetchMock = jest.MockedFunction<typeof fetch>;
const API_BASE_FALLBACK = 'http://localhost:8080'; // Default API base for tests (FE-IMP-005)

const createJsonResponse = (data: unknown, ok = true) =>
  ({
    ok,
    json: async () => data
  } as unknown as Response);

/**
 * TEST PATTERNS DOCUMENTATION
 *
 * Pattern 1: URL-based mocking for components with multiple API calls
 * Use mockImplementation to check URL and return appropriate responses.
 * This prevents undefined crashes when components make more calls than expected.
 *
 * Example:
 *   fetchMock.mockImplementation((url: string | URL | Request) => {
 *     const urlString = typeof url === 'string' ? url : url instanceof URL ? url.toString() : url.url;
 *     if (urlString.includes('/api/v1/admin/stats')) {
 *       return Promise.resolve(createJsonResponse(statsPayload));
 *     }
 *     return Promise.resolve(createJsonResponse(requestsPayload));
 *   });
 *
 * Used in: "resets page to 1 when endpoint filter changes" test
 * Benefit: Handles unlimited API calls, prevents "Cannot read properties of undefined" errors
 */

// Mock AdminCharts components
jest.mock('../../components/AdminCharts', () => ({
  EndpointDistributionChart: ({ endpointCounts }: { endpointCounts: Record<string, number> }) => (
    <div data-testid="endpoint-chart">{JSON.stringify(endpointCounts)}</div>
  ),
  LatencyDistributionChart: ({ requests }: { requests: Array<{ latencyMs: number }> }) => (
    <div data-testid="latency-chart">{requests.length} requests</div>
  ),
  RequestsTimeSeriesChart: ({ requests }: { requests: Array<{ createdAt: string }> }) => (
    <div data-testid="timeseries-chart">{requests.length} requests</div>
  ),
  FeedbackChart: ({ feedbackCounts }: { feedbackCounts: Record<string, number> }) => (
    <div data-testid="feedback-chart">{JSON.stringify(feedbackCounts)}</div>
  )
}));

describe('AdminDashboard', () => {
  const originalFetch = global.fetch;
  const boundCreateElement = document.createElement.bind(document) as typeof document.createElement;
  const originalCreateObjectURL = URL.createObjectURL;
  const originalRevokeObjectURL = URL.revokeObjectURL;
  let fetchMock: FetchMock;
  let createObjectURLMock: jest.Mock;
  let revokeObjectURLMock: jest.Mock;
  let createElementSpy: jest.SpyInstance;
  let clickSpy: jest.SpyInstance;
  const apiBase = 'https://api.example.com';

  const loadAdminDashboard = () => {
    const adminPath = require.resolve('../../pages/admin');
    const apiCacheKeys = Object.keys(require.cache).filter((key) => key.includes('lib/api'));

    delete require.cache[adminPath];
    apiCacheKeys.forEach((key) => {
      delete require.cache[key];
    });

    return require('../../pages/admin').default;
  };

  const sampleRequest = {
    id: 'req-1',
    userId: 'user-1',
    gameId: 'game-1',
    endpoint: 'qa',
    query: 'How do I win?',
    responseSnippet: 'To win, you must...',
    latencyMs: 1234,
    tokenCount: 567,
    promptTokens: 100,
    completionTokens: 467,
    confidence: 0.95,
    status: 'Success',
    errorMessage: null,
    ipAddress: '127.0.0.1',
    userAgent: 'Mozilla/5.0',
    createdAt: '2024-01-15T10:30:00Z',
    model: 'gpt-4',
    finishReason: 'stop'
  };

  const sampleStats = {
    totalRequests: 1000,
    avgLatencyMs: 1234,
    totalTokens: 50000,
    successRate: 0.955,
    endpointCounts: { qa: 600, explain: 300, setup: 100 },
    feedbackCounts: { helpful: 450, 'not-helpful': 50 },
    totalFeedback: 500
  };

  beforeAll(() => {
    fetchMock = jest.fn() as FetchMock;
    global.fetch = fetchMock;

    createObjectURLMock = jest.fn().mockReturnValue('blob:mock-url');
    revokeObjectURLMock = jest.fn();

    Object.defineProperty(URL, 'createObjectURL', {
      configurable: true,
      writable: true,
      value: createObjectURLMock
    });
    Object.defineProperty(URL, 'revokeObjectURL', {
      configurable: true,
      writable: true,
      value: revokeObjectURLMock
    });

    createElementSpy = jest.spyOn(document, 'createElement');
    clickSpy = jest.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});
  });

  afterAll(() => {
    global.fetch = originalFetch;

    if (originalCreateObjectURL) {
      Object.defineProperty(URL, 'createObjectURL', {
        configurable: true,
        writable: true,
        value: originalCreateObjectURL
      });
    } else {
      delete (URL as unknown as { createObjectURL?: unknown }).createObjectURL;
    }

    if (originalRevokeObjectURL) {
      Object.defineProperty(URL, 'revokeObjectURL', {
        configurable: true,
        writable: true,
        value: originalRevokeObjectURL
      });
    } else {
      delete (URL as unknown as { revokeObjectURL?: unknown }).revokeObjectURL;
    }

    createElementSpy.mockRestore();
    clickSpy.mockRestore();
  });

  beforeEach(() => {
    fetchMock.mockReset();
    createObjectURLMock.mockClear();
    revokeObjectURLMock.mockClear();
    clickSpy.mockClear();
    createElementSpy.mockImplementation((...args: Parameters<typeof boundCreateElement>) =>
      boundCreateElement(...args)
    );
  });

  afterEach(() => {
    delete process.env.NEXT_PUBLIC_API_BASE;
    jest.clearAllMocks();
    createElementSpy.mockImplementation((...args: Parameters<typeof boundCreateElement>) =>
      boundCreateElement(...args)
    );
  });

  // =============================================================================
  // DATA LOADING TESTS
  // =============================================================================

  describe('Data Loading', () => {
    it('renders loading state while data is being fetched', () => {
      fetchMock.mockImplementation(() => new Promise(() => {}));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      render(<AdminDashboard />);

      expect(screen.getByText('Loading...')).toBeInTheDocument();
    });

    it('fetches AI requests on mount with correct API call', async () => {
      const requestsPayload = { requests: [], totalCount: 0 };
      const statsPayload = { ...sampleStats, endpointCounts: {}, feedbackCounts: {}, totalFeedback: 0 };

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      render(<AdminDashboard />);

      await waitFor(() =>
        expect(fetchMock).toHaveBeenCalledWith(
          `${apiBase}/api/v1/admin/requests?limit=50&offset=0`,
          expect.objectContaining({ credentials: 'include' })
        )
      );
    });

    it('fetches stats on mount with correct API call', async () => {
      const requestsPayload = { requests: [], totalCount: 0 };
      const statsPayload = { ...sampleStats, endpointCounts: {}, feedbackCounts: {}, totalFeedback: 0 };

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      render(<AdminDashboard />);

      await waitFor(() =>
        expect(fetchMock).toHaveBeenCalledWith(
          `${apiBase}/api/v1/admin/stats`,
          expect.objectContaining({ credentials: 'include' })
        )
      );
    });

    it('displays total count from API response', async () => {
      const requestsPayload = {
        requests: [sampleRequest],
        totalCount: 150
      };
      const statsPayload = sampleStats;

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      render(<AdminDashboard />);

      await waitFor(() => {
        expect(screen.getByText(/Showing.*150.*requests/)).toBeInTheDocument();
      });
    });

    it('handles empty results with no requests', async () => {
      const requestsPayload = { requests: [], totalCount: 0 };
      const statsPayload = {
        totalRequests: 0,
        avgLatencyMs: 0,
        totalTokens: 0,
        successRate: 0,
        endpointCounts: {},
        feedbackCounts: {},
        totalFeedback: 0
      };

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      render(<AdminDashboard />);

      await waitFor(() => {
        expect(screen.getByText('No AI requests found.')).toBeInTheDocument();
      });
    });

    it('handles API error for requests with error message', async () => {
      fetchMock.mockResolvedValueOnce(createJsonResponse({}, false));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      render(<AdminDashboard />);

      await waitFor(() => {
        expect(screen.getByText('Error')).toBeInTheDocument();
        expect(screen.getByText(/API \/api\/v1\/admin\/requests/)).toBeInTheDocument();
      });
    });

    it('handles API error for stats', async () => {
      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      const requestsPayload = { requests: [], totalCount: 0 };

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse({}, false));

      render(<AdminDashboard />);

      await waitFor(() => {
        expect(screen.getByText(/API \/api\/v1\/admin\/stats/)).toBeInTheDocument();
      });
    });

    it('sets initial page to 1', async () => {
      const requestsPayload = { requests: [sampleRequest], totalCount: 100 };
      const statsPayload = sampleStats;

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      render(<AdminDashboard />);

      await waitFor(() => {
        expect(screen.getByText(/Page 1 of/)).toBeInTheDocument();
      });
    });
  });

  // =============================================================================
  // REQUEST TABLE DISPLAY TESTS
  // =============================================================================

  describe('Request Table Display', () => {
    it('renders table headers', async () => {
      const requestsPayload = { requests: [sampleRequest], totalCount: 1 };
      const statsPayload = sampleStats;

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      render(<AdminDashboard />);

      // Wait for component to load
      await waitFor(() => {
        expect(screen.getByText('Admin Dashboard')).toBeInTheDocument();
      });

      // Table headers should be present (rendered as title case, styled as uppercase)
      expect(screen.getByText('Timestamp')).toBeInTheDocument();
      expect(screen.getByText('Endpoint')).toBeInTheDocument();
      expect(screen.getByText('Latency')).toBeInTheDocument();
      expect(screen.getByText('Prompt')).toBeInTheDocument();
      expect(screen.getByText('Completion')).toBeInTheDocument();
      expect(screen.getAllByText('Total')[0]).toBeInTheDocument(); // Also in stats
      expect(screen.getByText('Confidence')).toBeInTheDocument();
      expect(screen.getByText('Status')).toBeInTheDocument();
    });

    it('displays request rows with formatted data', async () => {
      const requestsPayload = { requests: [sampleRequest], totalCount: 1 };
      const statsPayload = sampleStats;

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      render(<AdminDashboard />);

      await waitFor(() => {
        expect(screen.getByText('How do I win?')).toBeInTheDocument();
        expect(screen.getAllByText('qa').length).toBeGreaterThan(0);
      });
    });

    it('formats timestamp with toLocaleString', async () => {
      const request = { ...sampleRequest, createdAt: '2024-01-15T10:30:00.000Z' };
      const requestsPayload = { requests: [request], totalCount: 1 };
      const statsPayload = sampleStats;

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      render(<AdminDashboard />);

      await waitFor(() => {
        const expectedDate = new Date('2024-01-15T10:30:00.000Z').toLocaleString();
        expect(screen.getByText(expectedDate)).toBeInTheDocument();
      });
    });

    it('displays latency in ms format', async () => {
      const requestsPayload = { requests: [sampleRequest], totalCount: 1 };
      const statsPayload = sampleStats;

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      render(<AdminDashboard />);

      await waitFor(() => {
        expect(screen.getAllByText('1234ms').length).toBeGreaterThan(0);
      });
    });

    it('displays token count', async () => {
      const requestsPayload = { requests: [sampleRequest], totalCount: 1 };
      const statsPayload = sampleStats;

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      render(<AdminDashboard />);

      await waitFor(() => {
        expect(screen.getAllByText('100').length).toBeGreaterThan(0); // promptTokens
        expect(screen.getAllByText('467').length).toBeGreaterThan(0); // completionTokens
        expect(screen.getAllByText('567').length).toBeGreaterThan(0); // totalTokens
      });
    });

    it('shows status badge', async () => {
      const requestsPayload = { requests: [sampleRequest], totalCount: 1 };
      const statsPayload = sampleStats;

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      render(<AdminDashboard />);

      await waitFor(() => {
        expect(screen.getByText('Success')).toBeInTheDocument();
      });
    });

    it('displays confidence score when available', async () => {
      const requestsPayload = { requests: [sampleRequest], totalCount: 1 };
      const statsPayload = sampleStats;

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      render(<AdminDashboard />);

      await waitFor(() => {
        expect(screen.getByText('0.95')).toBeInTheDocument();
      });
    });

    it('shows dash when confidence is null', async () => {
      const request = { ...sampleRequest, confidence: null };
      const requestsPayload = { requests: [request], totalCount: 1 };
      const statsPayload = sampleStats;

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      render(<AdminDashboard />);

      await waitFor(() => {
        expect(screen.getByText('Admin Dashboard')).toBeInTheDocument();
      });

      // Find all cells with "-" and verify at least one exists (confidence column)
      const cells = screen.getAllByText('-');
      expect(cells.length).toBeGreaterThan(0);
    });
  });

  // =============================================================================
  // STATISTICS DISPLAY TESTS
  // =============================================================================

  describe('Statistics Display', () => {
    it('displays total requests count', async () => {
      const requestsPayload = { requests: [], totalCount: 0 };
      const statsPayload = sampleStats;

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      render(<AdminDashboard />);

      await waitFor(() => {
        const totalRequestsCard = screen.getByText('Total Requests').parentElement as HTMLElement;
        expect(within(totalRequestsCard).getByText('1000')).toBeInTheDocument();
      });
    });

    it('displays average latency formatted as ms', async () => {
      const requestsPayload = { requests: [], totalCount: 0 };
      const statsPayload = { ...sampleStats, avgLatencyMs: 1234.567 };

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      render(<AdminDashboard />);

      await waitFor(() => {
        expect(screen.getByText('1235ms')).toBeInTheDocument(); // rounded
      });
    });

    it('displays total tokens', async () => {
      const requestsPayload = { requests: [], totalCount: 0 };
      const statsPayload = sampleStats;

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      render(<AdminDashboard />);

      await waitFor(() => {
        const totalTokensCard = screen.getByText('Total Tokens').parentElement as HTMLElement;
        expect(within(totalTokensCard).getByText('50000')).toBeInTheDocument();
      });
    });

    it('displays success rate as percentage', async () => {
      const requestsPayload = { requests: [], totalCount: 0 };
      const statsPayload = { ...sampleStats, successRate: 0.955 };

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      render(<AdminDashboard />);

      await waitFor(() => {
        expect(screen.getByText('95.5%')).toBeInTheDocument();
      });
    });

    it('shows endpoint counts breakdown', async () => {
      const requestsPayload = { requests: [], totalCount: 0 };
      const statsPayload = sampleStats;

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      render(<AdminDashboard />);

      await waitFor(() => {
        expect(screen.getByText('600')).toBeInTheDocument(); // qa
        expect(screen.getByText('300')).toBeInTheDocument(); // explain
        expect(screen.getByText('100')).toBeInTheDocument(); // setup
      });
    });

    it('shows feedback counts (helpful and not helpful)', async () => {
      const requestsPayload = { requests: [], totalCount: 0 };
      const statsPayload = sampleStats;

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      render(<AdminDashboard />);

      await waitFor(() => {
        expect(screen.getByText('Utile: 450')).toBeInTheDocument();
        expect(screen.getByText('Non utile: 50')).toBeInTheDocument();
      });
    });
  });

  // =============================================================================
  // FILTERING TESTS
  // =============================================================================

  describe('Filtering', () => {
    it('filters by search query in text input', async () => {
      const requestsPayload = {
        requests: [
          sampleRequest,
          { ...sampleRequest, id: 'req-2', query: 'Different query' }
        ],
        totalCount: 2
      };
      const statsPayload = sampleStats;

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();
      const user = userEvent.setup();

      render(<AdminDashboard />);

      await waitFor(() => {
        expect(screen.getByText('How do I win?')).toBeInTheDocument();
      });

      const filterInput = screen.getByPlaceholderText('Filter by query, endpoint, user ID, or game ID...');
      await user.type(filterInput, 'Different');

      await waitFor(() => {
        expect(screen.queryByText('How do I win?')).not.toBeInTheDocument();
        expect(screen.getByText('Different query')).toBeInTheDocument();
      });
    });

    it('filters by endpoint via dropdown', async () => {
      const qaOnlyPayload = {
        requests: [sampleRequest],
        totalCount: 1
      };
      const requestsPayload = {
        requests: [
          sampleRequest,
          { ...sampleRequest, id: 'req-2', endpoint: 'explain' }
        ],
        totalCount: 2
      };
      const statsPayload = sampleStats;

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload))
        .mockResolvedValueOnce(createJsonResponse(qaOnlyPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();
      const user = userEvent.setup();

      render(<AdminDashboard />);

      await waitFor(() => {
        expect(screen.getByText('Admin Dashboard')).toBeInTheDocument();
      });

      const endpointSelect = screen.getByRole('combobox');
      await user.selectOptions(endpointSelect, 'qa');

      await waitFor(() =>
        expect(fetchMock).toHaveBeenCalledWith(
          `${apiBase}/api/v1/admin/requests?limit=50&offset=0&endpoint=qa`,
          expect.objectContaining({ credentials: 'include' })
        )
      );
    });

    it('filters by date range (startDate)', async () => {
      const requestsPayload = { requests: [sampleRequest], totalCount: 1 };
      const statsPayload = sampleStats;

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload))
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();
      const user = userEvent.setup();

      render(<AdminDashboard />);

      await waitFor(() => {
        expect(screen.getByText('Admin Dashboard')).toBeInTheDocument();
      });

      const startDateInput = screen.getByLabelText('Start Date');
      await user.type(startDateInput, '2024-01-01');

      await waitFor(() => {
        const calls = fetchMock.mock.calls;
        const lastCall = calls[calls.length - 2]; // Second-to-last call (last is stats)
        expect(lastCall[0]).toContain('startDate=');
      });
    });

    it('filters by date range (endDate)', async () => {
      const requestsPayload = { requests: [sampleRequest], totalCount: 1 };
      const statsPayload = sampleStats;

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload))
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();
      const user = userEvent.setup();

      render(<AdminDashboard />);

      await waitFor(() => {
        expect(screen.getByText('Admin Dashboard')).toBeInTheDocument();
      });

      const endDateInput = screen.getByLabelText('End Date');
      await user.type(endDateInput, '2024-01-31');

      await waitFor(() => {
        const calls = fetchMock.mock.calls;
        const lastCall = calls[calls.length - 2];
        expect(lastCall[0]).toContain('endDate=');
      });
    });

    it('clears date filters when Clear Dates button is clicked', async () => {
      const requestsPayload = { requests: [sampleRequest], totalCount: 1 };
      const statsPayload = sampleStats;

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload))
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload))
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();
      const user = userEvent.setup();

      render(<AdminDashboard />);

      await waitFor(() => {
        expect(screen.getByText('Admin Dashboard')).toBeInTheDocument();
      });

      const startDateInput = screen.getByLabelText('Start Date') as HTMLInputElement;
      fireEvent.change(startDateInput, { target: { value: '2024-01-01' } });

      await waitFor(() => {
        expect(fetchMock).toHaveBeenCalledTimes(4);
      });

      const clearButton = screen.getByRole('button', { name: 'Clear Dates' });
      await user.click(clearButton);

      // Wait for the API call to complete after clearing
      await waitFor(() => {
        expect(fetchMock).toHaveBeenCalledTimes(6);
      });

      // Re-query the input after state update to get fresh DOM reference
      await waitFor(() => {
        const updatedInput = screen.getByLabelText('Start Date') as HTMLInputElement;
        expect(updatedInput.value).toBe('');
      });
    });

    it('disables Clear Dates button when no dates are set', async () => {
      const requestsPayload = { requests: [], totalCount: 0 };
      const statsPayload = { ...sampleStats, endpointCounts: {}, feedbackCounts: {}, totalFeedback: 0 };

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      render(<AdminDashboard />);

      await waitFor(() => {
        expect(screen.getByText('Admin Dashboard')).toBeInTheDocument();
      });

      const clearButton = screen.getByRole('button', { name: 'Clear Dates' });
      expect(clearButton).toBeDisabled();
    });

    /**
     * SKIPPED: Complex useEffect interaction makes mock orchestration brittle
     *
     * This test verifies that changing the endpoint filter resets pagination to page 1.
     * The feature works correctly in production, but the test is challenging to maintain due to
     * the component's useEffect architecture.
     *
     * Current architecture (admin.tsx lines 103-110):
     * - fetchData depends on: [endpointFilter, startDate, endDate, page, pageSize]
     * - useEffect(..., [fetchData]) calls fetchData when it changes
     * - useEffect(..., [endpointFilter, startDate, endDate]) calls setPage(1) when filters change
     *
     * When endpointFilter changes:
     * 1. fetchData function recreates (dependency changed) → useEffect calls it
     * 2. setPage(1) runs → page state changes
     * 3. fetchData recreates again (page dependency changed) → useEffect calls it again
     * 4. This double-fetch makes mock orchestration brittle and error-prone
     *
     * The crash "Cannot read properties of undefined (reading 'filter')" occurs because:
     * - Test provides 8 mocked responses
     * - Component makes more calls due to useEffect timing
     * - Unmocked fetch returns undefined → requests becomes undefined → component crashes
     *
     * Recommended fixes:
     * 1. Refactor component to use a single useEffect with proper dependency management
     * 2. Add a ref to prevent double-fetching when both filter and page change
     * 3. Use React Query or similar library for better fetch orchestration
     *
     * Alternative test approach:
     * - Use MSW (Mock Service Worker) instead of jest mock for more realistic fetch behavior
     * - Add infinite mock responses that don't run out
     * - Test the page reset behavior with E2E tests (Playwright) instead of unit tests
     *
     * Estimated effort: 4-6 hours to refactor component architecture
     *
     * Related: CHAT-02 has similar useEffect orchestration complexity
     */
    it('resets page to 1 when endpoint filter changes', async () => {
      const requestsPayload = { requests: Array(50).fill(sampleRequest).map((r, i) => ({ ...r, id: `req-${i}` })), totalCount: 100 };
      const statsPayload = sampleStats;

      // Mock responses for:
      // 1. Initial load (requests + stats) = 2 calls
      // 2. Page 2 navigation (requests + stats) = 2 calls
      // 3. Filter change triggers two fetches due to useEffect architecture:
      //    - First: endpointFilter changes → fetchData recreated → useEffect calls it
      //    - Second: setPage(1) runs → page changes → fetchData recreated → useEffect calls it
      //    So we need 4 more calls (2 pairs of requests + stats)
      // Total: 2 + 2 + 4 = 8 calls
      // Pattern: Use URL-based mocking to handle alternating requests/stats calls
      fetchMock.mockImplementation((url: string | URL | Request) => {
        const urlString = typeof url === 'string' ? url : url instanceof URL ? url.toString() : url.url;
        if (urlString.includes('/api/v1/admin/stats')) {
          return Promise.resolve(createJsonResponse(statsPayload));
        }
        return Promise.resolve(createJsonResponse(requestsPayload));
      });

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();
      const user = userEvent.setup();

      render(<AdminDashboard />);

      // Wait for initial load with data
      await waitFor(() => {
        expect(screen.getByText(/Page 1 of/)).toBeInTheDocument();
      });

      // Wait for fetch calls to settle
      await waitFor(() => {
        expect(fetchMock).toHaveBeenCalledTimes(2);
      });

      // Go to page 2
      const nextButton = screen.getByRole('button', { name: 'Next' });
      await user.click(nextButton);

      // Wait for page transition
      await waitFor(() => {
        expect(screen.getByText(/Page 2 of/)).toBeInTheDocument();
      });

      // Wait for additional fetch calls
      await waitFor(() => {
        expect(fetchMock).toHaveBeenCalledTimes(4);
      });

      // Change filter - should reset to page 1
      const endpointSelect = screen.getByRole('combobox');
      await user.selectOptions(endpointSelect, 'qa');

      // Wait for filter change to trigger page reset and new data fetch
      // The filter change causes two sequential fetches (see comment above)
      await waitFor(
        () => {
          expect(screen.getByText(/Page 1 of/)).toBeInTheDocument();
        },
        { timeout: 3000 }
      );

      // After filter change, we should have made 8 total API calls:
      // Initial (2) + Page 2 (2) + Filter change double-fetch (4) = 8
      await waitFor(() => {
        expect(fetchMock).toHaveBeenCalledTimes(8);
      });
    });
  });

  // =============================================================================
  // PAGINATION TESTS
  // =============================================================================

  describe('Pagination', () => {
    it('displays current page number', async () => {
      const requestsPayload = { requests: [sampleRequest], totalCount: 100 };
      const statsPayload = sampleStats;

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      render(<AdminDashboard />);

      await waitFor(() => {
        expect(screen.getByText(/Page 1 of/)).toBeInTheDocument();
      });
    });

    it('displays total pages', async () => {
      const requestsPayload = { requests: [sampleRequest], totalCount: 200 };
      const statsPayload = sampleStats;

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      render(<AdminDashboard />);

      await waitFor(() => {
        expect(screen.getByText(/Page 1 of 4/)).toBeInTheDocument();
      });
    });

    it('Next button advances page', async () => {
      const requestsPayload = { requests: [sampleRequest], totalCount: 100 };
      const statsPayload = sampleStats;

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload))
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();
      const user = userEvent.setup();

      render(<AdminDashboard />);

      await waitFor(() => {
        expect(screen.getByText(/Page 1 of/)).toBeInTheDocument();
      });

      const nextButton = screen.getByRole('button', { name: 'Next' });
      await user.click(nextButton);

      await waitFor(() => {
        expect(screen.getByText(/Page 2 of/)).toBeInTheDocument();
      });

      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/api/v1/admin/requests?limit=50&offset=50`,
        expect.objectContaining({ credentials: 'include' })
      );
    });

    it('Previous button goes back', async () => {
      const requestsPayload = { requests: [sampleRequest], totalCount: 100 };
      const statsPayload = sampleStats;

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload))
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload))
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();
      const user = userEvent.setup();

      render(<AdminDashboard />);

      await waitFor(() => {
        expect(screen.getByText(/Page 1 of/)).toBeInTheDocument();
      });

      const nextButton = screen.getByRole('button', { name: 'Next' });
      await user.click(nextButton);

      await waitFor(() => {
        expect(screen.getByText(/Page 2 of/)).toBeInTheDocument();
      });

      const previousButton = screen.getByRole('button', { name: 'Previous' });
      await user.click(previousButton);

      await waitFor(() => {
        expect(screen.getByText(/Page 1 of/)).toBeInTheDocument();
      });
    });

    it('disables Previous button on page 1', async () => {
      const requestsPayload = { requests: [sampleRequest], totalCount: 100 };
      const statsPayload = sampleStats;

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      render(<AdminDashboard />);

      await waitFor(() => {
        expect(screen.getByText(/Page 1 of/)).toBeInTheDocument();
      });

      const previousButton = screen.getByRole('button', { name: 'Previous' });
      expect(previousButton).toBeDisabled();
    });

    it('disables Next button on last page', async () => {
      const requestsPayload = { requests: [sampleRequest], totalCount: 50 };
      const statsPayload = sampleStats;

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      render(<AdminDashboard />);

      await waitFor(() => {
        expect(screen.getByText(/Page 1 of 1/)).toBeInTheDocument();
      });

      const nextButton = screen.getByRole('button', { name: 'Next' });
      expect(nextButton).toBeDisabled();
    });
  });

  // =============================================================================
  // CHARTS INTEGRATION TESTS
  // =============================================================================

  describe('Charts Integration', () => {
    it('renders EndpointDistributionChart with data', async () => {
      const requestsPayload = { requests: [sampleRequest], totalCount: 1 };
      const statsPayload = sampleStats;

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      render(<AdminDashboard />);

      await waitFor(() => {
        const chart = screen.getByTestId('endpoint-chart');
        expect(chart).toBeInTheDocument();
      });
    });

    it('passes endpointCounts to EndpointDistributionChart', async () => {
      const requestsPayload = { requests: [sampleRequest], totalCount: 1 };
      const statsPayload = sampleStats;

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      render(<AdminDashboard />);

      await waitFor(() => {
        const chart = screen.getByTestId('endpoint-chart');
        expect(chart.textContent).toBe(JSON.stringify(sampleStats.endpointCounts));
      });
    });

    it('renders LatencyDistributionChart with data', async () => {
      const requestsPayload = { requests: [sampleRequest], totalCount: 1 };
      const statsPayload = sampleStats;

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      render(<AdminDashboard />);

      await waitFor(() => {
        const chart = screen.getByTestId('latency-chart');
        expect(chart).toBeInTheDocument();
      });
    });

    it('passes requests array to LatencyDistributionChart', async () => {
      const requestsPayload = { requests: [sampleRequest], totalCount: 1 };
      const statsPayload = sampleStats;

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      render(<AdminDashboard />);

      await waitFor(() => {
        const chart = screen.getByTestId('latency-chart');
        expect(chart.textContent).toBe('1 requests');
      });
    });

    it('renders RequestsTimeSeriesChart with data', async () => {
      const requestsPayload = { requests: [sampleRequest], totalCount: 1 };
      const statsPayload = sampleStats;

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      render(<AdminDashboard />);

      await waitFor(() => {
        const chart = screen.getByTestId('timeseries-chart');
        expect(chart).toBeInTheDocument();
      });
    });

    it('passes requests array to RequestsTimeSeriesChart', async () => {
      const requestsPayload = { requests: [sampleRequest, { ...sampleRequest, id: 'req-2' }], totalCount: 2 };
      const statsPayload = sampleStats;

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      render(<AdminDashboard />);

      await waitFor(() => {
        const chart = screen.getByTestId('timeseries-chart');
        expect(chart.textContent).toBe('2 requests');
      });
    });

    it('renders FeedbackChart with data', async () => {
      const requestsPayload = { requests: [sampleRequest], totalCount: 1 };
      const statsPayload = sampleStats;

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      render(<AdminDashboard />);

      await waitFor(() => {
        const chart = screen.getByTestId('feedback-chart');
        expect(chart).toBeInTheDocument();
      });
    });

    it('passes feedbackCounts to FeedbackChart', async () => {
      const requestsPayload = { requests: [sampleRequest], totalCount: 1 };
      const statsPayload = sampleStats;

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      render(<AdminDashboard />);

      await waitFor(() => {
        const chart = screen.getByTestId('feedback-chart');
        expect(chart.textContent).toBe(JSON.stringify(sampleStats.feedbackCounts));
      });
    });

    it('does not render charts when no requests exist', async () => {
      const requestsPayload = { requests: [], totalCount: 0 };
      const statsPayload = {
        totalRequests: 0,
        avgLatencyMs: 0,
        totalTokens: 0,
        successRate: 0,
        endpointCounts: {},
        feedbackCounts: {},
        totalFeedback: 0
      };

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      render(<AdminDashboard />);

      await waitFor(() => {
        expect(screen.getByText('No AI requests found.')).toBeInTheDocument();
      });

      expect(screen.queryByTestId('endpoint-chart')).not.toBeInTheDocument();
      expect(screen.queryByTestId('latency-chart')).not.toBeInTheDocument();
      expect(screen.queryByTestId('timeseries-chart')).not.toBeInTheDocument();
      expect(screen.queryByTestId('feedback-chart')).not.toBeInTheDocument();
    });
  });

  // =============================================================================
  // CSV EXPORT TESTS
  // =============================================================================

  describe('CSV Export', () => {
    it('exports CSV with correct data', async () => {
      const requestsPayload = { requests: [sampleRequest], totalCount: 1 };
      const statsPayload = sampleStats;

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();
      const user = userEvent.setup();

      const anchor = boundCreateElement('a') as HTMLAnchorElement;
      createElementSpy.mockImplementation((tagName: string, ...rest: unknown[]) => {
        if (tagName === 'a') {
          return anchor;
        }
        return boundCreateElement(tagName as keyof HTMLElementTagNameMap, ...(rest as [ElementCreationOptions?]));
      });

      render(<AdminDashboard />);

      await waitFor(() => {
        expect(screen.getByText('Admin Dashboard')).toBeInTheDocument();
      });

      const exportButton = screen.getByRole('button', { name: 'Export CSV' });
      await user.click(exportButton);

      expect(createObjectURLMock).toHaveBeenCalledWith(expect.any(Blob));
      expect(anchor.download).toMatch(/^ai_requests_/);
      expect(anchor.href).toBe('blob:mock-url');
      expect(clickSpy).toHaveBeenCalled();
      expect(revokeObjectURLMock).toHaveBeenCalledWith('blob:mock-url');
    });
  });

  // =============================================================================
  // EDGE CASES AND ERROR HANDLING
  // =============================================================================

  describe('Edge Cases', () => {
    it('handles requests without user or game identifiers when filtering', async () => {
      const requestsPayload = {
        requests: [
          {
            id: '3',
            userId: null,
            gameId: null,
            endpoint: 'qa',
            query: null,
            responseSnippet: null,
            latencyMs: 120,
            tokenCount: 24,
            promptTokens: 12,
            completionTokens: 12,
            confidence: null,
            status: 'Success',
            errorMessage: null,
            ipAddress: '127.0.0.3',
            userAgent: 'jest',
            createdAt: '2024-01-03T12:00:00.000Z',
            model: null,
            finishReason: null
          }
        ],
        totalCount: 1
      };

      const statsPayload = {
        totalRequests: 1,
        avgLatencyMs: 120,
        totalTokens: 24,
        successRate: 1,
        endpointCounts: { qa: 1 },
        feedbackCounts: {},
        totalFeedback: 0
      };

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();
      const user = userEvent.setup();

      render(<AdminDashboard />);

      expect(await screen.findByText('Admin Dashboard')).toBeInTheDocument();

      const filterInput = screen.getByPlaceholderText('Filter by query, endpoint, user ID, or game ID...');

      await user.type(filterInput, 'qa');

      expect(screen.getAllByText('qa').length).toBeGreaterThan(0);
      expect(screen.getByText('Success')).toBeInTheDocument();
    });

    it('renders endpoint colors for all endpoint types including default', async () => {
      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      const requestsPayload = {
        requests: [
          { ...sampleRequest, id: '1', endpoint: 'qa' },
          { ...sampleRequest, id: '2', endpoint: 'explain' },
          { ...sampleRequest, id: '3', endpoint: 'setup' },
          { ...sampleRequest, id: '4', endpoint: 'unknown' }
        ],
        totalCount: 4
      };

      const statsPayload = {
        totalRequests: 4,
        avgLatencyMs: 100,
        totalTokens: 40,
        successRate: 100,
        endpointCounts: { qa: 1, explain: 1, setup: 1, unknown: 1 },
        feedbackCounts: {},
        totalFeedback: 0
      };

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      render(<AdminDashboard />);

      await waitFor(() => {
        const qaElements = screen.getAllByText('qa');
        expect(qaElements.length).toBeGreaterThan(0);
        const explainElements = screen.getAllByText('explain');
        expect(explainElements.length).toBeGreaterThan(0);
        const setupElements = screen.getAllByText('setup');
        expect(setupElements.length).toBeGreaterThan(0);
        const unknownElements = screen.getAllByText('unknown');
        expect(unknownElements.length).toBeGreaterThan(0);
      });
    });

    it('falls back to the localhost API base when NEXT_PUBLIC_API_BASE is unset', async () => {
      delete process.env.NEXT_PUBLIC_API_BASE;

      const emptyRequests = { requests: [], totalCount: 0 };
      const emptyStats = {
        totalRequests: 0,
        avgLatencyMs: 0,
        totalTokens: 0,
        successRate: 0,
        endpointCounts: {},
        feedbackCounts: {},
        totalFeedback: 0
      };

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(emptyRequests))
        .mockResolvedValueOnce(createJsonResponse(emptyStats));

      const AdminDashboard = loadAdminDashboard();

      render(<AdminDashboard />);

      await waitFor(() =>
        expect(fetchMock).toHaveBeenNthCalledWith(
          1,
          `${API_BASE_FALLBACK}/api/v1/admin/requests?limit=50&offset=0`,
          expect.objectContaining({ credentials: 'include' })
        )
      );
      await waitFor(() =>
        expect(fetchMock).toHaveBeenNthCalledWith(
          2,
          `${API_BASE_FALLBACK}/api/v1/admin/stats`,
          expect.objectContaining({ credentials: 'include' })
        )
      );

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
    });

    it('displays Admin Dashboard title', async () => {
      const requestsPayload = { requests: [], totalCount: 0 };
      const statsPayload = {
        totalRequests: 0,
        avgLatencyMs: 0,
        totalTokens: 0,
        successRate: 0,
        endpointCounts: {},
        feedbackCounts: {},
        totalFeedback: 0
      };

      fetchMock
        .mockResolvedValueOnce(createJsonResponse(requestsPayload))
        .mockResolvedValueOnce(createJsonResponse(statsPayload));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      render(<AdminDashboard />);

      await waitFor(() => {
        expect(screen.getByText('Admin Dashboard')).toBeInTheDocument();
      });
    });

    it('shows error message with Back to Home link', async () => {
      fetchMock.mockResolvedValueOnce(createJsonResponse({}, false));

      process.env.NEXT_PUBLIC_API_BASE = apiBase;
      const AdminDashboard = loadAdminDashboard();

      render(<AdminDashboard />);

      await waitFor(() => {
        expect(screen.getByText('Error')).toBeInTheDocument();
        expect(screen.getByRole('link', { name: 'Back to Home' })).toBeInTheDocument();
      });
    });
  });
});