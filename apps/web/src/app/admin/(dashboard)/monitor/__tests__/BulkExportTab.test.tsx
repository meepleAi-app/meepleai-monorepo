import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { BulkExportTab } from '../BulkExportTab';

// Mock URL methods
global.URL.createObjectURL = vi.fn().mockReturnValue('blob:mock');
global.URL.revokeObjectURL = vi.fn();

const mocks = vi.hoisted(() => ({
  getAllUsers: vi.fn().mockResolvedValue({
    items: [
      {
        id: '1',
        email: 'a@b.com',
        displayName: 'A',
        role: 'user',
        createdAt: '2026-01-01T00:00:00Z',
        isSuspended: false,
        lastSeenAt: null,
        tier: 'Free',
        tokenUsage: 0,
        tokenLimit: 10000,
      },
    ],
    total: 1,
    page: 1,
    pageSize: 10000,
  }),
  exportAuditLogs: vi.fn().mockResolvedValue(new Blob(['csv'], { type: 'text/csv' })),
  getApiKeysWithStats: vi
    .fn()
    .mockResolvedValue({ keys: [], count: 0, filters: { includeRevoked: false } }),
}));

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getAllUsers: mocks.getAllUsers,
      exportAuditLogs: mocks.exportAuditLogs,
      getApiKeysWithStats: mocks.getApiKeysWithStats,
    },
  },
}));
vi.mock('@/hooks/useToast', () => ({ useToast: () => ({ toast: vi.fn() }) }));

describe('BulkExportTab', () => {
  it('mostra 3 card di export', () => {
    render(<BulkExportTab />);
    expect(screen.getByText('Users')).toBeInTheDocument();
    expect(screen.getByText('Audit Log')).toBeInTheDocument();
    expect(screen.getByText('API Keys')).toBeInTheDocument();
  });

  it('i bottoni Download non sono disabled', () => {
    render(<BulkExportTab />);
    const buttons = screen.getAllByRole('button', { name: /download/i });
    buttons.forEach(btn => expect(btn).not.toBeDisabled());
  });

  it('click su Users Download chiama getAllUsers', async () => {
    render(<BulkExportTab />);
    fireEvent.click(screen.getAllByRole('button', { name: /download/i })[0]);
    await waitFor(() => expect(mocks.getAllUsers).toHaveBeenCalled());
  });

  it('click su Audit Log Download chiama exportAuditLogs', async () => {
    render(<BulkExportTab />);
    fireEvent.click(screen.getAllByRole('button', { name: /download/i })[1]);
    await waitFor(() => expect(mocks.exportAuditLogs).toHaveBeenCalled());
  });
});
