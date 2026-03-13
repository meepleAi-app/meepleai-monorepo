/**
 * ContainerDashboard Component Tests
 * Issue #144 — Container Management tests
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

const mockGetDockerContainers = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getDockerContainers: mockGetDockerContainers,
    },
  },
}));

vi.mock('@/hooks/use-toast', () => ({
  useToast: () => ({ toast: vi.fn() }),
}));

import { ContainerDashboard } from '../ContainerDashboard';

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

    expect(screen.getByTestId('view-logs-abc123')).toHaveAttribute('href', '/admin/monitor/logs');
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
});
