import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { ActivityTable } from '../activity-table';

// Mock the API module
vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getAuditLogs: vi.fn().mockResolvedValue({
        entries: [
          {
            id: '00000000-0000-0000-0000-000000000001',
            adminUserId: '00000000-0000-0000-0000-000000000010',
            action: 'UserRoleChange',
            resource: 'User',
            resourceId: '123',
            result: 'Success',
            details: null,
            ipAddress: '192.168.1.45',
            createdAt: '2026-03-12T14:35:22Z',
            userName: 'Sarah Chen',
            userEmail: 'sarah@meepleai.com',
          },
          {
            id: '00000000-0000-0000-0000-000000000002',
            adminUserId: null,
            action: 'DomainEvent.InvitationAcceptedEvent',
            resource: 'InvitationAcceptedEvent',
            resourceId: null,
            result: 'Error',
            details: null,
            ipAddress: null,
            createdAt: '2026-03-12T13:22:17Z',
            userName: null,
            userEmail: null,
          },
        ],
        totalCount: 2,
        limit: 50,
        offset: 0,
      }),
    },
  },
}));

function renderWithQuery(ui: React.ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
}

describe('ActivityTable', () => {
  it('renders table headers', async () => {
    renderWithQuery(<ActivityTable />);

    expect(await screen.findByText('Timestamp')).toBeInTheDocument();
    // "User" appears in both header and data cells, so use role selector
    const headers = screen.getAllByRole('columnheader');
    const headerTexts = headers.map(h => h.textContent);
    expect(headerTexts).toContain('User');
    expect(headerTexts).toContain('Action');
    expect(headerTexts).toContain('Resource');
    expect(headerTexts).toContain('IP Address');
    expect(headerTexts).toContain('Result');
  });

  it('displays user information from API', async () => {
    renderWithQuery(<ActivityTable />);

    expect(await screen.findByText('Sarah Chen')).toBeInTheDocument();
    expect(screen.getByText('sarah@meepleai.com')).toBeInTheDocument();
  });

  it('shows action badges', async () => {
    renderWithQuery(<ActivityTable />);

    expect(await screen.findByText('User Role Change')).toBeInTheDocument();
  });

  it('displays success and error results', async () => {
    renderWithQuery(<ActivityTable />);

    expect(await screen.findByText('Success')).toBeInTheDocument();
    expect(screen.getByText('Error')).toBeInTheDocument();
  });

  it('shows system when no user name', async () => {
    renderWithQuery(<ActivityTable />);

    expect(await screen.findByText('System')).toBeInTheDocument();
  });

  it('formats timestamps correctly', async () => {
    const { container } = renderWithQuery(<ActivityTable />);

    // Wait for data to load, then check for font-mono class on timestamp cells
    await screen.findByText('Sarah Chen');
    const timestamps = container.querySelectorAll('.font-mono');
    expect(timestamps.length).toBeGreaterThan(0);
  });
});
