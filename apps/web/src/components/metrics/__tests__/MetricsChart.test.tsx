/**
 * @vitest-environment jsdom
 */
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { MetricsChart, type DataPoint, type DataSeries } from '../MetricsChart';

describe('MetricsChart', () => {
  const sampleData: DataPoint[] = [
    { time: '00:00', requests: 45, errors: 2 },
    { time: '01:00', requests: 52, errors: 1 },
    { time: '02:00', requests: 38, errors: 3 },
  ];

  const sampleSeries: DataSeries[] = [
    { key: 'requests', name: 'Requests' },
    { key: 'errors', name: 'Errors' },
  ];

  describe('Rendering', () => {
    it('renders chart container', () => {
      const { container } = render(
        <MetricsChart data={sampleData} xAxisKey="time" series={sampleSeries} />
      );

      expect(container.querySelector('.recharts-responsive-container')).toBeInTheDocument();
    });

    it('renders with area type', () => {
      const { container } = render(
        <MetricsChart type="area" data={sampleData} xAxisKey="time" series={sampleSeries} />
      );

      expect(container.querySelector('.recharts-responsive-container')).toBeInTheDocument();
    });

    it('renders with bar type', () => {
      const { container } = render(
        <MetricsChart type="bar" data={sampleData} xAxisKey="time" series={sampleSeries} />
      );

      expect(container.querySelector('.recharts-responsive-container')).toBeInTheDocument();
    });

    it('renders successfully with valid data', () => {
      const { container } = render(
        <MetricsChart data={sampleData} xAxisKey="time" series={sampleSeries} />
      );

      expect(container.querySelector('.recharts-responsive-container')).toBeInTheDocument();
    });
  });

  describe('Loading State', () => {
    it('shows skeleton when loading is true', () => {
      render(
        <MetricsChart data={sampleData} xAxisKey="time" series={sampleSeries} loading={true} />
      );

      const skeleton = screen.getByRole('status', { name: /loading chart/i });
      expect(skeleton).toBeInTheDocument();
      expect(skeleton).toHaveClass('animate-pulse');
    });

    it('does not show chart when loading', () => {
      render(
        <MetricsChart data={sampleData} xAxisKey="time" series={sampleSeries} loading={true} />
      );

      expect(screen.queryByTestId('line-chart')).not.toBeInTheDocument();
    });

    it('applies custom height to skeleton', () => {
      render(
        <MetricsChart
          data={sampleData}
          xAxisKey="time"
          series={sampleSeries}
          loading={true}
          height={500}
        />
      );

      const skeleton = screen.getByRole('status');
      expect(skeleton).toHaveStyle({ height: '500px' });
    });
  });

  describe('Empty State', () => {
    it('shows empty message when data is empty', () => {
      render(<MetricsChart data={[]} xAxisKey="time" series={sampleSeries} />);

      expect(screen.getByText('No data available')).toBeInTheDocument();
    });

    it('shows custom empty message', () => {
      render(
        <MetricsChart
          data={[]}
          xAxisKey="time"
          series={sampleSeries}
          emptyMessage="Custom empty message"
        />
      );

      expect(screen.getByText('Custom empty message')).toBeInTheDocument();
    });

    it('does not show chart when data is empty', () => {
      render(<MetricsChart data={[]} xAxisKey="time" series={sampleSeries} />);

      expect(screen.queryByTestId('line-chart')).not.toBeInTheDocument();
    });
  });

  describe('Chart Components', () => {
    it('renders with grid enabled', () => {
      const { container } = render(
        <MetricsChart data={sampleData} xAxisKey="time" series={sampleSeries} showGrid={true} />
      );

      expect(container.querySelector('.recharts-responsive-container')).toBeInTheDocument();
    });

    it('renders with grid disabled', () => {
      const { container } = render(
        <MetricsChart data={sampleData} xAxisKey="time" series={sampleSeries} showGrid={false} />
      );

      expect(container.querySelector('.recharts-responsive-container')).toBeInTheDocument();
    });

    it('renders with tooltip enabled', () => {
      const { container } = render(
        <MetricsChart data={sampleData} xAxisKey="time" series={sampleSeries} showTooltip={true} />
      );

      expect(container.querySelector('.recharts-responsive-container')).toBeInTheDocument();
    });

    it('renders with legend enabled', () => {
      const { container } = render(
        <MetricsChart data={sampleData} xAxisKey="time" series={sampleSeries} showLegend={true} />
      );

      expect(container.querySelector('.recharts-responsive-container')).toBeInTheDocument();
    });

    it('renders with brush enabled', () => {
      const { container } = render(
        <MetricsChart data={sampleData} xAxisKey="time" series={sampleSeries} enableBrush={true} />
      );

      expect(container.querySelector('.recharts-responsive-container')).toBeInTheDocument();
    });
  });

  describe('Props', () => {
    it('uses default height', () => {
      const { container } = render(
        <MetricsChart data={sampleData} xAxisKey="time" series={sampleSeries} />
      );

      const responsiveContainer = container.querySelector('.recharts-responsive-container');
      expect(responsiveContainer).toHaveStyle({ height: '300px' });
    });

    it('applies custom height', () => {
      const { container } = render(
        <MetricsChart data={sampleData} xAxisKey="time" series={sampleSeries} height={500} />
      );

      const responsiveContainer = container.querySelector('.recharts-responsive-container');
      expect(responsiveContainer).toHaveStyle({ height: '500px' });
    });

    it('renders with single series', () => {
      const singleSeries: DataSeries[] = [{ key: 'requests', name: 'Requests' }];

      const { container } = render(
        <MetricsChart data={sampleData} xAxisKey="time" series={singleSeries} />
      );

      expect(container.querySelector('.recharts-responsive-container')).toBeInTheDocument();
    });

    it('renders with multiple series', () => {
      const { container } = render(
        <MetricsChart data={sampleData} xAxisKey="time" series={sampleSeries} />
      );

      expect(container.querySelector('.recharts-responsive-container')).toBeInTheDocument();
    });
  });

  describe('Edge Cases', () => {
    it('handles undefined data gracefully', () => {
      render(<MetricsChart data={undefined as any} xAxisKey="time" series={sampleSeries} />);

      expect(screen.getByText('No data available')).toBeInTheDocument();
    });

    it('handles null data gracefully', () => {
      render(<MetricsChart data={null as any} xAxisKey="time" series={sampleSeries} />);

      expect(screen.getByText('No data available')).toBeInTheDocument();
    });

    it('handles empty series array', () => {
      const { container } = render(<MetricsChart data={sampleData} xAxisKey="time" series={[]} />);

      expect(container.querySelector('.recharts-responsive-container')).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('has accessible loading state', () => {
      render(
        <MetricsChart data={sampleData} xAxisKey="time" series={sampleSeries} loading={true} />
      );

      expect(screen.getByRole('status')).toHaveAttribute('aria-label', 'Loading chart');
    });

    it('empty state is readable', () => {
      render(<MetricsChart data={[]} xAxisKey="time" series={sampleSeries} />);

      const emptyMessage = screen.getByText('No data available');
      expect(emptyMessage).toBeVisible();
    });
  });

  describe('Chart Types', () => {
    it('renders line chart with all features enabled', () => {
      const { container } = render(
        <MetricsChart
          type="line"
          data={sampleData}
          xAxisKey="time"
          series={sampleSeries}
          showGrid={true}
          showTooltip={true}
          showLegend={true}
          enableBrush={true}
        />
      );

      expect(container.querySelector('.recharts-responsive-container')).toBeInTheDocument();
    });

    it('renders area chart with all features enabled', () => {
      const { container } = render(
        <MetricsChart
          type="area"
          data={sampleData}
          xAxisKey="time"
          series={sampleSeries}
          showGrid={true}
          showTooltip={true}
          showLegend={true}
          enableBrush={true}
        />
      );

      expect(container.querySelector('.recharts-responsive-container')).toBeInTheDocument();
    });

    it('renders bar chart with all features enabled', () => {
      const { container } = render(
        <MetricsChart
          type="bar"
          data={sampleData}
          xAxisKey="time"
          series={sampleSeries}
          showGrid={true}
          showTooltip={true}
          showLegend={true}
          enableBrush={true}
        />
      );

      expect(container.querySelector('.recharts-responsive-container')).toBeInTheDocument();
    });
  });
});
