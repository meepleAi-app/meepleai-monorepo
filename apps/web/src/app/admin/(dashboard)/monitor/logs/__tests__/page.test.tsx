/**
 * LogViewerPage Tests
 * Issue #142 — Log Viewer page-level test
 * Issue #3367 — LokiErrorViewer tab integration
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';

// Mock child components that use React Query internally
vi.mock('../AppLogViewer', () => ({
  AppLogViewer: () => <div data-testid="app-log-viewer">App Log Viewer</div>,
}));

vi.mock('../LogViewer', () => ({
  LogViewer: () => <div data-testid="container-log-viewer">Container Log Viewer</div>,
}));

vi.mock('../LokiErrorViewer', () => ({
  LokiErrorViewer: () => <div data-testid="mock-loki-viewer" />,
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

  it('shows Container Errors tab', () => {
    render(<LogViewerPage />);
    expect(screen.getByRole('button', { name: /container errors/i })).toBeInTheDocument();
  });

  it('shows LokiErrorViewer when Container Errors tab is active', () => {
    render(<LogViewerPage />);
    fireEvent.click(screen.getByRole('button', { name: /container errors/i }));
    expect(screen.getByTestId('mock-loki-viewer')).toBeInTheDocument();
  });

  it('renders without crashing regardless of NEXT_PUBLIC_GRAFANA_URL', () => {
    render(<LogViewerPage />);
    expect(screen.getByTestId('logs-page')).toBeInTheDocument();
  });
});
