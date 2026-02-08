/**
 * AuditLogWidget Tests
 * Issue #3691 - Audit Log System
 */

import { render, screen, waitFor } from '@testing-library/react';
import { vi, describe, it, expect, beforeEach } from 'vitest';

import { AuditLogWidget } from '../AuditLogWidget';

const mockEntries = [
  {
    id: '00000000-0000-0000-0000-000000000001',
    adminUserId: '00000000-0000-0000-0000-000000000099',
    action: 'UserImpersonate',
    resource: 'User',
    resourceId: null,
    result: 'Success',
    details: null,
    ipAddress: '127.0.0.1',
    createdAt: new Date().toISOString(),
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
    createdAt: new Date(Date.now() - 3600000).toISOString(),
  },
  {
    id: '00000000-0000-0000-0000-000000000003',
    adminUserId: null,
    action: 'FeatureFlagChange',
    resource: 'FeatureFlag',
    resourceId: null,
    result: 'Denied',
    details: null,
    ipAddress: null,
    createdAt: new Date(Date.now() - 86400000).toISOString(),
  },
];

const mockGetAuditLogs = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getAuditLogs: (...args: unknown[]) => mockGetAuditLogs(...args),
    },
  },
}));

vi.mock('next/navigation', () => ({
  usePathname: () => '/admin/overview',
}));

describe('AuditLogWidget', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetAuditLogs.mockResolvedValue({
      entries: mockEntries,
      totalCount: 3,
      limit: 5,
      offset: 0,
    });
  });

  it('renders recent audit log entries', async () => {
    render(<AuditLogWidget />);

    await waitFor(() => {
      expect(screen.getByText('UserImpersonate')).toBeInTheDocument();
      expect(screen.getByText('CacheClear')).toBeInTheDocument();
      expect(screen.getByText('FeatureFlagChange')).toBeInTheDocument();
    });
  });

  it('fetches audit logs with limit of 5', async () => {
    render(<AuditLogWidget />);

    await waitFor(() => {
      expect(mockGetAuditLogs).toHaveBeenCalledWith({ limit: 5, offset: 0 });
    });
  });

  it('renders audit log link button', async () => {
    render(<AuditLogWidget />);

    const link = screen.getByTestId('enterprise-audit-log-btn');
    expect(link).toBeInTheDocument();
    expect(link).toHaveAttribute('href', '/admin/audit-log');
  });

  it('shows "Audit Log" text in expanded mode', async () => {
    render(<AuditLogWidget />);

    expect(screen.getByText('Audit Log')).toBeInTheDocument();
  });

  it('renders only icon link when collapsed', async () => {
    render(<AuditLogWidget collapsed />);

    const link = screen.getByTestId('enterprise-audit-log-btn');
    expect(link).toBeInTheDocument();
    // Should NOT show entries in collapsed mode
    expect(screen.queryByTestId('audit-log-widget')).not.toBeInTheDocument();
  });

  it('shows loading skeleton initially', () => {
    mockGetAuditLogs.mockReturnValue(new Promise(() => {})); // never resolves
    render(<AuditLogWidget />);

    // Should show skeleton placeholders
    const widget = screen.getByTestId('audit-log-widget');
    expect(widget).toBeInTheDocument();
  });

  it('shows "No recent actions" when empty', async () => {
    mockGetAuditLogs.mockResolvedValue({
      entries: [],
      totalCount: 0,
      limit: 5,
      offset: 0,
    });

    render(<AuditLogWidget />);

    await waitFor(() => {
      expect(screen.getByText('No recent actions')).toBeInTheDocument();
    });
  });

  it('displays relative timestamps', async () => {
    render(<AuditLogWidget />);

    await waitFor(() => {
      expect(screen.getByText('just now')).toBeInTheDocument();
      expect(screen.getByText('1h ago')).toBeInTheDocument();
      expect(screen.getByText('1d ago')).toBeInTheDocument();
    });
  });

  it('handles API error gracefully (silent fail)', async () => {
    mockGetAuditLogs.mockRejectedValue(new Error('Network error'));

    render(<AuditLogWidget />);

    // Should not crash, should show empty state
    await waitFor(() => {
      expect(screen.queryByText('Error')).not.toBeInTheDocument();
    });
  });

  it('renders entry test IDs for each entry', async () => {
    render(<AuditLogWidget />);

    await waitFor(() => {
      expect(
        screen.getByTestId('audit-widget-entry-00000000-0000-0000-0000-000000000001')
      ).toBeInTheDocument();
      expect(
        screen.getByTestId('audit-widget-entry-00000000-0000-0000-0000-000000000002')
      ).toBeInTheDocument();
    });
  });
});
