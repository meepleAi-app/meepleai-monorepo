import { render, screen } from '@testing-library/react';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { GrafanaTab } from '../GrafanaTab';

describe('GrafanaTab', () => {
  beforeEach(() => {
    process.env.NEXT_PUBLIC_GRAFANA_URL = 'http://grafana.test';
  });

  afterEach(() => {
    delete process.env.NEXT_PUBLIC_GRAFANA_URL;
  });

  it('renders Grafana dashboard section', () => {
    render(<GrafanaTab />);
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

  it('shows not-configured state when NEXT_PUBLIC_GRAFANA_URL is not set', () => {
    delete process.env.NEXT_PUBLIC_GRAFANA_URL;
    render(<GrafanaTab />);
    expect(screen.getByTestId('grafana-not-configured')).toBeTruthy();
    expect(screen.queryByTestId('grafana-dashboard')).not.toBeInTheDocument();
  });
});
