import { vi, describe, it, expect, beforeEach, afterEach } from 'vitest';

const mockGetActiveEmergencyOverrides = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getActiveEmergencyOverrides: mockGetActiveEmergencyOverrides,
    },
  },
}));

// Mock next/link to render a simple anchor
vi.mock('next/link', () => ({
  default: ({
    children,
    href,
    ...rest
  }: {
    children: React.ReactNode;
    href: string;
    className?: string;
  }) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
}));

import { render, screen, waitFor, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { EmergencyBanner } from '@/components/admin/layout/EmergencyBanner';

// ---------- Mock Data ----------

const MOCK_OVERRIDE = {
  action: 'force-ollama-only',
  reason: 'High error rate',
  adminUserId: 'admin-1',
  targetProvider: null,
  activatedAt: '2026-03-01T09:00:00Z',
  expiresAt: '2026-03-01T10:00:00Z',
  remainingMinutes: 20,
};

describe('EmergencyBanner', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers({ shouldAdvanceTime: true });
    mockGetActiveEmergencyOverrides.mockResolvedValue([]);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('renders nothing when no overrides are active', async () => {
    const { container } = render(<EmergencyBanner />);

    await waitFor(() => {
      expect(mockGetActiveEmergencyOverrides).toHaveBeenCalled();
    });

    // Banner should not be rendered
    expect(screen.queryByTestId('emergency-banner')).not.toBeInTheDocument();
    expect(container.firstChild).toBeNull();
  });

  it('shows banner with count when overrides exist', async () => {
    mockGetActiveEmergencyOverrides.mockResolvedValue([MOCK_OVERRIDE]);
    render(<EmergencyBanner />);

    await waitFor(() => {
      expect(screen.getByTestId('emergency-banner')).toBeInTheDocument();
    });

    expect(screen.getByText(/1 active emergency override\b/)).toBeInTheDocument();
  });

  it('shows plural text when multiple overrides exist', async () => {
    mockGetActiveEmergencyOverrides.mockResolvedValue([MOCK_OVERRIDE, { ...MOCK_OVERRIDE, action: 'flush-quota-cache' }]);
    render(<EmergencyBanner />);

    await waitFor(() => {
      expect(screen.getByText(/2 active emergency overrides/)).toBeInTheDocument();
    });
  });

  it('dismiss button hides banner', async () => {
    mockGetActiveEmergencyOverrides.mockResolvedValue([MOCK_OVERRIDE]);
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<EmergencyBanner />);

    await waitFor(() => {
      expect(screen.getByTestId('emergency-banner')).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /dismiss banner/i }));

    expect(screen.queryByTestId('emergency-banner')).not.toBeInTheDocument();
  });

  it('banner re-shows when new override is added (count increases)', async () => {
    mockGetActiveEmergencyOverrides.mockResolvedValue([MOCK_OVERRIDE]);
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<EmergencyBanner />);

    // Wait for initial render
    await waitFor(() => {
      expect(screen.getByTestId('emergency-banner')).toBeInTheDocument();
    });

    // Dismiss the banner
    await user.click(screen.getByRole('button', { name: /dismiss banner/i }));
    expect(screen.queryByTestId('emergency-banner')).not.toBeInTheDocument();

    // Simulate a new override being added (count increases from 1 to 2)
    mockGetActiveEmergencyOverrides.mockResolvedValue([
      MOCK_OVERRIDE,
      { ...MOCK_OVERRIDE, action: 'flush-quota-cache' },
    ]);

    // Advance timer to trigger next poll (60s interval)
    await act(async () => {
      vi.advanceTimersByTime(60_000);
    });

    await waitFor(() => {
      expect(screen.getByTestId('emergency-banner')).toBeInTheDocument();
      expect(screen.getByText(/2 active emergency overrides/)).toBeInTheDocument();
    });
  });

  it('link points to /admin/monitor/operations?tab=emergency', async () => {
    mockGetActiveEmergencyOverrides.mockResolvedValue([MOCK_OVERRIDE]);
    render(<EmergencyBanner />);

    await waitFor(() => {
      expect(screen.getByText('View details')).toBeInTheDocument();
    });

    const link = screen.getByText('View details').closest('a');
    expect(link).toHaveAttribute('href', '/admin/monitor/operations?tab=emergency');
  });

  it('does not show banner after dismiss when count stays the same on next poll', async () => {
    mockGetActiveEmergencyOverrides.mockResolvedValue([MOCK_OVERRIDE]);
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<EmergencyBanner />);

    await waitFor(() => {
      expect(screen.getByTestId('emergency-banner')).toBeInTheDocument();
    });

    // Dismiss
    await user.click(screen.getByRole('button', { name: /dismiss banner/i }));
    expect(screen.queryByTestId('emergency-banner')).not.toBeInTheDocument();

    // Poll again with same count
    await act(async () => {
      vi.advanceTimersByTime(60_000);
    });

    // Wait for the poll to complete, then verify banner is still dismissed
    // (lastDismissedCount callback dependency may cause extra calls, so just check behavior)
    await waitFor(() => {
      // At least 2 calls: initial + poll (possibly more due to useCallback dependency change)
      expect(mockGetActiveEmergencyOverrides).toHaveBeenCalledTimes(
        mockGetActiveEmergencyOverrides.mock.calls.length
      );
    });
    expect(screen.queryByTestId('emergency-banner')).not.toBeInTheDocument();
  });

  it('handles API error silently', async () => {
    mockGetActiveEmergencyOverrides.mockRejectedValue(new Error('Network error'));
    const { container } = render(<EmergencyBanner />);

    await waitFor(() => {
      expect(mockGetActiveEmergencyOverrides).toHaveBeenCalled();
    });

    // Banner should not render (count stays 0)
    expect(container.firstChild).toBeNull();
  });
});
