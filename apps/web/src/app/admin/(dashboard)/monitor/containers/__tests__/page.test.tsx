/**
 * Container Dashboard Page Tests
 * Issue #144 — Container Management tests
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';

vi.mock('@/hooks/useSetNavConfig', () => ({
  useSetNavConfig: () => vi.fn(),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ refresh: vi.fn() }),
}));

const mockGetDockerContainers = vi.hoisted(() => vi.fn());
const mockRestartService = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getDockerContainers: mockGetDockerContainers,
      restartService: mockRestartService,
    },
  },
}));

vi.mock('@/hooks/use-toast', () => ({
  useToast: () => ({ toast: vi.fn() }),
}));

import ContainerDashboardPage from '../page';

describe('ContainerDashboardPage', () => {
  it('renders page with title', () => {
    mockGetDockerContainers.mockReturnValue(new Promise(() => {}));
    render(<ContainerDashboardPage />);

    expect(screen.getByTestId('containers-page')).toBeInTheDocument();
    expect(screen.getByText('Container Dashboard')).toBeInTheDocument();
  });

  it('renders container dashboard and restart panel', () => {
    mockGetDockerContainers.mockReturnValue(new Promise(() => {}));
    render(<ContainerDashboardPage />);

    expect(screen.getByTestId('container-dashboard')).toBeInTheDocument();
    expect(screen.getByTestId('restart-all-panel')).toBeInTheDocument();
  });
});
