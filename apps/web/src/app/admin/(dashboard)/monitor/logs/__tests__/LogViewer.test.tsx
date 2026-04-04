/**
 * LogViewer Component Tests
 * Issue #142 — Log Viewer tests for Admin Infrastructure Panel Phase 3
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

const mockGetDockerContainers = vi.hoisted(() => vi.fn());
const mockGetContainerLogs = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getDockerContainers: mockGetDockerContainers,
      getContainerLogs: mockGetContainerLogs,
    },
  },
}));

import { LogViewer } from '../LogViewer';

const mockContainers = [
  {
    id: 'abc123',
    name: 'meepleai-api',
    image: 'meepleai-api:latest',
    state: 'running',
    status: 'Up 2 hours',
    created: new Date().toISOString(),
    labels: {},
  },
  {
    id: 'def456',
    name: 'meepleai-postgres',
    image: 'pgvector/pgvector:pg16',
    state: 'running',
    status: 'Up 2 hours',
    created: new Date().toISOString(),
    labels: {},
  },
];

const mockLogs = {
  containerId: 'abc123',
  containerName: 'meepleai-api',
  lines: [
    '2026-03-13T10:00:00Z info: Application started',
    '2026-03-13T10:00:01Z info: Listening on :8080',
  ],
  fetchedAt: new Date().toISOString(),
};

describe('LogViewer', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders container list', async () => {
    mockGetDockerContainers.mockResolvedValue(mockContainers);
    render(<LogViewer />);

    await waitFor(() => {
      expect(screen.getByTestId('container-list')).toBeInTheDocument();
    });

    expect(screen.getByTestId('container-item-abc123')).toBeInTheDocument();
    expect(screen.getByTestId('container-item-def456')).toBeInTheDocument();
  });

  it('clicking container loads its logs', async () => {
    const user = userEvent.setup();
    mockGetDockerContainers.mockResolvedValue(mockContainers);
    mockGetContainerLogs.mockResolvedValue(mockLogs);
    render(<LogViewer />);

    await waitFor(() => {
      expect(screen.getByTestId('container-item-abc123')).toBeInTheDocument();
    });

    await user.click(screen.getByTestId('container-item-abc123'));

    await waitFor(() => {
      expect(screen.getByTestId('log-output')).toBeInTheDocument();
    });

    expect(screen.getByText(/Application started/)).toBeInTheDocument();
  });

  it('shows loading state', () => {
    mockGetDockerContainers.mockReturnValue(new Promise(() => {}));
    render(<LogViewer />);

    expect(screen.getByTestId('log-viewer-loading')).toBeInTheDocument();
  });

  it('shows empty state when no containers', async () => {
    mockGetDockerContainers.mockResolvedValue([]);
    render(<LogViewer />);

    await waitFor(() => {
      expect(screen.getByTestId('log-viewer-empty')).toBeInTheDocument();
    });
  });

  it('refresh button reloads logs', async () => {
    const user = userEvent.setup();
    mockGetDockerContainers.mockResolvedValue(mockContainers);
    mockGetContainerLogs.mockResolvedValue(mockLogs);
    render(<LogViewer />);

    await waitFor(() => {
      expect(screen.getByTestId('container-item-abc123')).toBeInTheDocument();
    });

    await user.click(screen.getByTestId('container-item-abc123'));

    await waitFor(() => {
      expect(screen.getByTestId('log-refresh-btn')).toBeInTheDocument();
    });

    await user.click(screen.getByTestId('log-refresh-btn'));

    await waitFor(() => {
      expect(mockGetContainerLogs).toHaveBeenCalledTimes(2);
    });
  });
});
