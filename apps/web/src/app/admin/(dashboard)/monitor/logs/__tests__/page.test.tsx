/**
 * LogViewerPage Tests
 * Issue #142 — Log Viewer page-level test
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';

const mockSetNavConfig = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getDockerContainers: vi.fn().mockResolvedValue([]),
    },
  },
}));

vi.mock('@/hooks/useSetNavConfig', () => ({
  useSetNavConfig: () => mockSetNavConfig,
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ refresh: vi.fn() }),
}));

import LogViewerPage from '../page';

describe('LogViewerPage', () => {
  it('renders page with title', () => {
    render(<LogViewerPage />);
    expect(screen.getByText('Log Viewer')).toBeInTheDocument();
    expect(screen.getByTestId('logs-page')).toBeInTheDocument();
  });

  it('renders page description', () => {
    render(<LogViewerPage />);
    expect(screen.getByText(/Container logs and application monitoring/)).toBeInTheDocument();
  });
});
