import { render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

const mockGetApiKeysWithStats = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getApiKeysWithStats: (...args: unknown[]) => mockGetApiKeysWithStats(...args),
    },
  },
}));

import { ApiKeysTab } from '../ApiKeysTab';

describe('ApiKeysTab', () => {
  beforeEach(() => {
    mockGetApiKeysWithStats.mockResolvedValue({
      keys: [
        {
          apiKey: {
            id: '00000000-0000-0000-0000-000000000001',
            keyName: 'Test Key',
            keyPrefix: 'mk_test',
            scopes: 'read',
            createdAt: '2026-01-15T08:00:00Z',
            expiresAt: null,
            lastUsedAt: '2026-03-01T12:00:00Z',
            isActive: true,
          },
          usageStats: {
            keyId: '00000000-0000-0000-0000-000000000001',
            totalUsageCount: 1500,
            lastUsedAt: '2026-03-01T12:00:00Z',
            usageCountLast24Hours: 10,
            usageCountLast7Days: 80,
            usageCountLast30Days: 350,
            averageRequestsPerDay: 50,
          },
        },
      ],
      count: 1,
      filters: { userId: null, includeRevoked: false },
    });
  });

  it('renders heading after loading', async () => {
    render(<ApiKeysTab />);
    await waitFor(() => {
      expect(screen.getByText('API Keys')).toBeInTheDocument();
    });
  });

  it('renders export button', async () => {
    render(<ApiKeysTab />);
    await waitFor(() => {
      expect(screen.getByText('Export CSV')).toBeInTheDocument();
    });
  });

  it('renders api key in table', async () => {
    render(<ApiKeysTab />);
    await waitFor(() => {
      expect(screen.getByText('Test Key')).toBeInTheDocument();
    });
  });

  it('shows empty state when no keys', async () => {
    mockGetApiKeysWithStats.mockResolvedValue({
      keys: [],
      count: 0,
      filters: { userId: null, includeRevoked: false },
    });
    render(<ApiKeysTab />);
    await waitFor(() => {
      expect(screen.getByText('No API keys found.')).toBeInTheDocument();
    });
  });
});
