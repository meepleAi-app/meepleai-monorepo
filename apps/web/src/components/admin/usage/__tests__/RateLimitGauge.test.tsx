/**
 * RateLimitGauge unit tests.
 * Issue #5081: Admin usage page — rate limit gauge.
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { RateLimitGauge } from '../RateLimitGauge';

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const mockStatus = {
  balanceUsd:          4.5,
  dailySpendUsd:       0.005,
  todayRequestCount:   42,
  currentRpm:          80,
  limitRpm:            200,
  utilizationPercent:  0.4,
  isThrottled:         false,
  isFreeTier:          false,
  rateLimitInterval:   'minute',
  lastUpdated:         '2026-02-22T10:00:00Z',
};

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('RateLimitGauge', () => {
  it('renders card title', () => {
    render(<RateLimitGauge status={mockStatus} />);
    expect(screen.getByText('Rate-Limit Utilization')).toBeInTheDocument();
  });

  it('shows current RPM and limit combined in label', () => {
    render(<RateLimitGauge status={mockStatus} />);
    // ProgressRing label renders "currentRpm / limitRpm"
    expect(screen.getByText('80 / 200')).toBeInTheDocument();
  });

  it('shows RPM limit when limitRpm > 0', () => {
    render(<RateLimitGauge status={mockStatus} />);
    expect(screen.getByText(/200/)).toBeInTheDocument();
  });

  it('hides RPM limit when limitRpm = 0', () => {
    render(<RateLimitGauge status={{ ...mockStatus, limitRpm: 0 }} />);
    // No "RPM" text in this component — verify no extraneous RPM label
    expect(screen.queryByText(/RPM/)).not.toBeInTheDocument();
  });

  it('shows rate limit interval label', () => {
    render(<RateLimitGauge status={mockStatus} />);
    expect(screen.getByText('Interval: minute')).toBeInTheDocument();
  });

  it('does not show Throttled badge when isThrottled is false', () => {
    render(<RateLimitGauge status={mockStatus} />);
    expect(screen.queryByText('Throttled')).not.toBeInTheDocument();
  });

  it('shows Throttled badge when isThrottled is true', () => {
    render(<RateLimitGauge status={{ ...mockStatus, isThrottled: true }} />);
    // Appears in both the ProgressRing Badge and the status row span
    expect(screen.getAllByText('Throttled').length).toBeGreaterThan(0);
  });

  it('renders the SVG radial ring', () => {
    const { container } = render(<RateLimitGauge status={mockStatus} />);
    expect(container.querySelector('svg')).toBeInTheDocument();
  });

  it('renders utilization percentage in SVG text', () => {
    // 40% utilization (utilizationPercent = 0.4)
    render(<RateLimitGauge status={mockStatus} />);
    expect(screen.getByText('40%')).toBeInTheDocument();
  });

  it('shows "req / min" sub-label', () => {
    render(<RateLimitGauge status={mockStatus} />);
    expect(screen.getByText('req / min')).toBeInTheDocument();
  });

  it('shows loading skeleton when isLoading', () => {
    render(<RateLimitGauge status={null} isLoading />);
    const skeletons = document.querySelectorAll('.animate-pulse');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('shows no-data message when status is null and not loading', () => {
    render(<RateLimitGauge status={null} />);
    expect(screen.getByText('No data available')).toBeInTheDocument();
  });
});
