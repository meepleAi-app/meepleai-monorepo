import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import AnalyticsDashboard from '../../pages/admin/analytics';
import { api } from '../../lib/api';

jest.mock('../../lib/api');
jest.mock('next/link', () => ({ children, href }: any) => <a href={href}>{children}</a>);

jest.mock('recharts', () => ({
  ResponsiveContainer: ({ children }: any) => <div>{children}</div>,
  LineChart: ({ children }: any) => <div>{children}</div>,
  Line: () => <div />, XAxis: () => <div />, YAxis: () => <div />,
  CartesianGrid: () => <div />, Tooltip: () => <div />, Legend: () => <div />,
}));

const mockApi = api as jest.Mocked<typeof api>;

const sampleStats = {
  metrics: {
    totalUsers: 150,
    activeSessions: 42,
    apiRequestsToday: 1250,
    totalPdfDocuments: 35,
    totalChatMessages: 8420,
    averageConfidenceScore: 0.87,
    totalRagRequests: 5320,
    totalTokensUsed: 1250000,
  },
  userTrend: [],
  sessionTrend: [],
  apiRequestTrend: [],
  pdfUploadTrend: [],
  chatMessageTrend: [],
  generatedAt: '2025-10-25T18:00:00Z',
};

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

    expect(screen.getByText('150')).toBeInTheDocument();
    expect(screen.getByText('42')).toBeInTheDocument();
    expect(screen.getByText('1,250')).toBeInTheDocument();
    expect(screen.getByText('8,420')).toBeInTheDocument();
    expect(screen.getByText('87.0%')).toBeInTheDocument();
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
