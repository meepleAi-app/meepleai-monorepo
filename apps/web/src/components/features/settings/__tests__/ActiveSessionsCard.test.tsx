import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ActiveSessionsCard } from '../two-factor/ActiveSessionsCard';
import { api } from '@/lib/api';

vi.mock('@/lib/api', () => ({
  api: {
    auth: {
      getUserSessions: vi.fn(),
      revokeSession: vi.fn(),
      revokeAllSessions: vi.fn(),
    },
  },
}));

// s1 is the "current" session (most recently seen — lastSeenAt closer to now)
const SESSIONS = [
  {
    id: 's1',
    userId: 'u1',
    userEmail: 'a@b.it',
    createdAt: new Date().toISOString(),
    expiresAt: new Date(Date.now() + 86400000).toISOString(),
    lastSeenAt: new Date(Date.now() - 60000).toISOString(), // 1 min ago
    revokedAt: null,
    ipAddress: '1.2.3.4',
    userAgent: 'Mozilla/5.0 (Macintosh) Chrome/124',
  },
  {
    id: 's2',
    userId: 'u1',
    userEmail: 'a@b.it',
    createdAt: new Date().toISOString(),
    expiresAt: new Date(Date.now() + 86400000).toISOString(),
    lastSeenAt: new Date(Date.now() - 86400000).toISOString(), // 1 day ago
    revokedAt: null,
    ipAddress: '5.6.7.8',
    userAgent: 'iPhone iOS 17',
  },
];

function wrap(ui: React.ReactNode) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>);
}

describe('ActiveSessionsCard', () => {
  beforeEach(() => {
    vi.mocked(api.auth.getUserSessions).mockResolvedValue(SESSIONS);
    vi.mocked(api.auth.revokeSession).mockResolvedValue({ ok: true, message: '' });
    vi.mocked(api.auth.revokeAllSessions).mockResolvedValue({
      ok: true,
      revokedCount: 1,
      currentSessionRevoked: false,
      message: '',
    });
  });

  it('renders sessions from getUserSessions', async () => {
    wrap(<ActiveSessionsCard />);
    await waitFor(() => expect(screen.getByText(/Chrome|Mozilla/i)).toBeInTheDocument());
  });

  it('revokes a non-current session', async () => {
    wrap(<ActiveSessionsCard />);
    await waitFor(() => screen.getByText(/iPhone/));
    // The Revoke button on session s2 (not current — not the most recently seen)
    const revokeButtons = screen.getAllByRole('button', { name: /^revoke$/i });
    fireEvent.click(revokeButtons[0]);
    await waitFor(() => expect(api.auth.revokeSession).toHaveBeenCalled());
  });

  it('calls revokeAllSessions when "Sign out all" is clicked', async () => {
    wrap(<ActiveSessionsCard />);
    await waitFor(() => screen.getByText(/iPhone/));
    fireEvent.click(screen.getByRole('button', { name: /sign out all|out all/i }));
    await waitFor(() => expect(api.auth.revokeAllSessions).toHaveBeenCalled());
  });
});
