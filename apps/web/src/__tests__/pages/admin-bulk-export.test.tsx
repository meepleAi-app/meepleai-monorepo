import {  screen, waitFor } from '@testing-library/react';
import { renderWithQuery } from '../utils/query-test-utils';
import userEvent from '@testing-library/user-event';

type FetchMock = jest.MockedFunction<typeof fetch>;

const createJsonResponse = (data: unknown, ok = true, status = 200) =>
  ({
    ok,
    status,
    json: async () => data
  } as unknown as Response);

describe('BulkExport Page', () => {
  const originalFetch = global.fetch;
  let fetchMock: FetchMock;
  const apiBase = 'https://api.example.com';

  const mockRouter = {
    push: jest.fn(),
    query: {},
    pathname: '/admin/bulk-export',
    route: '/admin/bulk-export',
    asPath: '/admin/bulk-export'
  };

  jest.mock('next/router', () => ({
    useRouter: () => mockRouter
  }));

  const loadBulkExportPage = () => {
    const pagePath = require.resolve('../../pages/admin/bulk-export');
    const apiCacheKeys = Object.keys(require.cache).filter((key) => key.includes('lib/api'));

    delete require.cache[pagePath];
    apiCacheKeys.forEach((key) => {
      delete require.cache[key];
    });

    return require('../../pages/admin/bulk-export').default;
  };

  beforeAll(() => {
    fetchMock = jest.fn() as FetchMock;
    global.fetch = fetchMock;

    // Mock URL.createObjectURL and URL.revokeObjectURL
    global.URL.createObjectURL = jest.fn(() => 'blob:mock-url');
    global.URL.revokeObjectURL = jest.fn();
  });

  afterAll(() => {
    global.fetch = originalFetch;
    jest.restoreAllMocks();
  });

  beforeEach(() => {
    fetchMock.mockReset();
    mockRouter.push.mockReset();
  });

  afterEach(() => {
    delete process.env.NEXT_PUBLIC_API_BASE;
    jest.clearAllMocks();
  });

  const mockAuthResponse = {
    user: {
      id: 'test-user-id',
      email: 'editor@test.com',
      displayName: 'Test Editor',
      role: 'Editor'
    },
    expiresAt: new Date(Date.now() + 3600000).toISOString()
  };

  const mockGamesResponse = [
    { id: 'chess', name: 'Chess', description: 'Classic board game', createdAt: '2024-01-15T10:00:00Z' },
    { id: 'checkers', name: 'Checkers', description: null, createdAt: '2024-01-16T12:00:00Z' }
  ];

  it('renders page with game list for authenticated editor', async () => {
    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const BulkExportPage = loadBulkExportPage();

    fetchMock
      .mockResolvedValueOnce(createJsonResponse(mockAuthResponse))
      .mockResolvedValueOnce(createJsonResponse(mockGamesResponse));

    renderWithQuery(<BulkExportPage />);

    await waitFor(() => {
      expect(screen.getByText('Bulk Export Rule Specs')).toBeInTheDocument();
    });

    expect(screen.getByText('Chess')).toBeInTheDocument();
    expect(screen.getByText('Checkers')).toBeInTheDocument();
    expect(screen.getByText('0 of 2 selected')).toBeInTheDocument();
  });

  it('allows selecting games and updates counter', async () => {
    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const BulkExportPage = loadBulkExportPage();

    fetchMock
      .mockResolvedValueOnce(createJsonResponse(mockAuthResponse))
      .mockResolvedValueOnce(createJsonResponse(mockGamesResponse));

    const user = userEvent.setup();
    renderWithQuery(<BulkExportPage />);

    // Wait for games to load
    await waitFor(() => {
      expect(screen.getByText('Chess')).toBeInTheDocument();
    });

    // Initial state: no selection
    expect(screen.getByText('0 of 2 selected')).toBeInTheDocument();

    // Select Chess game
    const chessCheckbox = screen.getAllByRole('checkbox')[1]; // First is "Select All"
    await user.click(chessCheckbox);

    // Verify selection counter updated
    expect(screen.getByText('1 of 2 selected')).toBeInTheDocument();

    // Select Checkers game
    const checkersCheckbox = screen.getAllByRole('checkbox')[2];
    await user.click(checkersCheckbox);

    // Verify selection counter updated
    expect(screen.getByText('2 of 2 selected')).toBeInTheDocument();
  });

  it('allows selecting all games', async () => {
    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const BulkExportPage = loadBulkExportPage();

    fetchMock
      .mockResolvedValueOnce(createJsonResponse(mockAuthResponse))
      .mockResolvedValueOnce(createJsonResponse(mockGamesResponse));

    const user = userEvent.setup();
    renderWithQuery(<BulkExportPage />);

    await waitFor(() => {
      expect(screen.getByText('Chess')).toBeInTheDocument();
    });

    // Click "Select All"
    const selectAllCheckbox = screen.getByLabelText('Select All');
    await user.click(selectAllCheckbox);

    // Verify all games selected
    expect(screen.getByText('2 of 2 selected')).toBeInTheDocument();
  });

  it('denies access for non-editor/non-admin users', async () => {
    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const BulkExportPage = loadBulkExportPage();

    const mockUserAuthResponse = {
      user: {
        id: 'test-user-id',
        email: 'user@test.com',
        displayName: 'Test User',
        role: 'User'
      },
      expiresAt: new Date(Date.now() + 3600000).toISOString()
    };

    fetchMock
      .mockResolvedValueOnce(createJsonResponse(mockUserAuthResponse))
      .mockResolvedValueOnce(createJsonResponse(mockGamesResponse));

    renderWithQuery(<BulkExportPage />);

    await waitFor(() => {
      expect(screen.getByText('Access Denied')).toBeInTheDocument();
    });

    expect(screen.getByText('Editor or Admin role required.')).toBeInTheDocument();
  });

  it('shows error when export fails', async () => {
    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const BulkExportPage = loadBulkExportPage();

    fetchMock
      .mockResolvedValueOnce(createJsonResponse(mockAuthResponse))
      .mockResolvedValueOnce(createJsonResponse(mockGamesResponse))
      .mockResolvedValueOnce(createJsonResponse({ error: 'Export failed' }, false, 500));

    const user = userEvent.setup();
    renderWithQuery(<BulkExportPage />);

    await waitFor(() => {
      expect(screen.getByText('Chess')).toBeInTheDocument();
    });

    // Select Chess
    const chessCheckbox = screen.getAllByRole('checkbox')[1];
    await user.click(chessCheckbox);

    // Try to export
    const exportButton = screen.getByRole('button', { name: /Export.*Rule Spec/i });
    await user.click(exportButton);

    // Verify error displayed
    await waitFor(() => {
      expect(screen.getByText(/Export failed/i)).toBeInTheDocument();
    });
  });

  it('disables export button when no games selected', async () => {
    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const BulkExportPage = loadBulkExportPage();

    fetchMock
      .mockResolvedValueOnce(createJsonResponse(mockAuthResponse))
      .mockResolvedValueOnce(createJsonResponse(mockGamesResponse));

    renderWithQuery(<BulkExportPage />);

    await waitFor(() => {
      expect(screen.getByText('Chess')).toBeInTheDocument();
    });

    // Verify export button is disabled when no selection
    const exportButton = screen.getByRole('button', { name: /Export.*Rule Spec/i });
    expect(exportButton).toBeDisabled();
  });
});
