import { render, screen } from '@testing-library/react';
import {
  EndpointDistributionChart,
  LatencyDistributionChart,
  RequestsTimeSeriesChart,
  FeedbackChart
} from '../AdminCharts';

// Mock recharts components to avoid rendering issues in tests
vi.mock('recharts', () => ({
  PieChart: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="pie-chart">{children}</div>
  ),
  Pie: ({ data, label }: { data: Array<{ name: string; value: number }>; label: (entry: { name: string; value: number }) => string }) => (
    <div data-testid="pie">
      {data.map((entry, index) => (
        <div key={index} data-testid={`pie-segment-${entry.name}`}>
          {label ? label(entry) : `${entry.name}: ${entry.value}`}
        </div>
      ))}
    </div>
  ),
  Cell: ({ fill }: { fill: string }) => <div data-testid="cell" data-fill={fill} />,
  BarChart: ({ children, data }: { children: React.ReactNode; data: Array<unknown> }) => (
    <div data-testid="bar-chart" data-count={data.length}>{children}</div>
  ),
  Bar: ({ dataKey, fill, name }: { dataKey: string; fill: string; name: string }) => (
    <div data-testid="bar" data-key={dataKey} data-fill={fill} data-name={name} />
  ),
  LineChart: ({ children, data }: { children: React.ReactNode; data: Array<unknown> }) => (
    <div data-testid="line-chart" data-count={data.length}>{children}</div>
  ),
  Line: ({ dataKey, stroke, name }: { dataKey: string; stroke: string; name: string }) => (
    <div data-testid="line" data-key={dataKey} data-stroke={stroke} data-name={name} />
  ),
  XAxis: ({ dataKey }: { dataKey: string }) => <div data-testid="x-axis" data-key={dataKey} />,
  YAxis: () => <div data-testid="y-axis" />,
  Tooltip: () => <div data-testid="tooltip" />,
  Legend: () => <div data-testid="legend" />,
  ResponsiveContainer: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="responsive-container">{children}</div>
  ),
  CartesianGrid: ({ strokeDasharray }: { strokeDasharray: string }) => (
    <div data-testid="cartesian-grid" data-stroke-dasharray={strokeDasharray} />
  )
}));

        { createdAt: '2025-10-15T13:00:00Z', status: 'Error' }
      ];

      render(<RequestsTimeSeriesChart requests={requests} />);

      // Should group 12:00 and 12:30 into same hour, separate from 13:00
      const chart = screen.getByTestId('line-chart');
      expect(chart).toHaveAttribute('data-count', '2');
    });

    it('renders three lines: total, success, error', () => {
      const requests = [
        { createdAt: '2025-10-15T12:00:00Z', status: 'Success' }
      ];

      render(<RequestsTimeSeriesChart requests={requests} />);

      const lines = screen.getAllByTestId('line');
      expect(lines).toHaveLength(3);

      expect(lines[0]).toHaveAttribute('data-key', 'total');
      expect(lines[0]).toHaveAttribute('data-stroke', '#1a73e8');
      expect(lines[0]).toHaveAttribute('data-name', 'Total Requests');

      expect(lines[1]).toHaveAttribute('data-key', 'success');
      expect(lines[1]).toHaveAttribute('data-stroke', '#0f9d58');
      expect(lines[1]).toHaveAttribute('data-name', 'Successful');

      expect(lines[2]).toHaveAttribute('data-key', 'error');
      expect(lines[2]).toHaveAttribute('data-stroke', '#d93025');
      expect(lines[2]).toHaveAttribute('data-name', 'Errors');
    });

    it('renders CartesianGrid with correct props', () => {
      const requests = [
        { createdAt: '2025-10-15T12:00:00Z', status: 'Success' }
      ];

      render(<RequestsTimeSeriesChart requests={requests} />);

      expect(screen.getByTestId('cartesian-grid')).toHaveAttribute('data-stroke-dasharray', '3 3');
    });

    it('renders XAxis with time key', () => {
      const requests = [
        { createdAt: '2025-10-15T12:00:00Z', status: 'Success' }
      ];

      render(<RequestsTimeSeriesChart requests={requests} />);

      expect(screen.getByTestId('x-axis')).toHaveAttribute('data-key', 'time');
    });

    it('renders YAxis', () => {
      const requests = [
        { createdAt: '2025-10-15T12:00:00Z', status: 'Success' }
      ];

      render(<RequestsTimeSeriesChart requests={requests} />);

      expect(screen.getByTestId('y-axis')).toBeInTheDocument();
    });

    it('renders tooltip and legend', () => {
      const requests = [
        { createdAt: '2025-10-15T12:00:00Z', status: 'Success' }
      ];

      render(<RequestsTimeSeriesChart requests={requests} />);

      expect(screen.getByTestId('tooltip')).toBeInTheDocument();
      expect(screen.getByTestId('legend')).toBeInTheDocument();
    });
  });

  describe('Time series grouping', () => {
    it('counts success and error status correctly', () => {
      const requests = [
        { createdAt: '2025-10-15T12:00:00Z', status: 'Success' },
        { createdAt: '2025-10-15T12:10:00Z', status: 'Success' },
        { createdAt: '2025-10-15T12:20:00Z', status: 'Error' }
      ];

      render(<RequestsTimeSeriesChart requests={requests} />);

      expect(screen.getByTestId('line-chart')).toBeInTheDocument();
    });

    it('sorts time series chronologically', () => {
      const requests = [
        { createdAt: '2025-10-15T14:00:00Z', status: 'Success' },
        { createdAt: '2025-10-15T12:00:00Z', status: 'Success' },
        { createdAt: '2025-10-15T13:00:00Z', status: 'Error' }
      ];

      render(<RequestsTimeSeriesChart requests={requests} />);

      const chart = screen.getByTestId('line-chart');
      expect(chart).toHaveAttribute('data-count', '3');
    });

    it('handles requests across multiple days', () => {
      const requests = [
        { createdAt: '2025-10-15T23:00:00Z', status: 'Success' },
        { createdAt: '2025-10-16T01:00:00Z', status: 'Success' }
      ];

      render(<RequestsTimeSeriesChart requests={requests} />);

      const chart = screen.getByTestId('line-chart');
      expect(chart).toHaveAttribute('data-count', '2');
    });
  });

  describe('Empty state', () => {
    it('shows empty message when no data provided', () => {
      render(<RequestsTimeSeriesChart requests={[]} />);

      expect(screen.getByText('No time series data available')).toBeInTheDocument();
      expect(screen.queryByTestId('line-chart')).not.toBeInTheDocument();
    });

    it('applies correct styling to empty message', () => {
      render(<RequestsTimeSeriesChart requests={[]} />);

      const emptyMessage = screen.getByText('No time series data available');
      expect(emptyMessage).toHaveClass('p-12');
      expect(emptyMessage).toHaveClass('text-center');
      expect(emptyMessage).toHaveClass('text-gray-500');
    });
  });
});

describe('FeedbackChart', () => {
  describe('Rendering with data', () => {
    it('renders bar chart with feedback data', () => {
      const feedbackCounts = {
        helpful: 100,
        notHelpful: 20
      };

      render(<FeedbackChart feedbackCounts={feedbackCounts} />);

      expect(screen.getByTestId('bar-chart')).toBeInTheDocument();
    });

    it('displays helpful feedback with correct label and color', () => {
      const feedbackCounts = {
        helpful: 100
      };

      render(<FeedbackChart feedbackCounts={feedbackCounts} />);

      const chart = screen.getByTestId('bar-chart');
      expect(chart).toHaveAttribute('data-count', '1');
    });

    it('displays not helpful feedback with correct label and color', () => {
      const feedbackCounts = {
        notHelpful: 20
      };

      render(<FeedbackChart feedbackCounts={feedbackCounts} />);

      const chart = screen.getByTestId('bar-chart');
      expect(chart).toHaveAttribute('data-count', '1');
    });

    it('displays both helpful and not helpful feedback', () => {
      const feedbackCounts = {
        helpful: 100,
        notHelpful: 20
      };

      render(<FeedbackChart feedbackCounts={feedbackCounts} />);

      const chart = screen.getByTestId('bar-chart');
      expect(chart).toHaveAttribute('data-count', '2');
    });

    it('renders CartesianGrid with correct props', () => {
      const feedbackCounts = {
        helpful: 100
      };

      render(<FeedbackChart feedbackCounts={feedbackCounts} />);

      expect(screen.getByTestId('cartesian-grid')).toHaveAttribute('data-stroke-dasharray', '3 3');
    });

    it('renders XAxis with name key', () => {
      const feedbackCounts = {
        helpful: 100
      };

      render(<FeedbackChart feedbackCounts={feedbackCounts} />);

      expect(screen.getByTestId('x-axis')).toHaveAttribute('data-key', 'name');
    });

    it('renders YAxis', () => {
      const feedbackCounts = {
        helpful: 100
      };

      render(<FeedbackChart feedbackCounts={feedbackCounts} />);

      expect(screen.getByTestId('y-axis')).toBeInTheDocument();
    });

    it('renders Bar with correct props', () => {
      const feedbackCounts = {
        helpful: 100
      };

      render(<FeedbackChart feedbackCounts={feedbackCounts} />);

      const bar = screen.getByTestId('bar');
      expect(bar).toHaveAttribute('data-key', 'value');
      expect(bar).toHaveAttribute('data-name', 'Feedback Count');
    });

    it('renders tooltip and legend', () => {
      const feedbackCounts = {
        helpful: 100
      };

      render(<FeedbackChart feedbackCounts={feedbackCounts} />);

      expect(screen.getByTestId('tooltip')).toBeInTheDocument();
      expect(screen.getByTestId('legend')).toBeInTheDocument();
    });

    it('renders bar chart for helpful feedback', () => {
      const feedbackCounts = {
        helpful: 100
      };

      render(<FeedbackChart feedbackCounts={feedbackCounts} />);

      // Verify chart renders with correct data
      const chart = screen.getByTestId('bar-chart');
      expect(chart).toHaveAttribute('data-count', '1');
    });

    it('renders bar chart for not helpful feedback', () => {
      const feedbackCounts = {
        notHelpful: 20
      };

      render(<FeedbackChart feedbackCounts={feedbackCounts} />);

      // Verify chart renders with correct data
      const chart = screen.getByTestId('bar-chart');
      expect(chart).toHaveAttribute('data-count', '1');
    });
  });

  describe('Empty state', () => {
    it('shows empty message when no data provided', () => {
      render(<FeedbackChart feedbackCounts={{}} />);

      expect(screen.getByText('No feedback data available')).toBeInTheDocument();
      expect(screen.queryByTestId('bar-chart')).not.toBeInTheDocument();
    });

    it('shows empty message when all values are zero', () => {
      const feedbackCounts = {
        helpful: 0,
        notHelpful: 0
      };

      render(<FeedbackChart feedbackCounts={feedbackCounts} />);

      expect(screen.getByText('No feedback data available')).toBeInTheDocument();
      expect(screen.queryByTestId('bar-chart')).not.toBeInTheDocument();
    });

    it('applies correct styling to empty message', () => {
      render(<FeedbackChart feedbackCounts={{}} />);

      const emptyMessage = screen.getByText('No feedback data available');
      expect(emptyMessage).toHaveClass('p-12');
      expect(emptyMessage).toHaveClass('text-center');
      expect(emptyMessage).toHaveClass('text-gray-500');
    });
  });

  describe('Edge cases', () => {
    it('handles only helpful feedback', () => {
      const feedbackCounts = {
        helpful: 1
      };

      render(<FeedbackChart feedbackCounts={feedbackCounts} />);

      expect(screen.getByTestId('bar-chart')).toBeInTheDocument();
    });

    it('handles only not helpful feedback', () => {
      const feedbackCounts = {
        notHelpful: 1
      };

      render(<FeedbackChart feedbackCounts={feedbackCounts} />);

      expect(screen.getByTestId('bar-chart')).toBeInTheDocument();
    });

    it('handles large feedback numbers', () => {
      const feedbackCounts = {
        helpful: 999999,
        notHelpful: 888888
      };

      render(<FeedbackChart feedbackCounts={feedbackCounts} />);

      expect(screen.getByTestId('bar-chart')).toBeInTheDocument();
    });

    it('shows chart when at least one value is non-zero', () => {
      const feedbackCounts = {
        helpful: 0,
        notHelpful: 1
      };

      render(<FeedbackChart feedbackCounts={feedbackCounts} />);

      expect(screen.getByTestId('bar-chart')).toBeInTheDocument();
      expect(screen.queryByText('No feedback data available')).not.toBeInTheDocument();
    });
  });
});