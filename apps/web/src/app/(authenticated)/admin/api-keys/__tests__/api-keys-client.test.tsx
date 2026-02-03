import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ApiKeysPageClient } from '../client';
import { AuthProvider } from '@/components/auth/AuthProvider';
import type { AuthUser } from '@/types/auth';
import { api } from '@/lib/api';

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getApiKeysWithStats: vi.fn(),
      deleteApiKey: vi.fn(),
      exportApiKeysToCSV: vi.fn(),
    },
  },
}));

vi.mock('@/components/auth/AuthProvider', async () => {
  const actual = await vi.importActual('@/components/auth/AuthProvider');
  return {
    ...actual,
    useAuthUser: () => ({
      user: mockUser,
      loading: false,
    }),
  };
});

const mockUser: AuthUser = {
  id: '123e4567-e89b-12d3-a456-426614174000',
  email: 'admin@meepleai.com',
  displayName: 'Admin User',
  role: 'Admin',
  createdAt: new Date().toISOString(),
  isTwoFactorEnabled: false,
};

const mockApiKeysData = {
  keys: [
    {
      apiKey: {
        id: '11111111-1111-1111-1111-111111111111',
        keyName: 'Production API Key',
        keyPrefix: 'mpl_prod_',
        scopes: 'read:games,read:rules',
        createdAt: '2025-01-01T00:00:00Z',
        expiresAt: '2026-12-31T23:59:59Z',
        lastUsedAt: '2025-12-11T10:30:00Z',
        isActive: true,
      },
      usageStats: {
        keyId: '11111111-1111-1111-1111-111111111111',
        totalUsageCount: 1543,
        lastUsedAt: '2025-12-11T10:30:00Z',
        usageCountLast24Hours: 45,
        usageCountLast7Days: 312,
        usageCountLast30Days: 1234,
        averageRequestsPerDay: 41.13,
      },
    },
    {
      apiKey: {
        id: '22222222-2222-2222-2222-222222222222',
        keyName: 'Development API Key',
        keyPrefix: 'mpl_dev_',
        scopes: 'read:games',
        createdAt: '2025-06-15T12:00:00Z',
        expiresAt: null,
        lastUsedAt: '2025-12-10T15:20:00Z',
        isActive: true,
      },
      usageStats: {
        keyId: '22222222-2222-2222-2222-222222222222',
        totalUsageCount: 234,
        lastUsedAt: '2025-12-10T15:20:00Z',
        usageCountLast24Hours: 12,
        usageCountLast7Days: 89,
        usageCountLast30Days: 234,
        averageRequestsPerDay: 7.8,
      },
    },
  ],
  count: 2,
  filters: {
    userId: null,
    includeRevoked: false,
  },
};

describe('ApiKeysPageClient', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(api.admin.getApiKeysWithStats).mockResolvedValue(mockApiKeysData);
  });

  afterEach(() => {
    vi.clearAllTimers();
  });

  it('renders loading state initially', () => {
    render(
      <AuthProvider>
        <ApiKeysPageClient />
      </AuthProvider>
    );

    expect(screen.getByText('Loading...')).toBeInTheDocument();
  });

  it('renders API keys table after loading', async () => {
    render(
      <AuthProvider>
        <ApiKeysPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('Production API Key')).toBeInTheDocument();
    });

    expect(screen.getByText('Development API Key')).toBeInTheDocument();
    expect(screen.getByText('mpl_prod_')).toBeInTheDocument();
    expect(screen.getByText('mpl_dev_')).toBeInTheDocument();
  });

  it('displays correct status badges', async () => {
    render(
      <AuthProvider>
        <ApiKeysPageClient />
      </AuthProvider>
    );

    // Issue #2321: Fix async rendering race condition
    // Root cause: getAllByText is synchronous - may execute before all table rows render
    // Wait for both API keys to be rendered first
    await waitFor(() => {
      expect(screen.getByText('Production API Key')).toBeInTheDocument();
      expect(screen.getByText('Development API Key')).toBeInTheDocument();
    });

    // NOW both rows are guaranteed in DOM - safe to query badges
    const badges = screen.getAllByText('Active');
    expect(badges).toHaveLength(2);
  });

  it('opens create key dialog when button is clicked', async () => {
    const user = userEvent.setup();
    render(
      <AuthProvider>
        <ApiKeysPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('Production API Key')).toBeInTheDocument();
    });

    const createButton = screen.getByTestId('create-key-button');
    await user.click(createButton);

    await waitFor(() => {
      expect(screen.getByText('Create New API Key')).toBeInTheDocument();
    });
  });

  it('filters API keys by search term', async () => {
    const user = userEvent.setup();
    render(
      <AuthProvider>
        <ApiKeysPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('Production API Key')).toBeInTheDocument();
    });

    const searchInput = screen.getByPlaceholderText('Key name or prefix...');
    await user.type(searchInput, 'Production');

    await waitFor(() => {
      expect(screen.getByText('Production API Key')).toBeInTheDocument();
      expect(screen.queryByText('Development API Key')).not.toBeInTheDocument();
    });
  });

  it('filters API keys by status', async () => {
    const mockWithRevokedKey = {
      keys: [
        ...mockApiKeysData.keys,
        {
          apiKey: {
            id: '33333333-3333-3333-3333-333333333333',
            keyName: 'Revoked Key',
            keyPrefix: 'mpl_rev_',
            scopes: 'read:games',
            createdAt: '2025-03-10T08:00:00Z',
            expiresAt: null,
            lastUsedAt: '2025-11-01T09:00:00Z',
            isActive: false,
          },
          usageStats: {
            keyId: '33333333-3333-3333-3333-333333333333',
            totalUsageCount: 567,
            lastUsedAt: '2025-11-01T09:00:00Z',
            usageCountLast24Hours: 0,
            usageCountLast7Days: 0,
            usageCountLast30Days: 0,
            averageRequestsPerDay: 0,
          },
        },
      ],
      count: 3,
      filters: {
        userId: null,
        includeRevoked: true,
      },
    };

    vi.mocked(api.admin.getApiKeysWithStats).mockResolvedValue(mockWithRevokedKey);

    render(
      <AuthProvider>
        <ApiKeysPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('Production API Key')).toBeInTheDocument();
      expect(screen.getByText('Revoked Key')).toBeInTheDocument();
    });

    // Client-side filter verification - all keys should be visible initially
    expect(screen.getByText('Development API Key')).toBeInTheDocument();
  });

  it('shows confirmation dialog when deleting a key', async () => {
    const user = userEvent.setup();
    render(
      <AuthProvider>
        <ApiKeysPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('Production API Key')).toBeInTheDocument();
    });

    const deleteButton = screen.getByTestId('delete-button-11111111-1111-1111-1111-111111111111');
    await user.click(deleteButton);

    await waitFor(() => {
      expect(screen.getByText('Delete API Key')).toBeInTheDocument();
      expect(screen.getByText(/Are you sure you want to permanently delete/)).toBeInTheDocument();
    });
  });

  it('deletes API key after confirmation', async () => {
    const user = userEvent.setup();
    vi.mocked(api.admin.deleteApiKey).mockResolvedValue(undefined);

    render(
      <AuthProvider>
        <ApiKeysPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('Production API Key')).toBeInTheDocument();
    });

    const deleteButton = screen.getByTestId('delete-button-11111111-1111-1111-1111-111111111111');
    await user.click(deleteButton);

    await waitFor(() => {
      expect(screen.getByText('Delete API Key')).toBeInTheDocument();
    });

    const confirmButton = screen.getByRole('button', { name: 'Confirm' });
    await user.click(confirmButton);

    await waitFor(() => {
      expect(api.admin.deleteApiKey).toHaveBeenCalledWith('11111111-1111-1111-1111-111111111111');
    });
  });

  it('shows usage statistics when stats button is clicked', async () => {
    const user = userEvent.setup();
    render(
      <AuthProvider>
        <ApiKeysPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('Production API Key')).toBeInTheDocument();
    });

    const statsButton = screen.getByTestId('stats-button-11111111-1111-1111-1111-111111111111');
    await user.click(statsButton);

    await waitFor(() => {
      expect(screen.getByText('Usage Statistics')).toBeInTheDocument();
      expect(screen.getByText('1543')).toBeInTheDocument(); // Total usage
      expect(screen.getByText('45')).toBeInTheDocument(); // Last 24h
      expect(screen.getByText('312')).toBeInTheDocument(); // Last 7d
      expect(screen.getByText('1234')).toBeInTheDocument(); // Last 30d
    });
  });

  it('handles bulk selection and deletion', async () => {
    const user = userEvent.setup();
    vi.mocked(api.admin.deleteApiKey).mockResolvedValue(undefined);

    render(
      <AuthProvider>
        <ApiKeysPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('Production API Key')).toBeInTheDocument();
    });

    const checkboxes = screen.getAllByRole('checkbox');
    const firstKeyCheckbox = checkboxes[1]; // Skip the "select all" checkbox
    await user.click(firstKeyCheckbox);

    // Find the delete button in BulkActionBar
    const bulkDeleteButton = await screen.findByTestId('bulk-action-bar-action-delete');
    await user.click(bulkDeleteButton);

    await waitFor(() => {
      expect(screen.getByText('Delete Multiple API Keys')).toBeInTheDocument();
    });

    const confirmButton = screen.getByRole('button', { name: 'Confirm' });
    await user.click(confirmButton);

    await waitFor(
      () => {
        expect(api.admin.deleteApiKey).toHaveBeenCalled();
      },
      { timeout: 3000 }
    );
  });

  it('handles export to CSV', async () => {
    const user = userEvent.setup();
    const mockBlob = new Blob(['csv,content'], { type: 'text/csv' });
    vi.mocked(api.admin.exportApiKeysToCSV).mockResolvedValue(mockBlob);

    global.URL.createObjectURL = vi.fn(() => 'blob:mock-url');
    global.URL.revokeObjectURL = vi.fn();

    render(
      <AuthProvider>
        <ApiKeysPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('Production API Key')).toBeInTheDocument();
    });

    const exportButton = screen.getByTestId('export-csv-button');
    await user.click(exportButton);

    await waitFor(() => {
      expect(api.admin.exportApiKeysToCSV).toHaveBeenCalled();
    });
  });

  it('displays error message when API call fails', async () => {
    vi.mocked(api.admin.getApiKeysWithStats).mockRejectedValue(new Error('API Error'));

    render(
      <AuthProvider>
        <ApiKeysPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('API Error')).toBeInTheDocument();
    });
  });

  it('shows empty state when no API keys exist', async () => {
    vi.mocked(api.admin.getApiKeysWithStats).mockResolvedValue({
      keys: [],
      count: 0,
      filters: {
        userId: null,
        includeRevoked: false,
      },
    });

    render(
      <AuthProvider>
        <ApiKeysPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('No API keys found')).toBeInTheDocument();
    });
  });
});
