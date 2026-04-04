import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import AdminUsersPage from '../page';

const makeUser = (overrides = {}) => ({
  id: '1',
  email: 'test@example.com',
  displayName: 'Test User',
  role: 'user',
  createdAt: '2026-01-01T00:00:00Z',
  isSuspended: false,
  lastSeenAt: null,
  tier: 'Free',
  tokenUsage: 0,
  tokenLimit: 10000,
  ...overrides,
});

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getAllUsers: vi.fn().mockResolvedValue({
        items: [
          {
            id: '1',
            email: 'test@example.com',
            displayName: 'Test User',
            role: 'user',
            createdAt: '2026-01-01T00:00:00Z',
            isSuspended: false,
            lastSeenAt: null,
            tier: 'Free',
            tokenUsage: 0,
            tokenLimit: 10000,
          },
          {
            id: '2',
            email: 'sus@example.com',
            displayName: 'Suspended User',
            role: 'user',
            createdAt: '2026-01-01T00:00:00Z',
            isSuspended: true,
            lastSeenAt: null,
            tier: 'Free',
            tokenUsage: 0,
            tokenLimit: 10000,
          },
        ],
        total: 2,
        page: 1,
        pageSize: 20,
      }),
    },
    invitations: {
      getInvitations: vi.fn().mockResolvedValue({ items: [], totalCount: 0 }),
      resendInvitation: vi.fn().mockResolvedValue({}),
      revokeInvitation: vi.fn().mockResolvedValue({}),
    },
  },
}));

function wrapper({ children }: { children: React.ReactNode }) {
  return (
    <QueryClientProvider
      client={
        new QueryClient({
          defaultOptions: { queries: { retry: false } },
        })
      }
    >
      {children}
    </QueryClientProvider>
  );
}

describe('AdminUsersPage - user status', () => {
  it('mostra badge Attivo per utente non sospeso', async () => {
    render(<AdminUsersPage />, { wrapper });
    const badges = await screen.findAllByText('Attivo');
    expect(badges.length).toBeGreaterThanOrEqual(1);
  });

  it('mostra badge Sospeso per utente sospeso', async () => {
    render(<AdminUsersPage />, { wrapper });
    expect(await screen.findByText('Sospeso')).toBeInTheDocument();
  });
});
