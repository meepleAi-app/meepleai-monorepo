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

  it('renders one polyline per series (avgLatencyMs + requestCount)', () => {
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
});
