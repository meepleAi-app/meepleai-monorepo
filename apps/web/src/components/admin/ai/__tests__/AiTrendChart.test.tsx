/**
 * @vitest-environment jsdom
 *
 * AiTrendChart — Issue #1722 PR 3/4.
 *
 * Inline SVG trend chart for AI request analytics. Today we render
 * the 2 series that `/api/v1/admin/model-performance?days=N` actually
 * exposes (avgLatencyMs + requestCount per day). The mockup asks for
 * p50/p95/error series too, but those need a dedicated metrics
 * endpoint (#1722 sub-task BE) — the component surfaces an "approx"
 * badge to make the gap explicit.
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import { AiTrendChart, type TrendDatapoint } from '../AiTrendChart';

const sample: TrendDatapoint[] = [
  { date: '2026-05-24', avgLatencyMs: 420, requestCount: 12 },
  { date: '2026-05-25', avgLatencyMs: 510, requestCount: 18 },
  { date: '2026-05-26', avgLatencyMs: 380, requestCount: 7 },
];

describe('AiTrendChart', () => {
  it('renders the chart region with the latency + volume label', () => {
    render(
      <AiTrendChart
        data={sample}
        range="7d"
        onRangeChange={vi.fn()}
        rangeOptions={['1d', '7d', '30d']}
      />
    );
    expect(screen.getByRole('img', { name: /latency/i })).toBeInTheDocument();
  });

  it('renders one polyline per series (avgLatencyMs + requestCount) when percentile fields absent', () => {
    const { container } = render(
      <AiTrendChart
        data={sample}
        range="7d"
        onRangeChange={vi.fn()}
        rangeOptions={['1d', '7d', '30d']}
      />
    );
    const polylines = container.querySelectorAll('polyline[data-series]');
    expect(polylines).toHaveLength(2);
  });

  it('renders 4 polylines (volume + p50 + p95 + error) when full percentile series provided (#1729)', () => {
    const fullSample: TrendDatapoint[] = sample.map(d => ({
      ...d,
      p50LatencyMs: d.avgLatencyMs - 50,
      p95LatencyMs: d.avgLatencyMs + 200,
      errorRate: 0.05,
    }));
    const { container } = render(
      <AiTrendChart
        data={fullSample}
        range="7d"
        onRangeChange={vi.fn()}
        rangeOptions={['Live', '1h', '24h', '7d']}
      />
    );
    const polylines = container.querySelectorAll('polyline[data-series]');
    // No legacy "latency" series — replaced by p50/p95
    expect(polylines).toHaveLength(4);
    expect(container.querySelector('polyline[data-series="p50"]')).toBeInTheDocument();
    expect(container.querySelector('polyline[data-series="p95"]')).toBeInTheDocument();
    expect(container.querySelector('polyline[data-series="error"]')).toBeInTheDocument();
    expect(container.querySelector('polyline[data-series="latency"]')).not.toBeInTheDocument();
  });

  it('hides the "approx" badge when full percentile series provided (#1729)', () => {
    const fullSample: TrendDatapoint[] = sample.map(d => ({
      ...d,
      p50LatencyMs: 100,
      p95LatencyMs: 500,
      errorRate: 0,
    }));
    render(
      <AiTrendChart
        data={fullSample}
        range="1h"
        onRangeChange={vi.fn()}
        rangeOptions={['Live', '1h', '24h', '7d']}
      />
    );
    expect(screen.queryByText(/approx/i)).not.toBeInTheDocument();
  });

  it('exposes a screen-reader table mirroring the datapoints', () => {
    render(
      <AiTrendChart
        data={sample}
        range="7d"
        onRangeChange={vi.fn()}
        rangeOptions={['1d', '7d', '30d']}
      />
    );
    expect(screen.getByText('2026-05-24')).toBeInTheDocument();
    expect(screen.getByText('420')).toBeInTheDocument();
    expect(screen.getByText('12')).toBeInTheDocument();
  });

  it('shows the empty state when no datapoints are provided', () => {
    render(
      <AiTrendChart
        data={[]}
        range="7d"
        onRangeChange={vi.fn()}
        rangeOptions={['1d', '7d', '30d']}
      />
    );
    expect(screen.getByText(/no data/i)).toBeInTheDocument();
  });

  it('renders the approx badge to flag missing p50/p95/error series', () => {
    render(
      <AiTrendChart
        data={sample}
        range="7d"
        onRangeChange={vi.fn()}
        rangeOptions={['1d', '7d', '30d']}
      />
    );
    expect(screen.getByText(/approx/i)).toBeInTheDocument();
  });

  it('invokes onRangeChange when the user picks a different range', async () => {
    const user = userEvent.setup();
    const onRangeChange = vi.fn();
    render(
      <AiTrendChart
        data={sample}
        range="7d"
        onRangeChange={onRangeChange}
        rangeOptions={['1d', '7d', '30d']}
      />
    );
    await user.click(screen.getByRole('button', { name: '30d' }));
    expect(onRangeChange).toHaveBeenCalledWith('30d');
  });

  it('marks the active range button with aria-pressed', () => {
    render(
      <AiTrendChart
        data={sample}
        range="7d"
        onRangeChange={vi.fn()}
        rangeOptions={['1d', '7d', '30d']}
      />
    );
    const active = screen.getByRole('button', { name: '7d', pressed: true });
    expect(active).toBeInTheDocument();
  });

  // #1735 B6: error polyline used to scale errorRate ∈ [0, 1] via `* 1000`
  // (cosmetic, since buildSvgPath auto-scales per-series). The fix anchors the
  // error series to a fixed [0, 1] axis and clamps out-of-range values, so the
  // polyline is guaranteed to stay within the SVG viewBox (Y between PADDING=24
  // and CHART_HEIGHT-PADDING=116) regardless of the errorRate magnitude.
  it('clamps the error polyline within the viewBox even with high errorRate (#1735 B6)', () => {
    const extreme: TrendDatapoint[] = [
      {
        date: '2026-05-24',
        avgLatencyMs: 100,
        requestCount: 10,
        p50LatencyMs: 80,
        p95LatencyMs: 120,
        errorRate: 0,
      },
      {
        date: '2026-05-25',
        avgLatencyMs: 105,
        requestCount: 11,
        p50LatencyMs: 82,
        p95LatencyMs: 125,
        errorRate: 0.5,
      },
      {
        date: '2026-05-26',
        avgLatencyMs: 110,
        requestCount: 12,
        p50LatencyMs: 85,
        p95LatencyMs: 130,
        errorRate: 1,
      },
    ];
    const { container } = render(
      <AiTrendChart
        data={extreme}
        range="Live"
        onRangeChange={vi.fn()}
        rangeOptions={['Live', '1h', '24h', '7d']}
      />
    );
    const errorPolyline = container.querySelector('polyline[data-series="error"]');
    expect(errorPolyline).toBeInTheDocument();
    const points = errorPolyline?.getAttribute('points') ?? '';
    const ys = points
      .split(' ')
      .map(pair => Number.parseFloat(pair.split(',')[1] ?? 'NaN'))
      .filter(n => !Number.isNaN(n));
    expect(ys).toHaveLength(3);
    // CHART_HEIGHT=140, PADDING=24 → valid Y range [24, 116]
    ys.forEach(y => {
      expect(y).toBeGreaterThanOrEqual(24);
      expect(y).toBeLessThanOrEqual(116);
    });
    // errorRate=1 (max) should map to the top edge (Y=PADDING=24),
    // errorRate=0 should map to the bottom edge (Y=CHART_HEIGHT-PADDING=116).
    expect(ys[0]).toBeCloseTo(116, 0);
    expect(ys[2]).toBeCloseTo(24, 0);
  });
});
