import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

vi.mock('@/components/admin/charts/ChartsSection', () => ({
  ChartsSection: () => <div data-testid="charts-section" />,
}));

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getOverviewStats: vi.fn().mockResolvedValue({
        totalUsers: 100,
        activeUsers: 50,
        publishedGames: 30,
        recentSubmissions: 10,
      }),
    },
  },
}));

import { OverviewTab } from '../OverviewTab';

function renderWithQuery(ui: React.ReactElement) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 } },
  });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe('OverviewTab', () => {
  it('renders heading', () => {
    renderWithQuery(<OverviewTab />);
    expect(screen.getByText('Panoramica Analitiche')).toBeInTheDocument();
  });

  it('renders ChartsSection component', () => {
    renderWithQuery(<OverviewTab />);
    expect(screen.getByTestId('charts-section')).toBeInTheDocument();
  });
});
