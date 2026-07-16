import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';

const useInfrastructureKpisMock = vi.hoisted(() => vi.fn());

vi.mock('../hooks/use-infrastructure-kpis', () => ({
  useInfrastructureKpis: () => useInfrastructureKpisMock(),
}));

import { KPISparklineStrip } from '../KPISparklineStrip';

function defaultKpis() {
  return {
    containers: { active: 7, total: 8, stopped: 1, loading: false },
    cpu: {
      value: 34,
      series: [
        { timestamp: '2026-06-03T14:00:00Z', value: 20 },
        { timestamp: '2026-06-03T15:00:00Z', value: 34 },
      ],
      trend: 'up' as const,
      trendPct: 70,
      loading: false,
    },
    memory: {
      value: 18.4,
      total: 32,
      series: [
        { timestamp: '2026-06-03T14:00:00Z', value: 17 },
        { timestamp: '2026-06-03T15:00:00Z', value: 18.4 },
      ],
      trend: 'up' as const,
      trendPct: 8,
      loading: false,
    },
    batchJobs: { queued: 4, running: 2, loading: false },
  };
}

function makeKpis(overrides: Partial<ReturnType<typeof defaultKpis>> = {}) {
  return { ...defaultKpis(), ...overrides };
}

describe('KPISparklineStrip', () => {
  beforeEach(() => {
    useInfrastructureKpisMock.mockReset();
  });

  it('renders 4 KPI cards', () => {
    useInfrastructureKpisMock.mockReturnValue(makeKpis());
    render(<KPISparklineStrip />);
    expect(screen.getAllByTestId('kpi-card')).toHaveLength(4);
  });

  it('shows container active/total', () => {
    useInfrastructureKpisMock.mockReturnValue(makeKpis());
    render(<KPISparklineStrip />);
    expect(screen.getByText('7')).toBeInTheDocument();
    expect(screen.getByText('/8')).toBeInTheDocument();
  });

  it('shows CPU percentage with sparkline svg', () => {
    useInfrastructureKpisMock.mockReturnValue(makeKpis());
    render(<KPISparklineStrip />);
    expect(screen.getByText('34')).toBeInTheDocument();
    expect(screen.getByTestId('kpi-sparkline-cpu')).toBeInTheDocument();
  });

  it('shows memory GB + total', () => {
    useInfrastructureKpisMock.mockReturnValue(makeKpis());
    render(<KPISparklineStrip />);
    expect(screen.getByText('18.4')).toBeInTheDocument();
    expect(screen.getByText('/32 GB')).toBeInTheDocument();
  });

  it('omits the denominator when memory.total is 0 (#3041 fallback, no "/0 GB")', () => {
    useInfrastructureKpisMock.mockReturnValue(
      makeKpis({
        memory: {
          value: 5,
          total: 0,
          series: [],
          trend: 'flat' as const,
          trendPct: 0,
          loading: false,
        },
      })
    );
    render(<KPISparklineStrip />);
    expect(screen.queryByText('/0 GB')).not.toBeInTheDocument();
    expect(screen.getByLabelText('RAM usata 5 GB')).toBeInTheDocument();
  });

  it('shows batch jobs queued + running', () => {
    useInfrastructureKpisMock.mockReturnValue(makeKpis());
    render(<KPISparklineStrip />);
    expect(screen.getByText('4')).toBeInTheDocument();
    expect(screen.getByText(/2 run/)).toBeInTheDocument();
  });

  it('shows skeleton when containers loading', () => {
    useInfrastructureKpisMock.mockReturnValue(
      makeKpis({
        containers: { active: 0, total: 0, stopped: 0, loading: true },
      })
    );
    render(<KPISparklineStrip />);
    expect(screen.getByTestId('kpi-skeleton-containers')).toBeInTheDocument();
  });

  it('shows skeleton when cpu loading', () => {
    useInfrastructureKpisMock.mockReturnValue(
      makeKpis({
        cpu: {
          value: 0,
          series: [],
          trend: 'flat',
          trendPct: 0,
          loading: true,
        },
      })
    );
    render(<KPISparklineStrip />);
    expect(screen.getByTestId('kpi-skeleton-cpu')).toBeInTheDocument();
  });

  it('renders up trend label with arrow', () => {
    useInfrastructureKpisMock.mockReturnValue(makeKpis());
    render(<KPISparklineStrip />);
    // default makeKpis has 2 'up' KPIs (cpu + memory)
    expect(screen.getAllByText(/▲/).length).toBeGreaterThanOrEqual(1);
  });

  it('renders down trend label with arrow', () => {
    useInfrastructureKpisMock.mockReturnValue(
      makeKpis({
        cpu: {
          value: 10,
          series: [],
          trend: 'down',
          trendPct: 50,
          loading: false,
        },
      })
    );
    render(<KPISparklineStrip />);
    expect(screen.getByText(/▼/)).toBeInTheDocument();
  });

  it('renders flat trend label', () => {
    useInfrastructureKpisMock.mockReturnValue(
      makeKpis({
        cpu: {
          value: 10,
          series: [],
          trend: 'flat',
          trendPct: 0,
          loading: false,
        },
      })
    );
    render(<KPISparklineStrip />);
    expect(screen.getByText(/stabile/i)).toBeInTheDocument();
  });

  it('omits sparkline for containers and batch jobs (no time-series available)', () => {
    useInfrastructureKpisMock.mockReturnValue(makeKpis());
    render(<KPISparklineStrip />);
    expect(screen.queryByTestId('kpi-sparkline-containers')).not.toBeInTheDocument();
    expect(screen.queryByTestId('kpi-sparkline-batchjobs')).not.toBeInTheDocument();
  });

  it('omits sparkline svg when series has < 2 points', () => {
    useInfrastructureKpisMock.mockReturnValue(
      makeKpis({
        cpu: {
          value: 34,
          series: [{ timestamp: '2026-06-03T14:00:00Z', value: 34 }],
          trend: 'flat',
          trendPct: 0,
          loading: false,
        },
      })
    );
    render(<KPISparklineStrip />);
    expect(screen.queryByTestId('kpi-sparkline-cpu')).not.toBeInTheDocument();
  });

  it('cards have accessible aria-label', () => {
    useInfrastructureKpisMock.mockReturnValue(makeKpis());
    render(<KPISparklineStrip />);
    expect(screen.getByLabelText(/Container attivi 7/)).toBeInTheDocument();
    expect(screen.getByLabelText(/CPU media 34/)).toBeInTheDocument();
    expect(screen.getByLabelText(/RAM usata 18.4/)).toBeInTheDocument();
    expect(screen.getByLabelText(/Batch job/)).toBeInTheDocument();
  });
});
