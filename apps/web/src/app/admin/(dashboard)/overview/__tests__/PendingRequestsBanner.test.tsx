import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';

// ============================================================================
// Mocks — must be hoisted before imports
// ============================================================================

const mockApprove = vi.hoisted(() => vi.fn());
const mockReject = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    accessRequests: {
      approveAccessRequest: mockApprove,
      rejectAccessRequest: mockReject,
    },
  },
}));

import { PendingRequestsBanner } from '../PendingRequestsBanner';

// ============================================================================
// Helpers
// ============================================================================

function makeRequest(overrides: Partial<{ id: string; email: string }> = {}) {
  return {
    id: overrides.id ?? 'req-1',
    email: overrides.email ?? 'user@example.com',
    status: 'Pending',
    requestedAt: '2026-03-10T12:00:00Z',
  };
}

function renderWithQuery(ui: React.ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
}

// ============================================================================
// Tests
// ============================================================================

describe('PendingRequestsBanner', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockApprove.mockResolvedValue(undefined);
    mockReject.mockResolvedValue(undefined);
  });

  it('returns null (empty container) when requests is empty', () => {
    const { container } = renderWithQuery(<PendingRequestsBanner requests={[]} totalCount={0} />);
    expect(container.firstChild).toBeNull();
  });

  it('renders emails with approve and reject buttons', () => {
    const requests = [
      makeRequest({ id: 'req-1', email: 'alice@example.com' }),
      makeRequest({ id: 'req-2', email: 'bob@example.com' }),
    ];

    renderWithQuery(<PendingRequestsBanner requests={requests} totalCount={2} />);

    expect(screen.getByText('alice@example.com')).toBeInTheDocument();
    expect(screen.getByText('bob@example.com')).toBeInTheDocument();

    expect(screen.getByRole('button', { name: 'Approva alice@example.com' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Rifiuta alice@example.com' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Approva bob@example.com' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Rifiuta bob@example.com' })).toBeInTheDocument();
  });

  it('renders the header with correct count', () => {
    const requests = [makeRequest()];
    renderWithQuery(<PendingRequestsBanner requests={requests} totalCount={3} />);

    expect(screen.getByText('3 richieste di accesso in attesa')).toBeInTheDocument();
  });

  it('does not show "Vedi tutte" link when totalCount <= 5', () => {
    const requests = [makeRequest()];
    renderWithQuery(<PendingRequestsBanner requests={requests} totalCount={5} />);

    expect(screen.queryByRole('link', { name: /vedi tutte/i })).not.toBeInTheDocument();
  });

  it('shows "Vedi tutte" link when totalCount > 5', () => {
    const requests = [makeRequest()];
    renderWithQuery(<PendingRequestsBanner requests={requests} totalCount={6} />);

    const link = screen.getByRole('link', { name: /vedi tutte/i });
    expect(link).toBeInTheDocument();
    expect(link).toHaveAttribute('href', '/admin/users/access-requests');
  });

  it('calls approveAccessRequest on approve click', async () => {
    const user = userEvent.setup();
    const requests = [makeRequest({ id: 'req-abc', email: 'charlie@example.com' })];

    renderWithQuery(<PendingRequestsBanner requests={requests} totalCount={1} />);

    await user.click(screen.getByRole('button', { name: 'Approva charlie@example.com' }));

    await waitFor(() => {
      expect(mockApprove).toHaveBeenCalledWith('req-abc');
    });
  });

  it('calls rejectAccessRequest on reject click', async () => {
    const user = userEvent.setup();
    const requests = [makeRequest({ id: 'req-def', email: 'diana@example.com' })];

    renderWithQuery(<PendingRequestsBanner requests={requests} totalCount={1} />);

    await user.click(screen.getByRole('button', { name: 'Rifiuta diana@example.com' }));

    await waitFor(() => {
      expect(mockReject).toHaveBeenCalledWith('req-def');
    });
  });

  it('limits visible rows to 5 even when more requests are present', () => {
    const requests = Array.from({ length: 7 }, (_, i) =>
      makeRequest({ id: `req-${i}`, email: `user${i}@example.com` })
    );

    renderWithQuery(<PendingRequestsBanner requests={requests} totalCount={7} />);

    // Only 5 emails visible (slice 0..4)
    expect(screen.getByText('user0@example.com')).toBeInTheDocument();
    expect(screen.getByText('user4@example.com')).toBeInTheDocument();
    expect(screen.queryByText('user5@example.com')).not.toBeInTheDocument();
    expect(screen.queryByText('user6@example.com')).not.toBeInTheDocument();
  });
});
