import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { GrafanaTab } from '../GrafanaTab';

describe('GrafanaTab', () => {
  it('renders Grafana dashboard section', () => {
    render(<GrafanaTab />);
    // GrafanaDashboard renders a root div with data-testid="grafana-dashboard"
    expect(screen.getByTestId('grafana-dashboard')).toBeTruthy();
  });

  it('renders dashboard selector with category sections', () => {
    render(<GrafanaTab />);
    // Should show the empty state when no dashboard is selected
    expect(screen.getByTestId('grafana-empty-state')).toBeTruthy();
  });

  it('renders application category section', () => {
    render(<GrafanaTab />);
    expect(screen.getByTestId('category-section-application')).toBeTruthy();
  });
});
