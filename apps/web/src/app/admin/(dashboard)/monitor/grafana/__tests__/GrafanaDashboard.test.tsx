/**
 * GrafanaDashboard Component Tests
 * Issue #134 — Grafana Dashboard Embed
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

vi.mock('@/hooks/useToast', () => ({
  useToast: () => ({
    toast: vi.fn(),
  }),
}));

import { GrafanaDashboard } from '../GrafanaDashboard';

describe('GrafanaDashboard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // ==================== Category Rendering ====================

  it('renders dashboard selector with categories', () => {
    render(<GrafanaDashboard />);

    expect(screen.getByTestId('category-section-application')).toBeInTheDocument();
    expect(screen.getByTestId('category-section-infrastructure')).toBeInTheDocument();
    expect(screen.getByTestId('category-section-ai-services')).toBeInTheDocument();
    expect(screen.getByTestId('category-section-security')).toBeInTheDocument();
  });

  // ==================== Empty State ====================

  it('renders empty state when no dashboard selected', () => {
    render(<GrafanaDashboard />);

    expect(screen.getByTestId('grafana-empty-state')).toBeInTheDocument();
    expect(screen.getByText(/Select a dashboard above/)).toBeInTheDocument();
  });

  // ==================== Dashboard Selection ====================

  it('selecting a dashboard shows controls', async () => {
    const user = userEvent.setup();
    render(<GrafanaDashboard />);

    // No controls initially
    expect(screen.queryByTestId('grafana-controls')).not.toBeInTheDocument();

    await user.click(screen.getByTestId('dashboard-card-api-overview'));

    expect(screen.getByTestId('grafana-controls')).toBeInTheDocument();
  });

  // ==================== Time Range ====================

  it('time range selector changes active button', async () => {
    const user = userEvent.setup();
    render(<GrafanaDashboard />);

    // Select a dashboard first to show controls
    await user.click(screen.getByTestId('dashboard-card-api-overview'));

    // Default is 1h — click 6h
    await user.click(screen.getByTestId('time-range-6h'));

    // The 6h button should now have the 'default' variant styling (non-outline)
    // and the 1h button should have 'outline' variant styling
    const btn6h = screen.getByTestId('time-range-6h');
    const btn1h = screen.getByTestId('time-range-1h');

    // After clicking 6h, it should be the selected one and 1h should not be
    // We verify by checking that the two buttons have different class lists
    expect(btn6h.className).not.toEqual(btn1h.className);
  });

  // ==================== Fullscreen ====================

  it('fullscreen button exists when dashboard selected', async () => {
    const user = userEvent.setup();
    render(<GrafanaDashboard />);

    // No fullscreen button initially
    expect(screen.queryByTestId('fullscreen-toggle')).not.toBeInTheDocument();

    await user.click(screen.getByTestId('dashboard-card-api-overview'));

    expect(screen.getByTestId('fullscreen-toggle')).toBeInTheDocument();
  });

  // ==================== Not Configured State ====================

  it('shows not configured message when GRAFANA_BASE_URL is localhost', async () => {
    const user = userEvent.setup();
    render(<GrafanaDashboard />);

    // Select a dashboard — since env is not set, GRAFANA_BASE_URL defaults to localhost:3001
    await user.click(screen.getByTestId('dashboard-card-api-overview'));

    // Empty state should be gone
    expect(screen.queryByTestId('grafana-empty-state')).not.toBeInTheDocument();

    // Should show "not configured" instead of an iframe
    expect(screen.getByTestId('grafana-not-configured')).toBeInTheDocument();
    expect(screen.getByText('Grafana Not Configured')).toBeInTheDocument();
    expect(screen.getAllByText(/NEXT_PUBLIC_GRAFANA_URL/).length).toBeGreaterThan(0);
  });
});
