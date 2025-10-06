import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import AdminDashboard from '../admin';
import { API_BASE_FALLBACK } from '../../lib/api';

type FetchMock = jest.MockedFunction<typeof fetch>;

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

  const createJsonResponse = (data: unknown, ok = true) =>
    ({
      ok,
      json: async () => data
    } as unknown as Response);

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
    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    fetchMock.mockReset();
    createObjectURLMock.mockClear();
    revokeObjectURLMock.mockClear();
    clickSpy.mockClear();
    createElementSpy.mockImplementation((...args: Parameters<typeof boundCreateElement>) =>
      boundCreateElement(...args)
    );
  });

  afterEach(() => {
    jest.clearAllMocks();
    createElementSpy.mockImplementation((...args: Parameters<typeof boundCreateElement>) =>
      boundCreateElement(...args)
    );
  });

  it('renders loading state while data is being fetched', () => {
    fetchMock.mockImplementation(() => new Promise(() => {}));

    render(<AdminDashboard />);

    expect(screen.getByText('Loading...')).toBeInTheDocument();
  });

  it('renders requests and stats, filters, refetches by endpoint, and exports CSV', async () => {
    const requestsPayload = {
      requests: [
        {
          id: '1',
          userId: 'user-1',
          gameId: 'game-1',
          endpoint: 'qa',
          query: 'How do I win?',
          responseSnippet: null,
          latencyMs: 210,
          tokenCount: 42,
          promptTokens: 30,
          completionTokens: 12,
          confidence: 0.8,
          status: 'Success',
          errorMessage: null,
          ipAddress: '127.0.0.1',
          userAgent: 'jest',
          createdAt: '2024-01-01T12:00:00.000Z',
          model: 'anthropic/claude-3.5-sonnet',
          finishReason: 'stop'
        },
        {
          id: '2',
          userId: 'user-2',
          gameId: 'game-2',
          endpoint: 'explain',
          query: 'Setup instructions',
          responseSnippet: null,
          latencyMs: 150,
          tokenCount: 18,
          promptTokens: 10,
          completionTokens: 8,
          confidence: 0.95,
          status: 'Success',
          errorMessage: null,
          ipAddress: '127.0.0.2',
          userAgent: 'jest',
          createdAt: '2024-01-02T12:00:00.000Z',
          model: 'anthropic/claude-3-haiku',
          finishReason: 'length'
        }
      ]
    };

    const statsPayload = {
      totalRequests: 2,
      avgLatencyMs: 180,
      totalTokens: 60,
      successRate: 0.95,
      endpointCounts: {
        qa: 1,
        explain: 1
      },
      feedbackCounts: {
        helpful: 1,
        "not-helpful": 1
      },
      totalFeedback: 2
    };

    const qaOnlyPayload = {
      requests: [requestsPayload.requests[0]]
    };

    fetchMock
      .mockResolvedValueOnce(createJsonResponse(requestsPayload))
      .mockResolvedValueOnce(createJsonResponse(statsPayload))
      .mockResolvedValueOnce(createJsonResponse(qaOnlyPayload))
      .mockResolvedValueOnce(createJsonResponse(statsPayload));

    render(<AdminDashboard />);

    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/admin/requests?limit=100`,
        expect.objectContaining({ credentials: 'include' })
      )
    );

    expect(await screen.findByText('Admin Dashboard')).toBeInTheDocument();
    const totalRequestsCard = screen.getByText('Total Requests').parentElement as HTMLElement;
    expect(within(totalRequestsCard).getByText('2')).toBeInTheDocument();
    expect(screen.getByText('180ms')).toBeInTheDocument();
    expect(screen.getByText('60')).toBeInTheDocument();
    expect(screen.getByText('95.0%')).toBeInTheDocument();
    expect(screen.getByText('Feedback Totali')).toBeInTheDocument();
    expect(screen.getByText('👍 Utile: 1')).toBeInTheDocument();
    expect(screen.getByText('👎 Non utile: 1')).toBeInTheDocument();

    expect(screen.getByText('How do I win?')).toBeInTheDocument();
    expect(screen.getByText('Setup instructions')).toBeInTheDocument();
    expect(screen.getByText('30')).toBeInTheDocument();
    expect(screen.getByText('0.80')).toBeInTheDocument();
    expect(screen.getByText('anthropic/claude-3.5-sonnet (stop)')).toBeInTheDocument();

    const user = userEvent.setup();
    const filterInput = screen.getByPlaceholderText(
      'Filter by query, endpoint, user ID, or game ID...'
    );
    await user.clear(filterInput);
    await user.type(filterInput, 'setup');

    await waitFor(() => expect(screen.queryByText('How do I win?')).not.toBeInTheDocument());
    expect(screen.getByText('Setup instructions')).toBeInTheDocument();

    await user.clear(filterInput);
    await waitFor(() => {
      expect(screen.getByText('How do I win?')).toBeInTheDocument();
      expect(screen.getByText('Setup instructions')).toBeInTheDocument();
    });

    const endpointSelect = screen.getByRole('combobox');
    await user.selectOptions(endpointSelect, 'qa');

    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/admin/requests?limit=100&endpoint=qa`,
        expect.objectContaining({ credentials: 'include' })
      )
    );

    await waitFor(() => expect(screen.getByText('How do I win?')).toBeInTheDocument());
    expect(screen.queryByText('Setup instructions')).not.toBeInTheDocument();

    const anchor = boundCreateElement('a') as HTMLAnchorElement;
    createElementSpy.mockImplementation((tagName: string, ...rest: unknown[]) => {
      if (tagName === 'a') {
        return anchor;
      }
      return boundCreateElement(tagName as keyof HTMLElementTagNameMap, ...(rest as [ElementCreationOptions?]));
    });

    const exportButton = screen.getByRole('button', { name: 'Export CSV' });
    await user.click(exportButton);

    expect(createObjectURLMock).toHaveBeenCalledWith(expect.any(Blob));
    expect(anchor.download).toMatch(/^ai_requests_/);
    expect(anchor.href).toBe('blob:mock-url');
    expect(clickSpy).toHaveBeenCalled();
    expect(revokeObjectURLMock).toHaveBeenCalledWith('blob:mock-url');
  });

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
      ]
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

    const user = userEvent.setup();

    render(<AdminDashboard />);

    expect(await screen.findByText('Admin Dashboard')).toBeInTheDocument();

    const filterInput = screen.getByPlaceholderText('Filter by query, endpoint, user ID, or game ID...');

    await user.type(filterInput, 'qa');

    expect(screen.getAllByText('qa').length).toBeGreaterThan(0);
    expect(screen.getByText('Success')).toBeInTheDocument();
  });

  it('renders error state when the API responds with an error', async () => {
    fetchMock.mockResolvedValueOnce(createJsonResponse({}, false));

    render(<AdminDashboard />);

    expect(await screen.findByText('Error')).toBeInTheDocument();
    expect(screen.getByText('Failed to fetch requests')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Back to Home' })).toBeInTheDocument();
  });

  it('falls back to the localhost API base when NEXT_PUBLIC_API_BASE is unset', async () => {
    delete process.env.NEXT_PUBLIC_API_BASE;

    const emptyRequests = { requests: [] };
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

    render(<AdminDashboard />);

    await waitFor(() =>
      expect(fetchMock).toHaveBeenNthCalledWith(
        1,
        `${API_BASE_FALLBACK}/admin/requests?limit=100`,
        expect.objectContaining({ credentials: 'include' })
      )
    );
    await waitFor(() =>
      expect(fetchMock).toHaveBeenNthCalledWith(
        2,
        `${API_BASE_FALLBACK}/admin/stats`,
        expect.objectContaining({ credentials: 'include' })
      )
    );

    process.env.NEXT_PUBLIC_API_BASE = apiBase;
  });
});
