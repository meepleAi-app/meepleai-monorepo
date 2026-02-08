/**
 * AuditLogClient Tests
 * Issue #3691 - Audit Log System
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect, beforeEach } from 'vitest';

import { AuditLogClient } from '../client';

const mockEntries = [
  {
    id: '00000000-0000-0000-0000-000000000001',
    adminUserId: '00000000-0000-0000-0000-000000000099',
    action: 'UserImpersonate',
    resource: 'User',
    resourceId: '00000000-0000-0000-0000-000000000050',
    result: 'Success',
    details: '{"targetUserId":"user-123"}',
    ipAddress: '192.168.1.1',
    createdAt: '2026-02-01T12:00:00Z',
  },
  {
    id: '00000000-0000-0000-0000-000000000002',
    adminUserId: null,
    action: 'CacheClear',
    resource: 'Cache',
    resourceId: null,
    result: 'Error',
    details: null,
    ipAddress: null,
    createdAt: '2026-02-01T11:00:00Z',
  },
];

const mockGetAuditLogs = vi.fn();
const mockExportAuditLogs = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getAuditLogs: (...args: unknown[]) => mockGetAuditLogs(...args),
      exportAuditLogs: (...args: unknown[]) => mockExportAuditLogs(...args),
    },
  },
}));

describe('AuditLogClient', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetAuditLogs.mockResolvedValue({
      entries: mockEntries,
      totalCount: 2,
      limit: 25,
      offset: 0,
    });
    mockExportAuditLogs.mockResolvedValue(new Blob(['csv'], { type: 'text/csv' }));
  });

  it('renders header with title', async () => {
    render(<AuditLogClient />);

    expect(screen.getByText('Audit Log')).toBeInTheDocument();
  });

  it('fetches and displays audit log entries', async () => {
    render(<AuditLogClient />);

    await waitFor(() => {
      expect(screen.getByText('UserImpersonate')).toBeInTheDocument();
      expect(screen.getByText('CacheClear')).toBeInTheDocument();
    });
  });

  it('displays entry count in description', async () => {
    render(<AuditLogClient />);

    await waitFor(() => {
      expect(screen.getByText(/2 entries/)).toBeInTheDocument();
    });
  });

  it('renders refresh button', async () => {
    render(<AuditLogClient />);

    expect(screen.getByText('Refresh')).toBeInTheDocument();
  });

  it('renders export button', async () => {
    render(<AuditLogClient />);

    expect(screen.getByText('Export CSV')).toBeInTheDocument();
  });

  it('renders filters button', async () => {
    render(<AuditLogClient />);

    expect(screen.getByText('Filters')).toBeInTheDocument();
  });

  it('toggles filter panel on click', async () => {
    const user = userEvent.setup();
    render(<AuditLogClient />);

    const filtersBtn = screen.getByText('Filters');
    await user.click(filtersBtn);

    // Filter panel should show option selectors and date inputs
    expect(screen.getByText('All Actions')).toBeInTheDocument();
    expect(screen.getByText('All Results')).toBeInTheDocument();
    expect(screen.getByText('From')).toBeInTheDocument();
    expect(screen.getByText('To')).toBeInTheDocument();
  });

  it('shows result badges with correct styling', async () => {
    render(<AuditLogClient />);

    await waitFor(() => {
      const successBadge = screen.getByText('Success');
      expect(successBadge).toBeInTheDocument();

      const errorBadge = screen.getByText('Error');
      expect(errorBadge).toBeInTheDocument();
    });
  });

  it('shows detail modal on row click', async () => {
    const user = userEvent.setup();
    render(<AuditLogClient />);

    await waitFor(() => {
      expect(screen.getByText('UserImpersonate')).toBeInTheDocument();
    });

    // Click on a row
    const row = screen.getByText('UserImpersonate').closest('tr');
    if (row) {
      await user.click(row);
    }

    await waitFor(() => {
      expect(screen.getByText('Audit Log Detail')).toBeInTheDocument();
    });
  });

  it('shows error state on fetch failure', async () => {
    mockGetAuditLogs.mockRejectedValue(new Error('Network error'));
    render(<AuditLogClient />);

    await waitFor(() => {
      expect(screen.getByText('Network error')).toBeInTheDocument();
    });
  });

  it('shows empty state when no entries', async () => {
    mockGetAuditLogs.mockResolvedValue({
      entries: [],
      totalCount: 0,
      limit: 25,
      offset: 0,
    });

    render(<AuditLogClient />);

    await waitFor(() => {
      expect(screen.getByText('No audit log entries found')).toBeInTheDocument();
    });
  });

  it('calls refresh on button click', async () => {
    const user = userEvent.setup();
    render(<AuditLogClient />);

    await waitFor(() => {
      expect(mockGetAuditLogs).toHaveBeenCalledTimes(1);
    });

    await user.click(screen.getByText('Refresh'));

    await waitFor(() => {
      expect(mockGetAuditLogs).toHaveBeenCalledTimes(2);
    });
  });

  it('renders table headers', async () => {
    render(<AuditLogClient />);

    expect(screen.getByText('Timestamp')).toBeInTheDocument();
    expect(screen.getByText('Action')).toBeInTheDocument();
    expect(screen.getByText('Resource')).toBeInTheDocument();
    expect(screen.getByText('Result')).toBeInTheDocument();
    expect(screen.getByText('Admin')).toBeInTheDocument();
    expect(screen.getByText('IP')).toBeInTheDocument();
  });
});
