/**
 * ContainerDashboard Component Tests
 * Issue #144  — Container Management tests (original polling implementation).
 * Issue #1853 — SSE-driven refresh + polling fallback (60s minimum).
 * Issue #1837 — Lift useLiveEvents subscription to page.tsx; props-driven SSE forwarding.
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import type { DomainEventDto } from '@/components/admin/monitor/live-event-types';

const mockGetDockerContainers = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getDockerContainers: mockGetDockerContainers,
    },
  },
}));

vi.mock('@/hooks/useToast', () => ({
  useToast: () => ({ toast: vi.fn() }),
}));

import { ContainerDashboard, computePollingBackoff } from '../ContainerDashboard';

const mockContainers = [
  {
    id: 'abc123',
    name: 'meepleai-api',
    image: 'meepleai-api:latest',
    state: 'running',
    status: 'Up 2 hours',
    created: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(),
    labels: {},
  },
  {
    id: 'def456',
    name: 'meepleai-postgres',
    image: 'pgvector/pgvector:pg16',
    state: 'running',
    status: 'Up 5 hours',
    created: new Date(Date.now() - 5 * 60 * 60 * 1000).toISOString(),
    labels: {},
  },
  {
    id: 'ghi789',
    name: 'meepleai-redis',
    image: 'redis:7-alpine',
    state: 'exited',
    status: 'Exited (0) 1 hour ago',
    created: new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString(),
    labels: {},
  },
];

function makeEvent(id: string): DomainEventDto {
  return {
    id,
    eventId: id,
    eventType: 'container.restarted',
    aggregateType: 'Container',
    aggregateId: 'abc123',
    userId: null,
    payloadJson: '{}',
    payloadVersion: 1,
    occurredAt: new Date().toISOString(),
    loggedAt: new Date().toISOString(),
  };
}

describe('ContainerDashboard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows loading state initially', () => {
    mockGetDockerContainers.mockReturnValue(new Promise(() => {}));
    render(<ContainerDashboard />);

    expect(screen.getByTestId('container-loading')).toBeInTheDocument();
  });

  it('shows empty state when no containers', async () => {
    mockGetDockerContainers.mockResolvedValue([]);
    render(<ContainerDashboard />);

    await waitFor(() => {
      expect(screen.getByTestId('container-empty')).toBeInTheDocument();
    });
  });

  it('renders container grid with cards', async () => {
    mockGetDockerContainers.mockResolvedValue(mockContainers);
    render(<ContainerDashboard />);

    await waitFor(() => {
      expect(screen.getByTestId('container-grid')).toBeInTheDocument();
    });

    expect(screen.getByTestId('container-card-abc123')).toBeInTheDocument();
    expect(screen.getByTestId('container-card-def456')).toBeInTheDocument();
    expect(screen.getByTestId('container-card-ghi789')).toBeInTheDocument();
  });

  it('displays status summary with correct counts', async () => {
    mockGetDockerContainers.mockResolvedValue(mockContainers);
    render(<ContainerDashboard />);

    await waitFor(() => {
      expect(screen.getByTestId('status-summary')).toBeInTheDocument();
    });

    // 3 total, 2 running, 1 stopped
    expect(screen.getByText('3')).toBeInTheDocument();
    expect(screen.getByText('2')).toBeInTheDocument();
    expect(screen.getByText('1')).toBeInTheDocument();
  });

  it('shows container name and image in cards', async () => {
    mockGetDockerContainers.mockResolvedValue(mockContainers);
    render(<ContainerDashboard />);

    await waitFor(() => {
      expect(screen.getByText('meepleai-api')).toBeInTheDocument();
    });

    expect(screen.getByText('meepleai-api:latest')).toBeInTheDocument();
    expect(screen.getByText('meepleai-postgres')).toBeInTheDocument();
  });

  it('shows status badges for each container', async () => {
    mockGetDockerContainers.mockResolvedValue(mockContainers);
    render(<ContainerDashboard />);

    await waitFor(() => {
      expect(screen.getByTestId('container-grid')).toBeInTheDocument();
    });

    const badges = screen.getAllByTestId('container-status-badge');
    expect(badges.length).toBe(3);
  });

  it('has View Logs link for each container', async () => {
    mockGetDockerContainers.mockResolvedValue(mockContainers);
    render(<ContainerDashboard />);

    await waitFor(() => {
      expect(screen.getByTestId('view-logs-abc123')).toBeInTheDocument();
    });

    expect(screen.getByTestId('view-logs-abc123')).toHaveAttribute(
      'href',
      '/admin/monitor?tab=logs'
    );
  });

  it('auto-refresh controls are visible', async () => {
    mockGetDockerContainers.mockResolvedValue(mockContainers);
    render(<ContainerDashboard />);

    await waitFor(() => {
      expect(screen.getByTestId('auto-refresh-controls')).toBeInTheDocument();
    });

    expect(screen.getByTestId('auto-refresh-toggle')).toBeInTheDocument();
    expect(screen.getByTestId('refresh-interval-select')).toBeInTheDocument();
    expect(screen.getByTestId('countdown')).toBeInTheDocument();
  });

  it('toggles auto-refresh on button click', async () => {
    const user = userEvent.setup();
    mockGetDockerContainers.mockResolvedValue(mockContainers);
    render(<ContainerDashboard />);

    await waitFor(() => {
      expect(screen.getByTestId('auto-refresh-toggle')).toBeInTheDocument();
    });

    // Initially auto-refresh is on, shows "Pause"
    expect(screen.getByText('Pause')).toBeInTheDocument();

    await user.click(screen.getByTestId('auto-refresh-toggle'));

    expect(screen.getByText('Resume')).toBeInTheDocument();
  });

  it('shows dash for uptime of stopped containers', async () => {
    mockGetDockerContainers.mockResolvedValue(mockContainers);
    render(<ContainerDashboard />);

    await waitFor(() => {
      expect(screen.getByTestId('container-card-ghi789')).toBeInTheDocument();
    });

    const stoppedCard = screen.getByTestId('container-card-ghi789');
    const uptimeEl = stoppedCard.querySelector('[data-testid="container-uptime"]');
    expect(uptimeEl).toHaveTextContent('—');
  });

  it('manual refresh button fetches containers', async () => {
    const user = userEvent.setup();
    mockGetDockerContainers.mockResolvedValue(mockContainers);
    render(<ContainerDashboard />);

    await waitFor(() => {
      expect(screen.getByTestId('manual-refresh')).toBeInTheDocument();
    });

    const callsBefore = mockGetDockerContainers.mock.calls.length;
    await user.click(screen.getByTestId('manual-refresh'));

    await waitFor(() => {
      expect(mockGetDockerContainers.mock.calls.length).toBeGreaterThan(callsBefore);
    });
  });

  /* ------------------------------------------------------------------ */
  /*  #1853 — SSE wiring                                                 */
  /* ------------------------------------------------------------------ */

  it('shows SSE status badge "Polling only" when sseConnected prop is false', async () => {
    mockGetDockerContainers.mockResolvedValue(mockContainers);
    render(<ContainerDashboard liveEvents={[]} sseConnected={false} />);

    const badge = await screen.findByTestId('sse-status-indicator');
    expect(badge).toHaveTextContent(/polling only/i);
  });

  it('shows SSE status badge "Live" when sseConnected prop is true', async () => {
    mockGetDockerContainers.mockResolvedValue(mockContainers);
    render(<ContainerDashboard liveEvents={[]} sseConnected={true} />);

    const badge = await screen.findByTestId('sse-status-indicator');
    expect(badge).toHaveTextContent(/^live$/i);
  });

  it('refreshes containers when a new live event arrives via prop change', async () => {
    mockGetDockerContainers.mockResolvedValue(mockContainers);
    // First render: events seeded by backfill (counts as "first observation",
    // so we expect NO extra fetch beyond the initial mount fetch).
    const { rerender } = render(
      <ContainerDashboard liveEvents={[makeEvent('ev-1')]} sseConnected={true} />
    );

    // Wait for the initial mount fetch to settle (StrictMode may invoke it twice;
    // we rely on a delta comparison below rather than an absolute call count).
    await waitFor(() => {
      expect(mockGetDockerContainers).toHaveBeenCalled();
    });
    const callsAfterMount = mockGetDockerContainers.mock.calls.length;

    // New event arrives mid-session — the dashboard must refresh containers.
    rerender(
      <ContainerDashboard liveEvents={[makeEvent('ev-2'), makeEvent('ev-1')]} sseConnected={true} />
    );

    await waitFor(() => {
      expect(mockGetDockerContainers.mock.calls.length).toBeGreaterThan(callsAfterMount);
    });
  });

  it('exposes aria-pressed on the Pause/Resume toggle (a11y)', async () => {
    const user = userEvent.setup();
    mockGetDockerContainers.mockResolvedValue(mockContainers);
    render(<ContainerDashboard />);

    const toggle = await screen.findByTestId('auto-refresh-toggle');
    // Initially auto-refresh is ON → aria-pressed="true"
    expect(toggle).toHaveAttribute('aria-pressed', 'true');
    expect(toggle).toHaveAttribute('aria-label', 'Pause auto-refresh');

    await user.click(toggle);

    expect(toggle).toHaveAttribute('aria-pressed', 'false');
    expect(toggle).toHaveAttribute('aria-label', 'Resume auto-refresh');
  });

  // Backoff is unit-tested in isolation below (`computePollingBackoff` table)
  // because exercising it through the real component would require fake
  // timers + a nested `setTimeout` chain + StrictMode double-invoke handling,
  // which together are very flaky.

  it('polling fallback offers 60s/2m/5m intervals only', async () => {
    mockGetDockerContainers.mockResolvedValue(mockContainers);
    render(<ContainerDashboard />);

    const select = (await screen.findByTestId('refresh-interval-select')) as HTMLSelectElement;
    const optionValues = Array.from(select.options).map(o => Number(o.value));
    expect(optionValues).toEqual([60, 120, 300]);
    expect(select.value).toBe('60'); // default
  });
});

describe('computePollingBackoff (unit)', () => {
  // Schedule table, baseSeconds=60, default caps (5× / 300s):
  it.each([
    [0, 60], // no failures → base interval
    [1, 60], // 2^0 = 1× — first failure tolerated, no backoff yet
    [2, 120], // 2^1 = 2×
    [3, 240], // 2^2 = 4×
    [4, 300], // 2^3 = 8× would be 480, but multiplier capped at 5 → 300
    [5, 300], // multiplier still 5 → 300
    [10, 300], // worst case stays at the 300s ceiling
  ])('failures=%d → %ds (base 60)', (failures, expected) => {
    expect(computePollingBackoff(60, failures)).toBe(expected);
  });

  it('honours custom caps', () => {
    expect(computePollingBackoff(30, 5, { maxMultiplier: 3, maxDelaySeconds: 60 })).toBe(60); // 30 * min(2^4, 3) = 30 * 3 = 90, clamped at 60
  });

  it('treats negative failures as 0', () => {
    expect(computePollingBackoff(60, -1)).toBe(60);
  });
});
