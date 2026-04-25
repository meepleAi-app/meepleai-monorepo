/**
 * @vitest-environment jsdom
 */
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import type { MechanicAnalysisMetricsDto } from '@/lib/api/schemas/admin-mechanic-extractor-validation.schemas';

import { MetricsCard } from '../MetricsCard';

const BASE_METRICS: MechanicAnalysisMetricsDto = {
  id: '11111111-1111-1111-1111-111111111111',
  mechanicAnalysisId: '22222222-2222-2222-2222-222222222222',
  sharedGameId: '33333333-3333-3333-3333-333333333333',
  coveragePct: 0.85,
  pageAccuracyPct: 0.92,
  bggMatchPct: 0.7,
  overallScore: 0.83,
  certificationStatus: 'Certified',
  goldenVersionHash: 'abcdef1234567890',
  thresholdsSnapshotJson: '{}',
  matchDetailsJson: '{}',
  computedAt: '2026-04-25T10:00:00Z',
};

describe('MetricsCard', () => {
  it('renders all four scores formatted as percentages', () => {
    render(<MetricsCard metrics={BASE_METRICS} />);

    expect(screen.getByTestId('metric-coverage')).toHaveTextContent('85%');
    expect(screen.getByTestId('metric-page-accuracy')).toHaveTextContent('92%');
    expect(screen.getByTestId('metric-bgg-match')).toHaveTextContent('70%');
    expect(screen.getByTestId('metric-overall')).toHaveTextContent('83%');
  });

  it('renders the Certified badge with certified label', () => {
    render(<MetricsCard metrics={BASE_METRICS} />);
    const badge = screen.getByTestId('metrics-certification-badge');
    expect(badge).toHaveTextContent(/Certified/);
    expect(badge.className).toMatch(/green/);
  });

  it('renders the NotCertified badge', () => {
    render(<MetricsCard metrics={{ ...BASE_METRICS, certificationStatus: 'NotCertified' }} />);
    const badge = screen.getByTestId('metrics-certification-badge');
    expect(badge).toHaveTextContent(/Not certified/i);
    expect(badge.className).toMatch(/rose/);
  });

  it('renders the NotEvaluated badge', () => {
    render(<MetricsCard metrics={{ ...BASE_METRICS, certificationStatus: 'NotEvaluated' }} />);
    const badge = screen.getByTestId('metrics-certification-badge');
    expect(badge).toHaveTextContent(/Not evaluated/i);
    expect(badge.className).toMatch(/slate|zinc/);
  });

  it('shows drift warning when current golden hash differs from snapshot hash', () => {
    render(<MetricsCard metrics={BASE_METRICS} currentGoldenVersionHash="ffffffffffffffff" />);
    const warning = screen.getByTestId('metrics-drift-warning');
    expect(warning).toBeInTheDocument();
    expect(warning).toHaveTextContent(/abcdef12/);
    expect(warning).toHaveTextContent(/ffffffff/);
    expect(warning).toHaveTextContent(/Re-evaluate/i);
  });

  it('does not show drift warning when hashes match', () => {
    render(
      <MetricsCard
        metrics={BASE_METRICS}
        currentGoldenVersionHash={BASE_METRICS.goldenVersionHash}
      />
    );
    expect(screen.queryByTestId('metrics-drift-warning')).not.toBeInTheDocument();
  });

  it('does not show drift warning when current golden hash is omitted', () => {
    render(<MetricsCard metrics={BASE_METRICS} />);
    expect(screen.queryByTestId('metrics-drift-warning')).not.toBeInTheDocument();
  });
});
