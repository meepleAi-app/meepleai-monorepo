/**
 * Container Dashboard Page Tests
 * Issue #144 — Container Management tests
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';

vi.mock('@/hooks/useSetNavConfig', () => ({
  useSetNavConfig: () => vi.fn(),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ refresh: vi.fn() }),
}));

const mockGetDockerContainers = vi.hoisted(() => vi.fn());
const mockRestartService = vi.hoisted(() => vi.fn());
const mockGetMetricsTimeSeries = vi.hoisted(() => vi.fn());
const mockGetAllBatchJobs = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getDockerContainers: mockGetDockerContainers,
      restartService: mockRestartService,
      getMetricsTimeSeries: mockGetMetricsTimeSeries,
      getAllBatchJobs: mockGetAllBatchJobs,
    },
  },
}));

vi.mock('@/hooks/useToast', () => ({
  useToast: () => ({ toast: vi.fn() }),
}));

// #1853: ContainerDashboard now consumes useLiveEvents — stub it out so
// the page test does not need a working EventSource / fetch backfill.
vi.mock('@/components/admin/monitor/use-live-events', () => ({
  useLiveEvents: () => ({
    events: [],
    isLoading: false,
    isStreaming: false,
    error: null,
    pause: vi.fn(),
    resume: vi.fn(),
    refetch: vi.fn(),
  }),
}));

import ContainerDashboardPage from '../page';

describe('ContainerDashboardPage', () => {
  beforeEach(() => {
    // Hanging promise for "loading" state; tests only check structure
    mockGetDockerContainers.mockReturnValue(new Promise(() => {}));
    mockGetMetricsTimeSeries.mockReturnValue(new Promise(() => {}));
    mockGetAllBatchJobs.mockReturnValue(new Promise(() => {}));
  });

  it('renders page with title', () => {
    render(<ContainerDashboardPage />);

    expect(screen.getByTestId('containers-page')).toBeInTheDocument();
    // SP5 #1837: titolo aggiornato da "Container Dashboard" a "Infrastructure & Containers"
    expect(screen.getByText('Infrastructure & Containers')).toBeInTheDocument();
  });

  it('renders all SP5 #1837 sections', () => {
    render(<ContainerDashboardPage />);

    expect(screen.getByTestId('kpi-sparkline-strip')).toBeInTheDocument();
    expect(screen.getByTestId('container-dashboard')).toBeInTheDocument();
    expect(screen.getByTestId('live-event-log')).toBeInTheDocument();
    expect(screen.getByTestId('restart-all-panel')).toBeInTheDocument();
  });

  it('LiveEventLog has role=log + aria-live=polite (a11y)', () => {
    render(<ContainerDashboardPage />);

    const log = screen.getByRole('log');
    expect(log).toHaveAttribute('aria-live', 'polite');
  });
});
