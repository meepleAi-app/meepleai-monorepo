import { vi, describe, it, expect, beforeEach, afterEach } from 'vitest';

const mockGetAuditLogs = vi.hoisted(() => vi.fn());
const mockExportAuditLogs = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getAuditLogs: mockGetAuditLogs,
      exportAuditLogs: mockExportAuditLogs,
    },
  },
}));

const mockToast = vi.hoisted(() => vi.fn());
vi.mock('@/hooks/use-toast', () => ({
  useToast: () => ({ toast: mockToast }),
}));

// Mock DateRangePicker to avoid complex calendar rendering
vi.mock('@/components/ui/inputs/date-range-picker', () => ({
  DateRangePicker: ({
    label,
    className,
  }: {
    value: unknown;
    onChange: unknown;
    label: string;
    className?: string;
  }) => <div data-testid="date-range-picker" className={className}>{label}</div>,
}));

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { AuditTab } from '../AuditTab';

// ---------- Mock Data ----------

const MOCK_AUDIT = {
  entries: [
    {
      id: 'a1',
      adminUserId: 'admin-1',
      action: 'UserCreated',
      resource: 'User',
      result: 'Success',
      createdAt: '2026-03-01T10:00:00Z',
      userName: 'Admin',
      userEmail: 'admin@test.com',
      resourceId: 'u-1',
      ipAddress: '127.0.0.1',
      details: '{"email":"test@test.com"}',
    },
    {
      id: 'a2',
      adminUserId: null,
      action: 'CacheClear',
      resource: 'Cache',
      result: 'Failure',
      createdAt: '2026-03-01T09:00:00Z',
      userName: null,
      userEmail: null,
      resourceId: null,
      ipAddress: null,
      details: null,
    },
  ],
  totalCount: 2,
  limit: 50,
  offset: 0,
};

const MOCK_AUDIT_PAGINATED = {
  ...MOCK_AUDIT,
  totalCount: 120,
};

describe('AuditTab', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetAuditLogs.mockResolvedValue(MOCK_AUDIT);
    mockExportAuditLogs.mockResolvedValue(
      new Blob(['"action","resource"\n"UserCreated","User"'], { type: 'text/csv' })
    );
  });

  it('renders heading "Audit Trail"', async () => {
    render(<AuditTab />);

    await waitFor(() => {
      expect(screen.getByText('Audit Trail')).toBeInTheDocument();
    });
  });

  it('renders audit table with entries', async () => {
    render(<AuditTab />);

    await waitFor(() => {
      expect(screen.getByText('UserCreated')).toBeInTheDocument();
    });

    expect(screen.getByText('CacheClear')).toBeInTheDocument();
    expect(screen.getByText('Admin')).toBeInTheDocument();
    expect(screen.getByText('admin@test.com')).toBeInTheDocument();
  });

  it('renders result badges with correct variants', async () => {
    render(<AuditTab />);

    await waitFor(() => {
      expect(screen.getByText('UserCreated')).toBeInTheDocument();
    });

    // "Success" and "Failure" also appear in the <option> filter, so use getAllByText
    const successElements = screen.getAllByText('Success');
    // At least one badge + one option
    expect(successElements.length).toBeGreaterThanOrEqual(2);

    const failureElements = screen.getAllByText('Failure');
    expect(failureElements.length).toBeGreaterThanOrEqual(2);
  });

  it('renders filter inputs', async () => {
    render(<AuditTab />);

    await waitFor(() => {
      expect(screen.getByTestId('audit-user-filter')).toBeInTheDocument();
    });

    expect(screen.getByTestId('audit-action-filter')).toBeInTheDocument();
    expect(screen.getByTestId('audit-resource-filter')).toBeInTheDocument();
    expect(screen.getByTestId('audit-result-filter')).toBeInTheDocument();
  });

  it('renders DateRangePicker', async () => {
    render(<AuditTab />);

    await waitFor(() => {
      expect(screen.getByTestId('date-range-picker')).toBeInTheDocument();
    });
  });

  it('renders export CSV and JSON buttons', async () => {
    render(<AuditTab />);

    await waitFor(() => {
      expect(screen.getByTestId('export-audit-csv-button')).toBeInTheDocument();
    });

    expect(screen.getByTestId('export-audit-json-button')).toBeInTheDocument();
  });

  it('Export CSV button triggers download', async () => {
    const user = userEvent.setup();
    // Mock URL.createObjectURL / revokeObjectURL
    const mockCreateObjectURL = vi.fn().mockReturnValue('blob:test');
    const mockRevokeObjectURL = vi.fn();
    global.URL.createObjectURL = mockCreateObjectURL;
    global.URL.revokeObjectURL = mockRevokeObjectURL;

    // Mock document.createElement for anchor click
    const mockClick = vi.fn();
    const origCreateElement = document.createElement.bind(document);
    vi.spyOn(document, 'createElement').mockImplementation((tag: string) => {
      if (tag === 'a') {
        const el = origCreateElement('a');
        el.click = mockClick;
        return el;
      }
      return origCreateElement(tag);
    });

    render(<AuditTab />);

    await waitFor(() => {
      expect(screen.getByTestId('export-audit-csv-button')).toBeInTheDocument();
    });

    await user.click(screen.getByTestId('export-audit-csv-button'));

    await waitFor(() => {
      expect(mockExportAuditLogs).toHaveBeenCalled();
      expect(mockClick).toHaveBeenCalled();
    });

    expect(mockToast).toHaveBeenCalledWith(
      expect.objectContaining({ title: 'Audit log exported as CSV' })
    );

    vi.restoreAllMocks();
  });

  it('Export JSON button triggers download with JSON conversion', async () => {
    const user = userEvent.setup();
    const mockCreateObjectURL = vi.fn().mockReturnValue('blob:test');
    const mockRevokeObjectURL = vi.fn();
    global.URL.createObjectURL = mockCreateObjectURL;
    global.URL.revokeObjectURL = mockRevokeObjectURL;

    const mockClick = vi.fn();
    const origCreateElement = document.createElement.bind(document);
    vi.spyOn(document, 'createElement').mockImplementation((tag: string) => {
      if (tag === 'a') {
        const el = origCreateElement('a');
        el.click = mockClick;
        return el;
      }
      return origCreateElement(tag);
    });

    render(<AuditTab />);

    await waitFor(() => {
      expect(screen.getByTestId('export-audit-json-button')).toBeInTheDocument();
    });

    await user.click(screen.getByTestId('export-audit-json-button'));

    await waitFor(() => {
      expect(mockExportAuditLogs).toHaveBeenCalled();
      expect(mockClick).toHaveBeenCalled();
    });

    expect(mockToast).toHaveBeenCalledWith(
      expect.objectContaining({ title: 'Audit log exported as JSON' })
    );

    vi.restoreAllMocks();
  });

  it('expandable row shows details when clicked', async () => {
    const user = userEvent.setup();
    render(<AuditTab />);

    await waitFor(() => {
      expect(screen.getByText('UserCreated')).toBeInTheDocument();
    });

    // Click the first row to expand
    await user.click(screen.getByText('UserCreated'));

    await waitFor(() => {
      expect(screen.getByText('Resource ID:')).toBeInTheDocument();
      expect(screen.getByText('u-1')).toBeInTheDocument();
      expect(screen.getByText('IP Address:')).toBeInTheDocument();
      expect(screen.getByText('127.0.0.1')).toBeInTheDocument();
    });
  });

  it('JSON details render as formatted pre when entry is expanded', async () => {
    const user = userEvent.setup();
    render(<AuditTab />);

    await waitFor(() => {
      expect(screen.getByText('UserCreated')).toBeInTheDocument();
    });

    await user.click(screen.getByText('UserCreated'));

    await waitFor(() => {
      expect(screen.getByText('Details:')).toBeInTheDocument();
    });

    // The JSON should be formatted in a pre element
    const preElement = screen.getByText(/"email": "test@test.com"/);
    expect(preElement.tagName).toBe('PRE');
  });

  it('shows empty state when no entries', async () => {
    mockGetAuditLogs.mockResolvedValue({
      entries: [],
      totalCount: 0,
      limit: 50,
      offset: 0,
    });
    render(<AuditTab />);

    await waitFor(() => {
      expect(screen.getByText('No audit log entries found.')).toBeInTheDocument();
    });
  });

  it('shows pagination when totalCount > 50', async () => {
    mockGetAuditLogs.mockResolvedValue(MOCK_AUDIT_PAGINATED);
    render(<AuditTab />);

    await waitFor(() => {
      expect(screen.getByText(/Showing 1/)).toBeInTheDocument();
    });

    expect(screen.getByRole('button', { name: /previous/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /next/i })).toBeInTheDocument();
  });

  it('does not show pagination when totalCount <= 50', async () => {
    render(<AuditTab />);

    await waitFor(() => {
      expect(screen.getByText('UserCreated')).toBeInTheDocument();
    });

    expect(screen.queryByRole('button', { name: /previous/i })).not.toBeInTheDocument();
  });

  it('shows error toast when loading audit logs fails', async () => {
    mockGetAuditLogs.mockRejectedValue(new Error('Network error'));
    render(<AuditTab />);

    await waitFor(() => {
      expect(mockToast).toHaveBeenCalledWith(
        expect.objectContaining({
          title: 'Failed to load audit logs',
          variant: 'destructive',
        })
      );
    });
  });

  it('shows error toast when export fails', async () => {
    mockExportAuditLogs.mockRejectedValue(new Error('Export failed'));
    const user = userEvent.setup();
    render(<AuditTab />);

    await waitFor(() => {
      expect(screen.getByTestId('export-audit-csv-button')).toBeInTheDocument();
    });

    await user.click(screen.getByTestId('export-audit-csv-button'));

    await waitFor(() => {
      expect(mockToast).toHaveBeenCalledWith(
        expect.objectContaining({
          title: 'Export failed',
          variant: 'destructive',
        })
      );
    });
  });

  it('filters entries by user search client-side', async () => {
    const user = userEvent.setup();
    render(<AuditTab />);

    await waitFor(() => {
      expect(screen.getByText('UserCreated')).toBeInTheDocument();
      expect(screen.getByText('CacheClear')).toBeInTheDocument();
    });

    // Type a user search that only matches the first entry
    await user.type(screen.getByTestId('audit-user-filter'), 'Admin');

    // CacheClear has no userName so should be filtered out
    await waitFor(() => {
      expect(screen.getByText('UserCreated')).toBeInTheDocument();
      expect(screen.queryByText('CacheClear')).not.toBeInTheDocument();
    });
  });

  it('shows dash for null userName in audit entries', async () => {
    render(<AuditTab />);

    await waitFor(() => {
      expect(screen.getByText('CacheClear')).toBeInTheDocument();
    });

    // The second entry has null userName, should show em dash
    expect(screen.getByText('\u2014')).toBeInTheDocument();
  });
});
