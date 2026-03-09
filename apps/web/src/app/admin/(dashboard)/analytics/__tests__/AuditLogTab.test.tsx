import { render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

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

import { AuditLogTab } from '../AuditLogTab';

describe('AuditLogTab', () => {
  beforeEach(() => {
    mockGetAuditLogs.mockResolvedValue({
      entries: [
        {
          id: '00000000-0000-0000-0000-000000000001',
          adminUserId: null,
          action: 'UserLogin',
          resource: 'Auth',
          result: 'Success',
          details: 'User logged in',
          createdAt: '2026-03-01T10:00:00Z',
        },
      ],
      totalCount: 1,
      limit: 50,
      offset: 0,
    });
    mockExportAuditLogs.mockResolvedValue(new Blob(['csv']));
  });

  it('renders heading after loading', async () => {
    render(<AuditLogTab />);
    await waitFor(() => {
      expect(screen.getByText('Audit Log')).toBeInTheDocument();
    });
  });

  it('renders export button', async () => {
    render(<AuditLogTab />);
    await waitFor(() => {
      expect(screen.getByText('Export CSV')).toBeInTheDocument();
    });
  });

  it('renders audit log entries in table', async () => {
    render(<AuditLogTab />);
    await waitFor(() => {
      expect(screen.getByText('UserLogin')).toBeInTheDocument();
      expect(screen.getByText('Auth')).toBeInTheDocument();
    });
  });

  it('shows empty state when no entries', async () => {
    mockGetAuditLogs.mockResolvedValue({
      entries: [],
      totalCount: 0,
      limit: 50,
      offset: 0,
    });
    render(<AuditLogTab />);
    await waitFor(() => {
      expect(screen.getByText('No audit log entries found.')).toBeInTheDocument();
    });
  });
});
