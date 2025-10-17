import { render, screen } from '@testing-library/react';
import {
  EndpointDistributionChart,
  LatencyDistributionChart,
  RequestsTimeSeriesChart,
  FeedbackChart
} from '../AdminCharts';

// Mock recharts components to avoid rendering issues in tests
jest.mock('recharts', () => ({
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

describe('EndpointDistributionChart', () => {
  describe('Rendering with data', () => {
    it('renders pie chart with endpoint data', () => {
      const endpointCounts = {
        qa: 50,
        explain: 30,
        setup: 20
      };

      render(<EndpointDistributionChart endpointCounts={endpointCounts} />);

      expect(screen.getByTestId('pie-chart')).toBeInTheDocument();
      expect(screen.getByTestId('pie')).toBeInTheDocument();
    });

    it('displays correct values for each endpoint', () => {
      const endpointCounts = {
        qa: 50,
        explain: 30,
        setup: 20
      };

      render(<EndpointDistributionChart endpointCounts={endpointCounts} />);

      expect(screen.getByText('qa: 50')).toBeInTheDocument();
      expect(screen.getByText('explain: 30')).toBeInTheDocument();
      expect(screen.getByText('setup: 20')).toBeInTheDocument();
    });

    it('renders tooltip and legend', () => {
      const endpointCounts = { qa: 50 };

      render(<EndpointDistributionChart endpointCounts={endpointCounts} />);

      expect(screen.getByTestId('tooltip')).toBeInTheDocument();
      expect(screen.getByTestId('legend')).toBeInTheDocument();
    });

    it('uses correct colors for known endpoints', () => {
      const endpointCounts = {
        qa: 50,
        explain: 30,
        setup: 20,
        chess: 10
      };

      render(<EndpointDistributionChart endpointCounts={endpointCounts} />);

      const segments = screen.getAllByTestId(/^pie-segment-/);
      expect(segments).toHaveLength(4);
    });

    it('uses default color for unknown endpoints', () => {
      const endpointCounts = {
        unknown: 50
      };

      render(<EndpointDistributionChart endpointCounts={endpointCounts} />);

      expect(screen.getByText('unknown: 50')).toBeInTheDocument();
    });

    it('handles multiple endpoints correctly', () => {
      const endpointCounts = {
        qa: 100,
        explain: 75,
        setup: 50,
        chess: 25,
        other: 10
      };

      render(<EndpointDistributionChart endpointCounts={endpointCounts} />);

      expect(screen.getByText('qa: 100')).toBeInTheDocument();
      expect(screen.getByText('explain: 75')).toBeInTheDocument();
      expect(screen.getByText('setup: 50')).toBeInTheDocument();
      expect(screen.getByText('chess: 25')).toBeInTheDocument();
      expect(screen.getByText('other: 10')).toBeInTheDocument();
    });
  });

  describe('Empty state', () => {
    it('shows empty message when no data provided', () => {
      render(<EndpointDistributionChart endpointCounts={{}} />);

      expect(screen.getByText('No endpoint data available')).toBeInTheDocument();
      expect(screen.queryByTestId('pie-chart')).not.toBeInTheDocument();
    });

    it('applies correct styling to empty message', () => {
      render(<EndpointDistributionChart endpointCounts={{}} />);

      const emptyMessage = screen.getByText('No endpoint data available');
      expect(emptyMessage).toHaveStyle({ padding: '48px', textAlign: 'center', color: '#64748b' });
    });
  });

  describe('Edge cases', () => {
    it('handles single endpoint', () => {
      const endpointCounts = { qa: 1 };

      render(<EndpointDistributionChart endpointCounts={endpointCounts} />);

      expect(screen.getByText('qa: 1')).toBeInTheDocument();
    });

    it('handles zero values', () => {
      const endpointCounts = { qa: 0, explain: 0 };

      render(<EndpointDistributionChart endpointCounts={endpointCounts} />);

      expect(screen.getByText('qa: 0')).toBeInTheDocument();
      expect(screen.getByText('explain: 0')).toBeInTheDocument();
    });

    it('handles large numbers', () => {
      const endpointCounts = { qa: 999999 };

      render(<EndpointDistributionChart endpointCounts={endpointCounts} />);

      expect(screen.getByText('qa: 999999')).toBeInTheDocument();
    });
  });
});

describe('LatencyDistributionChart', () => {
  describe('Rendering with data', () => {
    it('renders bar chart with latency data', () => {
      const requests = [
        { latencyMs: 50, endpoint: 'qa', createdAt: '2025-10-15T12:00:00Z' },
        { latencyMs: 150, endpoint: 'explain', createdAt: '2025-10-15T12:01:00Z' },
        { latencyMs: 250, endpoint: 'setup', createdAt: '2025-10-15T12:02:00Z' }
      ];

      render(<LatencyDistributionChart requests={requests} />);

      expect(screen.getByTestId('bar-chart')).toBeInTheDocument();
      expect(screen.getByTestId('bar')).toBeInTheDocument();
    });

    it('groups latency into correct bins', () => {
      const requests = [
        { latencyMs: 50, endpoint: 'qa', createdAt: '2025-10-15T12:00:00Z' },
        { latencyMs: 75, endpoint: 'qa', createdAt: '2025-10-15T12:01:00Z' },
        { latencyMs: 150, endpoint: 'explain', createdAt: '2025-10-15T12:02:00Z' },
        { latencyMs: 250, endpoint: 'setup', createdAt: '2025-10-15T12:03:00Z' }
      ];

      render(<LatencyDistributionChart requests={requests} />);

      // Should have 6 bins: 0-100, 100-200, 200-300, 300-400, 400-500, 500-1000
      const chart = screen.getByTestId('bar-chart');
      expect(chart).toHaveAttribute('data-count', '6');
    });

    it('renders CartesianGrid with correct props', () => {
      const requests = [
        { latencyMs: 50, endpoint: 'qa', createdAt: '2025-10-15T12:00:00Z' }
      ];

      render(<LatencyDistributionChart requests={requests} />);

      expect(screen.getByTestId('cartesian-grid')).toHaveAttribute('data-stroke-dasharray', '3 3');
    });

    it('renders XAxis with range labels', () => {
      const requests = [
        { latencyMs: 50, endpoint: 'qa', createdAt: '2025-10-15T12:00:00Z' }
      ];

      render(<LatencyDistributionChart requests={requests} />);

      expect(screen.getByTestId('x-axis')).toHaveAttribute('data-key', 'range');
    });

    it('renders YAxis', () => {
      const requests = [
        { latencyMs: 50, endpoint: 'qa', createdAt: '2025-10-15T12:00:00Z' }
      ];

      render(<LatencyDistributionChart requests={requests} />);

      expect(screen.getByTestId('y-axis')).toBeInTheDocument();
    });

    it('renders Bar with correct props', () => {
      const requests = [
        { latencyMs: 50, endpoint: 'qa', createdAt: '2025-10-15T12:00:00Z' }
      ];

      render(<LatencyDistributionChart requests={requests} />);

      const bar = screen.getByTestId('bar');
      expect(bar).toHaveAttribute('data-key', 'count');
      expect(bar).toHaveAttribute('data-fill', '#1a73e8');
      expect(bar).toHaveAttribute('data-name', 'Requests');
    });

    it('renders tooltip and legend', () => {
      const requests = [
        { latencyMs: 50, endpoint: 'qa', createdAt: '2025-10-15T12:00:00Z' }
      ];

      render(<LatencyDistributionChart requests={requests} />);

      expect(screen.getByTestId('tooltip')).toBeInTheDocument();
      expect(screen.getByTestId('legend')).toBeInTheDocument();
    });
  });

  describe('Latency binning', () => {
    it('correctly bins requests in 0-100ms range', () => {
      const requests = [
        { latencyMs: 0, endpoint: 'qa', createdAt: '2025-10-15T12:00:00Z' },
        { latencyMs: 50, endpoint: 'qa', createdAt: '2025-10-15T12:01:00Z' },
        { latencyMs: 99, endpoint: 'qa', createdAt: '2025-10-15T12:02:00Z' }
      ];

      render(<LatencyDistributionChart requests={requests} />);

      expect(screen.getByTestId('bar-chart')).toBeInTheDocument();
    });

    it('correctly bins requests at bin boundaries', () => {
      const requests = [
        { latencyMs: 100, endpoint: 'qa', createdAt: '2025-10-15T12:00:00Z' }, // 100-200 bin
        { latencyMs: 200, endpoint: 'qa', createdAt: '2025-10-15T12:01:00Z' }, // 200-300 bin
        { latencyMs: 500, endpoint: 'qa', createdAt: '2025-10-15T12:02:00Z' }  // 500-1000 bin
      ];

      render(<LatencyDistributionChart requests={requests} />);

      expect(screen.getByTestId('bar-chart')).toBeInTheDocument();
    });

    it('handles very high latency values', () => {
      const requests = [
        { latencyMs: 999, endpoint: 'qa', createdAt: '2025-10-15T12:00:00Z' },
        { latencyMs: 1500, endpoint: 'qa', createdAt: '2025-10-15T12:01:00Z' } // Beyond 1000ms
      ];

      render(<LatencyDistributionChart requests={requests} />);

      expect(screen.getByTestId('bar-chart')).toBeInTheDocument();
    });
  });

  describe('Empty state', () => {
    it('shows empty message when no data provided', () => {
      render(<LatencyDistributionChart requests={[]} />);

      expect(screen.getByText('No latency data available')).toBeInTheDocument();
      expect(screen.queryByTestId('bar-chart')).not.toBeInTheDocument();
    });

    it('applies correct styling to empty message', () => {
      render(<LatencyDistributionChart requests={[]} />);

      const emptyMessage = screen.getByText('No latency data available');
      expect(emptyMessage).toHaveStyle({ padding: '48px', textAlign: 'center', color: '#64748b' });
    });
  });
});

describe('RequestsTimeSeriesChart', () => {
  describe('Rendering with data', () => {
    it('renders line chart with time series data', () => {
      const requests = [
        { createdAt: '2025-10-15T12:00:00Z', status: 'Success' },
        { createdAt: '2025-10-15T13:00:00Z', status: 'Success' },
        { createdAt: '2025-10-15T14:00:00Z', status: 'Error' }
      ];

      render(<RequestsTimeSeriesChart requests={requests} />);

      expect(screen.getByTestId('line-chart')).toBeInTheDocument();
    });

    it('groups requests by hour', () => {
      const requests = [
        { createdAt: '2025-10-15T12:00:00Z', status: 'Success' },
        { createdAt: '2025-10-15T12:30:00Z', status: 'Success' },
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
      expect(emptyMessage).toHaveStyle({ padding: '48px', textAlign: 'center', color: '#64748b' });
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
      expect(emptyMessage).toHaveStyle({ padding: '48px', textAlign: 'center', color: '#64748b' });
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
