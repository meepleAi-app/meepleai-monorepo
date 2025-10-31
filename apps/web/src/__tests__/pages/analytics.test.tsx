import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import AnalyticsDashboard from '../../pages/admin/analytics';
import { api } from '../../lib/api';
import { createMockDashboardStats } from '../fixtures/common-fixtures';

jest.mock('../../lib/api');
jest.mock('next/link', () => {
  const MockLink = ({ children, href }: any) => <a href={href}>{children}</a>;
  MockLink.displayName = 'MockLink';
  return MockLink;
});

jest.mock('recharts', () => ({
  ResponsiveContainer: ({ children }: any) => <div>{children}</div>,
  LineChart: ({ children }: any) => <div>{children}</div>,
  Line: () => <div />, XAxis: () => <div />, YAxis: () => <div />,
  CartesianGrid: () => <div />, Tooltip: () => <div />, Legend: () => <div />,
}));

const mockApi = api as jest.Mocked<typeof api>;

// Use the factory function to ensure complete, type-safe mock data
const sampleStats = createMockDashboardStats({
  generatedAt: '2025-10-25T18:00:00Z',
});

describe('AnalyticsDashboard', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('renders metrics after successful fetch', async () => {
    mockApi.get.mockResolvedValueOnce(sampleStats as any);

    render(<AnalyticsDashboard />);

    await waitFor(() => {
      expect(screen.getByText('Analytics Dashboard')).toBeInTheDocument();
      expect(screen.getByText('Total Users')).toBeInTheDocument();
    });

    // Use flexible matchers that work with locale-formatted numbers
    // toLocaleString() may format differently in test vs production environments
    expect(screen.getByText((content, element) => {
      return element?.textContent === '150' || element?.textContent === '150';
    })).toBeInTheDocument();

    expect(screen.getByText((content, element) => {
      return element?.textContent === '42' || element?.textContent === '42';
    })).toBeInTheDocument();

    // Match both formatted (1,250) and unformatted (1250) variants
    expect(screen.getByText((content, element) => {
      const text = element?.textContent || '';
      return text === '1,250' || text === '1250';
    })).toBeInTheDocument();

    expect(screen.getByText((content, element) => {
      const text = element?.textContent || '';
      return text === '8,420' || text === '8420';
    })).toBeInTheDocument();

    expect(screen.getByText((content, element) => {
      const text = element?.textContent || '';
      return text === '87.0%' || text === '87%';
    })).toBeInTheDocument();
  });

  it('displays an error state when the API fails', async () => {
    mockApi.get.mockRejectedValueOnce(new Error('Network error'));

    render(<AnalyticsDashboard />);

    await waitFor(() => {
      expect(screen.getByText(/Error Loading Analytics/)).toBeInTheDocument();
      expect(screen.getByText('Network error')).toBeInTheDocument();
    });
  });

  it('allows toggling auto-refresh', async () => {
    mockApi.get.mockResolvedValue(sampleStats as any);

    render(<AnalyticsDashboard />);

    const autoButton = await screen.findByRole('button', { name: /Auto-refresh ON/i });
    fireEvent.click(autoButton);

    expect(await screen.findByRole('button', { name: /Auto-refresh OFF/i })).toBeInTheDocument();
  });

  it('runs the refresh action when the user clicks refresh', async () => {
    mockApi.get.mockResolvedValue(sampleStats as any);

    render(<AnalyticsDashboard />);

    await waitFor(() => expect(mockApi.get).toHaveBeenCalledTimes(1));

    mockApi.get.mockResolvedValueOnce(sampleStats as any);

    fireEvent.click(screen.getByRole('button', { name: 'Refresh' }));

    await waitFor(() => expect(mockApi.get).toHaveBeenCalledTimes(2));
  });
});
