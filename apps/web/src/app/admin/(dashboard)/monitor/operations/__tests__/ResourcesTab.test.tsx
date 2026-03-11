import { vi, describe, it, expect, beforeEach } from 'vitest';

const mockGetResourceDatabaseMetrics = vi.hoisted(() => vi.fn());
const mockGetResourceCacheMetrics = vi.hoisted(() => vi.fn());
const mockGetResourceVectorMetrics = vi.hoisted(() => vi.fn());
const mockGetResourceDatabaseTopTables = vi.hoisted(() => vi.fn());
const mockClearCache = vi.hoisted(() => vi.fn());
const mockVacuumDatabase = vi.hoisted(() => vi.fn());
const mockRebuildVectors = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getResourceDatabaseMetrics: mockGetResourceDatabaseMetrics,
      getResourceCacheMetrics: mockGetResourceCacheMetrics,
      getResourceVectorMetrics: mockGetResourceVectorMetrics,
      getResourceDatabaseTopTables: mockGetResourceDatabaseTopTables,
      clearCache: mockClearCache,
      vacuumDatabase: mockVacuumDatabase,
      rebuildVectors: mockRebuildVectors,
    },
  },
}));

const mockToast = vi.hoisted(() => vi.fn());
vi.mock('@/hooks/use-toast', () => ({
  useToast: () => ({ toast: mockToast }),
}));

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { ResourcesTab } from '../ResourcesTab';

// ---------- Mock Data ----------

const MOCK_DB = {
  sizeBytes: 1200000000,
  sizeFormatted: '1.2 GB',
  growthLast7Days: 2.5,
  growthLast30Days: 8.0,
  growthLast90Days: 15.0,
  activeConnections: 15,
  maxConnections: 100,
  transactionsCommitted: 5432,
  transactionsRolledBack: 12,
  measuredAt: '2026-03-01T10:00:00Z',
};

const MOCK_CACHE = {
  usedMemoryBytes: 268435456,
  usedMemoryFormatted: '256 MB',
  maxMemoryBytes: 536870912,
  maxMemoryFormatted: '512 MB',
  memoryUsagePercent: 65.2,
  totalKeys: 5432,
  keyspaceHits: 100000,
  keyspaceMisses: 5000,
  hitRate: 0.95,
  evictedKeys: 12,
  expiredKeys: 100,
  measuredAt: '2026-03-01T10:00:00Z',
};

const MOCK_VECTORS = {
  totalCollections: 3,
  totalVectors: 15000,
  indexedVectors: 14800,
  memoryBytes: 891289600,
  memoryFormatted: '850 MB',
  collections: [],
  measuredAt: '2026-03-01T10:00:00Z',
};

const MOCK_TABLES = [
  {
    tableName: 'games',
    sizeBytes: 4000000,
    sizeFormatted: '4 MB',
    rowCount: 1500,
    indexSizeBytes: 1000000,
    indexSizeFormatted: '1 MB',
    totalSizeBytes: 5000000,
    totalSizeFormatted: '5 MB',
  },
  {
    tableName: 'users',
    sizeBytes: 1500000,
    sizeFormatted: '1.5 MB',
    rowCount: 800,
    indexSizeBytes: 500000,
    indexSizeFormatted: '500 KB',
    totalSizeBytes: 2000000,
    totalSizeFormatted: '2 MB',
  },
];

function setupMocks() {
  mockGetResourceDatabaseMetrics.mockResolvedValue(MOCK_DB);
  mockGetResourceCacheMetrics.mockResolvedValue(MOCK_CACHE);
  mockGetResourceVectorMetrics.mockResolvedValue(MOCK_VECTORS);
  mockGetResourceDatabaseTopTables.mockResolvedValue(MOCK_TABLES);
  mockClearCache.mockResolvedValue(undefined);
  mockVacuumDatabase.mockResolvedValue(undefined);
  mockRebuildVectors.mockResolvedValue(undefined);
}

describe('ResourcesTab', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers({ shouldAdvanceTime: true });
    setupMocks();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('renders heading "Resources"', async () => {
    render(<ResourcesTab />);

    await waitFor(() => {
      expect(screen.getByText('Resources')).toBeInTheDocument();
    });
  });

  it('shows loading spinners initially', () => {
    // Make the API calls hang forever so loading state persists
    mockGetResourceDatabaseMetrics.mockReturnValue(new Promise(() => {}));
    mockGetResourceCacheMetrics.mockReturnValue(new Promise(() => {}));
    mockGetResourceVectorMetrics.mockReturnValue(new Promise(() => {}));
    mockGetResourceDatabaseTopTables.mockReturnValue(new Promise(() => {}));

    render(<ResourcesTab />);

    expect(screen.getByTestId('resources-tab')).toBeInTheDocument();
    // MetricCards show a spinner when loading
    expect(screen.getByText('Database')).toBeInTheDocument();
    expect(screen.getByText('Cache (Redis)')).toBeInTheDocument();
    expect(screen.getByText('Vector Store (Qdrant)')).toBeInTheDocument();
  });

  it('renders database metric card with data', async () => {
    render(<ResourcesTab />);

    await waitFor(() => {
      expect(screen.getByText('1.2 GB')).toBeInTheDocument();
    });

    expect(screen.getByText('15/100')).toBeInTheDocument();
    expect(screen.getByText('+2.5%')).toBeInTheDocument();
  });

  it('renders cache metric card with data', async () => {
    render(<ResourcesTab />);

    await waitFor(() => {
      expect(screen.getByText('256 MB / 512 MB')).toBeInTheDocument();
    });

    expect(screen.getByText('65.2%')).toBeInTheDocument();
    expect(screen.getByText('95.0%')).toBeInTheDocument();
  });

  it('renders vector metric card with data', async () => {
    render(<ResourcesTab />);

    await waitFor(() => {
      expect(screen.getByText('850 MB')).toBeInTheDocument();
    });

    expect(screen.getByText('3')).toBeInTheDocument();
  });

  it('renders top tables DataTable with table data', async () => {
    render(<ResourcesTab />);

    await waitFor(() => {
      expect(screen.getByText('Top Tables by Size')).toBeInTheDocument();
    });

    expect(screen.getByText('games')).toBeInTheDocument();
    expect(screen.getByText('users')).toBeInTheDocument();
    // Size column uses row.original.sizeFormatted
    expect(screen.getByText('4 MB')).toBeInTheDocument();
    expect(screen.getByText('1.5 MB')).toBeInTheDocument();
    // Index column uses row.original.indexSizeFormatted
    expect(screen.getByText('1 MB')).toBeInTheDocument();
    expect(screen.getByText('500 KB')).toBeInTheDocument();
  });

  it('Clear Cache button opens Level 1 confirmation dialog', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<ResourcesTab />);

    await waitFor(() => {
      expect(screen.getByTestId('clear-cache-button')).toBeInTheDocument();
    });

    await user.click(screen.getByTestId('clear-cache-button'));

    await waitFor(() => {
      // The dialog message confirms it opened
      expect(
        screen.getByText(/This will clear all cached data from Redis/)
      ).toBeInTheDocument();
    });
  });

  it('Standard Vacuum button opens Level 2 confirmation dialog', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<ResourcesTab />);

    await waitFor(() => {
      expect(screen.getByTestId('vacuum-db-button')).toBeInTheDocument();
    });

    await user.click(screen.getByTestId('vacuum-db-button'));

    await waitFor(() => {
      expect(
        screen.getByText(/Standard VACUUM reclaims storage/)
      ).toBeInTheDocument();
      // Level 2 requires typing CONFIRM
      expect(screen.getByLabelText(/Type CONFIRM to proceed/i)).toBeInTheDocument();
    });
  });

  it('Full Vacuum button opens Level 2 confirmation with lock warning', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<ResourcesTab />);

    await waitFor(() => {
      expect(screen.getByTestId('full-vacuum-db-button')).toBeInTheDocument();
    });

    await user.click(screen.getByTestId('full-vacuum-db-button'));

    await waitFor(() => {
      expect(
        screen.getByText(/This will briefly lock the database/)
      ).toBeInTheDocument();
    });
  });

  it('Rebuild Vectors button opens Level 2 confirmation dialog', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<ResourcesTab />);

    await waitFor(() => {
      expect(screen.getByTestId('rebuild-vectors-button')).toBeInTheDocument();
    });

    await user.click(screen.getByTestId('rebuild-vectors-button'));

    await waitFor(() => {
      expect(
        screen.getByText(/Existing indices will be temporarily unavailable/)
      ).toBeInTheDocument();
    });
  });

  it('shows error toast when API calls fail', async () => {
    mockGetResourceDatabaseMetrics.mockRejectedValue(new Error('Network error'));
    mockGetResourceCacheMetrics.mockRejectedValue(new Error('Network error'));
    mockGetResourceVectorMetrics.mockRejectedValue(new Error('Network error'));
    mockGetResourceDatabaseTopTables.mockRejectedValue(new Error('Network error'));

    render(<ResourcesTab />);

    await waitFor(() => {
      expect(mockToast).toHaveBeenCalledWith(
        expect.objectContaining({
          title: 'Failed to load resource metrics',
          variant: 'destructive',
        })
      );
    });
  });

  it('calls clearCache and shows success toast when confirmed', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<ResourcesTab />);

    await waitFor(() => {
      expect(screen.getByTestId('clear-cache-button')).toBeInTheDocument();
    });

    // Open clear cache dialog
    await user.click(screen.getByTestId('clear-cache-button'));

    // Level 1 dialog has a "Conferma" button
    await waitFor(() => {
      expect(screen.getByRole('button', { name: /conferma/i })).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /conferma/i }));

    await waitFor(() => {
      expect(mockClearCache).toHaveBeenCalledOnce();
    });

    expect(mockToast).toHaveBeenCalledWith(
      expect.objectContaining({ title: 'Cache cleared successfully' })
    );
  });

  it('shows error toast when clear cache fails', async () => {
    mockClearCache.mockRejectedValue(new Error('Failed'));
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<ResourcesTab />);

    await waitFor(() => {
      expect(screen.getByTestId('clear-cache-button')).toBeInTheDocument();
    });

    await user.click(screen.getByTestId('clear-cache-button'));

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /conferma/i })).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /conferma/i }));

    await waitFor(() => {
      expect(mockToast).toHaveBeenCalledWith(
        expect.objectContaining({
          title: 'Failed to clear cache',
          variant: 'destructive',
        })
      );
    });
  });

  it('renders all four action buttons', async () => {
    render(<ResourcesTab />);

    await waitFor(() => {
      expect(screen.getByTestId('clear-cache-button')).toBeInTheDocument();
    });

    expect(screen.getByTestId('vacuum-db-button')).toBeInTheDocument();
    expect(screen.getByTestId('full-vacuum-db-button')).toBeInTheDocument();
    expect(screen.getByTestId('rebuild-vectors-button')).toBeInTheDocument();
  });
});
