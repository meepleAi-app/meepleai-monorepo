/**
 * LogViewerPage Tests
 * Issue #142 — Log Viewer page-level test
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';

// Mock child components that use React Query internally
vi.mock('../AppLogViewer', () => ({
  AppLogViewer: () => <div data-testid="app-log-viewer">App Log Viewer</div>,
}));

vi.mock('../LogViewer', () => ({
  LogViewer: () => <div data-testid="container-log-viewer">Container Log Viewer</div>,
}));

vi.mock('@/hooks/useSetNavConfig', () => ({
  useSetNavConfig: () => vi.fn(),
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
    expect(screen.getByText(/Application and container log monitoring/)).toBeInTheDocument();
  });
});
