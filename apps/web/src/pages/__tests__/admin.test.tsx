import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import AdminDashboard from '../admin';

describe('AdminDashboard', () => {
  const originalFetch = global.fetch;
  const originalApiBase = process.env.NEXT_PUBLIC_API_BASE;
  const originalCreateObjectURL = URL.createObjectURL;
  const originalRevokeObjectURL = URL.revokeObjectURL;
  let fetchMock: jest.Mock;

  const createJsonResponse = (data: unknown, ok = true) =>
    Promise.resolve({
      ok,
      json: () => Promise.resolve(data)
    } as Response);

  beforeAll(() => {
    process.env.NEXT_PUBLIC_API_BASE = 'https://api.example.com';
  });

  beforeEach(() => {
    fetchMock = jest.fn();
    global.fetch = fetchMock as unknown as typeof fetch;
  });

  afterEach(() => {
    global.fetch = originalFetch;
    jest.clearAllMocks();
    URL.createObjectURL = originalCreateObjectURL;
    URL.revokeObjectURL = originalRevokeObjectURL;
  });

  afterAll(() => {
    process.env.NEXT_PUBLIC_API_BASE = originalApiBase;
  });

  it('renders loading state while data is being fetched', () => {
    fetchMock.mockReturnValue(new Promise(() => {}));

    render(<AdminDashboard />);

    expect(screen.getByRole('heading', { name: /loading/i })).toBeInTheDocument();
  });

  it('renders error state when requests fetch fails', async () => {
    fetchMock.mockResolvedValueOnce(createJsonResponse({}, false));

    render(<AdminDashboard />);

    await screen.findByRole('heading', { name: /error/i });
    expect(screen.getByText(/failed to fetch requests/i)).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith(
      'https://api.example.com/admin/requests?limit=100',
      expect.objectContaining({ credentials: 'include' })
    );
  });

  it('renders data, filters requests, and fetches filtered endpoint data', async () => {
    const requestsPayload = {
      requests: [
        {
          id: '1',
          userId: 'user-1',
          gameId: 'game-1',
          endpoint: 'qa',
          query: 'How to win?',
          responseSnippet: 'Win tips',
          latencyMs: 120,
          tokenCount: 45,
          confidence: 0.9,
          status: 'Success',
          errorMessage: null,
          ipAddress: '127.0.0.1',
          userAgent: 'jest',
          createdAt: new Date('2024-01-01T10:00:00Z').toISOString()
        },
        {
          id: '2',
          userId: 'user-2',
          gameId: 'game-2',
          endpoint: 'setup',
          query: 'Setup instructions',
          responseSnippet: 'Setup tips',
          latencyMs: 300,
          tokenCount: 80,
          confidence: 0.75,
          status: 'Error',
          errorMessage: 'Boom',
          ipAddress: '127.0.0.1',
          userAgent: 'jest',
          createdAt: new Date('2024-01-02T10:00:00Z').toISOString()
        }
      ]
    };

    const statsPayload = {
      totalRequests: 2,
      avgLatencyMs: 210,
      totalTokens: 125,
      successRate: 0.5,
      endpointCounts: {
        qa: 1,
        setup: 1
      }
    };

    fetchMock
      .mockResolvedValueOnce(createJsonResponse(requestsPayload))
      .mockResolvedValueOnce(createJsonResponse(statsPayload))
      .mockResolvedValueOnce(createJsonResponse(requestsPayload))
      .mockResolvedValueOnce(createJsonResponse(statsPayload));

    render(<AdminDashboard />);

    await screen.findByRole('heading', { name: /admin dashboard/i });
    expect(screen.getByText('2')).toBeInTheDocument();
    expect(screen.getByText(/setup instructions/i)).toBeInTheDocument();

    const user = userEvent.setup();
    const textFilter = screen.getByPlaceholderText(/filter by query, endpoint, user id, or game id/i);
    await user.type(textFilter, 'setup');

    await waitFor(() => {
      expect(screen.getByText(/setup instructions/i)).toBeInTheDocument();
      expect(screen.queryByText(/how to win/i)).not.toBeInTheDocument();
    });

    const endpointSelect = screen.getByRole('combobox');
    await user.selectOptions(endpointSelect, 'qa');

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(
        'https://api.example.com/admin/requests?limit=100&endpoint=qa',
        expect.objectContaining({ credentials: 'include' })
      );
    });
  });

  it('exports data to CSV using an object URL and synthetic anchor click', async () => {
    const requestsPayload = {
      requests: [
        {
          id: '1',
          userId: 'user-1',
          gameId: 'game-1',
          endpoint: 'qa',
          query: 'How to win?',
          responseSnippet: 'Win tips',
          latencyMs: 120,
          tokenCount: 45,
          confidence: 0.9,
          status: 'Success',
          errorMessage: null,
          ipAddress: '127.0.0.1',
          userAgent: 'jest',
          createdAt: new Date('2024-01-01T10:00:00Z').toISOString()
        }
      ]
    };

    const statsPayload = {
      totalRequests: 1,
      avgLatencyMs: 120,
      totalTokens: 45,
      successRate: 1,
      endpointCounts: {
        qa: 1
      }
    };

    fetchMock
      .mockResolvedValueOnce(createJsonResponse(requestsPayload))
      .mockResolvedValueOnce(createJsonResponse(statsPayload));

    const createObjectURLMock = jest.fn(() => 'blob:mock-url');
    const revokeObjectURLMock = jest.fn();
    URL.createObjectURL = createObjectURLMock as unknown as typeof URL.createObjectURL;
    URL.revokeObjectURL = revokeObjectURLMock as unknown as typeof URL.revokeObjectURL;

    const anchor = document.createElement('a');
    const clickSpy = jest.spyOn(anchor, 'click').mockImplementation(() => {});
    const realCreateElement = document.createElement.bind(document);
    const createElementSpy = jest
      .spyOn(document, 'createElement')
      .mockImplementation((tagName: string, options?: ElementCreationOptions) => {
        if (tagName === 'a') {
          return anchor;
        }

        return realCreateElement(tagName, options);
      });

    render(<AdminDashboard />);

    await screen.findByRole('heading', { name: /admin dashboard/i });

    const user = userEvent.setup();
    await user.click(screen.getByRole('button', { name: /export csv/i }));

    expect(createObjectURLMock).toHaveBeenCalledTimes(1);
    expect(createObjectURLMock.mock.calls[0][0]).toBeInstanceOf(Blob);
    expect(anchor.download).toMatch(/ai_requests_.*\.csv/);
    expect(clickSpy).toHaveBeenCalled();
    expect(revokeObjectURLMock).toHaveBeenCalledWith('blob:mock-url');

    createElementSpy.mockRestore();
    clickSpy.mockRestore();
  });
});
